namespace DocTask.Core.Dtos.AiGeneration;

/// <summary>
/// DTO cây Agile plan 4 tầng: Project → Epic → Story → Task.
/// Được sinh ra bởi AgilePlanner từ DocumentExtractDto.
/// </summary>
public class AgilePlanDto
{
    /// <summary>Node gốc (tầng 1: Project)</summary>
    public ProjectPlanNode Project { get; set; } = new();
}

// ============================================================
// TẦNG 1: PROJECT
// ============================================================

/// <summary>
/// Node Project — tầng cao nhất, tương ứng TaskType = Project (1).
/// ParentTaskId = null khi insert vào DB.
/// </summary>
public class ProjectPlanNode
{
    public string Title       { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate   { get; set; }

    /// <summary>Tổng giờ ước tính toàn dự án</summary>
    public decimal? EstimatedHours { get; set; }

    /// <summary>Danh sách tag kỹ năng yêu cầu (map từ TechStack)</summary>
    public List<string> SkillTags { get; set; } = new();

    /// <summary>Danh sách Epic (tầng 2)</summary>
    public List<EpicPlanNode> Epics { get; set; } = new();
}

// ============================================================
// TẦNG 2: EPIC
// ============================================================

/// <summary>
/// Node Epic — tương ứng 1 CHỨC NĂNG lớn (TaskType = Epic).
/// ParentTaskId = Project.TaskId.
/// </summary>
public class EpicPlanNode
{
    /// <summary>Số thứ tự chức năng (VD: 1, 2, 3…)</summary>
    public int FunctionNumber { get; set; }

    public string Title        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate   { get; set; }
    public decimal? EstimatedHours { get; set; }

    /// <summary>Tag kỹ năng map từ ModuleKeywordSkill rules</summary>
    public List<string> SkillTags { get; set; } = new();

    /// <summary>Danh sách Story (tầng 3)</summary>
    public List<StoryPlanNode> Stories { get; set; } = new();
}

// ============================================================
// TẦNG 3: STORY
// ============================================================

/// <summary>
/// Node Story — tương ứng 1 yêu cầu chi tiết X.Y (TaskType = Story).
/// ParentTaskId = Epic.TaskId.
/// </summary>
public class StoryPlanNode
{
    public string Title        { get; set; } = string.Empty;

    /// <summary>
    /// User Story chuẩn được Ollama enrich:
    /// "As a [role] I want [goal] so that [benefit]"
    /// Fallback = rawText từ document nếu Ollama fail.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Acceptance Criteria Given–When–Then (Ollama sinh).
    /// Null nếu Ollama fail.
    /// </summary>
    public string? AcceptanceCriteria { get; set; }

    public DateTime? StartDate  { get; set; }
    public DateTime? DueDate    { get; set; }
    public decimal? EstimatedHours { get; set; }

    /// <summary>Tag kỹ năng áp vào story từ rule engine</summary>
    public List<string> SkillTags { get; set; } = new();

    /// <summary>Danh sách Task implementation (tầng 4)</summary>
    public List<TaskPlanNode> Tasks { get; set; } = new();
}

// ============================================================
// TẦNG 4: TASK
// ============================================================

/// <summary>
/// Node Task implementation — đơn vị công việc nhỏ nhất (TaskType = Task).
/// ParentTaskId = Story.TaskId.
/// Được auto-assign cho nhân viên phù hợp.
/// </summary>
public class TaskPlanNode
{
    /// <summary>
    /// Tiêu đề theo vai trò, ví dụ:
    /// "[BE] Implement đăng ký tài khoản", "[FE] UI đăng ký", "[QA] Test đăng ký", "[DevOps] Deploy"
    /// </summary>
    public string Title        { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? DueDate   { get; set; }
    public decimal? EstimatedHours { get; set; }

    /// <summary>
    /// Tag kỹ năng chính của task này.
    /// Dùng để tìm user có UserSkill.Skill.SkillName match.
    /// VD: ["ASP.NET Core", "Entity Framework"]
    /// </summary>
    public List<string> SkillTags { get; set; } = new();

    /// <summary>Vai trò kỹ sư phụ trách (BE / FE / QA / DevOps)</summary>
    public string? RoleTag { get; set; }

    /// <summary>UserId được auto-assign (null nếu chưa assign)</summary>
    public int? AssigneeUserId { get; set; }

    /// <summary>Điểm match khi auto-assign (0.0–1.0)</summary>
    public decimal? AssignMatchScore { get; set; }

    /// <summary>Lý do assign — log vào AssignmentHistory</summary>
    public string? AssignReasoning { get; set; }
}
