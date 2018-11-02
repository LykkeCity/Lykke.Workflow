using System;

namespace Lykke.Workflow.Executors
{
    internal class ResumeAfterWorkflowExecutor<TContext> : WorkflowExecutorBase<TContext>, IActivityOutputProvider
    {
        private readonly IActivityOutputProvider m_OutputProvider;
        private readonly object m_Output;

        public ResumeAfterWorkflowExecutor(
            Execution execution,
            TContext context,
            Workflow<TContext> workflow,
            IActivityFactory factory,
            IExecutionObserver observer,
            object output)
            : base(
                execution,
                context,
                workflow,
                factory,
                observer)
        {
            m_Output = output;
        }

        public ResumeAfterWorkflowExecutor(
            Execution execution,
            TContext context,
            Workflow<TContext> workflow,
            IActivityFactory factory,
            IExecutionObserver observer,
            IActivityOutputProvider outputProvider) 
            : base(
                execution,
                context,
                workflow,
                factory,
                observer)
        {
            m_OutputProvider = outputProvider;
        }

        protected override ActivityResult VisitNode(
            IGraphNode<TContext> node,
            Guid activityExecutionId,
            out object activityOutput)
        {
            ExecutionObserver.ActivityStarted(
                activityExecutionId,
                node.Name,
                node.ActivityType + " [FAKE]",
                null);
            return node.ActivitySlot.Complete(
                activityExecutionId,
                Factory,
                Context,
                m_OutputProvider ?? this,
                out activityOutput);
        }

        public TOutput GetOuput<TOutput>()
        {
            return (TOutput) m_Output;
        }

        protected override WorkflowExecutorBase<TContext> GetNextNodeVisitor()
        {
            return new WorkflowExecutor<TContext>(
                Execution,
                Context,
                Workflow,
                Factory,
                ExecutionObserver);
        }
    }
}