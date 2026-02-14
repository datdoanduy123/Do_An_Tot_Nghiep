using DocTask.Core.DTOs.ApiResponses;
using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Paginations;
using Microsoft.AspNetCore.Mvc;
using TaskModel = DocTask.Core.Models.Task;

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using DocTask.Data;
using DocTask.Core.Exceptions;

using Microsoft.AspNetCore.Authorization;
using Swashbuckle.AspNetCore.Annotations;



namespace DocTask.Api.Controllers;

[ApiController]
[Route("/api/v1/task")]
[Authorize]
public class TaskController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TaskController(ITaskService taskService, ApplicationDbContext dbContext)
    {
        _taskService = taskService;
    }

    // GET: api/v1/task
    [HttpGet]
    [SwaggerOperation(Summary = "Lấy danh sách công việc của người dùng hiện tại, phân trang và tìm kiếm.")]
    public async Task<IActionResult> GetAll([FromQuery] PageOptionsRequest pageOptions, [FromQuery] string? key)
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")!.Value;

        int userId = int.Parse(userIdClaim);
        var tasks = await _taskService.GetAll(pageOptions, key, userId);
        return Ok(new ApiResponse<PaginatedList<TaskDto>>
        {
            Data = tasks,
            Message = "Get all tasks successfully."
        });
    }

    // POST: api/v1/task
    [HttpPost]
    [SwaggerOperation(Summary = "Giao một công việc mới.")]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskDto taskDto)
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")!.Value;
        int userId = int.Parse(userIdClaim);
        var createdTask = await _taskService.CreateTaskAsync(taskDto, userId);
        return Ok(new ApiResponse<TaskDto>
        {
            Success = true,
            Data = createdTask,
            Message = "Tạo task thành công."
        });
    }

    [HttpGet("{taskId}")]
    public async Task<IActionResult> GetById(int taskId)
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
        int userId = int.Parse(userIdClaim);

        var task = await _taskService.GetByIdAsync(taskId, userId);
        return Ok(new ApiResponse<TaskDto>
        {
            Success = true,
            Data = task,
            Message = "Lấy chi tiết task thành công."
        });
    }

    // PUT: api/v1/task/{taskId}
    [HttpPut("{taskId}")]
    [SwaggerOperation(Summary = "Cập nhật một công việc theo ID.")]
    public async Task<IActionResult> UpdateTask(int taskId, [FromBody] UpdateTaskDto taskDto)
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")!.Value;
        int userId = int.Parse(userIdClaim);
        var updatedTask = await _taskService.UpdateTaskAsync(taskId, taskDto, userId);
        return Ok(new ApiResponse<TaskDto>
        {
            Success = true,
            Data = updatedTask,
            Message = "Cập nhật task thành công."
        });
    }

    // DELETE: api/v1/task/{taskId}
    [HttpDelete("{taskId}")]
    [SwaggerOperation(Summary = "Xoá một công việc theo ID.")]
    public async Task<IActionResult> DeleteTask(int taskId)
    {
        var userId = GetUserId();
        await _taskService.DeleteTaskAsync(taskId, userId);
        return Ok(new ApiResponse<string>
        {
            Success = true,
            Data = null,
            Message = "Xóa task thành công."
        });
    }

    // ================================================================
    //  ASSIGNMENT — Gán user/unit cho task
    // ================================================================

    /// <summary>
    /// Gán user/unit cho task thủ công.
    /// PATCH semantics: null = không đổi, [] = clear.
    /// </summary>
    [HttpPost("{taskId}/assignments")]
    [SwaggerOperation(Summary = "Gán người/đơn vị cho task (PATCH semantics)")]
    public async Task<IActionResult> ManualAssign(int taskId, [FromBody] AssignTaskRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _taskService.ManualAssignAsync(taskId, request, userId);
            return Ok(new ApiResponse<TaskDto>
            {
                Success = true,
                Data = result,
                Message = "Gán người/đơn vị thành công."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Tự động gợi ý + gán người dựa trên rule-based scoring.
    /// Score = match(skill) * importance * level - workload_penalty.
    /// Chọn top 3 users phù hợp nhất.
    /// </summary>
    [HttpPost("{taskId}/auto-assign")]
    [SwaggerOperation(Summary = "Auto-assign: tính score theo skill+workload, chọn top 3")]
    public async Task<IActionResult> AutoAssign(int taskId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _taskService.AutoAssignByScoreAsync(taskId, userId);
            return Ok(new ApiResponse<TaskDto>
            {
                Success = true,
                Data = result,
                Message = "Auto-assign thành công."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }

    // ================================================================
    //  SCHEDULE — Sinh lịch nhắc
    // ================================================================

    /// <summary>
    /// Sinh batch reminders cho task theo tần suất (daily/weekly/monthly).
    /// Tạo lịch từ StartDate → DueDate.
    /// </summary>
    [HttpPost("{taskId}/schedule")]
    [SwaggerOperation(Summary = "Sinh lịch nhắc nộp báo cáo theo tần suất")]
    public async Task<IActionResult> GenerateSchedule(int taskId, [FromQuery] string frequency = "weekly")
    {
        try
        {
            var userId = GetUserId();
            var count = await _taskService.GenerateScheduleAsync(taskId, frequency, userId);
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { RemindersCreated = count, Frequency = frequency },
                Message = $"Đã tạo {count} lịch nhắc ({frequency})."
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