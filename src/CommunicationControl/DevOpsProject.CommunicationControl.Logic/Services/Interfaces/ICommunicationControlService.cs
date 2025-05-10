using DevOpsProject.Shared.Models;

namespace DevOpsProject.CommunicationControl.Logic.Services.Interfaces
{
    public interface ICommunicationControlService
    {
        Task<bool> DisconnectHive(string hiveId);
        Task<HiveModel> GetHiveModel(string hiveId);
        Task<List<HiveModel>> GetAllHives();
        Task<HiveOperationalArea> ConnectHive(HiveModel model);
        Task<bool> IsHiveConnected(string hiveId);
        Task<DateTime> AddTelemetry(HiveTelemetryModel model);
        Task<string> SendHiveControlSignal(string hiveId, Location destination);
    }
}
