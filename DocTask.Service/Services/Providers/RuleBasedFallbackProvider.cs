using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using static DocTask.Core.Dtos.Gemini.AiProviderDto;
// Alias tường minh để tránh xung đột với DocTask.Core.Models.Task
using Task = System.Threading.Tasks.Task;
using TaskModel = DocTask.Core.Models.Task;

namespace DocTask.Service.Services.Providers
{
    /// <summary>
    /// Rule-based fallback provider: đọc rule từ DB (qua List&lt;AiTaskRule&gt; truyền vào constructor).
    /// Admin quản lý rule qua bảng ai_task_rule — không còn hardcode trong code.
    ///
    /// Nếu không tìm thấy MODULE trong document → fallback DefaultPhase từ DB.
    /// </summary>
    public class RuleBasedFallbackProvider : IAiProvider
    {
        public string ProviderName => "RuleBased";
        public System.Threading.Tasks.Task<bool> IsAvailableAsync() => System.Threading.Tasks.Task.FromResult(true);

        // Rules được load từ DB và inject qua constructor
        private readonly List<AiTaskRule> _skillKeywordRules;
        private readonly List<AiTaskRule> _defaultPhaseRules;
        private readonly List<AiTaskRule> _moduleKeywordSkillRules;

        /// <summary>
        /// Constructor nhận danh sách rule đã load từ DB.
        /// AiProviderFactory chịu trách nhiệm load rule từ IAiTaskRuleRepository.
        /// </summary>
        public RuleBasedFallbackProvider(List<AiTaskRule> allRules)
        {
            // Phân loại rule theo type để tra cứu nhanh
            _skillKeywordRules       = allRules.Where(r => r.RuleType == "SkillKeyword"       && r.IsActive).OrderBy(r => r.SortOrder).ToList();
            _defaultPhaseRules       = allRules.Where(r => r.RuleType == "DefaultPhase"       && r.IsActive).OrderBy(r => r.SortOrder).ToList();
            _moduleKeywordSkillRules = allRules.Where(r => r.RuleType == "ModuleKeywordSkill" && r.IsActive).OrderBy(r => r.SortOrder).ToList();
        }

        public System.Threading.Tasks.Task<AiCompletionResponse> CompleteAsync(AiCompletionRequest request)
        {
            Console.WriteLine("⚙️ [RuleBased] Sinh task bằng rule-based fallback (rules từ DB)");

            var rawContent = "";
            foreach (var msg in request.Messages ?? new List<AiMessage>())
            {
                if (string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase)
                    && (msg.Content?.Length ?? 0) > rawContent.Length)
                {
                    rawContent = msg.Content ?? "";
                }
            }

            var content = CleanContent(rawContent);
            content = NormalizeForParsing(content);

            var title       = ExtractTitle(content);
            var description = ExtractOverviewDescription(content);
            var (startDate, endDate) = ExtractTimeline(content);

            // Trích xuất skill dựa trên rules từ DB
            var skills  = ExtractSkillsFromRules(content);
            var modules = ExtractModulesRobust(content);

            string taskJson;
            if (modules.Any())
            {
                Console.WriteLine($"✅ [RuleBased] Parse được {modules.Count} MODULE → sinh subtasks theo MODULE.");
                taskJson = BuildModuleBasedTaskJson(title, description, startDate, endDate, skills, modules);
            }
            else
            {
                Console.WriteLine("⚠️ [RuleBased] Không parse được MODULE → fallback DefaultPhase từ DB.");
                taskJson = BuildDefaultTaskJson(title, description, startDate, endDate, skills);
            }

            return System.Threading.Tasks.Task.FromResult(new AiCompletionResponse
            {
                Text         = taskJson,
                ProviderUsed = ProviderName,
                Success      = true
            });
        }

        // ================================================================
        // CLEAN + NORMALIZE
        // ================================================================
        private static string CleanContent(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";

            var clean = raw;
            clean = Regex.Replace(clean, @"\[FILE\s+CONTENT\s+CHUNK\]\s*", "", RegexOptions.IgnoreCase);
            clean = Regex.Replace(clean, @"\[RETRY_ID:\s*[^\]]+\]\s*", "", RegexOptions.IgnoreCase);
            clean = Regex.Replace(clean, @"\[(?:SYSTEM|CONTEXT|PROMPT)[^\]]*\]\s*", "", RegexOptions.IgnoreCase);
            clean = clean.Replace("\r\n", "\n").Replace("\r", "\n");
            clean = clean.Replace("•", "- ").Replace("●", "- ").Replace("▪", "- ");
            return clean.Trim();
        }

        /// <summary>
        /// DOCX thường bị "dính dòng". Chèn newline trước các keyword để parse ổn định.
        /// </summary>
        private static string NormalizeForParsing(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return "";

            var s = content;
            s = InsertNewLineBeforeKeyword(s, "THÔNG TIN DỰ ÁN");
            s = InsertNewLineBeforeKeyword(s, "MÔ TẢ TỔNG QUAN");
            s = InsertNewLineBeforeKeyword(s, "TECH STACK");
            s = InsertNewLineBeforeKeyword(s, "YÊU CẦU PHI CHỨC NĂNG");
            s = InsertNewLineBeforeKeyword(s, "DANH SÁCH MODULE");

            s = InsertNewLineBeforeKey(s, "Tên dự án");
            s = InsertNewLineBeforeKey(s, "Mã dự án");
            s = InsertNewLineBeforeKey(s, "Thời gian thực hiện");
            s = InsertNewLineBeforeKey(s, "Đơn vị thực hiện");
            s = InsertNewLineBeforeKey(s, "Mô hình triển khai");

            s = InsertNewLineBeforeKeywordRegex(s, @"\b(?:\d+(?:\.\d+)*)?\s*MODULE\b\s*:");
            s = InsertNewLineBeforeKeywordRegex(s, @"\bMô\s*tả\b\s*:");
            s = InsertNewLineBeforeKeywordRegex(s, @"\bChức\s*năng\b\s*:");
            s = InsertNewLineBeforeKeywordRegex(s, @"\bDữ\s*liệu\b\s*:");
            s = InsertNewLineBeforeKeywordRegex(s, @"\bRàng\s*buộc\b\s*:");
            s = InsertNewLineBeforeKeywordRegex(s, @"\bƯớc\s*lượng\b\s*:");

            s = Regex.Replace(s, @"\n{3,}", "\n\n");
            return s.Trim();
        }

        private static string InsertNewLineBeforeKeyword(string input, string keyword)
            => Regex.Replace(input, $@"(?i)(?<!\n)\s*({Regex.Escape(keyword)})", "\n$1");

        private static string InsertNewLineBeforeKey(string input, string key)
            => Regex.Replace(input, $@"(?i)(?<!\n)\s*({Regex.Escape(key)}\s*:)", "\n$1");

        private static string InsertNewLineBeforeKeywordRegex(string input, string keywordRegex)
            => Regex.Replace(input, $@"(?i)(?<!\n)\s*({keywordRegex})", "\n$1");

        private static string NormalizeSpaces(string s)
            => Regex.Replace((s ?? "").Replace("\n", " ").Trim(), @"\s+", " ");

        // ================================================================
        // TITLE / DESCRIPTION / TIMELINE
        // ================================================================
        private static string ExtractTitle(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return "Dự án mới";

            var m = Regex.Match(content,
                @"(?i)Tên\s+dự\s+án\s*:\s*(.+?)(?=\n\s*(?:Mã\s+dự\s+án|Thời\s+gian|Đơn\s+vị|Mô\s+hình)\s*:|\n|$)");

            if (m.Success)
            {
                var t = NormalizeSpaces(m.Groups[1].Value);
                return t.Length > 120 ? t[..120] : t;
            }

            var firstLine = content.Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.Length >= 5);
            return string.IsNullOrWhiteSpace(firstLine)
                ? "Dự án mới"
                : (firstLine.Length > 120 ? firstLine[..120] : firstLine);
        }

        private static string ExtractOverviewDescription(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return "Dự án phát triển phần mềm";

            var gi = Regex.Match(content, @"(?is)Giới\s*thiệu\s*:\s*(.+?)(?=\n\s*Phạm\s*vi\s*:|\n\s*TECH\s*STACK|\n\s*YÊU\s*CẦU|\n\s*DANH\s*SÁCH\s*MODULE|\Z)");
            var pv = Regex.Match(content, @"(?is)Phạm\s*vi\s*:\s*(.+?)(?=\n\s*TECH\s*STACK|\n\s*YÊU\s*CẦU|\n\s*DANH\s*SÁCH\s*MODULE|\Z)");

            var sb = new StringBuilder();
            if (gi.Success) sb.Append("Giới thiệu: ").Append(NormalizeSpaces(gi.Groups[1].Value)).Append(" ");
            if (pv.Success) sb.Append("Phạm vi: ").Append(NormalizeSpaces(pv.Groups[1].Value));

            var d = NormalizeSpaces(sb.ToString());
            if (!string.IsNullOrWhiteSpace(d))
                return d.Length > 500 ? d[..500] + "..." : d;

            var idx = Regex.Match(content, @"(?im)^\s*TECH\s*STACK\b").Index;
            var head = idx > 0 ? content[..idx] : content;
            head = NormalizeSpaces(head);
            return head.Length > 500
                ? head[..500] + "..."
                : (string.IsNullOrWhiteSpace(head) ? "Dự án phát triển phần mềm" : head);
        }

        private static (string startDate, string endDate) ExtractTimeline(string content)
        {
            var defaultStart = DateTime.Now.ToString("yyyy-MM-dd");
            var defaultEnd   = DateTime.Now.AddDays(45).ToString("yyyy-MM-dd");

            if (string.IsNullOrWhiteSpace(content)) return (defaultStart, defaultEnd);

            var m = Regex.Match(content,
                @"(?i)Thời\s+gian\s+thực\s+hiện\s*:\s*(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})\s*[-–]\s*(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})");

            if (!m.Success)
                m = Regex.Match(content, @"(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})\s*[-–]\s*(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})");

            if (m.Success)
            {
                try
                {
                    var start = new DateTime(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[1].Value));
                    var end   = new DateTime(int.Parse(m.Groups[6].Value), int.Parse(m.Groups[5].Value), int.Parse(m.Groups[4].Value));
                    if (start < end) return (start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));
                }
                catch { /* ignore */ }
            }

            return (defaultStart, defaultEnd);
        }

        // ================================================================
        // SKILLS — đọc từ SkillKeyword rules trong DB thay vì hardcode
        // ================================================================
        /// <summary>
        /// Trích xuất skill từ nội dung dựa trên SkillKeyword rules trong DB.
        /// Mỗi rule có Keyword (upper) — nếu content chứa keyword → thêm skill tương ứng.
        /// </summary>
        private List<(string name, int level, int importance)> ExtractSkillsFromRules(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return new List<(string, int, int)> { ("Software Development", 3, 3) };

            var upper  = content.ToUpperInvariant();
            var skills = new List<(string name, int level, int importance)>();

            foreach (var rule in _skillKeywordRules)
            {
                if (string.IsNullOrWhiteSpace(rule.Keyword) || string.IsNullOrWhiteSpace(rule.SkillName)) continue;

                // Tránh trùng lặp skill theo tên
                if (skills.Any(s => s.name == rule.SkillName)) continue;

                if (ContainsWord(upper, rule.Keyword.ToUpperInvariant()))
                    skills.Add((rule.SkillName, rule.RequiredLevel ?? 3, rule.Importance ?? 2));
            }

            if (!skills.Any())
            {
                skills.Add(("Software Development", 3, 3));
                skills.Add(("System Design", 3, 2));
            }

            return skills.Take(10).ToList();
        }

        private static bool ContainsWord(string textUpper, string wordUpper)
        {
            if (string.IsNullOrWhiteSpace(textUpper) || string.IsNullOrWhiteSpace(wordUpper)) return false;
            var pattern = $@"(?<![\\p{{L}}\\p{{N}}]){Regex.Escape(wordUpper)}(?![\\p{{L}}\\p{{N}}])";
            return Regex.IsMatch(textUpper, pattern, RegexOptions.IgnoreCase);
        }

        // ================================================================
        // MODULES (ROBUST) — giữ nguyên logic parse
        // ================================================================
        private class ModuleInfo
        {
            public string Name         { get; set; } = "";
            public string Desc         { get; set; } = "";
            public List<string> Features    { get; set; } = new();
            public List<string> DataFields  { get; set; } = new();
            public List<string> Constraints { get; set; } = new();
            public int Hours { get; set; } = 0;
        }

        private static List<ModuleInfo> ExtractModulesRobust(string content)
        {
            var modules = new List<ModuleInfo>();
            if (string.IsNullOrWhiteSpace(content)) return modules;

            var s     = content.Replace("\r\n", "\n").Replace("\r", "\n");
            var split = Regex.Split(s, @"(?is)(?:(?:^|[\n\s])(?:(?:\d+(?:\.\d+)*)\s*)?MODULE\s*:\s*)");

            if (split.Length <= 1) return modules;

            for (int i = 1; i < split.Length; i++)
            {
                var block = split[i].Trim();
                if (string.IsNullOrWhiteSpace(block)) continue;

                var nameMatch = Regex.Match(block, @"^(?<name>.+?)(?=\n|(?:Mô\s*tả|Description|Giới\s*thiệu)\s*:|$)", RegexOptions.IgnoreCase);
                var name      = nameMatch.Success ? NormalizeSpaces(nameMatch.Groups["name"].Value) : $"Module {i}";

                var body      = block;
                var firstLine = block.Split('\n').FirstOrDefault() ?? "";
                if (NormalizeSpaces(firstLine).Equals(name, StringComparison.OrdinalIgnoreCase))
                    body = string.Join("\n", block.Split('\n').Skip(1)).Trim();

                var info        = new ModuleInfo { Name = name };
                info.Desc       = ExtractFieldText(body, new[] { "Mô tả", "Description", "Giới thiệu" });
                info.Features   = ExtractBulletList(body, "Chức năng");
                info.DataFields = ExtractBulletList(body, "Dữ liệu");
                info.Constraints= ExtractBulletList(body, "Ràng buộc");
                info.Hours      = ExtractHours(body);

                if (string.IsNullOrWhiteSpace(info.Desc))
                {
                    var head = Regex.Split(body, @"(?is)\n\s*(?:Chức\s*năng|Dữ\s*liệu|Ràng\s*buộc|Ước\s*lượng)\s*:", RegexOptions.IgnoreCase).FirstOrDefault();
                    info.Desc = NormalizeSpaces(head);
                }
                if (string.IsNullOrWhiteSpace(info.Desc)) info.Desc = info.Name;

                if (info.Hours <= 0)
                {
                    // Heuristic nếu không có trường "Ước lượng"
                    info.Hours = 24 + (body.Length / 180) * 8;
                    if (info.Hours < 16)  info.Hours = 16;
                    if (info.Hours > 200) info.Hours = 200;
                }

                info.Desc = BuildModuleDescription(info);
                modules.Add(info);
            }

            return modules
                .Where(m => !string.IsNullOrWhiteSpace(m.Name) && m.Name.Length >= 3)
                .ToList();
        }

        private static string ExtractFieldText(string body, string[] fieldNames)
        {
            if (string.IsNullOrWhiteSpace(body)) return "";
            foreach (var f in fieldNames)
            {
                var m = Regex.Match(body,
                    $@"(?is)\b{Regex.Escape(f)}\b\s*:\s*(.+?)(?=\n\s*(?:Chức\s*năng|Dữ\s*liệu|Ràng\s*buộc|Ước\s*lượng)\s*:|\n\s*\Z|\Z)",
                    RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var t = NormalizeSpaces(m.Groups[1].Value);
                    if (!string.IsNullOrWhiteSpace(t)) return t;
                }
            }
            return "";
        }

        private static List<string> ExtractBulletList(string body, string header)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(body)) return result;

            var m = Regex.Match(body,
                $@"(?is)\b{Regex.Escape(header)}\b\s*:\s*(.+?)(?=\n\s*(?:Mô\s*tả|Chức\s*năng|Dữ\s*liệu|Ràng\s*buộc|Ước\s*lượng)\s*:|\Z)",
                RegexOptions.IgnoreCase);
            if (!m.Success) return result;

            var list = m.Groups[1].Value.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
            foreach (Match b in Regex.Matches(list, @"(?m)^\s*(?:-\s*|\d+[.)]\s*)(.+?)\s*$"))
            {
                var v = NormalizeSpaces(b.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(v)) result.Add(v);
            }

            if (!result.Any())
            {
                var parts = list.Split(new[] { ";", "•" }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(NormalizeSpaces)
                                .Where(x => x.Length >= 3)
                                .ToList();
                result.AddRange(parts.Take(8));
            }

            return result;
        }

        private static int ExtractHours(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return 0;

            var h = Regex.Match(body, @"(?i)Ước\s*lượng\s*:\s*(\d{1,4})\s*(h|giờ|hours?)\b");
            if (h.Success && int.TryParse(h.Groups[1].Value, out var hv)) return hv;

            var d = Regex.Match(body, @"(?i)Ước\s*lượng\s*:\s*(\d{1,4})\s*(d|ngày|days?)\b");
            if (d.Success && int.TryParse(d.Groups[1].Value, out var dv)) return dv * 8;

            return 0;
        }

        private static string BuildModuleDescription(ModuleInfo info)
        {
            var sb = new StringBuilder();
            sb.Append(info.Desc);

            if (info.Features.Any())
            {
                sb.Append(" | Chức năng: ");
                sb.Append(string.Join(", ", info.Features.Take(5)));
                if (info.Features.Count > 5) sb.Append(", ...");
            }
            if (info.DataFields.Any())
            {
                sb.Append(" | Dữ liệu: ");
                sb.Append(string.Join(", ", info.DataFields.Take(5)));
            }
            if (info.Constraints.Any())
            {
                sb.Append(" | Ràng buộc: ");
                sb.Append(string.Join(", ", info.Constraints.Take(3)));
            }

            var s = NormalizeSpaces(sb.ToString());
            return s.Length > 600 ? s[..600] + "..." : s;
        }

        // ================================================================
        // BUILD JSON — sử dụng DefaultPhase và ModuleKeywordSkill rules từ DB
        // ================================================================

        /// <summary>
        /// Sinh phases mặc định từ DefaultPhase rules trong DB (thay vì hardcode 5 phases).
        /// </summary>
        private string BuildDefaultTaskJson(
            string title, string desc, string start, string end,
            List<(string name, int level, int importance)> skills)
        {
            DateTime.TryParse(start, out var s);
            DateTime.TryParse(end,   out var e);
            var totalDays = (e - s).Days > 0 ? (e - s).Days : 45;

            var subtasks   = new List<string>();
            var offset     = 0;
            var totalHours = 0;

            for (int i = 0; i < _defaultPhaseRules.Count; i++)
            {
                var phase     = _defaultPhaseRules[i];
                var ratio     = phase.PhaseRatio ?? 0.20;
                var hours     = phase.PhaseHours ?? 40;
                var phaseName = phase.PhaseName  ?? $"Phase {i + 1}";

                var duration  = Math.Max(2, (int)Math.Round(totalDays * ratio));
                var pStart    = s.AddDays(offset);
                var pEnd      = pStart.AddDays(duration);
                offset       += duration;

                if (pEnd > e) pEnd = e;
                if (i == _defaultPhaseRules.Count - 1) pEnd = e;

                totalHours += hours;

                subtasks.Add($@"{{
  ""title"": ""{EscapeJson(phaseName)}"",
  ""description"": ""{EscapeJson(phaseName)} cho dự án"",
  ""startDate"": ""{pStart:yyyy-MM-dd}"",
  ""dueDate"": ""{pEnd:yyyy-MM-dd}"",
  ""estimatedHours"": {hours},
  ""requiredSkills"": []
}}");
            }

            return WrapTaskJson(title, desc, start, end, totalHours, skills, subtasks);
        }

        /// <summary>
        /// Sinh subtasks từ modules đã parse, map skill theo ModuleKeywordSkill rules từ DB.
        /// </summary>
        private string BuildModuleBasedTaskJson(
            string title, string desc, string start, string end,
            List<(string name, int level, int importance)> skills,
            List<ModuleInfo> modules)
        {
            DateTime.TryParse(start, out var s);
            DateTime.TryParse(end,   out var e);

            var totalDays  = (e - s).Days > 0 ? (e - s).Days : 45;
            var totalHours = modules.Sum(m => Math.Max(1, m.Hours));
            if (totalHours <= 0) totalHours = 1;

            var subtasks = new List<string>();
            var usedDays = 0;

            for (int i = 0; i < modules.Count; i++)
            {
                var m        = modules[i];
                var ratio    = (double)Math.Max(1, m.Hours) / totalHours;
                var duration = Math.Max(1, (int)Math.Round(totalDays * ratio));

                // Module cuối ăn phần dư để chạm đúng endDate
                if (i == modules.Count - 1)
                    duration = Math.Max(1, totalDays - usedDays);

                var pStart   = s.AddDays(usedDays);
                var pEnd     = pStart.AddDays(duration);
                usedDays    += duration;

                if (pEnd > e) pEnd = e;
                if (i == modules.Count - 1) pEnd = e;

                // Map skill từ ModuleKeywordSkill rules trong DB
                var subSkills  = MapSkillsToModuleFromRules(m, skills);
                var skillsJson = string.Join(",",
                    subSkills.Select(sk => $@"{{ ""skillName"": ""{EscapeJson(sk.name)}"", ""requiredLevel"": {sk.level}, ""importance"": {sk.importance} }}"));

                subtasks.Add($@"{{
  ""title"": ""{EscapeJson(m.Name)}"",
  ""description"": ""{EscapeJson(m.Desc)}"",
  ""startDate"": ""{pStart:yyyy-MM-dd}"",
  ""dueDate"": ""{pEnd:yyyy-MM-dd}"",
  ""estimatedHours"": {m.Hours},
  ""requiredSkills"": [{skillsJson}]
}}");
            }

            return WrapTaskJson(title, desc, start, end, totalHours, skills, subtasks);
        }

        /// <summary>
        /// Map skill cho module dựa trên ModuleKeywordSkill rules từ DB.
        /// Nếu tên/mô tả module chứa keyword → thêm skill tương ứng.
        /// </summary>
        private List<(string name, int level, int importance)> MapSkillsToModuleFromRules(
            ModuleInfo module,
            List<(string name, int level, int importance)> allSkills)
        {
            var result   = new List<(string name, int level, int importance)>();
            var combined = $"{(module?.Name ?? "").ToUpperInvariant()} {(module?.Desc ?? "").ToUpperInvariant()}";

            foreach (var rule in _moduleKeywordSkillRules)
            {
                if (string.IsNullOrWhiteSpace(rule.Keyword) || string.IsNullOrWhiteSpace(rule.SkillName)) continue;
                if (result.Any(r => r.name == rule.SkillName)) continue;

                if (combined.Contains(rule.Keyword.ToUpperInvariant()))
                {
                    // Ưu tiên lấy level/importance từ global skills nếu có
                    var match = allSkills.FirstOrDefault(sk => sk.name == rule.SkillName);
                    result.Add(!string.IsNullOrWhiteSpace(match.name)
                        ? match
                        : (rule.SkillName, rule.RequiredLevel ?? 3, rule.Importance ?? 2));
                }
            }

            // Fallback nếu không có rule nào match: lấy 1-3 skill quan trọng nhất
            if (!result.Any())
                result.AddRange(allSkills.Take(3));

            return result
                .GroupBy(x => x.name)
                .Select(g => g.First())
                .ToList();
        }

        private static string WrapTaskJson(
            string title, string desc, string start, string end, int hours,
            List<(string name, int level, int importance)> skills,
            List<string> subtasks)
        {
            var skillsJson = string.Join(",\n",
                skills.Select(k => $@"{{ ""skillName"": ""{EscapeJson(k.name)}"", ""requiredLevel"": {k.level}, ""importance"": {k.importance} }}"));
            var subJson = string.Join(",\n", subtasks);

            return $@"{{
  ""title"": ""{EscapeJson(title)}"",
  ""description"": ""{EscapeJson(desc)}"",
  ""startDate"": ""{start}"",
  ""endDate"": ""{end}"",
  ""estimatedHours"": {hours},
  ""requiredSkills"": [{skillsJson}],
  ""subtasks"": [{subJson}]
}}";
        }

        private static string EscapeJson(string s)
            => (s ?? "")
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", " ")
                .Replace("\r", " ");
    }
}