using System;
using System.Threading.Tasks;
using DocTask.Core.Dtos.OpenAIDto;
using DocTask.Core.DTOs.ApiResponses;
using DocTask.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocTask.Api.Controllers
{
    [ApiController]
    [Route("api/v1/chat")]
    public class ChatGPTController : ControllerBase
    {
        private readonly IOpenAIService _openAIService;

        public ChatGPTController(IOpenAIService openAIService)
        {
            _openAIService = openAIService;
        }

        [HttpPost("GPT")]
        public async Task<IActionResult> Ask([FromBody] string request)
        {
            var response = await _openAIService.AskAsync(new OpenAIDto.RequestDto { Prompt = request });
            return Content(response.Response, "text/plain; charset=utf-8");
        }

        [HttpPost("GPT/{fileId}")]
        public async Task<IActionResult> AskWithFile([FromBody] string request, [FromRoute] int fileId)
        {
            var response = await _openAIService.AskWithFileAsync(new OpenAIDto.RequestDto { Prompt = request }, fileId);
            return Content(response.Response, "text/plain; charset=utf-8");
        }

        [HttpPost("GPT/file/download/{taskId}")]
        public async Task<IActionResult> DownloadSummaryReport(
            [FromRoute] int taskId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? status,
            [FromQuery] int? assigneeId)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new ApiResponse<string>
                {
                    Success = false,
                    Error = "User not authenticated"
                });
            }

            const string format = "docx"; 

            try
            {
                var (fileContent, fileName, contentType) = await _openAIService.DownloadSummaryReportAsync(
                    taskId, userId, format, from, to, status, assigneeId);

                return File(fileContent, contentType, fileName);
            }
            catch (Exception ex)
            {
                return BadRequest($"Lỗi khi tạo file: {ex.Message}");
            }
        }

        [HttpPost("GPT/tasks/agent/analyze/{fileId}")]
        public async Task<IActionResult> AnalyzeAutomation([FromRoute] int fileId)
        {
            var request = new OpenAIDto.AgentRequestDto
            {
                Prompt = string.Empty,
                FileIds = new List<int> { fileId },
                AutoExecute = false,
            };

            var plan = await _openAIService.AnalyzeAutomationAsync(request);
            return Ok(new
            {
                message = "Planned actions generated. Review before execution.",
                plan
            });
        }
        
        [HttpPost("GPT/tasks/agent/action")]
        public async Task<IActionResult> ExecuteAutomationAction([FromBody] OpenAIDto.ActionDto action)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new ApiResponse<string>
                {
                    Success = false,
                    Error = "User not authenticated"
                });
            }

            var execution = await _openAIService.ExecuteActionAsync(action, userId);
            return Ok(execution);
        }

        [HttpPost("GPT/tasks/agent/run/{fileId}")]
        public async Task<IActionResult> RunAutomation([FromRoute] int fileId)
        {
            var request = new OpenAIDto.AgentRequestDto
            {
                Prompt = string.Empty,
                FileIds = new List<int> { fileId },
                AutoExecute = true,
            };

            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new ApiResponse<string>
                {
                    Success = false,
                    Error = "User not authenticated"
                });
            }

            var result = await _openAIService.AutomateTaskWorkflowAsync(request, userId);
            return Ok(result);
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString))
            {
                return false;
            }

            return int.TryParse(userIdString, out userId);
        }
    }
}