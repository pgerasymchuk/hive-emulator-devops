namespace DevOpsProject.Shared.Models
{
    public class HiveConnectRequest
    {
        public string HiveSchema { get; set; }
        public string HiveIP { get; set; }
        public int HivePort { get; set; }
        public string HiveID { get; set; }
    }
}
