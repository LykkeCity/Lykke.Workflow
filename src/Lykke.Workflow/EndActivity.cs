using System;

namespace Lykke.Workflow
{
    public class EndActivity  : ActivityBase<object,object,object>
    {
        public override ActivityResult Execute(
            Guid activityExecutionId,
            object input,
            Action<object> processOutput,
            Action<object> processFailOutput)
        {
            return ActivityResult.Succeeded;
        }
    }
}