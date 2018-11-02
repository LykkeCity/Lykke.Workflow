namespace Lykke.Workflow
{
    public interface IWorkflowPersister<TContext>
    {
        void Save(TContext context, Execution execution);
        Execution Load(TContext context);
    }
}