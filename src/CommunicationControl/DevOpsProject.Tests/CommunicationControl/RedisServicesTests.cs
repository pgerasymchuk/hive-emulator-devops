using System.Text.Json;
using DevOpsProject.CommunicationControl.Logic.Services;
using DevOpsProject.Shared.Configuration;
using DevOpsProject.Shared.Messages;
using DevOpsProject.Shared.Models;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace DevOpsProject.Tests.CommunicationControl
{
    [TestClass]
    public class RedisServicesTests
    {
        private Mock<IDatabase> _database;
        private Mock<ISubscriber> _subscriber;
        private Mock<IConnectionMultiplexer> _connection;

        [TestInitialize]
        public void Setup()
        {
            _database = new Mock<IDatabase>();
            _subscriber = new Mock<ISubscriber>();
            _connection = new Mock<IConnectionMultiplexer>();
            _connection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_database.Object);
            _connection.Setup(c => c.GetSubscriber(It.IsAny<object>())).Returns(_subscriber.Object);
        }

        private static string HiveJson(string id) => JsonSerializer.Serialize(new HiveModel { HiveID = id, HiveIP = "10.0.0.5", HivePort = 5149 });

        private string LastWrittenValue(string key)
        {
            var write = _database.Invocations.Last(i => i.Method.Name == nameof(IDatabase.StringSetAsync));
            Assert.AreEqual(key, write.Arguments[0].ToString());
            return write.Arguments[1].ToString();
        }

        [TestMethod]
        public async Task GetAsync_ExistingKey_DeserializesJson()
        {
            _database.Setup(d => d.StringGetAsync("Hive:1", It.IsAny<CommandFlags>())).ReturnsAsync(HiveJson("1"));

            var hive = await new RedisKeyValueService(_connection.Object).GetAsync<HiveModel>("Hive:1");

            Assert.AreEqual("1", hive.HiveID);
            Assert.AreEqual(5149, hive.HivePort);
        }

        [TestMethod]
        public async Task GetAsync_MissingKey_ReturnsDefault()
        {
            _database.Setup(d => d.StringGetAsync("Hive:1", It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null);

            var hive = await new RedisKeyValueService(_connection.Object).GetAsync<HiveModel>("Hive:1");

            Assert.IsNull(hive);
        }

        [TestMethod]
        public async Task SetAsync_WritesValueAsJson()
        {
            await new RedisKeyValueService(_connection.Object).SetAsync("Hive:3", new HiveModel { HiveID = "3" });

            var written = JsonSerializer.Deserialize<HiveModel>(LastWrittenValue("Hive:3"));
            Assert.AreEqual("3", written.HiveID);
        }

        [TestMethod]
        public async Task UpdateAsync_MissingKey_ReturnsFalseWithoutWriting()
        {
            _database.Setup(d => d.StringGetAsync("Hive:1", It.IsAny<CommandFlags>())).ReturnsAsync(RedisValue.Null);
            var updateCalled = false;

            var result = await new RedisKeyValueService(_connection.Object)
                .UpdateAsync<HiveModel>("Hive:1", _ => updateCalled = true);

            Assert.IsFalse(result);
            Assert.IsFalse(updateCalled);
            Assert.IsFalse(_database.Invocations.Any(i => i.Method.Name == nameof(IDatabase.StringSetAsync)));
        }

        [TestMethod]
        public async Task UpdateAsync_ExistingKey_AppliesChangeAndWritesItBack()
        {
            _database.Setup(d => d.StringGetAsync("Hive:1", It.IsAny<CommandFlags>())).ReturnsAsync(HiveJson("1"));

            await new RedisKeyValueService(_connection.Object)
                .UpdateAsync<HiveModel>("Hive:1", hive => hive.Telemetry = new HiveTelemetryModel { HiveID = "1", Height = 5 });

            var written = JsonSerializer.Deserialize<HiveModel>(LastWrittenValue("Hive:1"));
            Assert.AreEqual("10.0.0.5", written.HiveIP);
            Assert.AreEqual(5f, written.Telemetry.Height);
        }

        [TestMethod]
        public async Task Publish_SendsSerializedMessageToConfiguredChannel()
        {
            var options = Mock.Of<IOptionsMonitor<RedisOptions>>(o => o.CurrentValue == new RedisOptions { PublishChannel = "HiveChannel" });
            var message = new HiveDisconnectedMessage { HiveID = "42", IsSuccessfullyDisconnected = true };

            await new RedisPublishService(_connection.Object, options).Publish(message);

            var publish = _subscriber.Invocations.Single(i => i.Method.Name == nameof(ISubscriber.PublishAsync));
            Assert.AreEqual("HiveChannel", publish.Arguments[0].ToString());
            var sent = JsonSerializer.Deserialize<HiveDisconnectedMessage>(publish.Arguments[1].ToString());
            Assert.AreEqual("42", sent.HiveID);
            Assert.IsTrue(sent.IsSuccessfullyDisconnected);
        }

        [TestMethod]
        public async Task Publish_WithoutChannel_Throws()
        {
            var options = Mock.Of<IOptionsMonitor<RedisOptions>>(o => o.CurrentValue == new RedisOptions());

            await Assert.ThrowsExceptionAsync<Exception>(() =>
                new RedisPublishService(_connection.Object, options).Publish(new HiveDisconnectedMessage { HiveID = "42" }));

            Assert.AreEqual(0, _subscriber.Invocations.Count);
        }
    }
}
