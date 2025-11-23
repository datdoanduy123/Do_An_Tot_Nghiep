using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Paginations;
using TaskModel = DocTask.Core.Models.Task;

namespace DocTask.Core.Interfaces.Services;

public interface ITaskService
{
    // tasks
    Task<PaginatedList<TaskDto>> GetAll(PageOptionsRequest pageOptions, string? key, int userId);
    Task<TaskDto?> CreateTaskAsync(CreateTaskDto taskDto, int userId);
    Task<TaskDto?> GetByIdAsync(int taskId, int userId);
    Task<TaskDto?> UpdateTaskAsync(int taskId, UpdateTaskDto taskDto, int userId);
    Task DeleteTaskAsync(int taskId, int userId);
}