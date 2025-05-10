using DevOpsProject.Shared.Models;

namespace DevOpsProject.CommunicationControl.API.DTO.Client.Request
{
    public class MoveHivesRequest
    {
        public List<string> Hives { get;set;}
        public Location Destination { get; set; }
    }
}
