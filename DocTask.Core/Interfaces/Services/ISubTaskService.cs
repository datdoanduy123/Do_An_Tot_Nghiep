using DocTask.Core.Dtos.SubTasks;
using DocTask.Core.Dtos.Units;
using DocTask.Core.Dtos.Users;
using DocTask.Core.Models;
using DocTask.Core.Paginations;
using TaskEntity = DocTask.Core.Models.Task;
using Task = System.Threading.Tasks.Task;

namespace DocTask.Core.Interfaces.Services;

public interface ISubTaskService
{
    Task<SubTaskDto> GetSubTaskUnitByIdAsync(int subTaskId);

    Task<SubTaskDto> CreateAsync(int parentTaskId, CreateSubTaskRequest request, int userId);
    Task<SubTaskDto?> UpdateSubtask(int userId, int subtaskId, UpdateSubTaskRequest request);

    Task<List<object>> GetAssignedUsersAsync(int subTaskId);

    Task<bool> ChangeSubTaskStatusAsync(int taskId, int userId, string status);

    Task<bool> ChangeParentTaskStatusAsync(int taskId, int userId, string status);

    // Task<SubTaskDto> CreateAsync(int parentTaskId, CreateSubTaskRequest request, int? userId);
    // Task<SubTaskDto?> UpdateSubtask(int subtaskId, UpdateSubTaskRequest request);

    Task<bool> DeleteAsync(int subTaskId, int userId);
    Task<SubTaskDto> GetSubTaskByIdAsync(int subTaskId);

    // Query operations
    Task<PaginatedList<SubTaskDto>> GetAllByParentIdAsync(int userId, int parentTaskId, PageOptionsRequest pageOptions, string? key);
    Task<PaginatedList<SubTaskDto>> GetByAssignedUserIdPaginatedAsync(int userId, string? key, PageOptionsRequest pageOptions);
    Task<AssignableUnitsResponseDto> GetAssignableUnits(int userId);
    Task<AssignableUsersResponseDto> GetAssignableUsers(int userId);
}