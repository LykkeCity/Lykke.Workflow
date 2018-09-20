namespace Lykke.Workflow
{
    internal interface INodesResolver<TContext>
    {
        IGraphNode<TContext> this[string name] { get; }
    }
}