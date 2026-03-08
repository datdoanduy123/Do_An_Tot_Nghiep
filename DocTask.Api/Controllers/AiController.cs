using DocTask.Core.DTOs.ApiResponses;
using DocTask.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace DocTask.Api.Controllers;

// ─────────────────────────────────────────────────────────────────────────────
// Request model — bắt buộc phải bọc IFormFile vào class
// để Swashbuckle có thể generate schema cho multipart/form-data.
// Nếu truyền IFormFile trực tiếp qua [FromForm], Swashbuckle sẽ throw
// SwaggerGeneratorException khi load /swagger/v1/swagger.json.
// ─────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Request model cho endpoint generate-project.
/// Bọc IFormFile vào class để Swashbuckle generate schema đúng cho multipart/form-data.
/// Theo hướng dẫn chính thức: https://github.com/domaindrivendev/Swashbuckle.AspNetCore#handle-forms-and-file-uploads
/// </summary>
public class GenerateProjectRequest
{
    /// <summary>
    /// File tài liệu yêu cầu (.txt / .pdf / .docx).
    /// Upload qua form-data với key "File".
    /// </summary>
    public required IFormFile File { get; set; }
}

/// <summary>
/// Controller điều phối pipeline AI Project Generation.
/// POST /api/ai/generate-project — nhận file tài liệu yêu cầu → trả cây backlog Agile.
/// </summary>
[ApiController]
[Route("/api/ai")]
public class AiController : ControllerBase
{
    private readonly IAiProjectGenerationService _generationService;

    public AiController(IAiProjectGenerationService generationService)
    {
        _generationService = generationService;
    }

    /// <summary>
    /// Upload file tài liệu yêu cầu và sinh backlog Agile tự động.
    ///
    /// Pipeline nội bộ:
    ///   1. Parse tài liệu theo template CHỨC NĂNG X (deterministic)
    ///   2. Gọi Ollama sinh User Story + Acceptance Criteria (+ fallback nếu fail)
    ///   3. Áp rule từ DB (SkillKeyword / ModuleKeywordSkill / DefaultPhase)
    ///   4. Insert cây Project→Epic→Story→Task vào DB
    ///   5. Auto-assign theo skill match (70%) + workload (30%), log reasoning vào AssignmentHistory
    ///
    /// KHÔNG nhận orgId/unitId — task chỉ gắn với cây AI-generated Project.
    /// </summary>
    /// <param name="request">Multipart form chứa file tài liệu</param>
    /// <param name="requestingUserId">UserId của người tạo request (ghi vào AssignmentHistory)</param>
    [HttpPost("generate-project")]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary     = "Sinh backlog Agile từ file tài liệu yêu cầu",
        Description = "Upload file .txt/.pdf/.docx theo template chuẩn → hệ thống" +
                      " tạo cây Project→Epic→Story→Task, auto-assign nhân viên và trả JSON.")]
    public async Task<IActionResult> GenerateProject(
        [FromForm] GenerateProjectRequest request,
        [FromQuery] int requestingUserId = 1)
    {
        var file = request?.File;

        // --- Validate đầu vào ------------------------------------------------
        if (file == null || file.Length == 0)
            return BadRequest(new ApiResponse<object>
            {
                Message = "Vui lòng upload file tài liệu yêu cầu (.txt / .pdf / .docx)."
            });

        var allowedExt = new[] { ".txt", ".pdf", ".doc", ".docx" };
        var ext        = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExt.Contains(ext))
            return BadRequest(new ApiResponse<object>
            {
                Message = $"Định dạng '{ext}' không được hỗ trợ. Dùng: .txt, .pdf, .docx"
            });

        // --- Gọi pipeline chính -----------------------------------------------
        try
        {
            var result = await _generationService.GenerateProjectAsync(file, requestingUserId);

            return Ok(new ApiResponse<object>
            {
                Data    = result,
                Message = $"Tạo backlog Agile thành công — Project ID = {result.ProjectTaskId} | " +
                          $"{result.Stats.EpicCount} Epic | {result.Stats.StoryCount} Story | " +
                          $"{result.Stats.TaskCount} Task ({result.Stats.AssignedCount} đã assign, " +
                          $"{result.Stats.UnassignedCount} chưa assign)."
            });
        }
        catch (NotSupportedException ex)
        {
            // Định dạng file không hỗ trợ
            return BadRequest(new ApiResponse<object> { Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            // File rỗng hoặc thiếu dữ liệu bắt buộc
            return BadRequest(new ApiResponse<object> { Message = ex.Message });
        }
        catch (Exception ex)
        {
            // Lỗi không mong đợi (DB, Ollama crash hard, v.v.)
            var innerMsg = ex.InnerException != null ? $" | Inner: {ex.InnerException.Message}" : "";
            Console.WriteLine($"❌ [AiController] GenerateProject lỗi: {ex.Message}{innerMsg}\n{ex.StackTrace}");
            return StatusCode(500, new ApiResponse<object>
            {
                Message = $"Lỗi nội bộ khi sinh backlog: {ex.Message}{innerMsg}"
            });
        }
    }
}
