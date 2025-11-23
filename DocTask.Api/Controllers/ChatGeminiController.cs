using System.Text.Json;
using DocTask.Core.Dtos.Gemini;
using DocTask.Core.DTOs.ApiResponses;
using DocTask.Core.Interfaces.Services;
using DocTask.Service.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using static DocTask.Core.Dtos.Gemini.GeminiDto;

namespace DocTask.Api.Controllers
{
    [ApiController]
    [Route("api/v1/chat")]
    public class ChatGeminiController : ControllerBase
    {
        private readonly IGeminiService _geminiService;
        private readonly IAutomationService _automationService;

        public ChatGeminiController(
            IGeminiService geminiService,
            IAutomationService automationService
        )
        {
            _geminiService = geminiService;
            _automationService = automationService;
        }

        [HttpPost("ask-gemini")]
        [SwaggerOperation("Hỏi Gemini")]
        public async Task<IActionResult> Post([FromBody] ChatRequest request)
        {
            string systemPromptTemplate = @"
            Bạn là một trợ lý AI thông minh. Hãy trả lời người dùng một cách rõ ràng, tự nhiên và hữu ích.";

            var response = await _geminiService.AskAsync(
                userMessage: request.UserMessage,
                systemPromptTemplate: systemPromptTemplate
            );

            return Content(response.ToString(), "text/plain");
        }
        [HttpPost("generate-tasks/{fileId}")]
        [SwaggerOperation(Summary = "Tạo một kế hoạch công việc mới sử dụng Gemini")]
        public async Task<IActionResult> PostWithFile(int fileId, [FromQuery] bool redo = false)
        {
            try
            {
                // Truyền tham số redo xuống service
                var response = await _geminiService.AskWithFileAsync(fileId, redo);

                object data;

                // Kiểm tra xem response có phải JSON hay không
                if (!string.IsNullOrWhiteSpace(response.Response) &&
                    (response.Response.TrimStart().StartsWith("{") || response.Response.TrimStart().StartsWith("[")))
                {
                    using var doc = JsonDocument.Parse(response.Response);
                    data = doc.RootElement.Clone(); // clone để dùng ngoài using
                }
                else
                {
                    return BadRequest(new { error = "Response from Gemini is not valid JSON" });
                }

                var apiResponse = new ApiResponse<object>
                {
                    Success = true,
                    Data = data,
                    Message = "Tạo kế hoạch công việc thành công"
                };

                return Ok(apiResponse);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("automation/{fileId}")]
        public async Task<IActionResult> AnalyzeFile([FromRoute] int fileId)
        {
            var request = new AgentRequestDto
            {
                Prompt = string.Empty,
                FileIds = new List<int> { fileId },
                AutoExecute = false,
            };

            var analyze = await _automationService.TaskAnalyzeAsync(request);
            var savedContext = await _geminiService.CreateAsync(new CreateAgentDto
            {
                ContextName = "Báo cáo tự động bởi AI " + DateTime.Now.ToString("dd/MM/yyyy"),
                ContextDescription = JsonSerializer.Serialize(analyze.Tasks),                                   ///////// Lỗi định dạng trả về
                FileId = request.FileIds.First()
            });

            var apiResponse = new ApiResponse<object>
            {
                Success = true,
                Message = "Planned actions generated. Review before execution.",
                Data = analyze,
            };

            return Ok(apiResponse);
        }

        [HttpGet("view/{fileId}")]
        public async Task<IActionResult> View(int fileId)
        {
            var view = await _geminiService.GetByIdAsync(fileId);
            return Ok(new ApiResponse<AgentDto?>
            {
                Success = true,
                Data = view,
            });
        }

        // GET: /api/gemini/preview/{fileId}
        [HttpGet("preview/{fileId}")]
        [SwaggerOperation(Summary = "Xem trước công việc được tạo bởi AI")]
        public async Task<IActionResult> Preview(int fileId)
        {
            var preview = await _geminiService.GetPreviewAsync(fileId);
            object data;

            // Nếu response là JSON, parse thành JsonElement
            if (!string.IsNullOrWhiteSpace(preview.Response) &&
                (preview.Response.TrimStart().StartsWith("{") || preview.Response.TrimStart().StartsWith("[")))
            {
                using var doc = JsonDocument.Parse(preview.Response);
                data = doc.RootElement.Clone(); // clone để dùng ngoài using
            }
            else
            {
                data = preview.Response ?? string.Empty;
            }

            var apiResponse = new ApiResponse<object>
            {
                Success = true,
                Data = data,
                Message = "Tạo kế hoạch công việc thành công"
            };

            return new JsonResult(apiResponse);
        }

        // [HttpPost("approve")]
        // public async Task<IActionResult> ApprovePlan([FromBody] int fileId)
        // {
        //   try
        //   {

        //     var result = await _geminiService.ApprovePlanAsync(fileId);
        //     return Ok(result); // trả về GeminiTaskDto
        //   }
        //   catch (Exception ex)
        //   {
        //     return BadRequest(new { message = ex.Message });
        //   }
        // }

        [HttpDelete("reject/{fileId}")]
        [SwaggerOperation(Summary = "Từ chối công việc do AI tạo")]
        public IActionResult Reject(int fileId)
        {
            var result = _geminiService.RejectPlan(fileId);
            if (result)
                return Ok(new { message = "Đã hủy kế hoạch và xóa cache." });

            return NotFound(new { message = "Không có dữ liệu trong cache để xóa." });
        }

        /// <summary>
        /// Tổng hợp báo cáo của task bằng AI
        /// Đọc nội dung file và gộp thành một báo cáo duy nhất
        /// </summary>
        [HttpPost("{taskId}/summary")]
        [SwaggerOperation(Summary = "Tổng hợp báo cáo trong 1 công việc sử dụng Gemini")]
        public async Task<IActionResult> GetAITaskSummary(int taskId)
        {
            try
            {
                var userIdString = User.FindFirst("id")?.Value;
                if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                {
                    return Unauthorized(new ApiResponse<string>
                    {
                        Success = false,
                        Error = "User not authenticated"
                    });
                }

                var format = "pdf";

                try
                {
                    (byte[] fileContent, string fileName, string contentType) = await _geminiService.AskWithTaskSummaryAsync(
                        taskId, userId, format);

                    return File(fileContent, contentType, fileName);
                }
                catch (Exception ex)
                {
                    return BadRequest($"Lỗi khi tạo file: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

    }
}