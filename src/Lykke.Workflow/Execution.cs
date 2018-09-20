using System;
using System.Collections.Generic;

namespace Lykke.Workflow
{
    public class ActivityExecution
    {
        public Guid Id { get; }
        public string Node { get; }

        public ActivityExecution(string node, Guid id)
        {
            Id = id;
            Node = node;
        }

        public ActivityExecution(string node)
            : this(node, Guid.NewGuid())
        {
        }
    }

    public class Execution<TContext>
    {
        public WorkflowState State { get; set; }
        public string Error { get; set; }
        public List<ActivityExecution> ExecutingActivities { get; }

        public Execution()
        {
            ExecutingActivities = new List<ActivityExecution>();
        }
    }

    public interface IExecutionObserver
    {
        void ActivityStarted(
            Guid activityExecutionId,
            string node,
            string activityType,
            object inputValues);
        void ActivityFinished(
            Guid activityExecutionId,
            string node,
            string activityType,
            object outputValues);
        void ActivityFailed(
            Guid activityExecutionId,
            string node,
            string activityType,
            object outputValues);
        void ActivityCorrupted(
            Guid activityExecutionId,
            string node,
            string activityType);
    }
}