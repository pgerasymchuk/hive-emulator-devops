using DevOpsProject.CommunicationControl.Logic.Services.Interfaces;
using DevOpsProject.Shared.Clients;
using DevOpsProject.Shared.Configuration;
using DevOpsProject.Shared.Enums;
using DevOpsProject.Shared.Exceptions;
using DevOpsProject.Shared.Messages;
using DevOpsProject.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevOpsProject.CommunicationControl.Logic.Services
{
    public class CommunicationControlService : ICommunicationControlService
    {
        private readonly ISpatialService _spatialService;
        private readonly IRedisKeyValueService _redisService;
        private readonly RedisKeys _redisKeys;
        private readonly IPublishService _messageBus;
        private readonly CommunicationControlHttpClient _hiveHttpClient;
        private readonly ILogger<CommunicationControlService> _logger;
        private readonly IOptionsMonitor<ComControlCommunicationConfiguration> _communicationControlConfiguration;

        public CommunicationControlService(ISpatialService spatialService, IRedisKeyValueService redisService, IOptionsSnapshot<RedisKeys> redisKeysSnapshot, 
            IPublishService messageBus, CommunicationControlHttpClient hiveHttpClient, ILogger<CommunicationControlService> logger, IOptionsMonitor<ComControlCommunicationConfiguration> communicationControlConfiguration)
        {
            _spatialService = spatialService;
            _redisService = redisService;
            _redisKeys = redisKeysSnapshot.Value;
            _messageBus = messageBus;
            _hiveHttpClient = hiveHttpClient;
            _logger = logger;
            _communicationControlConfiguration = communicationControlConfiguration;
        }

        public async Task<bool> DisconnectHive(string hiveId)
        {
            bool isSuccessfullyDisconnected = false;
            try
            {
                var result = await _redisService.DeleteAsync(GetHiveKey(hiveId));
                isSuccessfullyDisconnected = result;
                return result;
            }
            finally
            {
                await _messageBus.Publish(new HiveDisconnectedMessage
                {
                    HiveID = hiveId,
                    IsSuccessfullyDisconnected = isSuccessfullyDisconnected
                });
            }
        }

        public async Task<HiveModel> GetHiveModel(string hiveId)
        {
            var result = await _redisService.GetAsync<HiveModel>(GetHiveKey(hiveId));
            return result;
        }

        public async Task<List<HiveModel>> GetAllHives()
        {
            var result = await _redisService.GetAllAsync<HiveModel>($"{_redisKeys.HiveKey}:");
            return result;
        }

        public async Task<HiveOperationalArea> ConnectHive(HiveModel model)
        {
            bool isHiveAlreadyConnected = await IsHiveConnected(model.HiveID);
            if (isHiveAlreadyConnected)
            {
                _logger.LogWarning("Reconnect Hive request: {@model}", model);
            }
            else
            {
                _logger.LogInformation("Trying to connect Hive: {@model}", model);
            }
            bool result = await _redisService.SetAsync(GetHiveKey(model.HiveID), model);
            if (result)
            {
                _logger.LogInformation("Successfully connected Hive: {@model}", model);
                var operationalArea = _spatialService.GetHiveOperationalArea(model);
                if (isHiveAlreadyConnected)
                {
                    await _messageBus.Publish(new HiveReconnectedMessage
                    {
                        HiveID = model.HiveID,
                        Hive = model,
                        InitialOperationalArea = operationalArea,
                        IsSuccessfullyReconnected = result
                    });
                }
                else
                {
                    await _messageBus.Publish(new HiveConnectedMessage
                    {
                        HiveID = model.HiveID,
                        Hive = model,
                        InitialOperationalArea = operationalArea,
                        IsSuccessfullyConnected = result
                    });
                }
                return operationalArea;
            }
            else
            {
                _logger.LogError("Failed to connect Hive: {@model}", model);
                throw new HiveConnectionException($"Failed to connect hive for HiveId: {model.HiveID}");
            }
        }

        public async Task<bool> IsHiveConnected(string hiveId)
        {
            string hiveKey = GetHiveKey(hiveId);
            return await _redisService.CheckIfKeyExists(hiveKey);
        }

        public async Task<DateTime> AddTelemetry(HiveTelemetryModel model)
        {
            string hiveKey = GetHiveKey(model.HiveID);
            bool result = await _redisService.UpdateAsync(hiveKey, (HiveModel hive) =>
            {
                hive.Telemetry = model;
            });

            if (result)
            {
                _logger.LogInformation("Telemetry updated for HiveID: {hiveId}. Updated telemetry timestamp: {timestamp}", model.HiveID, model.Timestamp);
            }
            else
            {
                _logger.LogError("Failed to update Telemetry - Redis update issue. HiveID: {hiveId}, Telemetry model: {@telemetry}", model.HiveID, model);
            }

            await _messageBus.Publish(new TelemetrySentMessage
            {
                HiveID = model.HiveID,
                Telemetry = model,
                IsSuccessfullySent = result
            });
            return model.Timestamp;
        }

        public async Task<string> SendHiveControlSignal(string hiveId, Location destination)
        {
            var hive = await GetHiveModel(hiveId);
            if (hive == null)
            {
                _logger.LogError("Sending Hive Control signal: Hive not found for HiveID: {hiveId}", hiveId);
                return null;
            }

            bool isSuccessfullySent = false;
            string hiveMindPath = _communicationControlConfiguration.CurrentValue.HiveMindPath;
            var command = new MoveHiveMindCommand
            {
                CommandType = State.Move,
                Location = destination,
                Timestamp = DateTime.Now
            };
            try
            {
                var result = await _hiveHttpClient.SendHiveControlCommandAsync(hive.HiveSchema, hive.HiveIP, hive.HivePort, hiveMindPath, command);
                isSuccessfullySent = result != null;
                return result;
            }
            finally
            {
                if (isSuccessfullySent)
                {
                    await _messageBus.Publish(new MoveHiveMessage
                    {
                        IsSuccessfullySent = isSuccessfullySent,
                        Destination = destination,
                        HiveID = hiveId
                    });
                }
                else
                {
                    _logger.LogError("Failed to send control command for Hive: {@hive}, path: {path}, \n Command: {@command}", hive, hiveMindPath, command);
                }
                
            }
        }

        private string GetHiveKey(string hiveId)
        {
            return $"{_redisKeys.HiveKey}:{hiveId}";
        }
    }
}
