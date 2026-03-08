using DocTask.Core.Dtos.AiGeneration;
using DocTask.Core.Models;

namespace DocTask.Api.Services;

/// <summary>
/// Rule engine: đọc cấu hình từ bảng AiTaskRule trong DB và áp vào AgilePlanDto.
/// Admin có thể thêm/sửa/xóa rule qua API mà không cần sửa code.
///
/// 3 loại rule:
///   SkillKeyword     → TechStack/Document chứa keyword → gắn SkillTag vào Project/Epic
///   ModuleKeywordSkill → Tên Epic/Story chứa keyword → gắn SkillTag vào Story/Task
///   DefaultPhase     → Dùng khi không có functions → tạo Epic mặc định
/// </summary>
public class AiRuleApplier
{
    // ================================================================
    // ENTRY POINT
    // ================================================================

    /// <summary>
    /// Áp toàn bộ rule lên AgilePlanDto đã được sinh bởi AgilePlanner.
    /// Mutates trực tiếp vào các node (thêm SkillTags).
    /// </summary>
    /// <param name="plan">Agile plan sẽ được enrich</param>
    /// <param name="extract">Kết quả parse tài liệu (dùng để match SkillKeyword)</param>
    /// <param name="allRules">Tất cả rule active từ DB (đã load sẵn)</param>
    /// <param name="warnings">Danh sách warning để ghi bổ sung</param>
    public void Apply(AgilePlanDto plan, DocumentExtractDto extract,
        List<AiTaskRule> allRules, List<string> warnings)
    {
        // Phân loại rule theo type để tra cứu nhanh
        var skillKeywordRules      = allRules
            .Where(r => r.RuleType == "SkillKeyword" && r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ToList();

        var moduleKeywordSkillRules = allRules
            .Where(r => r.RuleType == "ModuleKeywordSkill" && r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ToList();

        var defaultPhaseRules = allRules
            .Where(r => r.RuleType == "DefaultPhase" && r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ToList();

        // Chuỗi searchable từ nội dung tài liệu + tech stack
        var documentText = BuildSearchableDocumentText(extract);

        // ── 1. Gắn SkillTag vào Project (từ TechStack/doc toàn cục) ──
        ApplySkillKeywordRules(plan.Project.SkillTags, documentText, skillKeywordRules);

        // ── 2. Fallback DefaultPhase nếu không có Epic ───────────────
        if (!plan.Project.Epics.Any() && defaultPhaseRules.Any())
        {
            ApplyDefaultPhases(plan.Project, extract, defaultPhaseRules, warnings);
        }

        // ── 3. Gắn SkillTag vào từng Epic/Story/Task ─────────────────
        foreach (var epic in plan.Project.Epics)
        {
            // Match theo tên Epic
            ApplyModuleKeywordSkillRules(epic.SkillTags, epic.Title, epic.Description, moduleKeywordSkillRules);

            // Kế thừa skills từ Project nếu Epic chưa có gì
            if (!epic.SkillTags.Any())
                epic.SkillTags.AddRange(plan.Project.SkillTags.Take(3));

            foreach (var story in epic.Stories)
            {
                // Match theo tên Story
                ApplyModuleKeywordSkillRules(story.SkillTags, story.Title, story.Description, moduleKeywordSkillRules);

                // Kế thừa từ Epic
                if (!story.SkillTags.Any())
                    story.SkillTags.AddRange(epic.SkillTags.Take(3));

                foreach (var task in story.Tasks)
                {
                    // Task đã có SkillTags từ AgilePlanner (theo role)
                    // Bổ sung thêm từ module rule nếu còn thiếu
                    ApplyModuleKeywordSkillRules(task.SkillTags, task.Title, task.Description, moduleKeywordSkillRules);

                    // Bỏ trùng
                    task.SkillTags = task.SkillTags.Distinct().ToList();
                }

                story.SkillTags = story.SkillTags.Distinct().ToList();
            }

            epic.SkillTags = epic.SkillTags.Distinct().ToList();
        }

        if (!skillKeywordRules.Any() && !moduleKeywordSkillRules.Any())
        {
            warnings.Add("Không có rule nào trong DB — SkillTags lấy từ TechStack gốc. Thêm rule tại /api/ai-rules.");
        }
    }

    // ================================================================
    // RULE MATCHERS
    // ================================================================

    /// <summary>
    /// Áp SkillKeyword rules: nếu documentText chứa keyword → thêm skillName vào tags.
    /// </summary>
    private static void ApplySkillKeywordRules(
        List<string> skillTags,
        string documentTextUpper,
        List<AiTaskRule> rules)
    {
        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Keyword) || string.IsNullOrWhiteSpace(rule.SkillName))
                continue;

            // Tránh trùng
            if (skillTags.Contains(rule.SkillName, StringComparer.OrdinalIgnoreCase))
                continue;

            if (documentTextUpper.Contains(rule.Keyword.ToUpperInvariant()))
                skillTags.Add(rule.SkillName);
        }
    }

    /// <summary>
    /// Áp ModuleKeywordSkill rules: nếu title/description của node chứa keyword → thêm skillName.
    /// </summary>
    private static void ApplyModuleKeywordSkillRules(
        List<string> skillTags,
        string? title,
        string? description,
        List<AiTaskRule> rules)
    {
        var combined = $"{title} {description}".ToUpperInvariant();

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Keyword) || string.IsNullOrWhiteSpace(rule.SkillName))
                continue;

            if (skillTags.Contains(rule.SkillName, StringComparer.OrdinalIgnoreCase))
                continue;

            if (combined.Contains(rule.Keyword.ToUpperInvariant()))
                skillTags.Add(rule.SkillName);
        }
    }

    // ================================================================
    // DEFAULT PHASE FALLBACK
    // ================================================================

    /// <summary>
    /// Khi tài liệu không có chức năng nào → sinh Epic mặc định từ DefaultPhase rules.
    /// Thời gian phân bổ theo PhaseRatio của từng rule.
    /// </summary>
    private static void ApplyDefaultPhases(
        ProjectPlanNode project,
        DocumentExtractDto extract,
        List<AiTaskRule> defaultPhaseRules,
        List<string> warnings)
    {
        var start    = extract.StartDate ?? DateTime.Now;
        var end      = extract.EndDate   ?? DateTime.Now.AddMonths(6);
        var totalDays = Math.Max(1, (end - start).Days);
        var offset    = 0;

        for (int i = 0; i < defaultPhaseRules.Count; i++)
        {
            var phase    = defaultPhaseRules[i];
            var phaseName = phase.PhaseName ?? $"Phase {i + 1}";
            var ratio    = phase.PhaseRatio ?? (1.0 / defaultPhaseRules.Count);
            var hours    = phase.PhaseHours ?? 40;
            var duration = Math.Max(2, (int)Math.Round(totalDays * ratio));

            var pStart = start.AddDays(offset);
            var pEnd   = i == defaultPhaseRules.Count - 1
                ? end
                : pStart.AddDays(duration);

            offset += duration;
            if (pEnd > end) pEnd = end;

            var epic = new EpicPlanNode
            {
                FunctionNumber = i + 1,
                Title          = phaseName,
                Description    = $"{phaseName} (tự động sinh từ DefaultPhase rule)",
                StartDate      = pStart,
                DueDate        = pEnd,
                EstimatedHours = hours,
                SkillTags      = new List<string>()
            };

            // Sinh 1 Story + 4 Tasks cho mỗi phase
            var story = new StoryPlanNode
            {
                Title          = $"Triển khai {phaseName}",
                Description    = $"Hoàn thành toàn bộ nội dung {phaseName}",
                StartDate      = pStart,
                DueDate        = pEnd,
                EstimatedHours = hours,
                SkillTags      = new List<string>()
            };

            // 4 Task cố định cho mỗi story
            story.Tasks = new List<TaskPlanNode>
            {
                MakeDefaultTask("BE",     story, hours * 0.40m),
                MakeDefaultTask("FE",     story, hours * 0.30m),
                MakeDefaultTask("QA",     story, hours * 0.20m),
                MakeDefaultTask("DevOps", story, hours * 0.10m)
            };

            epic.Stories.Add(story);
            project.Epics.Add(epic);
        }

        warnings.Add($"Tài liệu không có CHỨC NĂNG — đã sinh {defaultPhaseRules.Count} Epic mặc định từ DefaultPhase rules.");
    }

    /// <summary>Tạo 1 Task implementation mặc định</summary>
    private static TaskPlanNode MakeDefaultTask(string role, StoryPlanNode story, decimal hours)
    {
        return new TaskPlanNode
        {
            Title          = $"[{role}] {story.Title}",
            Description    = $"[{role}] {story.Description}",
            StartDate      = story.StartDate,
            DueDate        = story.DueDate,
            EstimatedHours = Math.Max(0.5m, Math.Round(hours, 1)),
            RoleTag        = role,
            SkillTags      = new List<string>()
        };
    }

    // ================================================================
    // HELPERS
    // ================================================================

    /// <summary>
    /// Xây dựng chuỗi searchable từ tài liệu để match SkillKeyword rules.
    /// Gộp: ProjectName + TechStack + NFR + descriptions
    /// </summary>
    private static string BuildSearchableDocumentText(DocumentExtractDto extract)
    {
        var parts = new List<string>();
        parts.Add(extract.ProjectName);
        parts.AddRange(extract.TechStack);
        parts.AddRange(extract.NonFunctionalRequirements);
        parts.Add(extract.ProjectDescription ?? "");
        foreach (var f in extract.Functions)
        {
            parts.Add(f.FunctionName);
            parts.Add(f.Description ?? "");
            parts.AddRange(f.Details);
        }
        return string.Join(" ", parts).ToUpperInvariant();
    }
}
