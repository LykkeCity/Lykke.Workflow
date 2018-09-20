using System;

namespace Lykke.Workflow
{
    internal class EmptyActivitySlot<TContext> :  IActivitySlot<TContext>
    {
        public string ActivityType { get; }

        public EmptyActivitySlot(string activityType)
        {
            ActivityType = activityType ?? throw new ArgumentNullException();
        }

        public ActivityResult Execute(
            Guid activityExecutionId,
            IActivityFactory factory,
            TContext context,
            IActivityInputProvider inputProvider,
            out object activityOutput,
            Action<object> beforeExecute)
        {
            beforeExecute(null);
            activityOutput = null;
            return ActivityResult.Succeeded;
        }

        public ActivityResult Resume<TClosure>(
            Guid activityExecutionId,
            IActivityFactory factory,
            TContext context,
            TClosure closure,
            out object activityOutput)
        {
            activityOutput = null;
            return ActivityResult.Succeeded;
        }

        public ActivityResult Complete(
            Guid activityExecutionId,
            IActivityFactory factory,
            TContext context,
            IActivityOutputProvider outputProvider,
            out object activityOutput)
        {
            activityOutput = null;
            return ActivityResult.Succeeded;
        }
    }
}