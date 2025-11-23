using DocTask.Core.Models;

namespace DocTask.Core.Interfaces.Services;

public interface ITaskPermissionService
{
    /// <summary>
    /// Kiểm tra xem user có thể nộp báo cáo cho task không
    /// </summary>
    Task<bool> CanSubmitReportAsync(int userId, int taskId);
    
    /// <summary>
    /// Kiểm tra xem user có thể xem task không
    /// </summary>
    Task<bool> CanViewTaskAsync(int userId, int taskId);
    
    /// <summary>
    /// Kiểm tra xem user có thể chỉnh sửa task không
    /// </summary>
    Task<bool> CanEditTaskAsync(int userId, int taskId);
    
    /// <summary>
    /// Kiểm tra xem user có thể xóa task không
    /// </summary>
    Task<bool> CanDeleteTaskAsync(int userId, int taskId);
    
    /// <summary>
    /// Kiểm tra xem user có thể nộp báo cáo theo lịch trình không
    /// </summary>
    Task<bool> CanSubmitReportByScheduleAsync(int userId, int taskId);
    
    /// <summary>
    /// Lấy danh sách user có quyền truy cập task
    /// </summary>
    Task<List<int>> GetAuthorizedUserIdsAsync(int taskId);
    
    /// <summary>
    /// Kiểm tra xem task có phải là task con (có thể thêm tiến độ) không
    /// </summary>
    Task<bool> CanAddProgressAsync(int taskId);
    
    /// <summary>
    /// Kiểm tra xem user có thể thêm tiến độ cho task không
    /// </summary>
    Task<bool> CanAddProgressAsync(int userId, int taskId);
    
    /// <summary>
    /// Kiểm tra xem user có thể giao việc cho đơn vị không
    /// </summary>
    Task<bool> CanAssignTaskToUnitAsync(int userId, int targetUnitId);
    
    /// <summary>
    /// Kiểm tra xem user có thể xem task của đơn vị không
    /// </summary>
    Task<bool> CanViewUnitTasksAsync(int userId, int unitId);
    
    /// <summary>
    /// Kiểm tra xem user có thể quản lý đơn vị không
    /// </summary>
    Task<bool> CanManageUnitAsync(int userId, int unitId);
    
    /// <summary>
    /// Lấy danh sách ID đơn vị mà user có thể giao việc
    /// </summary>
    Task<List<int>> GetAssignableUnitIdsAsync(int userId);
}
