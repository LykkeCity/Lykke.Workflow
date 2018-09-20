using System;

namespace Lykke.Workflow
{
    internal class GraphEdge<TContext>
    {
        public string Node { get; }
        public string Description { get; }
        public Func<TContext, ActivityResult, bool> Condition { get; }

        public GraphEdge(
            string node,
            Func<TContext, ActivityResult, bool> condition,
            string description)
        {
            Node = node;
            Description = description;
            Condition = condition ?? ((context, state) => true);
        }
    }
}