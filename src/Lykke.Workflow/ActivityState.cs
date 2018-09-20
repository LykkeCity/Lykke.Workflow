namespace Lykke.Workflow
{
    public class ActivityState
    {
        public dynamic Values { get; set; }
        public string NodeName { get; set; }
        public ActivityResult Status { get; set; }

        public override string ToString()
        {
            return $"ActivityState for node name {NodeName} status {Status}";
        }
    }
}