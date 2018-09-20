using System.Collections.Generic;

namespace Lykke.Workflow
{
    internal class YumlClassGenerator<TContext> : IWorkflowVisitor<TContext, string>
    {
        private readonly List<IGraphNode<TContext>> m_Visited = new List<IGraphNode<TContext>>();
        private readonly IDictionary<string, IGraphNode<TContext>> m_Nodes;

        public YumlClassGenerator(IDictionary<string, IGraphNode<TContext>> nodes)
        {
            m_Nodes = nodes;
        }

        public string Visit(IGraphNode<TContext> node)
        {
            m_Visited.Add(node);
            string res = "";
            foreach (var edge in node.Edges)
            {
                var nextNode = m_Nodes[edge.Node];
                res += $"[{node.Name}]{edge.Description ?? ""}->[{edge.Node}]\n";
                if (!m_Visited.Contains(nextNode))
                    res += nextNode.Accept(this);
            }
            return res;
        }
    }
}