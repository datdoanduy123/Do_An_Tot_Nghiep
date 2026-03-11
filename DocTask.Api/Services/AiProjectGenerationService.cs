using System.Text;
using DocTask.Core.Dtos.AiGeneration;
using DocTask.Core.Enum;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using DocTask.Data;
using Microsoft.EntityFrameworkCore;
using TaskModel = DocTask.Core.Models.Task;

namespace DocTask.Api.Services;

/// <summary>
/// Orchestrator điều phối toàn bộ pipeline AI Project Generation.
///
/// Luồng 8 bước:
///   1. Đọc file → rawText (txt/docx/pdf)
///   2. DocumentTemplateParser → DocumentExtractDto
///   3. AgilePlanner (Ollama + fallback) → AgilePlanDto
///   4. AiRuleApplier (rule DB) → enrich SkillTags
///   5. Insert cây 4 tầng Task vào DB (Project→Epic→Story→Task)
///   6. Auto-assign theo skill match + workload
///   7. Log reasoning vào AssignmentHistory
///   8. Trả AiGenerationResultDto
/// </summary>
public class AiProjectGenerationService : IAiProjectGenerationService
{
    private readonly DocumentTemplateParser _parser;
    private readonly AgilePlanner          _planner;
    private readonly AiRuleApplier         _ruleApplier;
    private readonly IAiTaskRuleRepository _ruleRepo;
    private readonly ApplicationDbContext  _context;

    public AiProjectGenerationService(
        DocumentTemplateParser  parser,
        AgilePlanner            planner,
        AiRuleApplier           ruleApplier,
        IAiTaskRuleRepository   ruleRepo,
        ApplicationDbContext    context)
    {
        _parser      = parser;
        _planner     = planner;
        _ruleApplier = ruleApplier;
        _ruleRepo    = ruleRepo;
        _context     = context;
    }

    // ================================================================
    // ENTRY POINT
    // ================================================================

    /// <summary>
    /// Thực thi toàn bộ pipeline AI Project Generation.
    /// Ném ngoại lệ nếu file null hoặc rỗng.
    /// Các lỗi Ollama được xử lý nội bộ (fallback + warning).
    /// </summary>
    public async Task<AiGenerationResultDto> GenerateProjectAsync(
        IFormFile file,
        int requestingUserId)
    {
        var warnings      = new List<string>();
        var createdNodes  = new List<CreatedNodeDto>();

        // ── BƯỚC 1: Đọc rawText từ file upload ───────────────────────
        var rawText = await ReadRawTextAsync(file);

        // ── BƯỚC 2: Parse template deterministic ─────────────────
        var extract = _parser.Parse(rawText, file.FileName);
        warnings.AddRange(extract.ParseWarnings);

        var stats = new GenerationStatsDto();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            int projectTaskId;

            if (extract.Sprints.Any())
            {
                // =======================================================
                // NHANH MỚI: Biên bản dự án — tạo Task trực tiếp từ bảng Sprint
                // Bỏ qua AgilePlanner và Ollama, assign theo tên người.
                // =======================================================
                warnings.Add($"Phát hiện {extract.Sprints.Count} Sprint từ biên bản — tạo Task trực tiếp (bỏ qua Ollama).");

                projectTaskId = await InsertSprintTreeAsync(
                    extract, requestingUserId, createdNodes, stats, warnings);
            }
            else
            {
                // =======================================================
                // NHANH CŨ: CHỨC NĂNG X — AgilePlanner + Ollama + auto-assign
                // =======================================================

                // ── BƯỚC 3: Sinh Agile plan (Ollama + fallback) ────────
                var agilePlan = await _planner.PlanAsync(extract, warnings);

                // ── BƯỚC 4: Ảp rule từ DB ─────────────────────────
                var allRules = await _ruleRepo.GetAllAsync();
                _ruleApplier.Apply(agilePlan, extract, allRules, warnings);

                // ── BƯỚC 5-7: Insert vào DB + Auto-assign ───────────
                projectTaskId = await InsertTreeAsync(
                    agilePlan, requestingUserId, createdNodes, stats, warnings);
            }

            await transaction.CommitAsync();

            return new AiGenerationResultDto
            {
                ProjectTaskId = projectTaskId,
                CreatedNodes  = createdNodes,
                Stats         = stats,
                ProviderUsed  = extract.Sprints.Any()
                    ? "SprintTemplate (Biên bản trực tiếp)"
                    : (warnings.Any(w => w.Contains("fallback", StringComparison.OrdinalIgnoreCase))
                        ? "RuleBased (Ollama fallback)" : "Ollama"),
                Warnings      = warnings
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ================================================================
    // ĐỌC FILE
    // ================================================================

    /// <summary>
    /// Đọc nội dung file upload thành rawText.
    /// Hỗ trợ: txt, pdf, docx (dùng logic parse tương đương FileConvertService).
    /// </summary>
    private static async Task<string> ReadRawTextAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File không được rỗng.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        return ext switch
        {
            ".txt"        => Encoding.UTF8.GetString(bytes),
            ".pdf"        => ParsePdf(bytes),
            ".doc" or ".docx" => ParseDocx(bytes),
            _ => throw new NotSupportedException($"Định dạng file '{ext}' không được hỗ trợ. Dùng: .txt, .pdf, .docx")
        };
    }

    /// <summary>Parse PDF dùng PdfPig</summary>
    private static string ParsePdf(byte[] bytes)
    {
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    /// <summary>Parse Word (.docx) dùng OpenXml</summary>
    private static string ParseDocx(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(stream, false);
        if (doc.MainDocumentPart?.Document?.Body == null) return string.Empty;
        var sb = new StringBuilder();
        foreach (var para in doc.MainDocumentPart.Document.Body
            .Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
        {
            sb.AppendLine(para.InnerText);
        }
        return sb.ToString();
    }

    // ================================================================
    // INSERT CÂY SPRINT VÀO DB (NHANH MỚI — BIÊN BẢN TRỰC TIẾP)
    // ================================================================

    /// <summary>
    /// Tạo cây Task từ bảng Sprint trên biên bản:
    ///   Project (1) → Giai đoạn/Sprint = Epic (n) → Công việc = Task (n)
    /// Assign theo tên người trong bảng (match với User.FullName trong DB).
    /// </summary>
    private async Task<int> InsertSprintTreeAsync(
        DocumentExtractDto extract,
        int requestingUserId,
        List<CreatedNodeDto> createdNodes,
        GenerationStatsDto stats,
        List<string> warnings)
    {
        // Tạo Project node gốc
        var projectTask = new TaskModel
        {
            Title          = extract.ProjectName,
            Description    = extract.ProjectDescription ?? extract.ProjectName,
            StartDate      = extract.StartDate,
            DueDate        = extract.EndDate,
            TaskType       = TaskTypeEnum.Project,
            ParentTaskId   = null,
            IsAIGenerated  = true,
            IsAutoAssigned = false,
            Status         = "pending",
            Priority       = "high",
            AssignerId     = requestingUserId,
            CreatedAt      = DateTime.Now
        };
        _context.Tasks.Add(projectTask);
        await _context.SaveChangesAsync();
        createdNodes.Add(MapToNode(projectTask, "Project", null, null));

        // Tải tất cả users để tìm theo FullName
        var allUsers = await _context.Users
            .AsNoTracking()
            .ToListAsync();

        // Nhóm Sprint theo Phase → mỗi Phase tương ứng 1 Epic
        var byPhase = extract.Sprints
            .GroupBy(s => s.PhaseNumber)
            .OrderBy(g => g.Key);

        foreach (var phaseGroup in byPhase)
        {
            var firstSprint = phaseGroup.OrderBy(s => s.SprintNumber).First();
            var lastSprint  = phaseGroup.OrderByDescending(s => s.SprintNumber).First();

            // Tạo Epic node tương ứng Giai đoạn
            var epicTask = new TaskModel
            {
                Title          = $"Giai đoạn {phaseGroup.Key}",
                Description    = $"Giai đoạn {phaseGroup.Key} — {phaseGroup.Count()} Sprint",
                StartDate      = firstSprint.StartDate ?? extract.StartDate,
                DueDate        = lastSprint.EndDate   ?? extract.EndDate,
                TaskType       = TaskTypeEnum.Epic,
                ParentTaskId   = projectTask.TaskId,
                IsAIGenerated  = true,
                Status         = "pending",
                Priority       = "medium",
                AssignerId     = requestingUserId,
                CreatedAt      = DateTime.Now
            };
            _context.Tasks.Add(epicTask);
            await _context.SaveChangesAsync();
            createdNodes.Add(MapToNode(epicTask, "Epic", projectTask.TaskId, null));
            stats.EpicCount++;

            foreach (var sprint in phaseGroup.OrderBy(s => s.SprintNumber))
            {
                // Tạo Story node tương ứng Sprint
                var storyTask = new TaskModel
                {
                    Title          = $"Sprint {sprint.SprintNumber}: {sprint.SprintName}",
                    Description    = sprint.SprintName,
                    StartDate      = sprint.StartDate ?? extract.StartDate,
                    DueDate        = sprint.EndDate   ?? extract.EndDate,
                    TaskType       = TaskTypeEnum.Story,
                    ParentTaskId   = epicTask.TaskId,
                    IsAIGenerated  = true,
                    Status         = "pending",
                    Priority       = "medium",
                    AssignerId     = requestingUserId,
                    CreatedAt      = DateTime.Now
                };
                _context.Tasks.Add(storyTask);
                await _context.SaveChangesAsync();
                createdNodes.Add(MapToNode(storyTask, "Story", epicTask.TaskId, null));
                stats.StoryCount++;

                // Tạo từng Task từ bảng công việc
                foreach (var sprintTask in sprint.Tasks)
                {
                    // Tìm assignee theo tên trong bảng
                    var (assigneeId, assigneeName) = FindAssigneeByName(
                        sprintTask.AssigneeName, allUsers, warnings);

                    var dbTask = new TaskModel
                    {
                        Title          = $"[{sprintTask.Code}] {sprintTask.Title}",
                        Description    = $"[{sprintTask.Type ?? "Task"}] {sprintTask.Title}",
                        StartDate      = sprint.StartDate ?? extract.StartDate,
                        DueDate        = sprint.EndDate   ?? extract.EndDate,
                        TaskType       = TaskTypeEnum.Task,
                        ParentTaskId   = storyTask.TaskId,
                        IsAIGenerated  = true,
                        IsAutoAssigned = false, // Assign theo biên bản, không phải tự động
                        AssigneeId     = assigneeId,
                        AssignerId     = requestingUserId,
                        Status         = "pending",
                        Priority       = "medium",
                        CreatedAt      = DateTime.Now
                    };
                    _context.Tasks.Add(dbTask);
                    await _context.SaveChangesAsync();

                    // Ghi AssignmentHistory + taskassignees nếu assign được
                    if (assigneeId.HasValue)
                    {
                        _context.AssignmentHistories.Add(new AssignmentHistory
                        {
                            TaskId           = dbTask.TaskId,
                            AssignedToUserId = assigneeId.Value,
                            AssignedByUserId = requestingUserId,
                            AssignmentMethod = "SprintTemplate",
                            MatchScore       = 1.0m, // Assign theo tên — match 100%
                            AssignmentReason = $"[Biên bản] Giao việc trực tiếp: {sprintTask.AssigneeName}",
                            AssignedAt       = DateTime.Now
                        });
                        await _context.SaveChangesAsync();

                        // Ghi vào bảng taskassignees (composite PK: TaskId + UserId)
                        // Không có model class riêng → dùng raw SQL
                        await _context.Database.ExecuteSqlRawAsync(
                            "INSERT INTO taskassignees (TaskId, UserId) VALUES ({0}, {1})",
                            dbTask.TaskId, assigneeId.Value);

                        stats.AssignedCount++;
                    }
                    else
                    {
                        stats.UnassignedCount++;
                    }

                    createdNodes.Add(MapToNode(dbTask, "Task", storyTask.TaskId, assigneeId, assigneeName));
                    stats.TaskCount++;
                }
            }
        }

        return projectTask.TaskId;
    }

    // ================================================================
    // INSERT CÂY TASK VÀO DB
    // ================================================================

    /// <summary>
    /// Insert toàn bộ cây AgilePlanDto vào bảng Task theo thứ tự tầng.
    /// Returns ProjectTaskId.
    /// </summary>
    private async Task<int> InsertTreeAsync(
        AgilePlanDto agilePlan,
        int requestingUserId,
        List<CreatedNodeDto> createdNodes,
        GenerationStatsDto stats,
        List<string> warnings)
    {
        var proj = agilePlan.Project;

        // ── Tầng 1: Project ──────────────────────────────────────────
        var projectTask = new TaskModel
        {
            Title          = proj.Title,
            Description    = proj.Description ?? proj.Title,
            StartDate      = proj.StartDate,
            DueDate        = proj.DueDate,
            EstimatedHours = proj.EstimatedHours,
            TaskType       = TaskTypeEnum.Project,
            ParentTaskId   = null,
            IsAIGenerated  = true,
            IsAutoAssigned = false,
            Status         = "pending",
            Priority       = "high",
            AssignerId     = requestingUserId,
            CreatedAt      = DateTime.Now
        };
        _context.Tasks.Add(projectTask);
        await _context.SaveChangesAsync();

        createdNodes.Add(MapToNode(projectTask, "Project", null, null));

        // ── Tầng 2-4: Epics → Stories → Tasks ────────────────────────
        // Load tất cả employees có skill để tính skill-match một lần
        var employees = await LoadEmployeesWithSkillsAsync();

        foreach (var epic in proj.Epics)
        {
            var epicTask = new TaskModel
            {
                Title          = epic.Title,
                Description    = epic.Description ?? epic.Title,
                StartDate      = epic.StartDate,
                DueDate        = epic.DueDate,
                EstimatedHours = epic.EstimatedHours,
                TaskType       = TaskTypeEnum.Epic,
                ParentTaskId   = projectTask.TaskId,
                IsAIGenerated  = true,
                Status         = "pending",
                Priority       = "medium",
                AssignerId     = requestingUserId,
                CreatedAt      = DateTime.Now
            };
            _context.Tasks.Add(epicTask);
            await _context.SaveChangesAsync();

            createdNodes.Add(MapToNode(epicTask, "Epic", projectTask.TaskId, null));
            stats.EpicCount++;

            foreach (var story in epic.Stories)
            {
                var storyTask = new TaskModel
                {
                    Title          = story.Title,
                    Description    = FormatStoryDescription(story),
                    StartDate      = story.StartDate,
                    DueDate        = story.DueDate,
                    EstimatedHours = story.EstimatedHours,
                    TaskType       = TaskTypeEnum.Story,
                    ParentTaskId   = epicTask.TaskId,
                    IsAIGenerated  = true,
                    Status         = "pending",
                    Priority       = "medium",
                    AssignerId     = requestingUserId,
                    CreatedAt      = DateTime.Now
                };
                _context.Tasks.Add(storyTask);
                await _context.SaveChangesAsync();

                createdNodes.Add(MapToNode(storyTask, "Story", epicTask.TaskId, null));
                stats.StoryCount++;

                // ── Tầng 4: Task implementation ───────────────────────
                foreach (var taskNode in story.Tasks)
                {
                    // Auto-assign với skill match + workload
                    var (assigneeId, assigneeName, matchScore, reasoning) =
                        FindBestAssignee(taskNode, employees, warnings);

                    var dbTask = new TaskModel
                    {
                        Title          = taskNode.Title,
                        Description    = taskNode.Description,
                        StartDate      = taskNode.StartDate,
                        DueDate        = taskNode.DueDate,
                        EstimatedHours = taskNode.EstimatedHours,
                        TaskType       = TaskTypeEnum.Task,
                        ParentTaskId   = storyTask.TaskId,
                        IsAIGenerated  = true,
                        IsAutoAssigned = assigneeId.HasValue,
                        AssigneeId     = assigneeId,
                        AssignerId     = requestingUserId,
                        Status         = "pending",
                        Priority       = "medium",
                        CreatedAt      = DateTime.Now
                    };
                    _context.Tasks.Add(dbTask);
                    await _context.SaveChangesAsync();

                    // Log reasoning vào AssignmentHistory + taskassignees nếu assign thành công
                    if (assigneeId.HasValue)
                    {
                        var history = new AssignmentHistory
                        {
                            TaskId           = dbTask.TaskId,
                            AssignedToUserId = assigneeId.Value,
                            AssignedByUserId = requestingUserId,
                            AssignmentMethod = "Auto-AI",
                            MatchScore       = matchScore,
                            AssignmentReason = reasoning,
                            AssignedAt       = DateTime.Now
                        };
                        _context.AssignmentHistories.Add(history);

                        // Cộng thêm estimated hours vào workload employee (in-memory)
                        var emp = employees.FirstOrDefault(e => e.UserId == assigneeId.Value);
                        if (emp?.EmployeeProfile != null && dbTask.EstimatedHours.HasValue)
                            emp.EmployeeProfile.CurrentWorkloadHours += dbTask.EstimatedHours.Value;

                        stats.AssignedCount++;
                    }
                    else
                    {
                        stats.UnassignedCount++;
                    }

                    await _context.SaveChangesAsync();

                    // Ghi vào taskassignees sau khi SaveChanges (cần TaskId đã có từ DB)
                    if (assigneeId.HasValue)
                    {
                        await _context.Database.ExecuteSqlRawAsync(
                            "INSERT INTO taskassignees (TaskId, UserId) VALUES ({0}, {1})",
                            dbTask.TaskId, assigneeId.Value);
                    }
                    createdNodes.Add(MapToNode(dbTask, "Task", storyTask.TaskId, assigneeId, assigneeName, matchScore));
                    stats.TaskCount++;
                }
            }
        }

        return projectTask.TaskId;
    }

    // ================================================================
    // AUTO-ASSIGN: SKILL MATCH + WORKLOAD
    // ================================================================

    /// <summary>
    /// Tải danh sách employees kèm UserSkill và EmployeeProfile.
    /// Dùng để tính skill match + workload cùng lúc.
    /// </summary>
    private async Task<List<User>> LoadEmployeesWithSkillsAsync()
    {
        return await _context.Users
            .Include(u => u.UserSkills)
                .ThenInclude(us => us.Skill)
            .Include(u => u.EmployeeProfile)
            .Where(u => u.EmployeeProfile != null)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Tìm nhân viên phù hợp nhất cho 1 Task theo:
    ///   - Skill match: UserSkill.Skill.SkillName match với taskNode.SkillTags
    ///   - Workload: EmployeeProfile.AvailableHoursPerWeek > 0
    ///   - Score = skillMatchRatio * 0.7 + availabilityRatio * 0.3
    ///
    /// Log reasoning đầy đủ để ghi vào AssignmentHistory.
    /// Returns: (assigneeId, assigneeName, matchScore, reasoning)
    /// </summary>
    private static (int? id, string? name, decimal score, string reasoning) FindBestAssignee(
        TaskPlanNode taskNode,
        List<User> employees,
        List<string> warnings)
    {
        if (!employees.Any())
            return (null, null, 0, "Không có nhân viên nào trong hệ thống");

        // Normalize task skill tags
        var requiredTags = taskNode.SkillTags
            .Select(t => t.ToUpperInvariant())
            .ToHashSet();

        int? bestId       = null;
        string? bestName  = null;
        decimal bestScore = -1;
        string bestReason = "Không tìm được assignee phù hợp";

        foreach (var emp in employees)
        {
            var profile = emp.EmployeeProfile;
            if (profile == null) continue;

            // ── Skill Match Score (0.0–1.0) ──────────────────────────
            var empSkills = emp.UserSkills
                .Where(us => us.Skill != null)
                .Select(us => us.Skill.SkillName.ToUpperInvariant())
                .ToHashSet();

            decimal skillScore = 0;
            string  skillDetail;

            if (!requiredTags.Any())
            {
                // Không yêu cầu skill cụ thể → coi là match hoàn toàn
                skillScore  = 1.0m;
                skillDetail = "Không yêu cầu skill cụ thể";
            }
            else
            {
                var matchedSkills = requiredTags.Count(rt => empSkills.Any(es => es.Contains(rt) || rt.Contains(es)));
                skillScore  = (decimal)matchedSkills / requiredTags.Count;
                var matchedList = requiredTags.Where(rt => empSkills.Any(es => es.Contains(rt) || rt.Contains(es)));
                skillDetail = skillScore > 0
                    ? $"Skill match {matchedSkills}/{requiredTags.Count} ({string.Join(", ", matchedList.Take(3))})"
                    : $"Không có skill nào match (cần: {string.Join(", ", requiredTags.Take(3))})";
            }

            // ── Workload Score (0.0–1.0) ─────────────────────────────
            decimal capacity     = profile.WeeklyCapacity > 0 ? profile.WeeklyCapacity : 40;
            decimal currentLoad  = profile.CurrentWorkloadHours;
            decimal available    = Math.Max(0, capacity - currentLoad);
            decimal availScore   = currentLoad >= capacity ? 0m : available / capacity;
            string  workDetail   = $"Workload: {currentLoad:N1}/{capacity:N1}h ({available:N1}h rảnh)";

            // ── Tổng Score: skill 70% + workload 30% ─────────────────
            const decimal SKILL_WEIGHT    = 0.7m;
            const decimal WORKLOAD_WEIGHT = 0.3m;
            var totalScore = skillScore * SKILL_WEIGHT + availScore * WORKLOAD_WEIGHT;

            if (totalScore > bestScore && availScore > 0)
            {
                bestScore  = totalScore;
                bestId     = emp.UserId;
                bestName   = emp.FullName;
                bestReason = $"[Auto-AI] {emp.FullName}: {skillDetail}. {workDetail}. " +
                             $"Score={totalScore:P0} (Skill={skillScore:P0}×70% + Workload={availScore:P0}×30%)";
            }
        }

        // Ngưỡng tối thiểu để assign: tổng score ≥ 20%
        const decimal ASSIGNMENT_THRESHOLD = 0.20m;
        if (bestScore < ASSIGNMENT_THRESHOLD)
        {
            warnings.Add($"Task '{taskNode.Title[..Math.Min(50, taskNode.Title.Length)]}': Không tìm được assignee đủ điều kiện (score max = {bestScore:P0}).");
            return (null, null, 0, $"Không đạt ngưỡng assign (score = {bestScore:P0} < {ASSIGNMENT_THRESHOLD:P0})");
        }

        return (bestId, bestName, bestScore, bestReason);
    }

    // ================================================================
    // HELPERS
    // ================================================================

    /// <summary>Ghép description + acceptance criteria của Story thành 1 chuỗi</summary>
    private static string FormatStoryDescription(StoryPlanNode story)
    {
        if (string.IsNullOrWhiteSpace(story.AcceptanceCriteria))
            return story.Description ?? story.Title;

        return $"{story.Description}\n\nAcceptance Criteria:\n{story.AcceptanceCriteria}";
    }

    /// <summary>Map TaskModel thành CreatedNodeDto để trả frontend</summary>
    private static CreatedNodeDto MapToNode(
        TaskModel task,
        string nodeType,
        int? parentId,
        int? assigneeId,
        string? assigneeName = null,
        decimal matchScore   = 0)
    {
        return new CreatedNodeDto
        {
            TaskId         = task.TaskId,
            ParentTaskId   = parentId,
            NodeType       = nodeType,
            Title          = task.Title,
            AssigneeId     = assigneeId,
            AssigneeName   = assigneeName,
            AssignMatchScore = matchScore > 0 ? matchScore : null
        };
    }

    /// <summary>
    /// Tìm User trong DB theo tên người thực hiện ghi trong biên bản.
    /// Chiến lược match (giảm dần độ nghiêm ngặt):
    ///   1. FullName khớp chính xác (case-insensitive)
    ///   2. FullName chứa tên cần tìm (hoặc ngược lại)
    /// Trả null nếu không tìm thấy, ghi warning.
    /// </summary>
    private static (int? id, string? name) FindAssigneeByName(
        string? assigneeName,
        List<User> allUsers,
        List<string> warnings)
    {
        // Không có tên trong bảng → chưa phân công
        if (string.IsNullOrWhiteSpace(assigneeName))
            return (null, null);

        var target = assigneeName.Trim();

        // Bước 1: Khớp chính xác FullName
        var exact = allUsers.FirstOrDefault(u =>
            string.Equals(u.FullName?.Trim(), target, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return (exact.UserId, exact.FullName);

        // Bước 2: Khớp một phần (FullName chứa target hoặc ngược lại)
        var partial = allUsers.FirstOrDefault(u =>
            u.FullName != null &&
            (u.FullName.Contains(target, StringComparison.OrdinalIgnoreCase) ||
             target.Contains(u.FullName, StringComparison.OrdinalIgnoreCase)));
        if (partial != null) return (partial.UserId, partial.FullName);

        // Không tìm thấy → cảnh báo để PM biết cần tạo tài khoản
        warnings.Add($"Không tìm thấy user '{assigneeName}' trong DB — task sẽ không được assign.");
        return (null, null);
    }
}

