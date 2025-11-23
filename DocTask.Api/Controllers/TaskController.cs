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
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")!.Value;

        int userId = int.Parse(userIdClaim);
        await _taskService.DeleteTaskAsync(taskId, userId);
        return Ok(new ApiResponse<string>
        {
            Success = true,
            Data = null,
            Message = "Xóa task thành công."
        });
    }
}