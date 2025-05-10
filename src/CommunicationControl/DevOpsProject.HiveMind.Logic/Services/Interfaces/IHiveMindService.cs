using DevOpsProject.Shared.Models;

namespace DevOpsProject.HiveMind.Logic.Services.Interfaces
{
    public interface IHiveMindService
    {
        Task ConnectHive();
        void StopAllTelemetry();
    }
}
