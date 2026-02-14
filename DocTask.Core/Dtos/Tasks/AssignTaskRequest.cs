using System.Collections.Generic;

namespace DocTask.Core.Dtos.Tasks
{
    /// <summary>
    /// DTO yêu cầu gán user/unit cho một task.
    /// PATCH semantics:
    ///   - null  = không thay đổi danh sách hiện tại
    ///   - []    = xóa hết assignment (clear)
    ///   - [1,2] = gán cho user/unit ID 1 và 2
    /// Dùng cho POST /tasks/{taskId}/assignments
    /// </summary>
    public class AssignTaskRequest
    {
        /// <summary>Danh sách UserIds để gán (null = không đổi)</summary>
        public List<int>? UserIds { get; set; }

        /// <summary>Danh sách UnitIds để gán (null = không đổi)</summary>
        public List<int>? UnitIds { get; set; }
    }
}
