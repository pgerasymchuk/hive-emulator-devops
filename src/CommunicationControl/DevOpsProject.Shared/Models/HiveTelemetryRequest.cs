using DevOpsProject.Shared.Enums;

namespace DevOpsProject.Shared.Models
{
    public class HiveTelemetryRequest
    {
        public string HiveID { get; set; }
        public Location Location { get; set; }
        public float Speed { get; set; }
        public float Height { get; set; }
        public State State { get; set; }
    }
}
