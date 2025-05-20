using DevOpsProject.HiveMind.Logic.Services;
using DevOpsProject.HiveMind.Logic.State;
using DevOpsProject.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevOpsProject.Tests.HiveMind
{
    // HiveInMemoryState is static, so these tests must not run in parallel
    [TestClass]
    [DoNotParallelize]
    public class HiveMindMovingServiceTests
    {
        private const float Tolerance = 0.0001f;
        private HiveMindMovingService _service;

        [TestInitialize]
        public void Setup()
        {
            HiveInMemoryState.OperationalArea = new HiveOperationalArea { RadiusKM = 5 };
            HiveInMemoryState.CurrentLocation = new Location { Latitude = 48.0f, Longitude = 38.0f };
            HiveInMemoryState.Destination = null;
            HiveInMemoryState.IsMoving = false;
            _service = new HiveMindMovingService(NullLogger<HiveMindMovingService>.Instance);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // a move to the current location stops the movement timer on its first tick
            if (HiveInMemoryState.IsMoving && HiveInMemoryState.CurrentLocation != null)
            {
                _service.MoveToLocation(HiveInMemoryState.CurrentLocation.Value);
                WaitUntil(() => !HiveInMemoryState.IsMoving);
            }
            HiveInMemoryState.OperationalArea = null;
            HiveInMemoryState.CurrentLocation = null;
        }

        private static bool WaitUntil(Func<bool> condition, int timeoutMs = 2000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                Thread.Sleep(20);
            }
            return condition();
        }

        [TestMethod]
        public void MoveToLocation_HiveNotConnected_IsIgnored()
        {
            HiveInMemoryState.OperationalArea = null;

            _service.MoveToLocation(new Location { Latitude = 49.0f, Longitude = 39.0f });

            Assert.IsFalse(HiveInMemoryState.IsMoving);
            Assert.IsNull(HiveInMemoryState.Destination);
        }

        [TestMethod]
        public void MoveToLocation_StartsMovingTowardsDestination()
        {
            var destination = new Location { Latitude = 49.0f, Longitude = 40.0f };

            _service.MoveToLocation(destination);

            Assert.IsTrue(HiveInMemoryState.IsMoving);
            Assert.AreEqual(destination, HiveInMemoryState.Destination);
        }

        [TestMethod]
        public void MoveToLocation_FirstStepCoversTenPercentOfRemainingDistance()
        {
            _service.MoveToLocation(new Location { Latitude = 49.0f, Longitude = 40.0f });

            Assert.IsTrue(WaitUntil(() => HiveInMemoryState.CurrentLocation.Value.Latitude > 48.0f), "hive did not move");
            var location = HiveInMemoryState.CurrentLocation.Value;
            Assert.AreEqual(48.1f, location.Latitude, Tolerance);
            Assert.AreEqual(38.2f, location.Longitude, Tolerance);
        }

        [TestMethod]
        public void MoveToLocation_AlreadyAtDestination_StopsMoving()
        {
            _service.MoveToLocation(HiveInMemoryState.CurrentLocation.Value);

            Assert.IsTrue(WaitUntil(() => !HiveInMemoryState.IsMoving), "movement was not stopped");
            Assert.IsNull(HiveInMemoryState.Destination);
        }

        [TestMethod]
        public void MoveToLocation_NewCommandWhileMoving_ReplacesDestination()
        {
            var first = new Location { Latitude = 49.0f, Longitude = 40.0f };
            var second = new Location { Latitude = 47.0f, Longitude = 37.0f };

            _service.MoveToLocation(first);
            _service.MoveToLocation(second);

            Assert.IsTrue(HiveInMemoryState.IsMoving);
            Assert.AreEqual(second, HiveInMemoryState.Destination);
        }
    }
}
