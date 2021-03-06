﻿using System;
using System.Collections.Generic;
using System.Linq;
using Lykke.Workflow.Fluent;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Lykke.Workflow.Tests
{
    [TestFixture]
    public class WorkflowTests
    {
        private class TestActivity1 : TestActivity
        {
        }

        private class TestActivity2 : TestActivity
        {
        }

        private class TestActivity3 : TestActivity
        {
        }

        private class FailingTestActivity : TestActivity
        {
            public override ActivityResult Execute(Guid activityExecutionId, List<string> input, Action<List<string>> processOutput, Action<List<string>> processFailOutput)
            {
                return ActivityResult.Failed;
            }
        }

        private class AsyncTestActivity : TestActivity
        {
            public override ActivityResult Execute(Guid activityExecutionId, List<string> input, Action<List<string>> processOutput, Action<List<string>> processFailOutput)
            {
                return ActivityResult.Pending;
            }

            public override ActivityResult Resume<TClosure>(Guid activityExecutionId, Action<List<string>> processOutput, Action<List<string>> processFailOutput, TClosure closure)
            {
                return ActivityResult.Succeeded;
            }
        }

        private class TestActivity : ActivityBase<List<string>, List<string>>
        {
            public override ActivityResult Execute(Guid activityExecutionId, List<string> input, Action<List<string>> processOutput, Action<List<string>> processFailOutput)
            {
                processOutput(new List<string>(input.ToArray().Reverse().ToArray()));
                return ActivityResult.Succeeded;
            }
        }

        [Test]
        public void InvalidEdgeValidationTest()
        {
            void Test()
            {
                var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>());
                wf.Configure(
                    cfg => cfg.Do("node1").ContinueWith("Not existing node"));
            }

            var ex = Assert.Throws(typeof(ApplicationException), Test);
            Assert.That(ex.Message == "Node 'node1' references unknown node 'Not existing node'");
        }

        [Test]
        public void NotTerminatingNodeHasNoEdgesValidationTest()
        {
            void Test()
            {
                var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>());
                wf.Configure(cfg =>
                    cfg.Do("node1")
                        .WithBranch().Do("node2").End());
            }

            var ex = Assert.Throws(typeof(ApplicationException), Test);
            Assert.That(ex.Message == "Node 'node1' is not connected with any other node.");
        }

        [Test]
        public void MultipleEdgesFailureTest()
        {
            var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>());
            wf.Configure(cfg =>
                cfg.Do("node1").ContinueWith("node2").End()
                .WithBranch().Do("node2").End());

            var execution = wf.Run(new List<string>());
            Assert.That(execution.State,Is.EqualTo(WorkflowState.Corrupted));
            Assert.That(execution.Error, Is.EqualTo("Failed to get next node - more then one transition condition was met: \r\n[node1]-Success-> [node2]\r\n[node1]-Success-> [end]"));
        }

        [Test]
        public void MultipleEdgesWithNamedEdgesFailureTest()
        {
            var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>());
            wf.Configure(cfg =>
                cfg.Do("node1").On("conditionName").DeterminedAs(x=>true).ContinueWith("node2").End()
                .WithBranch().Do("node2").End());

            var execution = wf.Run(new List<string>());
            Assert.That(execution.State, Is.EqualTo(WorkflowState.Corrupted));
            Assert.That(execution.Error, Is.EqualTo("Failed to get next node - more then one transition condition was met: \r\n[node1]-conditionName-> [node2]\r\n[node1]-Success-> [end]"));
        }

        [Test]
        public void NoMatchingEdgesFailureTest()
        {
            var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>());
            wf.Configure(cfg =>
                cfg.Do("node1")
                .On("conditionName1").DeterminedAs(x=>false).ContinueWith("node2")
                .On("conditionName2").DeterminedAs(x=>false).End()
                .WithBranch().Do("node2").End());

            var execution = wf.Run(new List<string>());
            Assert.That(execution.State, Is.EqualTo(WorkflowState.Corrupted));
            Assert.That(execution.Error, Is.EqualTo("Failed to get next node - none of transition condition was met: \r\n[node1]-conditionName1-> [node2]\r\n[node1]-conditionName2-> [end]"));
        }

        [Test]
        public void DuplicateNodeNameFailureTest()
        {
            void Test()
            {
                var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>());
                wf.Configure(cfg =>
                    cfg.Do("node1").Do("node1").End());
            }

            var ex = Assert.Throws(typeof(ApplicationException), Test);
            Assert.That(ex.Message == "Can not create node 'node1', node with this name already exists");
        }

        [Test]
        public void StraightExecutionFlowTest()
        {
            var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>());
            wf.Configure(cfg => cfg.Do("node1").Do("node2").End());
            wf.Node<TestActivity1>("node1").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity1"));
            wf.Node<TestActivity2>("node2").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity2"));

            var wfContext = new List<string>();
            var execution = wf.Run(wfContext);
            Assert.That(execution.State, Is.EqualTo(WorkflowState.Complete));
            Assert.That(wfContext, Is.EquivalentTo(new[] {"TestActivity1", "TestActivity2"}), "Wrong activities were executed");
        }

        [Test]
        public void ProcessInputFailureTest()
        {
            var observer = new Mock<IExecutionObserver>();
            observer.Setup(o => o.ActivityStarted(
                It.IsAny<Guid>(), 
                It.Is<string>(v => v == "node1"), 
                It.Is<string>(v => v == "TestActivity1"), It.IsAny<object>()));

            var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>(), null, observer.Object);
            wf.Configure(cfg => cfg.Do("node1").Do("node2").End());
            wf.Node<TestActivity1>("node1").WithInput(list => throw new Exception("FAIL!!!")).ProcessOutput((context, output) => context.Add("TestActivity1"));
            wf.Node<TestActivity2>("node2").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity2"));

            var wfContext = new List<string>();
            var execution = wf.Run(wfContext);
            observer.VerifyAll();
        }

        [Test]
        public void WorkflowCorruptsOnActivityFailWithoutExplicitFailBranchTest()
        {
            var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>());
            wf.Configure(cfg => cfg.Do("node1").Do("node2").End());
            wf.Node<TestActivity1>("node1").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity1"));
            wf.Node<FailingTestActivity>("node2").WithInput(list => list).ProcessOutput((context, output) => context.Add("FailingTestActivity"));

            var wfContext = new List<string>();
            var execution = wf.Run(wfContext);
            Assert.That(execution.State, Is.EqualTo(WorkflowState.Corrupted));
        }

        [Test]
        public void ExplicitFailBranchTest()
        {
            var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>());
            wf.Configure(cfg => cfg.Do("node1").Do("node2").Fail());
            wf.Node<TestActivity1>("node1").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity1"));
            wf.Node<TestActivity2>("node2").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity2"));

            var wfContext = new List<string>();
            var execution = wf.Run(wfContext);
            Assert.That(execution.State, Is.EqualTo(WorkflowState.Failed));
            Assert.That(wfContext, Is.EquivalentTo(new[] { "TestActivity1", "TestActivity2" }), "Wrong activities were executed");
        }

        [Test]
        public void ExecutionFlowWithContextConditionsTest()
        {
            var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>());
            int branchSelector = 0;
            wf.Configure(cfg => cfg.Do("node1")
                .On("constraint1").DeterminedAs(list => branchSelector == 0).ContinueWith("node2")
                .On("constraint2").DeterminedAs(list => branchSelector == 1).End()
                .On("constraint3").DeterminedAs(list => branchSelector == 2).Fail()
                .On("constraint4").DeterminedAs(list => branchSelector == 3).ContinueWith("failingNode")
                .WithBranch().Do ("node2").End()
                .WithBranch().Do ("node3").End()
                .WithBranch().Do("failingNode").OnFail().Fail());

            wf.Node<TestActivity1>("node1").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity1"));
            wf.Node<TestActivity2>("node2").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity2"));
            wf.Node<TestActivity3>("node3").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity3"));
            wf.Node<FailingTestActivity>("failingNode").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity3"));

            var context0 = new List<string>();
            var execution1 = wf.Run(context0);
            var context1 = new List<string>();
            branchSelector = 1;
            var execution2 = wf.Run(context1);
            var context2 = new List<string>();
            branchSelector = 2;
            var execution3 = wf.Run(context2);

            var context3 = new List<string>();
            branchSelector = 3;
            var execution4 = wf.Run(context3);

            Assert.That(execution1.State, Is.EqualTo(WorkflowState.Complete));
            Assert.That(execution2.State, Is.EqualTo(WorkflowState.Complete));
            Assert.That(execution3.State, Is.EqualTo(WorkflowState.Failed));
            Assert.That(execution4.State, Is.EqualTo(WorkflowState.Failed));
            Assert.That(context0, Is.EquivalentTo(new[] {"TestActivity1", "TestActivity2"}), "Wrong activities were executed");
            Assert.That(context1, Is.EquivalentTo(new[] {"TestActivity1"}), "Wrong activities were executed");
            Assert.That(context2, Is.EquivalentTo(new[] {"TestActivity1"}), "Wrong activities were executed");
            Assert.That(context3, Is.EquivalentTo(new[] {"TestActivity1"}), "Wrong activities were executed");
            Console.WriteLine(wf.ToString());
        }

        [Test]
        public void ExecutionFlowWithContextConditionAsFirstStepTest()
        {
            var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>());
            int branchSelector = 0;
            wf.Configure(cfg => cfg.On("constraint1").DeterminedAs(list => branchSelector == 0).ContinueWith("node2")
                .On("constraint2").DeterminedAs(list => branchSelector == 1).End()
                .WithBranch().Do("node2").End()
                .WithBranch().Do("node3").End());

            wf.Node<TestActivity2>("node2").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity2"));
            wf.Node<TestActivity3>("node3").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity3"));

            var context0 = new List<string>();
            var execution1 = wf.Run(context0);
            var context1 = new List<string>();
            branchSelector = 1;
            var execution2 = wf.Run(context1);
            Assert.That(execution1.State, Is.EqualTo(WorkflowState.Complete));
            Assert.That(execution2.State, Is.EqualTo(WorkflowState.Complete));
            Assert.That(context0, Is.EquivalentTo(new[] {"TestActivity2"}), "Wrong activities were executed");
            Assert.That(context1, Is.EquivalentTo(new string[0]), "Wrong activities were executed");
        }

        [Test]
        public void OnFailTest()
        {
            var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>());
            wf.Configure(cfg => 
                                cfg.Do("node1").OnFail("node2")
                                .WithBranch().Do("node2").End()
                                );

            wf.Node<FailingTestActivity>("node1")
                .WithInput(list =>
                                    {
                                        list.Add("FailingTestActivity");
                                        return list;
                                    })
                .ProcessOutput((context, output) => context.Add("FailingTestActivity"));

            wf.Node<TestActivity2>("node2").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity2"));

            var wfContext = new List<string>();
            var execution = wf.Run(wfContext);
            Assert.That(execution.State, Is.EqualTo(WorkflowState.Complete));
            Assert.That(wfContext, Is.EquivalentTo(new[] {"FailingTestActivity", "TestActivity2"}));
            Console.WriteLine(wf.ToString());
        }

        [Test]
        public void ResumeTest()
        {
            var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>());
            wf.Configure(cfg => cfg.Do("node1").Do("node2").Do("node3").End());

            wf.Node<TestActivity1>("node1").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity1"));
            wf.Node<AsyncTestActivity>("node2").WithInput(list =>
            {
                list.Add("AsyncTestActivity");
                return list;
            }).ProcessOutput((context, output) => context.Add("TestActivity2"));
            wf.Node<TestActivity3>("node3").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity3"));

            var wfContext = new List<string>();
            var execution = wf.Run(wfContext);
            Assert.That(execution.State, Is.EqualTo(WorkflowState.InProgress), "Execution was not paused when async activity returned Pednding status");
            Assert.That(wfContext, Is.EquivalentTo(new[] {"TestActivity1", "AsyncTestActivity"}), "Wrong activities were executed");
            Assert.That(execution.ExecutingActivities.Count, Is.EqualTo(1), "execution does not store paused activity info");
            Assert.That(execution.ExecutingActivities.First().Node, Is.EqualTo("node2"), "execution stores paused activity info with wrong node name");
/*
            execution.ExecutingActivities.Single(e=>e.Node==)
*/
            execution = wf.Resume(wfContext,execution.ExecutingActivities.First().Id, new {message = "йа - кложурка!!!"});
            Assert.That(execution.State, Is.EqualTo(WorkflowState.Complete),
                "Execution was not complete after async activity was successfully resumed and returned Succeeded status");
            Assert.That(wfContext, Is.EquivalentTo(new[] {"TestActivity1", "AsyncTestActivity", "TestActivity3"}), "Wrong activities were executed");
        }

        [Test]
        public void ResumeFromTest()
        {
            var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>());
            wf.Configure(cfg => cfg.Do("node1").Do("node2").Do("node3").Do("node4").End());

            wf.Node<TestActivity1>("node1").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity1"));
            wf.Node<AsyncTestActivity>("node2").WithInput(list =>
            {
                list.Add("AsyncTestActivity");
                return list;
            }).ProcessOutput((context, output) => context.Add("TestActivity2"));
            wf.Node<TestActivity3>("node3").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity3"));
            wf.Node<TestActivity2>("node4").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity2"));

            var wfContext = new List<string>();
            var execution = wf.Run(wfContext);
            Assert.That(execution.State, Is.EqualTo(WorkflowState.InProgress), "Execution was not paused when async activity returned Pednding status");
            Assert.That(wfContext, Is.EquivalentTo(new[] {"TestActivity1", "AsyncTestActivity"}), "Wrong activities were executed");

            execution = wf.ResumeFrom(wfContext, "node4");
            Assert.That(execution.State, Is.EqualTo(WorkflowState.Complete),
                "Execution was not complete after async activity was successfully resumed and returned Succeeded status");
            Assert.That(wfContext, Is.EquivalentTo(new[] {"TestActivity1", "AsyncTestActivity", "TestActivity2"}), "Wrong activities were executed");
        }

        [Test]
        public void ResumeFromWithNewInputTest()
        {
            var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>());
            wf.Configure(cfg => cfg.Do("node1").Do("node2").Do("node3").Do("node4").End());

            wf.Node<TestActivity1>("node1").WithInput(list => new List<string>(new[]{"1"})).ProcessOutput((context, output) => context.AddRange(output));
            wf.Node<AsyncTestActivity>("node2").WithInput(list => new List<string>(new[]{"2"})).ProcessOutput((context, output) => context.AddRange(output));
            wf.Node<TestActivity3>("node3").WithInput(list => new List<string>(new[]{"3"})).ProcessOutput((context, output) => context.AddRange(output));
            wf.Node<TestActivity2>("node4").WithInput(list => new List<string>(new[]{"4"})).ProcessOutput((context, output) => context.AddRange(output));

            var wfContext = new List<string>();
            var execution = wf.Run(wfContext);
            Assert.That(execution.State, Is.EqualTo(WorkflowState.InProgress), "Execution was not paused when async activity returned Pednding status");
            Assert.That(wfContext, Is.EquivalentTo(new[] {"1"}), "Wrong activities were executed");

            execution = wf.ResumeFrom(wfContext, "node3", new [] {"77"}.ToList());
            Assert.That(execution.State, Is.EqualTo(WorkflowState.Complete), "Execution was not complete after async activity was successfully resumed and returned Succeeded status");
            Assert.That(wfContext, Is.EquivalentTo(new[] {"1", "77", "4"}), "Wrong activities were executed");
        }

        [Test]
        public void ResumeFromWithInputProviderTest()
        {
            var inputProvider = new Mock<IActivityInputProvider>();
            inputProvider.Setup(p => p.GetInput<List<string>>()).Returns(new[] { "77" }.ToList());
            var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>());
            wf.Configure(cfg => cfg.Do("node1").Do("node2").Do("node3").Do("node4").End());

            wf.Node<TestActivity1>("node1").WithInput(list => new List<string>(new[] { "1" })).ProcessOutput((context, output) => context.AddRange(output));
            wf.Node<AsyncTestActivity>("node2").WithInput(list => new List<string>(new[] { "2" })).ProcessOutput((context, output) => context.AddRange(output));
            wf.Node<TestActivity3>("node3").WithInput(list => new List<string>(new[] { "3" })).ProcessOutput((context, output) => context.AddRange(output));
            wf.Node<TestActivity2>("node4").WithInput(list => new List<string>(new[] { "4" })).ProcessOutput((context, output) => context.AddRange(output));

            var wfContext = new List<string>();
            var execution = wf.Run(wfContext);
            Assert.That(execution.State, Is.EqualTo(WorkflowState.InProgress), "Execution was not paused when async activity returned Pednding status");
            Assert.That(wfContext, Is.EquivalentTo(new[] { "1" }), "Wrong activities were executed");

            execution = wf.ResumeFrom(wfContext, "node3", inputProvider.Object);
            inputProvider.Verify(m => m.GetInput<List<string>>(), Times.Once());
            Assert.That(execution.State, Is.EqualTo(WorkflowState.Complete), "Execution was not complete after async activity was successfully resumed and returned Succeeded status");
            Assert.That(wfContext, Is.EquivalentTo(new[] { "1", "77", "4" }), "Wrong activities were executed");
        }

        [Test]
        public void ResumeAfterWithOutputProviderTest()
        {
            var observer = new Mock<IExecutionObserver>();
            observer.Setup(o => o.ActivityStarted(It.IsAny<Guid>(), It.Is<string>(s => s == "start"), It.Is<string>(s => s == "start"), It.IsAny<string>()));
            observer.Setup(o => o.ActivityFinished(It.IsAny<Guid>(), It.Is<string>(s => s == "start"), It.Is<string>(s => s == "start"), It.IsAny<string>()));
            observer.Setup(o => o.ActivityStarted(It.IsAny<Guid>(), It.Is<string>(s => s == "end"), It.Is<string>(s => s == "end"), It.IsAny<string>()));
            observer.Setup(o => o.ActivityFinished(It.IsAny<Guid>(), It.Is<string>(s => s == "end"), It.Is<string>(s => s == "end"), It.IsAny<string>()));
            observer.Setup(o => o.ActivityStarted(It.IsAny<Guid>(), It.Is<string>(s => s == "node1"), It.Is<string>(s => s == "TestActivity1"), It.IsAny<string>()));
            observer.Setup(o => o.ActivityFinished(It.IsAny<Guid>(), It.Is<string>(s => s == "node1"), It.Is<string>(s => s == "TestActivity1"), It.IsAny<string>()));
            observer.Setup(o => o.ActivityStarted(It.IsAny<Guid>(), It.Is<string>(s => s == "node2"), It.Is<string>(s => s == "AsyncTestActivity"), It.IsAny<string>()));
            observer.Setup(o => o.ActivityFailed(It.IsAny<Guid>(), It.Is<string>(s => s == "node2"), It.Is<string>(s => s == "AsyncTestActivity [FAKE]"), It.IsAny<string>()));
            observer.Setup(o => o.ActivityStarted(It.IsAny<Guid>(), It.Is<string>(s => s == "node2"), It.Is<string>(s => s == "AsyncTestActivity [FAKE]"), It.IsAny<string>()));
            observer.Setup(o => o.ActivityFinished(It.IsAny<Guid>(), It.Is<string>(s => s == "node2"), It.Is<string>(s => s == "AsyncTestActivity"), It.IsAny<string>()));
            observer.Setup(o => o.ActivityStarted(It.IsAny<Guid>(), It.Is<string>(s => s == "node3"), It.Is<string>(s => s == "TestActivity3"), It.IsAny<string>()));
            observer.Setup(o => o.ActivityFinished(It.IsAny<Guid>(), It.Is<string>(s => s == "node3"), It.Is<string>(s => s == "TestActivity3"), It.IsAny<string>()));
            observer.Setup(o => o.ActivityStarted(It.IsAny<Guid>(), It.Is<string>(s => s == "node4"), It.Is<string>(s => s == "TestActivity2"), It.IsAny<string>()));
            observer.Setup(o => o.ActivityFinished(It.IsAny<Guid>(), It.Is<string>(s => s == "node4"), It.Is<string>(s => s == "TestActivity2"), It.IsAny<string>()));

            var outputProvider = new Mock<IActivityOutputProvider>();
            outputProvider.Setup(p => p.GetOuput<List<string>>()).Returns(new[] { "77" }.ToList());
            var inMemoryPersister = new InMemoryPersister<List<string>>();
            var wf = new Workflow<List<string>>(inMemoryPersister, null, observer.Object);
            wf.Configure(cfg => cfg.Do("node1").Do("node2").Do("node3").Do("node4").End());

            wf.Node<TestActivity1>("node1").WithInput(list => new List<string>(new[] { "1" })).ProcessOutput((context, output) => context.AddRange(output));
            wf.Node<AsyncTestActivity>("node2").WithInput(list => new List<string>(new[] { "2" })).ProcessOutput((context, output) => context.AddRange(output));
            wf.Node<TestActivity3>("node3").WithInput(list => new List<string>(new[] { "3" })).ProcessOutput((context, output) => context.AddRange(output));
            wf.Node<TestActivity2>("node4").WithInput(list => new List<string>(new[] { "4" })).ProcessOutput((context, output) => context.AddRange(output));

            var wfContext = new List<string>();
            var execution = wf.Run(wfContext);
            Assert.That(execution.State, Is.EqualTo(WorkflowState.InProgress), "Execution was not paused when async activity returned Pending status\r\n\r\nERROR:\r\n" + execution.Error);
            Assert.That(wfContext, Is.EquivalentTo(new[] { "1" }), "Wrong activities were executed");

            //corrupt workflow
            wf.Resume(wfContext, Guid.Parse("84424EBE-BB5B-4F35-A80D-F57265F78FF0"), new object());
            Assert.That(execution.State, Is.EqualTo(WorkflowState.Corrupted), "Execution was not Corrupted");

            //resume with custom output of node2
            execution = wf.ResumeAfter(wfContext, "node2", outputProvider.Object);
            Assert.That(execution.State, Is.EqualTo(WorkflowState.Complete), "Execution was not complete after async activity was successfully resumed and returned Succeeded status\r\n\r\nERROR:\r\n" + execution.Error);
            Assert.That(wfContext, Is.EquivalentTo(new[] { "1", "77", "3", "4" }), "Wrong activities were executed");
        }

        [Test]
        public void DelegateActivityTest()
        {
            var wf = new Workflow<JObject>(new InMemoryPersister<JObject>() );
            wf.Configure(cfg => cfg.Do("node").End());
            wf.DelegateNode<string, string>("node", x => ActivityMethod(x))
                .WithInput(context => (string)(((dynamic)context).Input))
                .ProcessOutput((context, output) => ((dynamic)context).Output = output);

            var wfContext = JObject.FromObject(new {Input="test"});

            wf.Run(wfContext);
            dynamic o = wfContext;
            Assert.That(((string)(o.Output)), Is.EqualTo("test!!!"), "delegate was not executed");
            Assert.That(wf.Nodes["node"].ActivityType, Is.EqualTo("DelegateActivity ActivityMethod"), "Wrong activity type");
        }

        public string ActivityMethod(string input)
        {
            return input + "!!!";
        }

        [Test]
        public void ExecutionObserverStraightExecutionFlowTest()
        {
            StubExecutionObserver eo = new StubExecutionObserver();
            var wf = new Workflow<List<string>>(new InMemoryPersister<List<string>>(), null, eo);
            wf.Configure(cfg => cfg.Do("node1").Do("node2").End());
            wf.Node<TestActivity1>("node1").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity1"));
            wf.Node<TestActivity2>("node2").WithInput(list => list).ProcessOutput((context, output) => context.Add("TestActivity2"));

            var wfContext = new List<string>();
            var execution = wf.Run(wfContext);
            Assert.That(execution.State, Is.EqualTo(WorkflowState.Complete));
            Assert.That(wfContext, Is.EquivalentTo(new[] { "TestActivity1", "TestActivity2" }), "Wrong activities were executed");

            CollectionAssert.AreEqual(new[]{"start","node1", "node2","end"}, eo.State.Keys);
        }
    }

    internal class StubExecutionObserver : IExecutionObserver
    {
        public Dictionary<string, string> State { get; } = new Dictionary<string, string>();

        public void ActivityStarted(Guid activityExecutionId, string node, string activityType, object inputValues)
        {
            State.Add(node, "started");
        }

        public void ActivityFinished(Guid activityExecutionId, string node, string activityType, object outputValues)
        {
            if(!State.ContainsKey(node)) throw new Exception($"Trying to finish not started node {node}");
            State[node] = "finished";
        }

        public void ActivityFailed(Guid activityExecutionId, string node, string activityType, object outputValues)
        {
            if (!State.ContainsKey(node)) throw new Exception($"Trying to fail not started node {node}");
            State[node] = "failed";
        }

        public void ActivityCorrupted(Guid activityExecutionId, string node, string activityType)
        {
            if (!State.ContainsKey(node)) throw new Exception($"Trying to corrupt not started node {node}");
            State[node] = "corrupted";
        }
    }

    public class InMemoryPersister<TContext> : IWorkflowPersister<TContext>
    {
        readonly Dictionary<TContext, Execution> m_Cahce = new Dictionary<TContext, Execution>();
        public void Save(TContext context, Execution execution)
        {
            if (!m_Cahce.ContainsValue(execution))
                m_Cahce.Add(context, execution);
        }

        public Execution Load(TContext context)
        {
            return m_Cahce[context];
        }
    }

    class Executable
    {
        public string AccountType { get; set; }
        public string DiasDoc { get; set; }
    }
}
