using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Paginations;
using TaskModel = DocTask.Core.Models.Task;

namespace DocTask.Core.Interfaces.Services;

/// <summary>
/// Service quản lý Task (công việc chính thức).
/// Bao gồm CRUD + Assign + Auto-assign + Schedule generation.
/// </summary>
public interface ITaskService
{
    // ===== CRUD =====

    /// <summary>Lấy danh sách tasks phân trang</summary>
    Task<PaginatedList<TaskDto>> GetAll(PageOptionsRequest pageOptions, string? key, int userId);

    /// <summary>Tạo task mới</summary>
    Task<TaskDto?> CreateTaskAsync(CreateTaskDto taskDto, int userId);

    /// <summary>Lấy chi tiết task</summary>
    Task<TaskDto?> GetByIdAsync(int taskId, int userId);

    /// <summary>Cập nhật task</summary>
    Task<TaskDto?> UpdateTaskAsync(int taskId, UpdateTaskDto taskDto, int userId);

    /// <summary>Xóa task</summary>
    Task DeleteTaskAsync(int taskId, int userId);

    // ===== ASSIGNMENT =====

    /// <summary>
    /// Gán user/unit cho task thủ công.
    /// PATCH semantics: null = không đổi, [] = clear.
    /// </summary>
    Task<TaskDto> ManualAssignAsync(int taskId, AssignTaskRequest request, int userId);

    /// <summary>
    /// Tự động gợi ý + gán người dựa trên rule-based scoring.
    /// Score = match(skill) * importance * level - workload_penalty.
    /// Chọn top N users phù hợp nhất.
    /// </summary>
    Task<TaskDto> AutoAssignByScoreAsync(int taskId, int userId);

    // ===== SCHEDULE =====

    /// <summary>
    /// Sinh lịch nhắc (Reminders) cho task theo tần suất (weekly/monthly).
    /// Tạo batch reminders từ StartDate → DueDate.
    /// </summary>
    Task<int> GenerateScheduleAsync(int taskId, string frequency, int userId);
}