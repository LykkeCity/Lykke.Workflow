using System;

namespace Lykke.Workflow
{
    public class DelegateActivity<TInput, TOutput> : ActivityBase<TInput, TOutput, Exception>
        where TInput : class
        where TOutput : class
    {
        private readonly Func<TInput, TOutput> m_ActivityMethod;

        public override bool IsInputSerializable { get; }

        public DelegateActivity(Func<TInput, TOutput> activityMethod, bool isInputSerializable)
        {
            m_ActivityMethod = activityMethod;
            IsInputSerializable = isInputSerializable;
        }

        public override ActivityResult Execute(
            Guid activityExecutionId,
            TInput input,
            Action<TOutput> processOutput,
            Action<Exception> processFailOutput)
        {
            try
            {
                var output = m_ActivityMethod(input);
                processOutput(output);
                return ActivityResult.Succeeded;
            }
            catch (Exception e)
            {
                processFailOutput(e);
                return ActivityResult.Failed;
            }
        }
    }
}