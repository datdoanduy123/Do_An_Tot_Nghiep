using DocTask.Core.Dtos.Progress;
using DocTask.Core.Dtos.Tasks;
using DocTask.Core.DTOs.ApiResponses;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Paginations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace DocTask.Api.Controllers;

[ApiController]
[Route("/api/v1/progress")]
[Authorize] // Require authentication for all endpoints
public class ProgressController : ControllerBase
{
    private readonly IProgressService _progressService;
    private readonly ITaskPermissionService _taskPermissionService;
    private readonly IProgressCalculationService _progressCalculationService;
    private readonly ILogger<ProgressController> _logger;

    // thuy
    private readonly IReminderService _reminderService;
    private readonly ITaskRepository _taskRepository;


    public ProgressController(IProgressService progressService, ITaskPermissionService taskPermissionService, IProgressCalculationService progressCalculationService, ILogger<ProgressController> logger, IReminderService reminderService,
    ITaskRepository taskRepository)
    {
        _progressService = progressService;
        _taskPermissionService = taskPermissionService;
        _progressCalculationService = progressCalculationService;
        _reminderService = reminderService;
        _taskRepository = taskRepository;
        _logger = logger;
    }

    // thuy


    [HttpPost("{progressId}/accept")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Accept(int progressId)
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            return Unauthorized(new ApiResponse<string> { Success = false, Error = "Không thể xác định người dùng." });

        var success = await _progressService.AcceptProgressAsync(progressId, userId);
        if (!success)
            return NotFound(new ApiResponse<string> { Success = false, Error = "Không tìm thấy tiến độ hoặc không có quyền." });

        return Ok(new ApiResponse<string> { Success = true, Message = "Phê duyệt báo cáo thành công." });
    }

    [HttpPost("{progressId}/reject")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Reject(int progressId, [FromBody] RejectProgressRequest request)
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            return Unauthorized(new ApiResponse<string> { Success = false, Error = "Không thể xác định người dùng." });

        var success = await _progressService.RejectProgressAsync(progressId, userId, request.Reason);
        if (!success)
            return NotFound(new ApiResponse<string> { Success = false, Error = "Không tìm thấy tiến độ hoặc không có quyền." });

        return Ok(new ApiResponse<string> { Success = true, Message = "Từ chối báo cáo thành công." });
    }











    // thuy 

    // CREATE: api/v1/progress?taskId={taskId}
    [HttpPost]
    [Consumes("multipart/form-data")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Create(
    [FromQuery] int taskId,
    [FromQuery] int? periodIndex,  // THÊM THAM SỐ NÀY
    [FromForm] DocTask.Core.Dtos.Tasks.UpdateProgressFormDto form)
    {
        try
        {
            var userIdClaim = User.FindFirst("id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new ApiResponse<string>
                {
                    Success = false,
                    Error = "Không thể xác định người dùng."
                });
            }

            //if (!await _taskPermissionService.CanAddProgressAsync(taskId))
            //{
            //    return StatusCode(StatusCodes.Status400BadRequest, new ApiResponse<string>
            //    {
            //        Success = false,
            //        Error = "Chỉ có thể thêm tiến độ cho công việc con."
            //    });
            //}

            if (!await _taskPermissionService.CanSubmitReportAsync(userId, taskId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<string>
                {
                    Success = false,
                    Error = "Bạn không có quyền nộp báo cáo cho task này."
                });
            }

            Stream? fileStream = null;
            string? fileName = null;
            if (form.ReportFile != null && form.ReportFile.Length > 0)
            {
                fileStream = form.ReportFile.OpenReadStream();
                fileName = form.ReportFile.FileName;
            }

            var request = new UpdateProgressRequest
            {
                Proposal = form.Proposal,
                Result = form.Result,
                Feedback = form.Feedback,
                Status = "in_progress",
                ReportFileName = fileName,
                ReportFileStream = fileStream,
                SubmittedByUserId = userId,
                PeriodIndex = periodIndex  // THÊM TRƯỜNG NÀY
            };

            var result = await _progressService.UpdateProgressAsync(taskId, request, userId);

            await _progressCalculationService.CalculateTaskProgressAsync(taskId);

            return Ok(new ApiResponse<UpdateProgressResponse>
            {
                Success = true,
                Data = result,
                Message = "Tạo tiến độ thành công."
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<string> { Success = false, Error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<string> { Success = false, Error = ex.Message });
        }
    }


    // REVIEW SUBTASK PROGRESS: api/v1/progress/review-subtask/{taskId}
    [HttpGet("review/{taskId}")]
    [SwaggerOperation(Summary = "Rà soát tiến độ các công việc con của một công việc cha với các bộ lọc tùy chọn.")]
    public async Task<IActionResult> ReviewSubTaskProgress(
        int taskId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? status,
        [FromQuery] int? assigneeId)
    {
        _logger.LogInformation($"[CONTROLLER-DEBUG] ReviewSubTaskProgress called for task {taskId}");

        // Lấy user ID từ JWT token
        var userIdClaim = User.FindFirst("id");
        int.TryParse(userIdClaim.Value, out var userId);

        // Kiểm tra quyền xem task
        if (!await _taskPermissionService.CanViewTaskAsync(userId, taskId))
        {
            _logger.LogInformation($"[CONTROLLER-DEBUG] Permission denied for user {userId} on task {taskId}");
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<string>
            {
                Success = false,
                Error = "Bạn không có quyền xem báo cáo của task này.."
            });
        }

        _logger.LogInformation($"[CONTROLLER-DEBUG] Calling ReviewSubTaskProgressAsync for task {taskId}");
        var items = await _progressService.ReviewSubTaskProgressAsync(taskId, from, to, status, assigneeId);
        _logger.LogInformation($"[CONTROLLER-DEBUG] ReviewSubTaskProgressAsync returned {items?.Count ?? 0} items");
        if (items == null || items.Count == 0)
        {
            return Ok(new ApiResponse<List<SubTaskProgressReviewDto>>
            {
                Success = true,
                Data = null,
                Message = "Không có báo cáo trong kì này."
            });
        }

        return Ok(new ApiResponse<List<SubTaskProgressReviewDto>>
        {
            Success = true,
            Data = items,
            Message = "Rà soát tiến độ task con thành công."
        });
    }

    [HttpGet("review-unit/{taskId}")]
    [SwaggerOperation(Summary = "Rà soát tiến độ các task được giao cho unit")]
    public async Task<IActionResult> ReviewUnitProgress(
    int taskId,
    [FromQuery] DateTime? from,
    [FromQuery] DateTime? to,
    [FromQuery] string? status,
    [FromQuery] int? unitId)
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            return Unauthorized(new ApiResponse<string> { Success = false, Error = "Không thể xác định người dùng." });

        // Kiểm tra quyền xem task
        if (!await _taskPermissionService.CanViewTaskAsync(userId, taskId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<string>
            {
                Success = false,
                Error = "Bạn không có quyền xem báo cáo của task này."
            });
        }

        var items = await _progressService.ReviewUnitProgressAsync(taskId, from, to, status, unitId);

        if (items == null || items.Count == 0)
        {
            return Ok(new ApiResponse<List<UnitProgressReviewDto>>
            {
                Success = true,
                Data = new List<UnitProgressReviewDto>(),
                Message = "Không có báo cáo trong kì này."
            });
        }

        return Ok(new ApiResponse<List<UnitProgressReviewDto>>
        {
            Success = true,
            Data = items,
            Message = "Rà soát tiến độ unit thành công."
        });
    }

    [HttpGet("review/user/{userId}")]
    [SwaggerOperation(Summary = "Xem báo cáo theo đầu người")]
    public async Task<IActionResult> ReviewProgressByUserId(
    [FromRoute] int userId,
    [FromQuery] string? search,
    [FromQuery] PageOptionsRequest pageOptions,
    [FromQuery] DateTime? from = null,
    [FromQuery] DateTime? to = null,
    [FromQuery] string? status = null)
    {
        try
        {
            var userIdClaim = User.FindFirst("id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int parsedUserId))
            {
                return Unauthorized(new ApiResponse<string>
                {
                    Success = false,
                    Error = "Không thể xác định người dùng."
                });
            }

            var result = await _progressService.ReviewUserProgressAsync(userId, search, pageOptions, from, to, status);

            return Ok(new ApiResponse<PaginatedList<ReviewUserProgressResponse>>
            {
                Success = true,
                Data = result,
                Message = result.MetaData.TotalItems > 0
                    ? $"Lấy tiến độ báo cáo của user {userId} thành công"
                    : "Không có dữ liệu báo cáo"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<string>
            {
                Success = false,
                Error = "Có lỗi xảy ra khi lấy tiến độ báo cáo",
                Message = ex.Message
            });
        }
    }




    [HttpDelete("{progressId:int}")]
    [SwaggerOperation(Summary = "Xóa báo cáo")]
    public async Task<IActionResult> DeleteProgress(int progressId)
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            return Unauthorized(new ApiResponse<string> { Success = false, Error = "Không thể xác định người dùng." });

        var existingProgress = await _progressService.GetProgressByIdAsync(progressId);
        if (existingProgress == null)
            return NotFound(new ApiResponse<string> { Success = false, Error = "Không tìm thấy báo cáo." });

        if (!await _taskPermissionService.CanSubmitReportAsync(userId, existingProgress.TaskId))
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<string>
            {
                Success = false,
                Error = "Bạn không có quyền xóa báo cáo này."
            });

        try
        {
            var success = await _progressService.DeleteProgressAsync(progressId);
            if (!success)
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiResponse<string> { Success = false, Error = "Xóa báo cáo thất bại." });

            return Ok(new ApiResponse<string> { Success = true, Message = "Xóa báo cáo thành công." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<string> { Success = false, Error = ex.Message });
        }
    }



}