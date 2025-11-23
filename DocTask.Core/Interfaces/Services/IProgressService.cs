using DocTask.Core.Dtos.Progress;
using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Paginations;

namespace DocTask.Core.Interfaces.Services;

public interface IProgressService
{
    Task<PaginatedList<ReviewUserProgressResponse>> ReviewUserProgressAsync(int userId, string? search, PageOptionsRequest pageOptions, DateTime? from, DateTime? to, string? status);
    Task<List<UnitProgressReviewDto>> ReviewUnitProgressAsync(int taskId, DateTime? from, DateTime? to, string? status, int? unitId);
    Task<bool> AcceptProgressAsync(int progressId, int approverId);
    Task<bool> RejectProgressAsync(int progressId, int approverId, string reason);


    // thuy
    Task<UpdateProgressResponse> UpdateProgressAsync(int taskId, UpdateProgressRequest request, int? updatedBy = null);

    Task<List<ProgressDto>> GetProgressesByTaskAsync(int taskId);

    Task<Core.Models.Progress?> GetProgressByIdAsync(int progressId);

    Task<Core.Models.Progress?> UpdateProgressEntryAsync(int progressId, UpdateProgressRequest request, int? updatedBy = null);

    Task<bool> DeleteProgressAsync(int progressId);
    Task<List<ProgressReviewByUserDto>> ReviewProgressByUserAsync(int taskId, DateTime? from, DateTime? to, string? status);
    Task<List<SubTaskProgressReviewDto>> ReviewSubTaskProgressAsync(int taskId, DateTime? from, DateTime? to, string? status, int? assigneeId);
    Task<List<ProgressDto>> GetLatestProgressesAsync(int top = 10);
}


