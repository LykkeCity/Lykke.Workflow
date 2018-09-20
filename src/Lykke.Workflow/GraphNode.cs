using System;
using System.Collections.Generic;

namespace Lykke.Workflow
{
    internal class GraphNode<TContext> : IGraphNode<TContext>
    {
        private readonly List<GraphEdge<TContext>> m_Constraints = new List<GraphEdge<TContext>>();

        private IActivitySlot<TContext> m_ActivitySlot;

        public string Name { get; }

        public string ActivityType => ActivitySlot != null ? ActivitySlot.ActivityType : "";

        public IActivitySlot<TContext> ActivitySlot => m_ActivitySlot;

        public IEnumerable<GraphEdge<TContext>> Edges => m_Constraints;

        public GraphNode(string name)
        {
            Name = name;
            m_ActivitySlot = new EmptyActivitySlot<TContext>(name);
        }

        public T Accept<T>(IWorkflowVisitor<TContext, T> workflowExecutor)
        {
            return workflowExecutor.Visit(this);
        }

        public virtual void AddConstraint(
            string node,
            Func<TContext, ActivityResult, bool> condition,
            string description)
        {
            m_Constraints.Add(
                new GraphEdge<TContext>(
                    node,
                    condition,
                    description));
        }

        public ISlotCreationHelper<TContext, TActivity> Activity<TActivity>(string activityType, object activityCreationParams = null)
            where TActivity : IActivityWithOutput<object, object, object>
        {
            m_ActivitySlot = new EmptyActivitySlot<TContext>(activityType);
            return new SlotCreationHelper<TContext, TActivity>(
                this,
                activityType,
                activityCreationParams);
        }

        public void AddActivitySlot(IActivitySlot<TContext> activitySlot)
        {
            m_ActivitySlot = activitySlot;
        }
    }
}