using System;
using System.Collections.Generic;

namespace Lykke.Workflow
{
    internal interface IWorkflowVisitor<TContext,  out TResult>
    {
        TResult Visit(IGraphNode<TContext> node);
    }

    internal interface IGraphNode<TContext>
    {
        string Name { get; }
        string ActivityType { get;  }
        IEnumerable<GraphEdge<TContext>> Edges { get; }
        IActivitySlot<TContext> ActivitySlot { get; }

        T Accept<T>(IWorkflowVisitor<TContext, T> workflowExecutor);
        void AddConstraint(
            string node,
            Func<TContext, ActivityResult, bool> condition,
            string description);
        ISlotCreationHelper<TContext, TActivity> Activity<TActivity>(string activityName, object activityCreationParams = null)
            where TActivity : IActivityWithOutput<object, object, object>;
    }
}