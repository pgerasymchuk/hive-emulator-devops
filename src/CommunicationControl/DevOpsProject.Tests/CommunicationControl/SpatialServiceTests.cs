using DevOpsProject.CommunicationControl.Logic.Services;
using DevOpsProject.Shared.Models;
using Microsoft.Extensions.Options;
using Moq;

namespace DevOpsProject.Tests.CommunicationControl
{
    [TestClass]
    public class SpatialServiceTests
    {
        private static OperationalAreaConfig CreateConfig() => new OperationalAreaConfig
        {
            Latitude = 48.697189f,
            Longitude = 38.066246f,
            Radius_KM = 5,
            InitialHeight_KM = 1,
            InitialSpeed_KM = 5,
            TelemetryInterval_MS = 30000,
            PingInterval_MS = 15000
        };

        [TestMethod]
        public void GetHiveOperationalArea_MapsOperationalAreaConfig()
        {
            var config = CreateConfig();
            var options = Mock.Of<IOptionsMonitor<OperationalAreaConfig>>(o => o.CurrentValue == config);
            var service = new SpatialService(options);

            var area = service.GetHiveOperationalArea(new HiveModel { HiveID = "1" });

            Assert.AreEqual(5d, area.RadiusKM);
            Assert.AreEqual(48.697189f, area.InitialLocation.Latitude);
            Assert.AreEqual(38.066246f, area.InitialLocation.Longitude);
            Assert.AreEqual(1f, area.InitialHeight);
            Assert.AreEqual(5f, area.Speed);
            Assert.AreEqual(30000, area.TelemetryIntervalMs);
            Assert.AreEqual(15000, area.PingIntervalMs);
        }

        [TestMethod]
        public void GetHiveOperationalArea_UsesCurrentConfigValue()
        {
            var config = CreateConfig();
            var optionsMock = new Mock<IOptionsMonitor<OperationalAreaConfig>>();
            optionsMock.SetupGet(o => o.CurrentValue).Returns(() => config);
            var service = new SpatialService(optionsMock.Object);

            var before = service.GetHiveOperationalArea(new HiveModel { HiveID = "1" });
            config = new OperationalAreaConfig { Latitude = 50.45f, Longitude = 30.52f, Radius_KM = 12 };
            var after = service.GetHiveOperationalArea(new HiveModel { HiveID = "1" });

            Assert.AreEqual(5d, before.RadiusKM);
            Assert.AreEqual(12d, after.RadiusKM);
            Assert.AreEqual(50.45f, after.InitialLocation.Latitude);
            Assert.AreEqual(30.52f, after.InitialLocation.Longitude);
        }
    }
}
