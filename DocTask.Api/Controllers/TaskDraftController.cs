using DocTask.Core.Dtos.Draft;
using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DocTask.Core.DTOs.ApiResponses;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocTask.Api.Controllers
{
    /// <summary>
    /// API quản lý Draft (bản nháp task từ AI).
    /// Luồng: AI sinh Draft → Review/Edit → Approve → Task thật
    /// </summary>
    [ApiController]
    [Route("api/v1/drafts")]
    [Authorize]
    public class TaskDraftController : ControllerBase
    {
        private readonly ITaskDraftService _draftService;
        private readonly ITaskService _taskService;

        public TaskDraftController(ITaskDraftService draftService, ITaskService taskService)
        {
            _draftService = draftService;
            _taskService = taskService;
        }

        // ================================================================
        //  REVIEW — Xem draft tree
        // ================================================================

        /// <summary>
        /// Lấy danh sách root drafts theo file đã upload.
        /// Trả về danh sách draft gốc (không kèm subtasks — chỉ preview).
        /// </summary>
        [HttpGet("file/{fileId}")]
        [SwaggerOperation(Summary = "Lấy danh sách root drafts theo fileId")]
        public async Task<IActionResult> GetDraftsByFile(int fileId)
        {
            var drafts = await _draftService.GetDraftsByFileIdAsync(fileId);
            return Ok(new ApiResponse<List<TaskDraft>> { Data = drafts, Success = true });
        }

        /// <summary>
        /// Lấy cây draft đầy đủ — root + subtasks + skills.
        /// Dùng cho màn Review trước khi Approve.
        /// Response: DraftTreeDto flatten (không có vòng lặp).
        /// </summary>
        [HttpGet("{draftId}/tree")]
        [SwaggerOperation(Summary = "Lấy cây draft đầy đủ: root + subtasks + skills")]
        public async Task<IActionResult> GetDraftTree(int draftId)
        {
            var tree = await _draftService.GetDraftTreeDtoAsync(draftId);
            if (tree == null)
                return NotFound(new ApiResponse<object> { Success = false, Message = "Draft không tồn tại." });

            return Ok(new ApiResponse<DraftTreeDto> { Data = tree, Success = true });
        }

        /// <summary>
        /// Lấy chi tiết 1 draft (raw model — backward compatible).
        /// </summary>
        [HttpGet("{draftId}")]
        [SwaggerOperation(Summary = "Lấy chi tiết draft (raw model)")]
        public async Task<IActionResult> GetDraftDetail(int draftId)
        {
            var draft = await _draftService.GetDraftTreeAsync(draftId);
            if (draft == null)
                return NotFound(new ApiResponse<object> { Success = false, Message = "Draft không tồn tại." });

            return Ok(new ApiResponse<TaskDraft> { Data = draft, Success = true });
        }

        // ================================================================
        //  EDIT — Sửa draft trước khi Approve
        // ================================================================

        /// <summary>
        /// Sửa draft (title, description, date, hours).
        /// PATCH semantics: null = không thay đổi field đó.
        /// Chỉ cho phép khi Status = "Pending".
        /// </summary>
        [HttpPatch("{draftId}")]
        [SwaggerOperation(Summary = "Sửa draft trước khi approve (PATCH semantics)")]
        public async Task<IActionResult> UpdateDraft(int draftId, [FromBody] UpdateDraftDto dto)
        {
            try
            {
                var userId = GetUserId();
                var updated = await _draftService.UpdateDraftAsync(draftId, dto, userId);
                return Ok(new ApiResponse<DraftTreeDto>
                {
                    Data = updated,
                    Success = true,
                    Message = "Cập nhật draft thành công."
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
        }

        /// <summary>
        /// Xóa subtask draft.
        /// Bảo vệ: không cho xóa root (ParentDraftId == null).
        /// Bảo vệ: không cho xóa nếu Status != "Pending".
        /// </summary>
        [HttpDelete("{draftId}/children/{childDraftId}")]
        [SwaggerOperation(Summary = "Xóa subtask draft (không cho xóa root hoặc draft đã duyệt)")]
        public async Task<IActionResult> DeleteSubDraft(int draftId, int childDraftId)
        {
            try
            {
                var userId = GetUserId();
                await _draftService.DeleteSubDraftAsync(childDraftId, userId);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Đã xóa subtask draft."
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
        }

        // ================================================================
        //  APPROVE / REJECT
        // ================================================================

        /// <summary>
        /// Approve draft → tạo Task thật trong bảng Tasks.
        /// Validate: StartDate <= EndDate, title not empty, EstimatedHours > 0.
        /// Timeline clamp: subtask date auto-adjust nằm trong parent range.
        /// Set IsAIGenerated = true, ParentTaskId cho child tasks.
        /// </summary>
        [HttpPost("{draftId}/approve")]
        [SwaggerOperation(Summary = "Duyệt draft → tạo Task thật (validate + timeline clamp)")]
        public async Task<IActionResult> ApproveDraft(int draftId)
        {
            try
            {
                var userId = GetUserId();
                var task = await _draftService.ApproveDraftAsync(draftId, userId);
                return Ok(new ApiResponse<TaskDto>
                {
                    Data = task,
                    Success = true,
                    Message = "Draft đã được duyệt và tạo task thật thành công!"
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
        }

        /// <summary>
        /// Reject draft — lưu lý do từ chối.
        /// Đánh trạng thái cả cây (root + subtasks) = "Rejected".
        /// </summary>
        [HttpPost("{draftId}/reject")]
        [SwaggerOperation(Summary = "Từ chối draft (lưu lý do để audit)")]
        public async Task<IActionResult> RejectDraft(int draftId, [FromBody] RejectRequest req)
        {
            try
            {
                var userId = GetUserId();
                await _draftService.RejectDraftAsync(draftId, userId, req?.Reason);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Draft đã bị từ chối.{(string.IsNullOrEmpty(req?.Reason) ? "" : $" Lý do: {req.Reason}")}"
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
        }

        // ================================================================
        //  SMART ASSIGNMENT
        // ================================================================

        /// <summary>
        /// Trigger Smart Assignment Engine cho toàn bộ draft.
        /// Tính toán dựa trên Skill + Availability + Workload.
        /// </summary>
        [HttpPost("{draftId}/auto-assign")]
        [SwaggerOperation(Summary = "Tự động gợi ý phân công cho draft (Smart Engine)")]
        public async Task<IActionResult> AutoAssignDraft(int draftId)
        {
            try
            {
                var proposals = await _taskService.AutoAssignDraftAsync(draftId);
                return Ok(new ApiResponse<List<AssignmentProposalDto>> 
                { 
                    Data = proposals, 
                    Success = true,
                    Message = $"Đã đề xuất phân công cho {proposals.Count} tasks."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
        }

        // ================================================================
        //  HELPER — Lấy userId từ JWT Claims
        // ================================================================

        /// <summary>
        /// Trích xuất userId từ JWT token claims.
        /// </summary>
        private int GetUserId()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("Không tìm thấy thông tin người dùng.");
            return int.Parse(userIdClaim);
        }
    }

    /// <summary>
    /// Request body cho Reject endpoint
    /// </summary>
    public class RejectRequest
    {
        /// <summary>Lý do từ chối (để audit log)</summary>
        public string? Reason { get; set; }
    }
}
