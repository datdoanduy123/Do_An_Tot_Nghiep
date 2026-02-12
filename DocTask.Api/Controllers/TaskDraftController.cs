using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DocTask.Core.DTOs.ApiResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocTask.Api.Controllers
{
    [ApiController]
    [Route("api/v1/drafts")]
    [Authorize]
    public class TaskDraftController : ControllerBase
    {
        private readonly ITaskDraftService _draftService;

        public TaskDraftController(ITaskDraftService draftService)
        {
            _draftService = draftService;
        }

        [HttpGet("file/{fileId}")]
        public async Task<IActionResult> GetDraftsByFile(int fileId)
        {
            var drafts = await _draftService.GetDraftsByFileIdAsync(fileId);
            return Ok(new ApiResponse<List<TaskDraft>> { Data = drafts, Success = true });
        }

        [HttpGet("{draftId}")]
        public async Task<IActionResult> GetDraftDetail(int draftId)
        {
            var draft = await _draftService.GetDraftTreeAsync(draftId);
            if (draft == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Draft not found" });
            return Ok(new ApiResponse<TaskDraft> { Data = draft, Success = true });
        }

        [HttpPost("{draftId}/approve")]
        public async Task<IActionResult> ApproveDraft(int draftId)
        {
            // ... (Code approve như cũ - xử lý user id và gọi service)
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
            if (userIdClaim == null) return Unauthorized();
            int userId = int.Parse(userIdClaim);

            var task = await _draftService.ApproveDraftAsync(draftId, userId);
            return Ok(new ApiResponse<TaskDto> { Data = task, Success = true, Message = "Approved!" });
        }

        [HttpPost("{draftId}/reject")]
        public async Task<IActionResult> RejectDraft(int draftId, [FromBody] RejectRequest req)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
            if (userIdClaim == null) return Unauthorized();
            int userId = int.Parse(userIdClaim);

            await _draftService.RejectDraftAsync(draftId, userId, req.Reason);
            return Ok(new ApiResponse<object> { Success = true, Message = "Rejected." });
        }
    }
    public class RejectRequest { public string Reason { get; set; } }
}
