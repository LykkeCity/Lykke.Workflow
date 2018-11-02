using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using Lykke.Workflow.Executors;
using Lykke.Workflow.Fluent;

namespace Lykke.Workflow
{
    public class Workflow<TContext> : IActivityFactory , INodesResolver<TContext> , IDisposable
    {
        private readonly IGraphNode<TContext> m_End;
        private readonly IGraphNode<TContext> m_Fail;
        private readonly Dictionary<string, IGraphNode<TContext>> m_Nodes = new Dictionary<string, IGraphNode<TContext>>();
        private readonly IGraphNode<TContext> m_Start;
        private readonly IWorkflowPersister<TContext> m_Persister;
        private readonly IActivityFactory  m_ActivityFactory;
        private readonly IExecutionObserver m_ExecutionObserver;

        internal Dictionary<string, IGraphNode<TContext>> Nodes => m_Nodes;

        internal IGraphNode<TContext> Start => m_Start;

        [Obsolete("Name is not required for workflow. Will be removed with on next version change.")]
        public string Name { get; set; }

        public Workflow(
            IWorkflowPersister<TContext> persister,
            IActivityFactory  activityFactory = null,
            IExecutionObserver executionObserver = null)
        {
            m_ExecutionObserver = executionObserver;
            m_ActivityFactory = activityFactory ?? this;
            m_Persister = persister;
            m_Start = new GraphNode<TContext>("start");
            m_End = new GraphNode<TContext>("end");
            m_Fail = new GraphNode<TContext>("fail");
            RegisterNode(m_Start);
            RegisterNode(m_End);
            RegisterNode(m_Fail);
        }

        [Obsolete("Name is not required for workflow. Will be removed with on next version change.")]
        public Workflow(
            string name,
            IWorkflowPersister<TContext> persister,
            IActivityFactory activityFactory = null,
            IExecutionObserver executionObserver = null)
            : this(
                persister,
                activityFactory,
                executionObserver)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        #region IActivityFactory<TContext> Members

        TActivity IActivityFactory.Create<TActivity>(object activityCreationParams)
        {
            var values = new Dictionary<string, object>();
            foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(activityCreationParams))
            {
                var value = descriptor.GetValue(activityCreationParams);
                values.Add(descriptor.Name, value);
            }

            var constructor = typeof(TActivity)
                .GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault(info => info.GetParameters().All(p => values.ContainsKey(p.Name) && values[p.Name].GetType() == p.ParameterType));
            if (constructor == null)
                throw new MissingMethodException("No public constructor defined for this object");

            var instance = constructor.Invoke(constructor.GetParameters().Select(p => values[p.Name]).ToArray());
            return (TActivity)instance;
        }

        public void Release<TActivity>(TActivity activity)
            where TActivity : IActivityWithOutput<object, object, object>
        {
        }

        #endregion

        #region INodesResolver<TContext> Members

        IGraphNode<TContext> INodesResolver<TContext>.this[string name] => GetNode(name);

        #endregion

        public void Configure(Action<WorkflowConfiguration<TContext>> configure)
        {
            var conf = new WorkflowConfiguration<TContext>(this);

            configure(conf);

            string[] errors = Nodes.Values
                .SelectMany(n => n.Edges
                    .Where(e => !Nodes.ContainsKey(e.Node))
                    .Select(e => $"Node '{n.Name}' references unknown node '{e.Node}'"))
                .Union(
                    Nodes.Values
                        .Where(n => n.Name != "end" &&  n.Name != "fail" && !n.Edges.Any())
                        .Select(n => $"Node '{n.Name}' is not connected with any other node."))
                .ToArray();
            if (errors.Any())
                throw new ApplicationException(string.Join(Environment.NewLine, errors));
        }

        public virtual Execution Run(TContext context)
        {
            var execution = new Execution { State = WorkflowState.InProgress };
            var executor = new WorkflowExecutor<TContext>(
                execution,
                context,
                this,
                m_ActivityFactory,
                m_ExecutionObserver);
            try
            {
                Accept(executor);
            }
            catch (Exception e)
            {
                execution.Error = e.ToString();
                execution.State = WorkflowState.Corrupted;
            }
            m_Persister.Save(context, execution);
            return execution;
        }

        public virtual Execution Resume<TClosure>(
            TContext context,
            Guid activityExecutionId,
            TClosure closure)
        {
            var execution = m_Persister.Load(context);

            var activityExecution = execution.ExecutingActivities.FirstOrDefault(a => a.Id == activityExecutionId);
            if (activityExecution == null)
            {
                execution.Error = $"Failed to resume. Provided activity execution id '{activityExecutionId}' not found";
                execution.State = WorkflowState.Corrupted;
            }
            else
            {
                var executor = new ResumeWorkflowExecutor<TContext>(
                    execution,
                    context,
                    this,
                    m_ActivityFactory,
                    m_ExecutionObserver,
                    activityExecution,
                    closure);
                try
                {
                    string node = activityExecution.Node;
                    Accept(executor, node);
                }
                catch (Exception e)
                {
                    execution.Error = e.ToString();
                    execution.State = WorkflowState.Corrupted;
                }
            }
            m_Persister.Save(context, execution);
            return execution;
        }

        public virtual Execution ResumeAfter(
            TContext context,
            string node,
            IActivityOutputProvider outputProvider)
        {
            var execution = m_Persister.Load(context);

            var executor = new ResumeAfterWorkflowExecutor<TContext>(
                execution,
                context,
                this,
                m_ActivityFactory,
                m_ExecutionObserver,
                outputProvider);
            try
            {
                Accept(executor, node);
            }
            catch (Exception e)
            {
                execution.Error = e.ToString();
                execution.State = WorkflowState.Corrupted;
            }
            m_Persister.Save(context, execution);
            return execution;
        }

        public virtual Execution ResumeFrom(
            TContext context,
            string node,
            IActivityInputProvider inputProvider)
        {
            var execution = m_Persister.Load(context);
            var executor = new ResumeFromWorkflowExecutor<TContext>(
                execution,
                context,
                this,
                m_ActivityFactory,
                m_ExecutionObserver,
                inputProvider);
            try
            {
                Accept(executor, node);
            }
            catch (Exception e)
            {
                execution.Error = e.ToString();
                execution.State = WorkflowState.Corrupted;
            }
            m_Persister.Save(context, execution);
            return execution;
        }

        public virtual Execution ResumeFrom(
            TContext context,
            string node,
            object input = null)
        {
            var execution = m_Persister.Load(context);
            var executor = new ResumeFromWorkflowExecutor<TContext>(
                execution,
                context,
                this,
                m_ActivityFactory,
                m_ExecutionObserver,
                input);
            try
            {
                Accept(executor, node);
            }
            catch (Exception e)
            {
                execution.Error = e.ToString();
                execution.State = WorkflowState.Corrupted;
            }
            m_Persister.Save(context, execution);
            return execution;
        }

        internal IGraphNode<TContext> CreateNode(string name, params string[] aliases)
        {
            if (m_Nodes.ContainsKey(name))
                throw new ApplicationException($"Can not create node '{name}', node with this name already exists");
            var node = new GraphNode<TContext>(name);
            RegisterNode(node, aliases);
            return node;
        }

        private void RegisterNode(IGraphNode<TContext> node, params string[] aliases)
        {
            m_Nodes.Add(node.Name, node);
            foreach (string alias in aliases)
            {
                m_Nodes.Add(alias, node);
            }
        }

        private T Accept<T>(IWorkflowVisitor<TContext, T> workflowExecutor, string startFrom = null)
        {
            IGraphNode<TContext> node = startFrom == null ? m_Start : m_Nodes[startFrom];
            return node.Accept(workflowExecutor);
        }

        public ActivityResult Execute(
            string activityType,
            string nodeName,
            dynamic input,
            Action<dynamic> processOutput,
            Action<dynamic> processFailOutput)
        {
            return ActivityResult.Failed;
        }
 
        public override string ToString()
        {
            var generator = new GraphvizGenerator<TContext>(this);
            return string.Format(@"digraph {{
graph [ resolution=64];

{0}
}}", Accept(generator));
 
        }

        public void Dispose()
        {
//TODO[KN]: release activity
        }

        public ISlotCreationHelper<TContext, TActivity> Node<TActivity>(
            string name,
            object activityCreationParams = null,
            string activityType = null)
            where TActivity : IActivityWithOutput<object, object, object>
        {
            return GetNode(name).Activity<TActivity>(activityType ?? typeof(TActivity).Name, activityCreationParams);
        }

        private IGraphNode<TContext> GetNode(string name)
        {
            if(!Nodes.ContainsKey(name))
                throw new ApplicationException($"Node '{name}' not found");
            return Nodes[name];
        }

        public ISlotCreationHelper<TContext, DelegateActivity<TInput, TOutput>> DelegateNode<TInput, TOutput>(string name, Expression<Func<TInput, TOutput>> method)
            where TInput : class where TOutput : class
        {
            string activityType = GetActivityType(method);
            var activityMethod = method.Compile();
            return GetNode(name).Activity<DelegateActivity<TInput, TOutput>>(activityType, new { activityMethod, isInputSerializable = true });
        }

        public IActivitySlot<TContext, object, TOutput, Exception> DelegateNode<TOutput>(string name, Expression<Func<TContext, TOutput>> method)
            where TOutput : class
        {
            string activityType = GetActivityType(method);
            Func<TContext, TOutput> compiled = method.Compile();
            Func<object, TOutput> activityMethod = context => compiled((TContext)context);
            return GetNode(name)
                .Activity<DelegateActivity<object, TOutput>>(activityType, new { activityMethod, isInputSerializable = false })
                .WithInput(context => (object)context);
        }

        public IActivitySlot<TContext, object, object, Exception> DelegateNode(string name, Expression<Action<TContext>> method) 
        {
            string activityType = GetActivityType(method);
            Action<TContext> compiled = method.Compile();
            Func<object,object> activityMethod = context =>
            {
                compiled((TContext) context);
                return null;
            };
            return GetNode(name)
                .Activity<DelegateActivity<object, object>>(activityType, new { activityMethod, isInputSerializable = false })
                .WithInput(context => context as object);
        }

        public ISlotCreationHelper<TContext, DelegateActivity<TInput, object>> DelegateNode<TInput>(string name, Expression<Action<TInput>> method) 
            where TInput : class
        {
            string activityType = GetActivityType(method);
            Action<TInput> compiled = method.Compile();
            Func<object,object> activityMethod = input =>
            {
                compiled((TInput) input);
                return null;
            };
            return GetNode(name).Activity<DelegateActivity<TInput, object>>(activityType, new { activityMethod, isInputSerializable = true });
        }

        private static string GetActivityType(LambdaExpression method)
        {
            var methodCall = method.Body as MethodCallExpression;
            return methodCall == null
                ? "DelegateActivity"
                : $"DelegateActivity {methodCall.Method.Name}";
        }
    }
}