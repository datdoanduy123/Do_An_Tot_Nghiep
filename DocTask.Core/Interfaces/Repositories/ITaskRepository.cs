using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Models;
using DocTask.Core.Paginations;
using TaskModel = DocTask.Core.Models.Task;

namespace DocTask.Core.Interfaces.Repositories;

public interface ITaskRepository
{
    Task<List<int>> GetAssignedTaskIdsForUserAsync(int userId, string? search);
    // tasks
    Task<PaginatedList<TaskModel>> GetAllAsync(PageOptionsRequest pageOptions, string? key, int userId);
    Task<TaskModel?> GetTaskByIdAsync(int taskId);
    Task<TaskModel?> CreateTaskAsync(TaskModel task);
    Task<TaskModel?> UpdateTaskAsync(int taskId, UpdateTaskDto taskDto);
    Task<bool>  DeleteAsync(TaskModel task);
    Task<bool> CreateTaskUnitAssignmentAsync(int taskId, int unitId);
    System.Threading.Tasks.Task AssignUsersToTaskAsync(int taskId, List<int> userIds);
    System.Threading.Tasks.Task AssignUnitsToTaskAsync(int taskId, List<int> unitIds);
    Task<TaskModel?> GetByIdWithUsersAndUnitsAsync(int taskId);
    Task<Frequency> CreateFrequencyAsync(string frequencyType, int? intervalValue, List<int> days);
    Task<Frequency> UpdateFrequencyAsync(Frequency frequency, string frequencyType, int? intervalValue, List<int>? days);
    System.Threading.Tasks.Task UpdateTaskFrequencyAsync(int taskId, int frequencyId);
}