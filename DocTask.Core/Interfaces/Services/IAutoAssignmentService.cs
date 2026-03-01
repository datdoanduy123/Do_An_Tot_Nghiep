using System.Collections.Generic;
using System.Threading.Tasks;
using DocTask.Core.Dtos.Tasks;

namespace DocTask.Core.Interfaces.Services
{
    /// <summary>
    /// Interface cho AutoAssignmentService — đề xuất phân công task tự động.
    /// Lưu ý: ProposeAssignmentsForDraftAsync đã bị xóa cùng TaskDraft flow.
    /// </summary>
    public interface IAutoAssignmentService
    {
        /// <summary>
        /// Đề xuất phân công cho một danh sách task dựa trên workload availability.
        /// </summary>
        /// <param name="taskIds">Danh sách task cần assign</param>
        /// <returns>Danh sách đề xuất assignment</returns>
        Task<List<AssignmentProposalDto>> ProposeAssignmentsAsync(List<int> taskIds);
    }

    public class AssignmentProposalDto 
    {
        public int TaskId { get; set; }
        public string TaskTitle { get; set; } = string.Empty;
        public int AssignedUserId { get; set; }
        public string AssignedUserName { get; set; } = string.Empty;
        public decimal MatchScore { get; set; } // 0.0 - 1.0
        public string Reasoning { get; set; } = string.Empty; // Giải thích lý do phân công
        public decimal PredictedUtilization { get; set; } // Workload sau khi assign
    }
}
