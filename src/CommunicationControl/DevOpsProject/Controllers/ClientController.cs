using Asp.Versioning;
using DevOpsProject.CommunicationControl.API.DTO.Client.Request;
using DevOpsProject.CommunicationControl.Logic.Services.Interfaces;
using DevOpsProject.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DevOpsProject.CommunicationControl.API.Controllers
{
    [ApiVersion("1.0")]
    [ApiController]
    [Route("api/v{version:apiVersion}/client")]
    public class ClientController : Controller
    {
        private readonly ICommunicationControlService _communicationControlService;
        private readonly IOptionsMonitor<OperationalAreaConfig> _operationalAreaConfig;
        private readonly ILogger<ClientController> _logger;

        public ClientController(ICommunicationControlService communicationControlService, IOptionsMonitor<OperationalAreaConfig> operationalAreaConfig, ILogger<ClientController> logger)
        {
            _communicationControlService = communicationControlService;
            _operationalAreaConfig = operationalAreaConfig;
            _logger = logger;
        }

        [HttpGet("area")]
        public IActionResult GetOperationalArea()
        {
            return Ok(_operationalAreaConfig.CurrentValue);
        }

        [HttpGet("hive/{hiveId}")]
        public async Task<IActionResult> GetHive(string hiveId)
        {
            var hiveExists = await _communicationControlService.IsHiveConnected(hiveId);
            if (!hiveExists)
            {
                _logger.LogWarning("Failed to get Hive for HiveID: {hiveId}", hiveId);
                return NotFound($"Hive with HiveID: {hiveId} is not found");
            }

            var hiveModel = await _communicationControlService.GetHiveModel(hiveId);
            return Ok(hiveModel);
        }

        [HttpGet("hive")]
        public async Task<IActionResult> GetHives()
        {
            var hives = await _communicationControlService.GetAllHives();
            return Ok(hives);
        }

        [HttpDelete("hive/{hiveId}")]
        public async Task<IActionResult> DisconnectHive(string hiveId)
        {
            var disconnetResult = await _communicationControlService.DisconnectHive(hiveId);
            return Ok(disconnetResult);
        }

        [HttpPatch("hive")]
        public IActionResult SendBulkHiveMovingSignal(MoveHivesRequest request)
        {
            if (request?.Hives == null || !request.Hives.Any())
                return BadRequest("No hive IDs provided.");

            _logger.LogInformation("Hive moving request accepted by enpdoint. Request: {@request}", request);
            foreach (var id in request.Hives)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _communicationControlService.SendHiveControlSignal(id, request.Destination);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send control signal for HiveID: {id} \n Request: {@request}", id, request);
                    }
                });
            }


            return Accepted("Hives are being moved asynchronously.");
        }
    }
}
