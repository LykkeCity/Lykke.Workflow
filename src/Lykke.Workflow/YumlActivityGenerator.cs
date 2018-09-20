using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lykke.Workflow
{
    internal class YumlActivityGenerator<TContext> : IWorkflowVisitor<TContext, string>
    {
        private readonly Dictionary<char, string> m_Traslit = new Dictionary<char, string>()
        {
            {'à',"a"},{'á',"b"},{'â',"v"},{'ã',"g"},{'ä',"d"},{'å',"e"},{'æ',"zh"},{'ç',"z"},{'è',"i"},{'é',"y"},{'ê',"k"},{'ë',"l"},{'ì',"m"},{'í',"n"},{'î',"o"},{'ï',"p"},{'ð',"r"},{'ñ',"s"},{'ò',"t"},{'ó',"u"},{'ô',"f"},{'õ',"h"},{'ö',"c"},{'÷',"ch"},{'ø',"sh"},{'ù',"shh"},{'ú',""},{'û',"i"},{'ü',""},{'ý',"e"},{'þ',"u"},{'ÿ',"ya"},
            {'À',"A"},{'Á',"B"},{'Â',"V"},{'Ã',"G"},{'Ä',"D"},{'Å',"E"},{'Æ',"ZH"},{'Ç',"Z"},{'È',"I"},{'É',"Y"},{'Ê',"K"},{'Ë',"L"},{'Ì',"M"},{'Í',"N"},{'Î',"O"},{'Ï',"P"},{'Ð',"R"},{'Ñ',"S"},{'Ò',"T"},{'Ó',"U"},{'Ô',"F"},{'Õ',"H"},{'Ö',"C"},{'×',"CH"},{'Ø',"SH"},{'Ù',"SHH"},{'Ú',""},{'Û',"I"},{'Ü',""},{'Ý',"E"},{'Þ',"U"},{'ß',"YA"},
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