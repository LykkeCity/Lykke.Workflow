using System;
using System.Linq;

namespace Lykke.Workflow.Executors
{
    internal abstract class WorkflowExecutorBase<TContext> : IWorkflowVisitor<TContext, WorkflowState>
    {
        private readonly Workflow<TContext> _workflow;
        private readonly Execution m_Execution;
        private readonly TContext m_Context;
        private readonly IExecutionObserver m_ExecutionObserver;
        private readonly string _workflowTypeName;

        protected Workflow<TContext> Workflow => _workflow;

        protected IExecutionObserver ExecutionObserver => m_ExecutionObserver;

        protected IActivityFactory Factory { get; }

        protected TContext Context => m_Context;

        protected Execution Execution => m_Execution;

        protected WorkflowExecutorBase(
            Execution execution,
            TContext context,
            Workflow<TContext> workflow,
            IActivityFactory factory,
            IExecutionObserver observer)
        {
            m_ExecutionObserver = observer??new NullExecutionObserver();
            m_Context = context;
            Factory = factory;
            m_Execution = execution;
            _workflow = workflow;
            _workflowTypeName = _workflow.GetType().Name;
        }

        protected abstract ActivityResult VisitNode(
            IGraphNode<TContext> node,
            Guid activityExecutionId,
            out  object activityOutput);

        protected virtual ActivityExecution GetActivityExecution(IGraphNode<TContext> node)
        {
            var activityExecution = new ActivityExecution(node.Name);
            Execution.ExecutingActivities.Clear();
            Execution.ExecutingActivities.Add(activityExecution);
            return activityExecution;
        }

        public WorkflowState Visit(IGraphNode<TContext> node)
        {
            var activityExecution = GetActivityExecution(node);

            var telemtryOperation = TelemetryHelper.InitTelemetryOperation(
                _workflowTypeName,
                node.Name,
                node.ActivityType,
                activityExecution.Id.ToString());

            ActivityResult result;
            object activityOutput;
            try
            {
                result = VisitNode(
                    node,
                    activityExecution.Id,
                    out activityOutput);
            }
            catch (Exception e)
            {
                TelemetryHelper.SubmitException(telemtryOperation, e);
                throw;
            }
            finally
            {
                TelemetryHelper.SubmitOperationResult(telemtryOperation);
            }

            switch (result)
            {
                case ActivityResult.None:
                    m_Execution.State = WorkflowState.Corrupted;
                    m_ExecutionObserver.ActivityCorrupted(
                        activityExecution.Id,
                        node.Name,
                        node.ActivityType);
                    return WorkflowState.Corrupted;
                case ActivityResult.Failed:
                    m_ExecutionObserver.ActivityFailed(
                        activityExecution.Id,
                        node.Name,
                        node.ActivityType,
                        activityOutput);
                    m_Execution.ExecutingActivities.Remove(activityExecution);
                    break;
                case ActivityResult.Succeeded:
                    m_ExecutionObserver.ActivityFinished(
                        activityExecution.Id,
                        node.Name,
                        node.ActivityType,
                        activityOutput);
                    m_Execution.ExecutingActivities.Remove(activityExecution);
                    break;
                case ActivityResult.Pending:
                    m_Execution.State = WorkflowState.InProgress;
                    return WorkflowState.InProgress;
            }

            var edges = node.Edges.Where(e => e.Condition(m_Context, result)).ToArray();

            if(edges.Length > 1)
            {
                m_Execution.Error = "Failed to get next node - more then one transition condition was met: "
                    + Environment.NewLine
                    + string.Join(Environment.NewLine, edges.Select(e => $"[{node.Name}]-{e.Description}-> [{e.Node}]"));
                m_Execution.State = WorkflowState.Corrupted;
                return WorkflowState.Corrupted;
            }
            if (edges.Length == 0 && node.Name != "end" && node.Name != "fail")
            {
                m_Execution.Error = "Failed to get next node - none of transition condition was met: "
                    + Environment.NewLine
                    + string.Join(Environment.NewLine, node.Edges.Select(e => $"[{node.Name}]-{e.Description}-> [{e.Node}]"));
                m_Execution.State = WorkflowState.Corrupted;
                return WorkflowState.Corrupted;
            }

            var transition = edges.FirstOrDefault();
            if (transition != null)
            {
                var nextNode = ((INodesResolver<TContext>)_workflow)[transition.Node];
                var nextResult = nextNode.Accept(GetNextNodeVisitor());
                return nextResult;
            }

            //TODO: =="end" is not good idea
            if (node.Name == "end" && result == ActivityResult.Succeeded)
            {
                m_Execution.State = WorkflowState.Complete;
                return WorkflowState.Complete;
            }

            //TODO: =="end" is not good idea
            if (node.Name == "fail")
            {
                m_Execution.State = WorkflowState.Failed;
                return WorkflowState.Failed;
            }

            m_Execution.State = WorkflowState.Corrupted;
            return WorkflowState.Corrupted;
        }

        protected virtual WorkflowExecutorBase<TContext> GetNextNodeVisitor()
        {
            return this;
        }
    }
}