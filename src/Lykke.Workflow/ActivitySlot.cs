using System;

namespace Lykke.Workflow
{
    internal class ActivitySlot<TContext, TInput, TOutput, TFailOutput> : IActivitySlot<TContext>, IActivitySlot<TContext, TInput, TOutput, TFailOutput>
        where TInput : class
        where TOutput : class
        where TFailOutput : class
    {
        private readonly Func<TContext, TInput> m_GetActivityInput;
        private Action<TContext, TOutput> m_ProcessOutput= (context, output) => { };
        private Action<TContext, TFailOutput> m_ProcessFailOutput= (context, output) => { };
        private readonly Func<IActivityFactory, IActivity<TInput, TOutput, TFailOutput>> m_ActivityCreation;

        public string ActivityType { get; }

        public ActivitySlot(
            Func<IActivityFactory, IActivity<TInput, TOutput, TFailOutput>> activityCreation,
            Func<TContext, TInput> getInput,
            string activityType)
        {
            m_ActivityCreation = activityCreation;
            m_GetActivityInput = getInput;
            ActivityType = activityType;
        }

        public  IActivitySlot<TContext, TInput, TOutput, TFailOutput> ProcessOutput(Action<TContext, TOutput> processOutput)
        {
            m_ProcessOutput = processOutput;
            return this;
        }

        public  IActivitySlot<TContext, TInput, TOutput, TFailOutput> ProcessFailOutput(Action<TContext, TFailOutput> processFailOutput)
        {
            m_ProcessFailOutput = processFailOutput;
            return this;
        }

        public ActivityResult Execute(
            Guid activityExecutionId,
            IActivityFactory factory,
            TContext context,
            IActivityInputProvider inputProvider,
            out object activityOutput,
            Action<object> beforeExecute)
        {
            IActivity<TInput, TOutput, TFailOutput> activity = null;
            try
            {
                activity = m_ActivityCreation(factory);
                object actout = null;

                TInput activityInput = null;
                try
                {
                    if (inputProvider != null)
                        activityInput = inputProvider.GetInput<TInput>();
                    if (activityInput == null)
                        activityInput = m_GetActivityInput(context);
                    beforeExecute(activity.IsInputSerializable ? activityInput : null);
                }
                catch (Exception e)
                {
                    beforeExecute("Failed to get activity  input: " + e);
                    throw;
                }
                var result = activity.Execute(
                    activityExecutionId,
                    activityInput,
                    output =>
                    {
                        actout = output;
                        m_ProcessOutput(context, output);
                    },
                    output =>
                    {
                        actout = output;
                        m_ProcessFailOutput(context, output);
                    });
                activityOutput = actout;
                return result;
            }
            finally
            {
                if (activity != null)
                    factory.Release(activity);
            }
        }

        public ActivityResult Resume<TClosure>(
            Guid activityExecutionId,
            IActivityFactory factory,
            TContext context,
            TClosure closure,
            out object activityOutput)
        {
            IActivity<TInput, TOutput, TFailOutput> activity = null;
            try
            {
                activity = m_ActivityCreation(factory);

                object actout = null;
                var result = activity.Resume(
                    activityExecutionId,
                    output =>
                    {
                        actout = output;
                        m_ProcessOutput(context, output);
                    },
                    output =>
                    {
                        actout = output;
                        m_ProcessFailOutput(context, output);
                    },
                    closure);
                activityOutput = actout;
                return result;
            }
            finally
            {
                if (activity != null)
                    factory.Release(activity);
            }
        }

        public ActivityResult Complete(
            Guid activityExecutionId,
            IActivityFactory factory,
            TContext context,
            IActivityOutputProvider outputProvider,
            out object activityOutput)
        {
            var output = outputProvider.GetOuput<TOutput>();
            
            m_ProcessOutput(context, output);
            activityOutput = output;

            return ActivityResult.Succeeded;
        }
    }
}