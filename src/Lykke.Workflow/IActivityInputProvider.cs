namespace Lykke.Workflow
{
    public interface IActivityInputProvider
    {
        TInput GetInput<TInput>();
    }
}