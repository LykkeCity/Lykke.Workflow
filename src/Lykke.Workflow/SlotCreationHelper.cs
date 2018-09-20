using System;

namespace Lykke.Workflow
{
    public interface ISlotCreationHelper<TContext, out TActivity> : IHideObjectMembers
    {
    }

    interface ISlotCreationHelperWithNode<TContext>
    {
        GraphNode<TContext> GraphNode { get; }
        string ActivityType { get; }
        IActivity<TInput, TOutput, TFailOutput> CreateActivity<TInput, TOutput, TFailOutput>(IActivityFactory activityFactory)
            where TInput : class
            where TOutput : class
            where TFailOutput : class;
    }

    internal class SlotCreationHelper<TContext, TActivity> : ISlotCreationHelper<TContext,  TActivity>, ISlotCreationHelperWithNode<TContext>
        where TActivity : IActivityWithOutput<object, object, object>
    {
        private readonly object m_ActivityCreationParams;

        public GraphNode<TContext> GraphNode { get; }

        public string ActivityType { get; }

        public SlotCreationHelper(
            GraphNode<TContext> graphNode,
            string activityType,
            object activityCreationParams)
        {
            ActivityType = activityType;
            m_ActivityCreationParams = activityCreationParams;
            GraphNode = graphNode;
        }

        public IActivity<TInput, TOutput, TFailOutput> CreateActivity<TInput, TOutput, TFailOutput>(IActivityFactory activityFactory)
            where TInput : class
            where TOutput : class
            where TFailOutput : class
        {
            return (IActivity<TInput, TOutput, TFailOutput>) activityFactory.Create<TActivity>(m_ActivityCreationParams);
        }
    }

    public static class SlotCreationHelperExtensions
    {
        public static IActivitySlot<TContext, TInput, TOutput, TFailOutput> WithInput<TContext, TInput, TOutput, TFailOutput>(
            this ISlotCreationHelper<TContext, IActivity<TInput, TOutput, TFailOutput>> n, Func<TContext, TInput> getInput)
            where TInput : class
            where TOutput : class
            where TFailOutput : class
        {
            var helper = (n as ISlotCreationHelperWithNode<TContext>);
            var activitySlot = new ActivitySlot<TContext, TInput, TOutput, TFailOutput>(
                activityFactory => helper.CreateActivity<TInput, TOutput, TFailOutput>(activityFactory),
                getInput,
                helper.ActivityType);
            helper.GraphNode.AddActivitySlot(activitySlot);
            return activitySlot;
        }
    }
}