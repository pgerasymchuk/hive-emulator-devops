using DevOpsProject.HiveMind.Logic.Services;
using DevOpsProject.HiveMind.Logic.Services.Interfaces;

namespace DevOpsProject.HiveMind.API.DI
{
    public static class LogicConfiguration
    {
        public static IServiceCollection AddHiveMindLogic(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IHiveMindService, HiveMindService>();
            serviceCollection.AddTransient<IHiveMindMovingService, HiveMindMovingService>();

            return serviceCollection;
        }
    }
}
