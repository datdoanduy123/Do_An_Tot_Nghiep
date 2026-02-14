using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocTask.Core.Interfaces.Services;
using static DocTask.Core.Dtos.Gemini.AiProviderDto;

namespace DocTask.Service.Services.Providers
{
    /// <summary>
    /// Rule-based fallback provider (regex + template).
    /// Mục tiêu: luôn parse ra MODULE -> sinh subtasks theo MODULE, kể cả khi nội dung DOCX bị dính dòng.
    /// Nếu không tìm thấy MODULE hợp lệ -> fallback 5 phases mặc định.
    /// </summary>
    public class RuleBasedFallbackProvider : IAiProvider
    {
        public string ProviderName => "RuleBased";
        public Task<bool> IsAvailableAsync() => Task.FromResult(true);

        public Task<AiCompletionResponse> CompleteAsync(AiCompletionRequest request)
        {
            Console.WriteLine("⚙️ [RuleBased] Sinh task bằng rule-based fallback");

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
            content = NormalizeForParsing(content); // <-- quan trọng: chống dính dòng

            var title = ExtractTitle(content);
            var description = ExtractOverviewDescription(content);
            var (startDate, endDate) = ExtractTimeline(content);

            var skills = ExtractSkillsFromTechStackAndNFR(content);
            var modules = ExtractModulesRobust(content);

            string taskJson;
            if (modules.Any())
            {
                Console.WriteLine($"✅ [RuleBased] Parse được {modules.Count} MODULE -> sinh subtasks theo MODULE.");
                taskJson = BuildModuleBasedTaskJson(title, description, startDate, endDate, skills, modules);
            }
            else
            {
                Console.WriteLine($"⚠️ [RuleBased] Không parse được MODULE -> fallback 5 phases.");
                taskJson = BuildDefaultTaskJson(title, description, startDate, endDate, skills);
            }

            return Task.FromResult(new AiCompletionResponse
            {
                Text = taskJson,
                ProviderUsed = ProviderName,
                Success = true
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

            // normalize newline
            clean = clean.Replace("\r\n", "\n").Replace("\r", "\n");

            // normalize bullets to "-"
            clean = clean.Replace("•", "- ").Replace("●", "- ").Replace("▪", "- ");

            return clean.Trim();
        }

        /// <summary>
        /// DOCX thường bị "dính dòng": "MODULE: A Mô tả: ... Chức năng: ...".
        /// Hàm này chèn newline trước các keyword để việc parse ổn định.
        /// </summary>
        private static string NormalizeForParsing(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return "";

            var s = content;

            // Chèn newline trước các section title phổ biến nếu bị dính
            s = InsertNewLineBeforeKeyword(s, "THÔNG TIN DỰ ÁN");
            s = InsertNewLineBeforeKeyword(s, "MÔ TẢ TỔNG QUAN");
            s = InsertNewLineBeforeKeyword(s, "TECH STACK");
            s = InsertNewLineBeforeKeyword(s, "YÊU CẦU PHI CHỨC NĂNG");
            s = InsertNewLineBeforeKeyword(s, "DANH SÁCH MODULE");

            // Chèn newline trước các key field
            s = InsertNewLineBeforeKey(s, "Tên dự án");
            s = InsertNewLineBeforeKey(s, "Mã dự án");
            s = InsertNewLineBeforeKey(s, "Thời gian thực hiện");
            s = InsertNewLineBeforeKey(s, "Đơn vị thực hiện");
            s = InsertNewLineBeforeKey(s, "Mô hình triển khai");

            // Chèn newline trước cấu trúc module
            s = InsertNewLineBeforeKeywordRegex(s, @"\b(?:\d+(?:\.\d+)*\s*)?MODULE\b\s*:");
            s = InsertNewLineBeforeKeywordRegex(s, @"\bMô\s*tả\b\s*:");
            s = InsertNewLineBeforeKeywordRegex(s, @"\bChức\s*năng\b\s*:");
            s = InsertNewLineBeforeKeywordRegex(s, @"\bDữ\s*liệu\b\s*:");
            s = InsertNewLineBeforeKeywordRegex(s, @"\bRàng\s*buộc\b\s*:");
            s = InsertNewLineBeforeKeywordRegex(s, @"\bƯớc\s*lượng\b\s*:");

            // Dọn multiple blank lines
            s = Regex.Replace(s, @"\n{3,}", "\n\n");
            return s.Trim();
        }

        private static string InsertNewLineBeforeKeyword(string input, string keyword)
        {
            // chèn newline nếu keyword không ở đầu dòng
            return Regex.Replace(
                input,
                $@"(?i)(?<!\n)\s*({Regex.Escape(keyword)})",
                "\n$1");
        }

        private static string InsertNewLineBeforeKey(string input, string key)
        {
            return Regex.Replace(
                input,
                $@"(?i)(?<!\n)\s*({Regex.Escape(key)}\s*:)",
                "\n$1");
        }

        private static string InsertNewLineBeforeKeywordRegex(string input, string keywordRegex)
        {
            return Regex.Replace(
                input,
                $@"(?i)(?<!\n)\s*({keywordRegex})",
                "\n$1");
        }

        private static string NormalizeSpaces(string s)
            => Regex.Replace((s ?? "").Replace("\n", " ").Trim(), @"\s+", " ");

        // ================================================================
        // TITLE / DESCRIPTION / TIMELINE
        // ================================================================
        private static string ExtractTitle(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return "Dự án mới";

            var m = Regex.Match(
                content,
                @"(?i)Tên\s+dự\s+án\s*:\s*(.+?)(?=\n\s*(?:Mã\s+dự\s+án|Thời\s+gian|Đơn\s+vị|Mô\s+hình)\s*:|\n|$)");

            if (m.Success)
            {
                var t = NormalizeSpaces(m.Groups[1].Value);
                return t.Length > 120 ? t[..120] : t;
            }

            // fallback: lấy dòng đầu có chữ
            var firstLine = content.Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.Length >= 5);
            return string.IsNullOrWhiteSpace(firstLine) ? "Dự án mới" : (firstLine.Length > 120 ? firstLine[..120] : firstLine);
        }

        private static string ExtractOverviewDescription(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return "Dự án phát triển phần mềm";

            // ưu tiên lấy đoạn giới thiệu/phạm vi nếu có
            var gi = Regex.Match(content, @"(?is)Giới\s*thiệu\s*:\s*(.+?)(?=\n\s*Phạm\s*vi\s*:|\n\s*TECH\s*STACK|\n\s*YÊU\s*CẦU|\n\s*DANH\s*SÁCH\s*MODULE|\Z)");
            var pv = Regex.Match(content, @"(?is)Phạm\s*vi\s*:\s*(.+?)(?=\n\s*TECH\s*STACK|\n\s*YÊU\s*CẦU|\n\s*DANH\s*SÁCH\s*MODULE|\Z)");

            var sb = new StringBuilder();
            if (gi.Success) sb.Append("Giới thiệu: ").Append(NormalizeSpaces(gi.Groups[1].Value)).Append(" ");
            if (pv.Success) sb.Append("Phạm vi: ").Append(NormalizeSpaces(pv.Groups[1].Value));

            var d = NormalizeSpaces(sb.ToString());
            if (!string.IsNullOrWhiteSpace(d))
                return d.Length > 500 ? d[..500] + "..." : d;

            // fallback: lấy phần trước TECH STACK
            var idx = Regex.Match(content, @"(?im)^\s*TECH\s*STACK\b").Index;
            var head = idx > 0 ? content[..idx] : content;
            head = NormalizeSpaces(head);
            return head.Length > 500 ? head[..500] + "..." : (string.IsNullOrWhiteSpace(head) ? "Dự án phát triển phần mềm" : head);
        }

        private static (string startDate, string endDate) ExtractTimeline(string content)
        {
            var defaultStart = DateTime.Now.ToString("yyyy-MM-dd");
            var defaultEnd = DateTime.Now.AddDays(45).ToString("yyyy-MM-dd");

            if (string.IsNullOrWhiteSpace(content)) return (defaultStart, defaultEnd);

            // ưu tiên: "Thời gian thực hiện: dd/MM/yyyy - dd/MM/yyyy"
            var m = Regex.Match(
                content,
                @"(?i)Thời\s+gian\s+thực\s+hiện\s*:\s*(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})\s*[-–]\s*(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})");

            if (!m.Success)
            {
                // fallback: tìm range ngày bất kỳ
                m = Regex.Match(content, @"(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})\s*[-–]\s*(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})");
            }

            if (m.Success)
            {
                try
                {
                    var start = new DateTime(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[1].Value));
                    var end = new DateTime(int.Parse(m.Groups[6].Value), int.Parse(m.Groups[5].Value), int.Parse(m.Groups[4].Value));
                    if (start < end)
                        return (start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));
                }
                catch { /* ignore */ }
            }

            return (defaultStart, defaultEnd);
        }

        // ================================================================
        // SKILLS
        // ================================================================
        private static List<(string name, int level, int importance)> ExtractSkillsFromTechStackAndNFR(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return new List<(string, int, int)> { ("Software Development", 3, 3) };

            var upper = content.ToUpperInvariant();

            var skills = new List<(string name, int level, int importance)>();
            void Add(string name, int level, int importance)
            {
                if (skills.Any(x => x.name == name)) return;
                skills.Add((name, level, importance));
            }

            // Backend
            if (ContainsWord(upper, "ASP.NET") || ContainsWord(upper, ".NET CORE")) Add("ASP.NET Core", 4, 3);
            if (ContainsWord(upper, "C#") || ContainsWord(upper, "CSHARP")) Add("C# .NET", 4, 3);
            if (ContainsWord(upper, "NODEJS") || ContainsWord(upper, "NODE.JS")) Add("NodeJS", 4, 3);
            if (ContainsWord(upper, "JAVA") || ContainsWord(upper, "SPRING")) Add("Java/Spring", 4, 3);

            // Frontend
            if (ContainsWord(upper, "REACT")) Add("React", 4, 2);
            if (ContainsWord(upper, "ANGULAR")) Add("Angular", 4, 2);
            if (ContainsWord(upper, "VUE")) Add("Vue", 4, 2);

            // DB
            if (ContainsWord(upper, "SQL SERVER") || ContainsWord(upper, "MSSQL")) Add("SQL Server", 3, 2);
            if (ContainsWord(upper, "MYSQL")) Add("MySQL", 3, 2);
            if (ContainsWord(upper, "POSTGRES")) Add("PostgreSQL", 3, 2);
            if (ContainsWord(upper, "MONGODB")) Add("MongoDB", 3, 2);

            // Security
            if (ContainsWord(upper, "JWT") || ContainsWord(upper, "OAUTH2") || ContainsWord(upper, "BẢO MẬT") || ContainsWord(upper, "RBAC"))
                Add("Security", 3, 2);

            // DevOps
            if (ContainsWord(upper, "DOCKER") || ContainsWord(upper, "CI/CD") || ContainsWord(upper, "DEVOPS") || ContainsWord(upper, "GITHUB ACTIONS") || ContainsWord(upper, "AZURE"))
                Add("DevOps", 3, 2);

            // Testing
            if (ContainsWord(upper, "TESTING") || ContainsWord(upper, "UNIT TEST") || ContainsWord(upper, "INTEGRATION TEST") || ContainsWord(upper, "KIỂM THỬ"))
                Add("Software Testing", 3, 2);

            // Payment: strong keywords only
            if (ContainsWord(upper, "PAYMENT GATEWAY") || ContainsWord(upper, "CỔNG THANH TOÁN") || ContainsWord(upper, "VNPAY") || ContainsWord(upper, "MOMO") || ContainsWord(upper, "STRIPE") || ContainsWord(upper, "ZALOPAY"))
                Add("Payment Integration", 3, 2);

            if (skills.Count == 0)
            {
                Add("Software Development", 3, 3);
                Add("System Design", 3, 2);
            }

            return skills.Take(10).ToList();
        }

        private static bool ContainsWord(string textUpper, string wordUpper)
        {
            if (string.IsNullOrWhiteSpace(textUpper) || string.IsNullOrWhiteSpace(wordUpper)) return false;
            var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(wordUpper)}(?![\p{{L}}\p{{N}}])";
            return Regex.IsMatch(textUpper, pattern, RegexOptions.IgnoreCase);
        }

        // ================================================================
        // MODULES (ROBUST)
        // ================================================================
        private class ModuleInfo
        {
            public string Name { get; set; } = "";
            public string Desc { get; set; } = "";
            public List<string> Features { get; set; } = new();
            public List<string> DataFields { get; set; } = new();
            public List<string> Constraints { get; set; } = new();
            public int Hours { get; set; } = 0;
        }

        /// <summary>
        /// Parse module cực chắc:
        /// - tìm tất cả "MODULE:" trong toàn văn bản (không cần ở đầu dòng)
        /// - mỗi module kéo đến trước "MODULE:" tiếp theo hoặc EOF
        /// - name lấy đến trước "Mô tả:" hoặc xuống dòng đầu tiên
        /// </summary>
        private static List<ModuleInfo> ExtractModulesRobust(string content)
        {
            var modules = new List<ModuleInfo>();
            if (string.IsNullOrWhiteSpace(content)) return modules;

            var s = content.Replace("\r\n", "\n").Replace("\r", "\n");

            // Match mọi vị trí "MODULE:" (có thể có prefix số "4.2 MODULE:")
            var re = new Regex(@"(?is)(?:^|[\n\s])(?:(?:\d+(?:\.\d+)*)\s*)?MODULE\s*:\s*(?<rest>.+?)(?=(?:\n|$))",
                RegexOptions.IgnoreCase);

            // Cách chắc hơn: split theo token MODULE:
            // Giữ cả prefix số trước MODULE (nếu có) nhưng không cần.
            var split = Regex.Split(s, @"(?is)(?:(?:^|[\n\s])(?:(?:\d+(?:\.\d+)*)\s*)?MODULE\s*:\s*)");

            // split[0] là phần trước module đầu tiên
            if (split.Length <= 1) return modules;

            // Mỗi phần split[i] bắt đầu bằng "<name> ...."
            for (int i = 1; i < split.Length; i++)
            {
                var block = split[i].Trim();
                if (string.IsNullOrWhiteSpace(block)) continue;

                // Cắt block đến trước "MODULE:" tiếp theo đã được split sẵn nên OK.
                // Name: đến trước "Mô tả:" hoặc newline
                var nameMatch = Regex.Match(block, @"^(?<name>.+?)(?=\n|(?:Mô\s*tả|Description|Giới\s*thiệu)\s*:|$)", RegexOptions.IgnoreCase);
                var name = nameMatch.Success ? NormalizeSpaces(nameMatch.Groups["name"].Value) : $"Module {i}";

                // Body = phần sau tên (nếu tên nằm riêng 1 dòng thì body là các dòng tiếp)
                // Nếu tên và "Mô tả:" cùng dòng thì body vẫn chứa "Mô tả:" do normalize đã chèn newline trước "Mô tả:"
                var body = block;

                // Remove first line if it's exactly the name line (common case)
                var firstLine = block.Split('\n').FirstOrDefault() ?? "";
                if (NormalizeSpaces(firstLine).Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    body = string.Join("\n", block.Split('\n').Skip(1)).Trim();
                }

                var info = new ModuleInfo { Name = name };

                info.Desc = ExtractFieldText(body, new[] { "Mô tả", "Description", "Giới thiệu" });
                info.Features = ExtractBulletList(body, "Chức năng");
                info.DataFields = ExtractBulletList(body, "Dữ liệu");
                info.Constraints = ExtractBulletList(body, "Ràng buộc");
                info.Hours = ExtractHours(body);

                if (string.IsNullOrWhiteSpace(info.Desc))
                {
                    // fallback: lấy 1-2 câu đầu tiên trước "Chức năng/Dữ liệu/Ước lượng"
                    var head = Regex.Split(body, @"(?is)\n\s*(?:Chức\s*năng|Dữ\s*liệu|Ràng\s*buộc|Ước\s*lượng)\s*:", RegexOptions.IgnoreCase).FirstOrDefault();
                    info.Desc = NormalizeSpaces(head);
                }

                if (string.IsNullOrWhiteSpace(info.Desc)) info.Desc = info.Name;

                if (info.Hours <= 0)
                {
                    // heuristic nếu thiếu Ước lượng
                    info.Hours = 24 + (body.Length / 180) * 8;
                    if (info.Hours < 16) info.Hours = 16;
                    if (info.Hours > 200) info.Hours = 200;
                }

                info.Desc = BuildModuleDescription(info);
                modules.Add(info);
            }

            // lọc rác: nếu name quá ngắn và body rỗng
            modules = modules
                .Where(m => !string.IsNullOrWhiteSpace(m.Name) && m.Name.Length >= 3)
                .ToList();

            return modules;
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

            // block sau "Header:" đến trước header khác
            var m = Regex.Match(body,
                $@"(?is)\b{Regex.Escape(header)}\b\s*:\s*(.+?)(?=\n\s*(?:Mô\s*tả|Chức\s*năng|Dữ\s*liệu|Ràng\s*buộc|Ước\s*lượng)\s*:|\Z)",
                RegexOptions.IgnoreCase);

            if (!m.Success) return result;

            var list = m.Groups[1].Value.Replace("\r\n", "\n").Replace("\r", "\n").Trim();

            // bắt bullet "- xxx" hoặc "1. xxx" hoặc "1) xxx"
            foreach (Match b in Regex.Matches(list, @"(?m)^\s*(?:-\s*|\d+[.)]\s*)(.+?)\s*$"))
            {
                var v = NormalizeSpaces(b.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(v)) result.Add(v);
            }

            // nếu không có bullet mà vẫn có content: tách theo dấu chấm/phẩy (nhẹ)
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

            // Ước lượng: 80h / 80 giờ / 10 days / 10 ngày
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
                if (info.DataFields.Count > 5) sb.Append(", ...");
            }

            if (info.Constraints.Any())
            {
                sb.Append(" | Ràng buộc: ");
                sb.Append(string.Join(", ", info.Constraints.Take(3)));
                if (info.Constraints.Count > 3) sb.Append(", ...");
            }

            var s = NormalizeSpaces(sb.ToString());
            return s.Length > 600 ? s[..600] + "..." : s;
        }

        // ================================================================
        // BUILD JSON
        // ================================================================
        private static string BuildDefaultTaskJson(string title, string desc, string start, string end, List<(string name, int level, int importance)> skills)
        {
            DateTime.TryParse(start, out var s);
            DateTime.TryParse(end, out var e);
            var totalDays = (e - s).Days > 0 ? (e - s).Days : 45;

            var phases = new (string Name, double Ratio, int Hours)[]
            {
                ("Phân tích & Thiết kế", 0.20, 40),
                ("Phát triển Backend",   0.30, 80),
                ("Phát triển Frontend",  0.30, 80),
                ("Kiểm thử (Testing)",   0.10, 30),
                ("Triển khai & Bàn giao",0.10, 20),
            };

            var subtasks = new List<string>();
            var offset = 0;
            var totalHours = 0;

            for (int i = 0; i < phases.Length; i++)
            {
                var p = phases[i];
                var duration = Math.Max(2, (int)Math.Round(totalDays * p.Ratio));

                var pStart = s.AddDays(offset);
                var pEnd = pStart.AddDays(duration);
                offset += duration;

                if (pEnd > e) pEnd = e;
                if (i == phases.Length - 1) pEnd = e;

                totalHours += p.Hours;

                subtasks.Add($@"{{
  ""title"": ""{EscapeJson(p.Name)}"",
  ""description"": ""{EscapeJson(p.Name)} cho dự án"",
  ""startDate"": ""{pStart:yyyy-MM-dd}"",
  ""dueDate"": ""{pEnd:yyyy-MM-dd}"",
  ""estimatedHours"": {p.Hours},
  ""requiredSkills"": []
}}");
            }

            return WrapTaskJson(title, desc, start, end, totalHours, skills, subtasks);
        }

        private static string BuildModuleBasedTaskJson(
            string title,
            string desc,
            string start,
            string end,
            List<(string name, int level, int importance)> skills,
            List<ModuleInfo> modules)
        {
            DateTime.TryParse(start, out var s);
            DateTime.TryParse(end, out var e);

            var totalDays = (e - s).Days > 0 ? (e - s).Days : 45;

            var totalHours = modules.Sum(m => Math.Max(1, m.Hours));
            if (totalHours <= 0) totalHours = 1;

            var subtasks = new List<string>();
            var usedDays = 0;

            for (int i = 0; i < modules.Count; i++)
            {
                var m = modules[i];

                var ratio = (double)Math.Max(1, m.Hours) / totalHours;
                var duration = Math.Max(1, (int)Math.Round(totalDays * ratio));

                // module cuối ăn phần dư để chạm đúng endDate
                if (i == modules.Count - 1)
                    duration = Math.Max(1, totalDays - usedDays);

                var pStart = s.AddDays(usedDays);
                var pEnd = pStart.AddDays(duration);
                usedDays += duration;

                if (pEnd > e) pEnd = e;
                if (i == modules.Count - 1) pEnd = e;

                var subSkills = MapSkillsToModule(m, skills);
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

        private static List<(string name, int level, int importance)> MapSkillsToModule(ModuleInfo module, List<(string name, int level, int importance)> allSkills)
        {
            var result = new List<(string name, int level, int importance)>();
            var upperName = (module?.Name ?? "").ToUpperInvariant();
            var upperBody = (module?.Desc ?? "").ToUpperInvariant();

            void Add(string skillName, int defLevel = 3, int defImportance = 2)
            {
                var match = allSkills.FirstOrDefault(s => s.name == skillName);
                if (!string.IsNullOrWhiteSpace(match.name)) result.Add(match);
                else result.Add((skillName, defLevel, defImportance));
            }

            // Backend keywords
            if (upperName.Contains("API") || upperName.Contains("XỬ LÝ") || upperBody.Contains("API"))
            {
                Add("ASP.NET Core", 4, 3);
                Add("C# .NET", 4, 3);
            }

            // UI keywords
            if (upperName.Contains("GIAO DIỆN") || upperName.Contains("FRONTEND") || upperName.Contains("UI") || upperBody.Contains("DASHBOARD"))
            {
                Add("React", 4, 2);
            }

            // DB keywords
            if (upperName.Contains("CSDL") || upperName.Contains("DATABASE") || upperBody.Contains("DỮ LIỆU"))
            {
                Add("SQL Server", 3, 2);
            }

            // Security
            if (upperName.Contains("BẢO MẬT") || upperBody.Contains("JWT") || upperBody.Contains("RBAC") || upperBody.Contains("OAUTH"))
            {
                Add("Security", 3, 2);
            }

            // Testing
            if (upperBody.Contains("TEST") || upperBody.Contains("KIỂM THỬ"))
            {
                Add("Software Testing", 3, 2);
            }

            // DevOps (deploy/export/upload thường cần)
            if (upperBody.Contains("CI/CD") || upperBody.Contains("DOCKER") || upperBody.Contains("AZURE"))
            {
                Add("DevOps", 3, 2);
            }

            // Payment
            if (upperName.Contains("THANH TOÁN") || upperName.Contains("PAYMENT") || upperBody.Contains("VNPAY") || upperBody.Contains("STRIPE"))
            {
                Add("Payment Integration", 3, 2);
            }

            // nếu rỗng -> dùng bộ kỹ năng chung
            if (!result.Any())
            {
                // lấy 1-3 skill quan trọng nhất trong global skills
                result.AddRange(allSkills.Take(3));
            }

            return result
                .GroupBy(x => x.name)
                .Select(g => g.First())
                .ToList();
        }

        private static string WrapTaskJson(
            string title,
            string desc,
            string start,
            string end,
            int hours,
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