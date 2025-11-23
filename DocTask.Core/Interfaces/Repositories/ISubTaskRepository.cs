using DocTask.Core.Dtos.SubTasks;
using DocTask.Core.Models;
using DocTask.Core.Paginations;
using TaskEntity = DocTask.Core.Models.Task;

namespace DocTask.Core.Interfaces.Repositories;

public interface ISubTaskRepository
{
    Task<TaskEntity?> GetTaskUnitAssgment(int subtaskId);

    Task<List<Taskunitassignment>> GetUnitAssignmentsByTaskIdAsync(int subtaskId);
    Task<TaskEntity?> GetSubTaskWithParentAsync(int taskId);
    Task<bool> UpdateSubTaskStatus(int taskId, string status);
    // Basic CRUD operations
    Task<TaskEntity?> GetByIdAsync(int subTaskId);
    Task<TaskEntity?> GetByIdWithUsersAndUnitsAsync(int subTaskId);
    Task<TaskEntity?> GetByIdWithFrequenciesAsync(int subTaskId);
    Task<TaskEntity?> GetBySubIdAsync(int parentTaskId, int subTaskId);
    Task<TaskEntity> CreateAsync(TaskEntity subTask);
    Task<TaskEntity?> CreateSubtaskAsync(int parentTaskId, CreateSubTaskRequest request, int userId);
    Task<TaskEntity?> UpdateSubtaskAsync(TaskEntity subtask, UpdateSubTaskRequest request);
    Task<TaskEntity?> UpdateSubtask(int subTaskId, TaskEntity subtask);

    Task<bool> DeleteAsync(int subTaskId);
    Task<bool> ExistsAsync(int subTaskId);

    // Query operations
    Task<List<TaskEntity>> GetAllByParentIdAsync(int parentTaskId);
    Task<PaginatedList<TaskEntity>> GetAllByParentIdPaginatedAsync(int parentTaskId, PageOptionsRequest pageOptions, string? key);
    Task<List<TaskEntity>> GetByAssigneeIdAsync(int assigneeId);
    Task<PaginatedList<TaskEntity>> GetByAssigneeIdPaginatedAsync(int assigneeId, PageOptionsRequest pageOptions);
    Task<List<TaskEntity>> GetByAssignedUserIdAsync(int userId);
    Task<PaginatedList<TaskEntity>> GetByAssignedUserIdPaginatedAsync(int userId, string? key, PageOptionsRequest pageOptions);
    Task<List<TaskEntity>> GetByKeywordAsync(string keyword);

    // Task assignment operations
    System.Threading.Tasks.Task AssignUsersToTaskAsync(int taskId, List<int> userIds);
    //Task<List<int>> GetAssignedUserIdsAsync(int taskId);
    Task<List<UserResponse>> GetAssignedUsersAsync(int taskId);
    System.Threading.Tasks.Task RemoveUserFromTaskAsync(int taskId, int userId);

}