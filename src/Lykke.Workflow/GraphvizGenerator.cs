using System;
using System.Collections.Generic;
using System.Linq;

namespace Lykke.Workflow
{
    internal class GraphvizGenerator<TContext> : IWorkflowVisitor<TContext, string>
    {
        private readonly INodesResolver<TContext> m_Nodes;
        private readonly List<IGraphNode<TContext>> m_Visited = new List<IGraphNode<TContext>>();

        public GraphvizGenerator(INodesResolver<TContext> nodes)
        {
            m_Nodes = nodes;
        }

        public string Visit(IGraphNode<TContext> node)
        {
            m_Visited.Add(node);
            string res = "";
            if (node.Name != "fail")
            {
                res += string.Format(
                    "\"{0}\" [label=\"{0}\", shape={1}]",
                    node.Name,
                    node.Name == "end" || node.Name == "start"
                        ? "ellipse, style=filled,fillcolor=\"yellow\""
                        : "box");
                res += Environment.NewLine;
            }
            if (node.Edges.Count() > 1 || (node.Edges.Count() == 1 && node.Edges.First().Description != "Success"))
            {
                res += $"\"{node.Name}\"->\"{node.Name} decision\"";
                res += Environment.NewLine;
                res += $"\"{node.Name} decision\" [shape=diamond, label=\"\",style=filled,fillcolor=\"gray\"]";
                res += Environment.NewLine;
            }
            foreach (var edge in node.Edges.OrderBy(e=>e.Description=="Fail"?1:0))
            {
                var nextNode = m_Nodes[edge.Node];

                var nextNodeName = nextNode.Name;
                if (nextNodeName == "fail")
                {
                    res += $"\"{node.Name} fail\" [label=\"fail\",style=filled,fillcolor=\"red\"]";
                    res += Environment.NewLine;
                    nextNodeName = $"{node.Name} fail";
                }
                var edgeDescription = edge.Description;
                if (edgeDescription == "Success")
                    edgeDescription = "";
                if (node.Edges.Count() > 1 || edge.Description != "Success")
                    res += $"\"{node.Name} decision\"->\"{nextNodeName}\"  [label=\"{edgeDescription}\"]";
                else
                    res += $"\"{node.Name}\"->\"{nextNodeName}\"  [label=\"{edgeDescription}\"]";
                res += Environment.NewLine;
              /*  if (res != "") res += ",";
                res += string.Format("{0}{2}->{1}",
                    NodeStringFrom(node),
                    NodeStringTo(nextNode),
                    string.IsNullOrEmpty(edge.Description) || node.Edges.Count() == 1 ? "" : "[" + edge.Description + "]");*/
                if (!m_Visited.Contains(nextNode))
                    res +=  nextNode.Accept(this);
            }
            return res;
        }

        private string NodeStringTo(IGraphNode<TContext> node)
        {
            return $"({node.Name})";
        }

        private string NodeStringFrom(IGraphNode<TContext> node)
        {
            if (node.Edges.Count() > 1)
                return $"<{node.Name} decision>";
            return $"({node.Name})";
        }
    }
}