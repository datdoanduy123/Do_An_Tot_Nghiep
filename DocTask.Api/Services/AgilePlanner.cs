using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using DocTask.Core.Dtos.AiGeneration;
using DocTask.Service.Services.Providers;
using static DocTask.Core.Dtos.Gemini.AiProviderDto;

namespace DocTask.Api.Services;

/// <summary>
/// Chuyển DocumentExtractDto thành AgilePlanDto theo cây Project→Epic→Story→Task.
///
/// Chiến lược:
///   1. Build khung cây từ DocumentExtractDto (deterministic, không fail)
///   2. Gọi Ollama để enrich mỗi Story: viết User Story chuẩn + Acceptance Criteria
///   3. Validate JSON response từ Ollama — nếu lỗi thì fallback về text gốc
///   4. Sinh các Task implementation cố định: [BE] [FE] [QA] [DevOps]
/// </summary>
    public class AgilePlanner
    {
        private readonly AiProviderFactory _aiFactory;
        private readonly SemaphoreSlim      _ollamaSemaphore = new(2); // Giới hạn 2 Epic được xử lý song song để tránh treo máy local

    // Tỷ lệ giờ ước tính phân bổ xuống tầng con
    // Epic → Story: chia đều hoặc theo số details
    // Story → Task: chia theo role weight
    private static readonly Dictionary<string, decimal> RoleHourWeight = new()
    {
        { "BE",     0.40m }, // Backend chiếm 40% giờ của story
        { "FE",     0.30m }, // Frontend 30%
        { "QA",     0.20m }, // Kiểm thử 20%
        { "DevOps", 0.10m }  // Triển khai 10%
    };

    public AgilePlanner(AiProviderFactory aiFactory)
    {
        _aiFactory = aiFactory;
    }

    // ================================================================
    // ENTRY POINT
    // ================================================================

    /// <summary>
    /// Chuyển DocumentExtractDto (đã parse) thành AgilePlanDto 4 tầng.
    /// </summary>
    /// <param name="extract">Kết quả parse từ DocumentTemplateParser</param>
    /// <param name="warnings">Mảng warning để ghi thêm cảnh báo AI</param>
    public async Task<AgilePlanDto> PlanAsync(DocumentExtractDto extract, List<string> warnings)
    {
        // ── Tầng 1: Project ──────────────────────────────────────────
        var project = new ProjectPlanNode
        {
            Title          = extract.ProjectName,
            Description    = extract.ProjectDescription,
            StartDate      = extract.StartDate,
            DueDate        = extract.EndDate,
            EstimatedHours = extract.Functions.Sum(f => f.EstimatedHours ?? 0),
            SkillTags      = BuildProjectSkillTags(extract.TechStack)
        };

        // ── Tầng 2 & 3: Epic & Story (Xử lý song song các Epic) ────────
        var epicTasks = extract.Functions.Select(async func =>
        {
            // Lock semaphore để giới hạn số lượng request đồng thời tới Ollama
            await _ollamaSemaphore.WaitAsync();
            try
            {
                var epic = BuildEpicNode(func, extract.StartDate, extract.EndDate);

                // Gộp tất cả details của Epic để enrich 1 lần
                var stories = await BuildStoriesInEpicAsync(func, extract, warnings);
                
                foreach (var story in stories)
                {
                    // ── Tầng 4: Task (BE/FE/QA/DevOps) ──────────────────
                    story.Tasks = BuildTaskNodes(story, extract);
                    epic.Stories.Add(story);
                }

                // Nếu không có detail nào → sinh 1 story mặc định
                if (!epic.Stories.Any())
                {
                    var defaultStory = BuildDefaultStory(func, extract);
                    defaultStory.Tasks = BuildTaskNodes(defaultStory, extract);
                    epic.Stories.Add(defaultStory);
                    lock(warnings) // warnings là List shared nên cần lock khi add từ nhiều thread
                    {
                        warnings.Add($"Chức năng {func.FunctionNumber} không có yêu cầu chi tiết — sinh 1 Story mặc định.");
                    }
                }

                return epic;
            }
            finally
            {
                _ollamaSemaphore.Release();
            }
        });

        var completedEpics = await Task.WhenAll(epicTasks);
        project.Epics.AddRange(completedEpics);

        return new AgilePlanDto { Project = project };
    }

    // ================================================================
    // XÂY DỰNG CÁC NODE
    // ================================================================

    /// <summary>Build SkillTags cho Project từ TechStack text</summary>
    private static List<string> BuildProjectSkillTags(List<string> techStack)
    {
        // Lấy tối đa 10 tag từ TechStack (bóc phần chính trước dấu phẩy/ngoặc)
        return techStack
            .Select(t => t.Split(',', '(', '–', '-')[0].Trim())
            .Where(t => t.Length >= 2)
            .Take(10)
            .ToList();
    }

    /// <summary>Xây dựng Epic node từ FunctionExtractDto</summary>
    private static EpicPlanNode BuildEpicNode(FunctionExtractDto func,
        DateTime? projectStart, DateTime? projectEnd)
    {
        return new EpicPlanNode
        {
            FunctionNumber = func.FunctionNumber,
            Title          = $"CHỨC NĂNG {func.FunctionNumber}: {func.FunctionName}",
            Description    = func.Description,
            StartDate      = func.StartDate ?? projectStart,
            DueDate        = func.Deadline  ?? projectEnd,
            EstimatedHours = func.EstimatedHours,
            SkillTags      = new List<string>() // AiRuleApplier sẽ bổ sung sau
        };
    }

    /// <summary>
    /// Xây dựng danh sách Story nodes cho 1 Function (Epic).
    /// Quy trình:
    ///   1. Tạo khung Story từ rawDetails.
    ///   2. Gọi Ollama 1 lần (batch) để enrich User Story + AC cho cả Epic.
    /// </summary>
    private async Task<List<StoryPlanNode>> BuildStoriesInEpicAsync(
        FunctionExtractDto func,
        DocumentExtractDto extract,
        List<string> warnings)
    {
        var stories = new List<StoryPlanNode>();
        if (!func.Details.Any()) return stories;

        // Chia đều giờ ước tính
        decimal? storyHours = func.EstimatedHours.HasValue
            ? (decimal?)(func.EstimatedHours.Value / func.Details.Count)
            : null;

        // BƯỚC 1: Tạo khung Story ban đầu (Dùng raw detail làm fallback)
        for (int i = 0; i < func.Details.Count; i++)
        {
            stories.Add(new StoryPlanNode
            {
                Title          = $"{func.FunctionNumber}.{i + 1} {func.Details[i]}",
                Description    = func.Details[i], 
                EstimatedHours = storyHours,
                StartDate      = func.StartDate ?? extract.StartDate,
                DueDate        = func.Deadline  ?? extract.EndDate,
                SkillTags      = new List<string>()
            });
        }

        // BƯỚC 2: Gọi Ollama Batch để enrich
        var enrichResults = await TryEnrichStoriesWithOllamaBatchAsync(
            func.Details, func.FunctionName, extract.ProjectName, extract.TechStack);

        // BƯỚC 3: Apply kết quả từ AI
        if (enrichResults.warnings != null)
        {
            lock (warnings) warnings.Add(enrichResults.warnings);
        }

        if (enrichResults.items != null && enrichResults.items.Count > 0)
        {
            // Map kết quả mảng trả về từ AI vào danh sách story theo đúng index
            for (int i = 0; i < Math.Min(stories.Count, enrichResults.items.Count); i++)
            {
                var item = enrichResults.items[i];
                if (!string.IsNullOrWhiteSpace(item.UserStory))
                    stories[i].Description = item.UserStory;
                
                stories[i].AcceptanceCriteria = item.AcceptanceCriteria;
            }
        }

        return stories;
    }

    /// <summary>
    /// Giao tiếp với Ollama theo mô hình BATCH: gửi danh sách yêu cầu và nhận mảng JSON.
    /// Giúp tiết kiệm thời gian đáng kể so với gọi lẻ từng story.
    /// </summary>
    private async Task<(List<OllamaStoryItem>? items, string? warnings)> TryEnrichStoriesWithOllamaBatchAsync(
        List<string> rawDetails,
        string epicName,
        string projectName,
        List<string> techStack)
    {
        if (!rawDetails.Any()) return (null, null);

        try
        {
            var techStackStr = techStack.Any() ? string.Join(", ", techStack.Take(5)) : "không rõ";
            
            // Build danh sách yêu cầu đánh số để LLM dễ map
            var requirementsList = new StringBuilder();
            for (int i = 0; i < rawDetails.Count; i++)
                requirementsList.AppendLine($"{i + 1}. {rawDetails[i]}");

            var prompt = $@"
Bạn là BA chuyên viết Agile User Story.
Dự án: {projectName}
Công nghệ: {techStackStr}
Chức năng chính: {epicName}

Hãy viết User Story và Acceptance Criteria cho danh sách {rawDetails.Count} yêu cầu sau.
YÊU CẦU: Trả về một mảng JSON các object theo đúng thứ tự, KHÔNG thêm văn bản giải thích.

Danh sách yêu cầu:
{requirementsList}

ĐỊNH DẠNG JSON MONG ĐỢI:
[
  {{
    ""userStory"": ""As a [role] I want [goal] so that [benefit]"",
    ""acceptanceCriteria"": ""Given [context] When [action] Then [outcome]""
  }},
  ...
]
".Trim();

            var request = new AiCompletionRequest
            {
                Temperature     = 0.2, // Giảm sáng tạo để đảm bảo cấu trúc mảng JSON
                MaxOutputTokens = 2048, // Nâng token limit vì response là mảng
                Messages = new List<AiMessage>
                {
                    new() { Role = "system", Content = "Bạn là BA. Chỉ trả JSON array, không giải thích nội dung." },
                    new() { Role = "user",   Content = prompt }
                }
            };

            var response = await _aiFactory.CompleteAsync(request);

            if (!response.Success || string.IsNullOrWhiteSpace(response.Text))
                return (null, $"Ollama không trả kết quả cho Epic '{epicName}': {response.ErrorMessage}");

            // Parse mảng JSON
            var items = ParseOllamaBatchJson(response.Text);
            if (items == null || !items.Any())
                return (null, $"Ollama JSON mảng không hợp lệ cho Epic '{epicName}' — fallback về text gốc.");

            return (items, null);
        }
        catch (Exception ex)
        {
            return (null, $"Ollama exception (Batch): {ex.Message}");
        }
    }

    private class OllamaStoryItem
    {
        public string? UserStory { get; set; }
        public string? AcceptanceCriteria { get; set; }
    }

    private static List<OllamaStoryItem>? ParseOllamaBatchJson(string rawText)
    {
        var jsonStart = rawText.IndexOf('[');
        var jsonEnd   = rawText.LastIndexOf(']');
        if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart) return null;

        var jsonBlock = rawText[jsonStart..(jsonEnd + 1)];
        try
        {
            return JsonSerializer.Deserialize<List<OllamaStoryItem>>(jsonBlock, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch { return null; }
    }


    /// <summary>Story mặc định khi chức năng không có yêu cầu chi tiết</summary>
    private static StoryPlanNode BuildDefaultStory(FunctionExtractDto func, DocumentExtractDto extract)
    {
        return new StoryPlanNode
        {
            Title          = $"Triển khai {func.FunctionName}",
            Description    = $"Triển khai toàn bộ chức năng: {func.FunctionName}",
            EstimatedHours = func.EstimatedHours,
            StartDate      = func.StartDate ?? extract.StartDate,
            DueDate        = func.Deadline  ?? extract.EndDate,
            SkillTags      = new List<string>()
        };
    }

    // ================================================================
    // TASK NODES (tầng 4)
    // ================================================================

    /// <summary>
    /// Sinh 4 Task implementation cố định cho mỗi Story: BE, FE, QA, DevOps.
    /// Giờ phân bổ theo weight (BE 40%, FE 30%, QA 20%, DevOps 10%).
    /// </summary>
    private static List<TaskPlanNode> BuildTaskNodes(StoryPlanNode story, DocumentExtractDto extract)
    {
        var tasks    = new List<TaskPlanNode>();
        var baseHrs  = story.EstimatedHours ?? 8; // 8h mặc định nếu không có estimate

        foreach (var (role, weight) in RoleHourWeight)
        {
            var taskHours = Math.Round(baseHrs * weight, 1);
            if (taskHours < 0.5m) taskHours = 0.5m; // tối thiểu 30 phút

            // Sinh SkillTags theo vai trò
            var skillTags = GetDefaultSkillTagsForRole(role, extract.TechStack);

            tasks.Add(new TaskPlanNode
            {
                Title          = $"[{role}] {story.Title}",
                Description    = $"[{role}] Triển khai: {story.Description}",
                StartDate      = story.StartDate,
                DueDate        = story.DueDate,
                EstimatedHours = taskHours,
                RoleTag        = role,
                SkillTags      = skillTags
            });
        }

        return tasks;
    }

    /// <summary>Gán SkillTags mặc định theo role và TechStack</summary>
    private static List<string> GetDefaultSkillTagsForRole(string role, List<string> techStack)
    {
        return role switch
        {
            // BE: ưu tiên backend tech
            "BE" => techStack
                .Where(t => ContainsCaseInsensitive(t,
                    "ASP.NET", ".NET", "C#", "Java", "Spring", "Node", "Python", "SQL", "EF", "Entity"))
                .Select(t => t.Split(',', '(')[0].Trim())
                .Take(3)
                .DefaultIfEmpty("Backend Development")
                .ToList(),

            // FE: ưu tiên frontend tech
            "FE" => techStack
                .Where(t => ContainsCaseInsensitive(t,
                    "React", "Angular", "Vue", "TypeScript", "JavaScript", "CSS", "HTML", "Tailwind"))
                .Select(t => t.Split(',', '(')[0].Trim())
                .Take(3)
                .DefaultIfEmpty("Frontend Development")
                .ToList(),

            // DevOps: ưu tiên infra tech
            "DevOps" => techStack
                .Where(t => ContainsCaseInsensitive(t,
                    "Docker", "Kubernetes", "CI/CD", "GitHub", "Azure", "AWS", "Jenkins", "Nginx"))
                .Select(t => t.Split(',', '(')[0].Trim())
                .Take(3)
                .DefaultIfEmpty("DevOps")
                .ToList(),

            // QA: không phụ thuộc tech stack cụ thể
            "QA" => new List<string> { "Software Testing", "QA" },

            _ => new List<string>()
        };
    }

    /// <summary>Kiểm tra string có chứa bất kỳ keyword nào (case-insensitive)</summary>
    private static bool ContainsCaseInsensitive(string text, params string[] keywords)
        => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
}
