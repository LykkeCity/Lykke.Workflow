using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lykke.Workflow
{
    internal class YumlActivityGenerator<TContext> : IWorkflowVisitor<TContext, string>
    {
        private readonly Dictionary<char, string> m_Traslit = new Dictionary<char, string>()
        {
            {'�',"a"},{'�',"b"},{'�',"v"},{'�',"g"},{'�',"d"},{'�',"e"},{'�',"zh"},{'�',"z"},{'�',"i"},{'�',"y"},{'�',"k"},{'�',"l"},{'�',"m"},{'�',"n"},{'�',"o"},{'�',"p"},{'�',"r"},{'�',"s"},{'�',"t"},{'�',"u"},{'�',"f"},{'�',"h"},{'�',"c"},{'�',"ch"},{'�',"sh"},{'�',"shh"},{'�',""},{'�',"i"},{'�',""},{'�',"e"},{'�',"u"},{'�',"ya"},
            {'�',"A"},{'�',"B"},{'�',"V"},{'�',"G"},{'�',"D"},{'�',"E"},{'�',"ZH"},{'�',"Z"},{'�',"I"},{'�',"Y"},{'�',"K"},{'�',"L"},{'�',"M"},{'�',"N"},{'�',"O"},{'�',"P"},{'�',"R"},{'�',"S"},{'�',"T"},{'�',"U"},{'�',"F"},{'�',"H"},{'�',"C"},{'�',"CH"},{'�',"SH"},{'�',"SHH"},{'�',""},{'�',"I"},{'�',""},{'�',"E"},{'�',"U"},{'�',"YA"},
        };
        private readonly List<IGraphNode<TContext>> m_Visited = new List<IGraphNode<TContext>>();
        private readonly INodesResolver<TContext> m_Nodes;

        public YumlActivityGenerator(INodesResolver<TContext> nodes)
        {
            m_Nodes = nodes;
        }

        public string Visit(IGraphNode<TContext> node)
        {
            m_Visited.Add(node);
            string res = "";
            if (node.Edges.Count() > 1)
            {
                var translit = Translit(node.Name);
                res += $"({translit})-><{translit} decision>";
            }

            foreach (var edge in node.Edges)
            {
                var nextNode = m_Nodes[edge.Node];
                if (res != "") res += ",";
                res += string.Format("{0}{2}->{1}",
                    NodeStringFrom(node),
                    NodeStringTo(nextNode),
                    string.IsNullOrEmpty(edge.Description) || node.Edges.Count() == 1 ? "" : Translit("[" + edge.Description + "]"));
                if (!m_Visited.Contains(nextNode))
                    res += "," + nextNode.Accept(this);
            }
            return res;
        }

        private string NodeStringTo(IGraphNode<TContext> node)
        {
            return $"({Translit(node.Name)})";
        }
        private string NodeStringFrom(IGraphNode<TContext> node) 
        {
            if (node.Edges.Count() > 1)
                return $"<{Translit(node.Name)} decision>";
            return $"({Translit(node.Name)})";
        }

        private string Translit(string str)
        {
            return str.Aggregate(
                new StringBuilder(),
                (builder, c) => builder.Append(m_Traslit.ContainsKey(c) ? m_Traslit[c] : c.ToString()),
                builder => builder.ToString());
        }
    }
}