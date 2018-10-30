using System;

namespace Lykke.Workflow.Executors
{
    internal class WorkflowExecutor<TContext> : WorkflowExecutorBase<TContext>
    {
        public WorkflowExecutor(
            Execution<TContext> execution,
            TContext context,
            INodesResolver<TContext> nodes,
            IActivityFactory factory,
            IExecutionObserver observer)
            : base(
                execution,
                context,
                nodes,
                factory,
                observer)
        {
        }

        protected override ActivityResult VisitNode(
            IGraphNode<TContext> node,
            Guid activityExecutionId,
            out object activityOutput)
        {
            var telemtryOperation = TelemetryHelper.InitTelemetryOperation(
                "Workflow node execution",
                node.Name,
                node.ActivityType,
                activityExecutionId.ToString());

            try
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
            catch (Exception e)
            {
                TelemetryHelper.SubmitException(telemtryOperation, e);
                throw;
            }
            finally
            {
                TelemetryHelper.SubmitOperationResult(telemtryOperation);
            }
        }
    }
}