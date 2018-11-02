using System;

namespace Lykke.Workflow.Executors
{
    internal class WorkflowExecutor<TContext> : WorkflowExecutorBase<TContext>
    {
        public WorkflowExecutor(
            Execution execution,
            TContext context,
            Workflow<TContext> workflow,
            IActivityFactory factory,
            IExecutionObserver observer)
            : base(
                execution,
                context,
                workflow,
                factory,
                observer)
        {
        }

        protected override ActivityResult VisitNode(
            IGraphNode<TContext> node,
            Guid activityExecutionId,
            out object activityOutput)
        {
            return node.ActivitySlot.Execute(
                activityExecutionId,
                Factory,
                Context,
                null,
                out activityOutput,
                activityInput => ExecutionObserver.ActivityStarted(
                    activityExecutionId,
                    node.Name,
                    node.ActivityType,
                    activityInput));
        }
    }
}