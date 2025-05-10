using DevOpsProject.Shared.Enums;

namespace DevOpsProject.Shared.Models
{
    public class MoveHiveMindCommand
    {
        public State CommandType { get; set; }
        // TODO: CLARIFY CommandPayload
        public Location Location { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
