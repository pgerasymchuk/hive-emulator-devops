using System.Net;
using System.Text.Json;
using DevOpsProject.CommunicationControl.Logic.Services;
using DevOpsProject.CommunicationControl.Logic.Services.Interfaces;
using DevOpsProject.Shared.Clients;
using DevOpsProject.Shared.Configuration;
using DevOpsProject.Shared.Enums;
using DevOpsProject.Shared.Exceptions;
using DevOpsProject.Shared.Messages;
using DevOpsProject.Shared.Models;
using DevOpsProject.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DevOpsProject.Tests.CommunicationControl
{
    [TestClass]
    public class CommunicationControlServiceTests
    {
        private Mock<ISpatialService> _spatialService;
        private Mock<IRedisKeyValueService> _redisService;
        private Mock<IPublishService> _publishService;
        private StubHttpMessageHandler _hiveHandler;

        [TestInitialize]
        public void Setup()
        {
            _spatialService = new Mock<ISpatialService>();
            _redisService = new Mock<IRedisKeyValueService>();
            _publishService = new Mock<IPublishService>();
            _hiveHandler = new StubHttpMessageHandler(HttpStatusCode.OK, "ok");
        }

        private CommunicationControlService CreateService()
        {
            var redisKeys = Mock.Of<IOptionsSnapshot<RedisKeys>>(o => o.Value == new RedisKeys { HiveKey = "Hive" });
            var communicationConfig = Mock.Of<IOptionsMonitor<ComControlCommunicationConfiguration>>(
                o => o.CurrentValue == new ComControlCommunicationConfiguration { HiveMindPath = "api/v1" });
            var httpClient = new CommunicationControlHttpClient(new HttpClient(_hiveHandler));

            return new CommunicationControlService(_spatialService.Object, _redisService.Object, redisKeys,
                _publishService.Object, httpClient, NullLogger<CommunicationControlService>.Instance, communicationConfig);
        }

        private static HiveModel CreateHive(string id = "42") => new HiveModel
        {
            HiveID = id,
            HiveIP = "10.0.0.5",
            HivePort = 5149,
            HiveSchema = "http"
        };

        [TestMethod]
        public async Task ConnectHive_NewHive_StoresHiveAndPublishesConnectedMessage()
        {
            var hive = CreateHive();
            var area = new HiveOperationalArea { RadiusKM = 5 };
            _redisService.Setup(r => r.CheckIfKeyExists("Hive:42")).ReturnsAsync(false);
            _redisService.Setup(r => r.SetAsync("Hive:42", hive)).ReturnsAsync(true);
            _spatialService.Setup(s => s.GetHiveOperationalArea(hive)).Returns(area);

            var result = await CreateService().ConnectHive(hive);

            Assert.AreSame(area, result);
            _redisService.Verify(r => r.SetAsync("Hive:42", hive), Times.Once);
            _publishService.Verify(p => p.Publish(It.Is<HiveConnectedMessage>(m =>
                m.HiveID == "42" && m.IsSuccessfullyConnected && m.Hive == hive && m.InitialOperationalArea == area)), Times.Once);
            _publishService.Verify(p => p.Publish(It.IsAny<HiveReconnectedMessage>()), Times.Never);
        }

        [TestMethod]
        public async Task ConnectHive_AlreadyConnectedHive_PublishesReconnectedMessage()
        {
            var hive = CreateHive();
            _redisService.Setup(r => r.CheckIfKeyExists("Hive:42")).ReturnsAsync(true);
            _redisService.Setup(r => r.SetAsync("Hive:42", hive)).ReturnsAsync(true);
            _spatialService.Setup(s => s.GetHiveOperationalArea(hive)).Returns(new HiveOperationalArea());

            await CreateService().ConnectHive(hive);

            _publishService.Verify(p => p.Publish(It.Is<HiveReconnectedMessage>(m =>
                m.HiveID == "42" && m.IsSuccessfullyReconnected)), Times.Once);
            _publishService.Verify(p => p.Publish(It.IsAny<HiveConnectedMessage>()), Times.Never);
        }

        [TestMethod]
        public async Task ConnectHive_RedisWriteFails_ThrowsAndPublishesNothing()
        {
            var hive = CreateHive();
            _redisService.Setup(r => r.SetAsync("Hive:42", hive)).ReturnsAsync(false);

            await Assert.ThrowsExceptionAsync<HiveConnectionException>(() => CreateService().ConnectHive(hive));

            _spatialService.Verify(s => s.GetHiveOperationalArea(It.IsAny<HiveModel>()), Times.Never);
            _publishService.Verify(p => p.Publish(It.IsAny<It.IsAnyType>()), Times.Never);
        }

        [TestMethod]
        public async Task DisconnectHive_DeletesKeyAndPublishesResult()
        {
            _redisService.Setup(r => r.DeleteAsync("Hive:42")).ReturnsAsync(true);

            var result = await CreateService().DisconnectHive("42");

            Assert.IsTrue(result);
            _publishService.Verify(p => p.Publish(It.Is<HiveDisconnectedMessage>(m =>
                m.HiveID == "42" && m.IsSuccessfullyDisconnected)), Times.Once);
        }

        [TestMethod]
        public async Task DisconnectHive_RedisFails_StillPublishesUnsuccessfulDisconnect()
        {
            _redisService.Setup(r => r.DeleteAsync("Hive:42")).ThrowsAsync(new InvalidOperationException("redis is down"));

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => CreateService().DisconnectHive("42"));

            _publishService.Verify(p => p.Publish(It.Is<HiveDisconnectedMessage>(m =>
                m.HiveID == "42" && !m.IsSuccessfullyDisconnected)), Times.Once);
        }

        [TestMethod]
        public async Task GetAllHives_ReadsAllKeysWithHivePrefix()
        {
            var hives = new List<HiveModel> { CreateHive("1"), CreateHive("2") };
            _redisService.Setup(r => r.GetAllAsync<HiveModel>("Hive:")).ReturnsAsync(hives);

            var result = await CreateService().GetAllHives();

            CollectionAssert.AreEqual(hives, result);
        }

        [TestMethod]
        public async Task AddTelemetry_UpdatesStoredHiveAndPublishesTelemetry()
        {
            var storedHive = CreateHive("7");
            var telemetry = new HiveTelemetryModel
            {
                HiveID = "7",
                Location = new Location { Latitude = 48.7f, Longitude = 38.1f },
                Height = 5,
                Speed = 15,
                State = State.Move,
                Timestamp = new DateTime(2025, 5, 20, 12, 0, 0)
            };
            _redisService.Setup(r => r.UpdateAsync("Hive:7", It.IsAny<Action<HiveModel>>()))
                .Callback<string, Action<HiveModel>>((_, update) => update(storedHive))
                .ReturnsAsync(true);

            var timestamp = await CreateService().AddTelemetry(telemetry);

            Assert.AreEqual(telemetry.Timestamp, timestamp);
            Assert.AreSame(telemetry, storedHive.Telemetry);
            _publishService.Verify(p => p.Publish(It.Is<TelemetrySentMessage>(m =>
                m.HiveID == "7" && m.IsSuccessfullySent && m.Telemetry == telemetry)), Times.Once);
        }

        [TestMethod]
        public async Task AddTelemetry_UpdateFails_PublishesUnsuccessfulTelemetry()
        {
            var telemetry = new HiveTelemetryModel { HiveID = "7", Timestamp = DateTime.Now };
            _redisService.Setup(r => r.UpdateAsync("Hive:7", It.IsAny<Action<HiveModel>>())).ReturnsAsync(false);

            await CreateService().AddTelemetry(telemetry);

            _publishService.Verify(p => p.Publish(It.Is<TelemetrySentMessage>(m =>
                m.HiveID == "7" && !m.IsSuccessfullySent)), Times.Once);
        }

        [TestMethod]
        public async Task SendHiveControlSignal_UnknownHive_ReturnsNullWithoutCallingHive()
        {
            _redisService.Setup(r => r.GetAsync<HiveModel>("Hive:404")).ReturnsAsync((HiveModel)null);

            var result = await CreateService().SendHiveControlSignal("404", new Location { Latitude = 1, Longitude = 2 });

            Assert.IsNull(result);
            Assert.AreEqual(0, _hiveHandler.CallCount);
            _publishService.Verify(p => p.Publish(It.IsAny<It.IsAnyType>()), Times.Never);
        }

        [TestMethod]
        public async Task SendHiveControlSignal_PostsMoveCommandToHiveMindAndPublishesMove()
        {
            var destination = new Location { Latitude = 48.72f, Longitude = 38.09f };
            _redisService.Setup(r => r.GetAsync<HiveModel>("Hive:42")).ReturnsAsync(CreateHive());

            var result = await CreateService().SendHiveControlSignal("42", destination);

            Assert.AreEqual("ok", result);
            Assert.AreEqual(1, _hiveHandler.CallCount);
            Assert.AreEqual(HttpMethod.Post, _hiveHandler.LastRequest.Method);
            Assert.AreEqual("http://10.0.0.5:5149/api/v1/command", _hiveHandler.LastRequest.RequestUri.ToString());

            var command = JsonSerializer.Deserialize<MoveHiveMindCommand>(_hiveHandler.LastRequestBody);
            Assert.AreEqual(State.Move, command.CommandType);
            Assert.AreEqual(destination, command.Location);

            _publishService.Verify(p => p.Publish(It.Is<MoveHiveMessage>(m =>
                m.HiveID == "42" && m.IsSuccessfullySent && m.Destination.Equals(destination))), Times.Once);
        }

        [TestMethod]
        public async Task SendHiveControlSignal_HiveMindRejectsCommand_DoesNotPublishMove()
        {
            _hiveHandler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError);
            _redisService.Setup(r => r.GetAsync<HiveModel>("Hive:42")).ReturnsAsync(CreateHive());

            var result = await CreateService().SendHiveControlSignal("42", new Location { Latitude = 1, Longitude = 2 });

            Assert.IsNull(result);
            Assert.AreEqual(1, _hiveHandler.CallCount);
            _publishService.Verify(p => p.Publish(It.IsAny<MoveHiveMessage>()), Times.Never);
        }
    }
}
