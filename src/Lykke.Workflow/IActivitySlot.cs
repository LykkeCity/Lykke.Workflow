using System;

namespace Lykke.Workflow
{
    interface IActivitySlot<TContext>
    {
        string ActivityType { get; }
        ActivityResult Execute(
            Guid activityExecutionId,
            IActivityFactory factory,
            TContext context,
            IActivityInputProvider inputProvider,
            out object activityOutput,
            Action<object> beforeExecute);
        ActivityResult Resume<TClosure>(
            Guid activityExecutionId,
            IActivityFactory factory,
            TContext context,
            TClosure closure,
            out object activityOutput);
        ActivityResult Complete(
            Guid activityExecutionId,
            IActivityFactory factory,
            TContext context,
            IActivityOutputProvider outputProvider,
            out object activityOutput);
    }

    public interface IActivitySlot<TContext, TInput, TOutput, TFailOutput> : IHideObjectMembers
    {
        IActivitySlot<TContext, TInput, TOutput, TFailOutput> ProcessOutput(Action<TContext, TOutput> processOutput);
        IActivitySlot<TContext, TInput, TOutput, TFailOutput> ProcessFailOutput(Action<TContext, TFailOutput> processFailOutput);
    }
}