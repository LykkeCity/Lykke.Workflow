namespace Lykke.Workflow
{
    public interface IActivityOutputProvider
    {
        TOutput GetOuput<TOutput>();
    }
}