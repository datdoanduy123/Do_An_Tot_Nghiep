namespace DocTask.Core.Dtos.AiGeneration;

/// <summary>
/// Kết quả trả về cho frontend sau khi pipeline AI tạo Project xong.
/// Frontend dùng ProjectTaskId để load cây và hiển thị backlog.
/// </summary>
public class AiGenerationResultDto
{
    // ────────────────────────────────────────────
    // Node gốc
    // ────────────────────────────────────────────

    /// <summary>TaskId của node Project (tầng cao nhất) vừa được tạo trong DB</summary>
    public int ProjectTaskId { get; set; }

    // ────────────────────────────────────────────
    // Danh sách node đã tạo
    // ────────────────────────────────────────────

    /// <summary>
    /// Danh sách tất cả node được tạo (Project + Epics + Stories + Tasks).
    /// Frontend dùng để render cây backlog ngay lập tức mà không cần load lại.
    /// </summary>
    public List<CreatedNodeDto> CreatedNodes { get; set; } = new();

    // ────────────────────────────────────────────
    // Thống kê
    // ────────────────────────────────────────────

    /// <summary>Thống kê số lượng node theo tầng</summary>
    public GenerationStatsDto Stats { get; set; } = new();

    // ────────────────────────────────────────────
    // Truy xuất nguồn AI
    // ────────────────────────────────────────────

    /// <summary>
    /// Provider AI đã được sử dụng: "Ollama" | "RuleBased" | "Fallback".
    /// Hiển thị trên UI để người dùng biết nguồn gốc plan.
    /// </summary>
    public string ProviderUsed { get; set; } = "Unknown";

    // ────────────────────────────────────────────
    // Cảnh báo
    // ────────────────────────────────────────────

    /// <summary>
    /// Danh sách cảnh báo phát sinh trong quá trình generation.
    /// VD: "Không parse được ngày bắt đầu", "Ollama timeout — dùng RuleBased fallback",
    ///     "3 task không tìm được assignee phù hợp"
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Thông tin tóm tắt về 1 node đã tạo trong DB.
/// </summary>
public class CreatedNodeDto
{
    /// <summary>TaskId trong DB</summary>
    public int TaskId { get; set; }

    /// <summary>ParentTaskId (null = node gốc Project)</summary>
    public int? ParentTaskId { get; set; }

    /// <summary>Tầng: "Project" | "Epic" | "Story" | "Task"</summary>
    public string NodeType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>UserId được gán (chỉ tầng Task), null nếu chưa assign được</summary>
    public int? AssigneeId { get; set; }

    /// <summary>Tên người được gán (để hiển thị, tránh query thêm)</summary>
    public string? AssigneeName { get; set; }

    /// <summary>Điểm match khi auto-assign</summary>
    public decimal? AssignMatchScore { get; set; }
}

/// <summary>
/// Thống kê số lượng node theo tầng sau khi generation.
/// </summary>
public class GenerationStatsDto
{
    public int EpicCount      { get; set; }
    public int StoryCount     { get; set; }
    public int TaskCount      { get; set; }
    public int AssignedCount  { get; set; }  // Số task được assign thành công
    public int UnassignedCount { get; set; } // Số task chưa tìm được assignee
}
