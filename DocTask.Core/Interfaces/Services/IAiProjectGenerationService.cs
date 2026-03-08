using DocTask.Core.Dtos.AiGeneration;
using Microsoft.AspNetCore.Http;

namespace DocTask.Core.Interfaces.Services;

/// <summary>
/// Interface cho service điều phối toàn bộ pipeline AI Project Generation.
/// Pipeline: Upload file → Parse template → Agile plan (Ollama) → Rule DB → Insert Task tree → Auto-assign.
/// </summary>
public interface IAiProjectGenerationService
{
    /// <summary>
    /// Tạo backlog Agile đầy đủ từ file tài liệu yêu cầu.
    ///
    /// Quy trình:
    ///   1. Đọc rawText từ file (txt/docx/pdf) qua IFileConvertService
    ///   2. Parse deterministic theo template → DocumentExtractDto
    ///   3. Sinh Agile plan → AgilePlanDto (dùng Ollama khi có thể)
    ///   4. Áp rule từ DB → gắn SkillTags vào story/task
    ///   5. Insert cây 4 tầng vào bảng Task (ParentTaskId)
    ///   6. Auto-assign theo skill match + workload
    ///   7. Log reasoning vào AssignmentHistory
    ///
    /// KHÔNG nhận orgId/unitId — task chỉ gắn với cây Project/Epic/Story/Task.
    /// requestingUserId dùng để ghi AssignedByUserId trong AssignmentHistory.
    /// </summary>
    /// <param name="file">File txt/docx/pdf upload lên</param>
    /// <param name="requestingUserId">UserId của người tạo request</param>
    /// <returns>Kết quả bao gồm cây node đã tạo, stats, warnings</returns>
    Task<AiGenerationResultDto> GenerateProjectAsync(
        IFormFile file,
        int requestingUserId);
}
