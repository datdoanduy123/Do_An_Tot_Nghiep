using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocTask.Core.Dtos.Gemini;

namespace DocTask.Service.Helpers
{
    public static class ModuleExtractor
    {
        public class ProjectInfo
        {
            public string Title { get; set; } = "Dự án mới";
            public string Description { get; set; } = "";
            public List<string> TechStack { get; set; } = new();
            public List<ModuleInfo> Modules { get; set; } = new();
        }

        public class ModuleInfo
        {
            public string Name { get; set; } = "";
            public string Desc { get; set; } = "";
            public List<string> Features { get; set; } = new();
            public List<string> DataFields { get; set; } = new();
            public List<string> Constraints { get; set; } = new();
            public int Hours { get; set; } = 0;
        }

        /// <summary>
        /// Extract full project info from template.
        /// </summary>
        public static ProjectInfo ExtractProjectInfo(string content)
        {
            var info = new ProjectInfo();
            if (string.IsNullOrWhiteSpace(content)) return info;

            var s = content.Replace("\r\n", "\n").Replace("\r", "\n");

            // 1. Extract Title — dừng trước các metadata khác: Thời gian, Ngân sách, Client
            var titleMatch = Regex.Match(s,
                @"(?i)(?:Tên\s+(?:dự\s+án|project)|Project\s*Name)\s*[:\.\-–]\s*(.+?)(?=\r?\n|\s*(?:Thời\s*gian|Ngân\s*sách|Client|Budget)|$)",
                RegexOptions.IgnoreCase);
            if (titleMatch.Success)
            {
                info.Title = NormalizeSpaces(titleMatch.Groups[1].Value);
            }

            // 2. Extract Overview Description
            // Hỗ trợ format: "=====\nMÔ TẢ DỰ ÁN\n=====\n<text>" và "MÔ TẢ TỔNG QUAN: text"
            var descMatch = Regex.Match(s,
                @"(?:MÔ\s*TẢ\s*(?:DỰ\s*ÁN|TỔNG\s*QUAN)|Giới\s*thiệu|Overview|THÔNG\s*TIN\s*DỰ\s*ÁN)" +
                @"[:\s\-–]*(?:={3,})?[\r\n]+((?:(?!={3,})[\s\S])+?)" +
                @"(?=\s*={3,}|\s*(?:CÔNG\s*NGHỆ|TECH\s*STACK|CHỨC\s*NĂNG|MODULE|YÊU\s*CẦU|PHẠM\s*VI)|\Z)",
                RegexOptions.IgnoreCase);

            if (descMatch.Success)
            {
                // Lấy 2-3 câu đầu để có description ngắn gọn
                var rawDesc = descMatch.Groups[1].Value.Trim();
                // Loại bỏ dấu chấm cuối cùng nếu có
                var sentences = rawDesc.Replace("\r\n", " ").Replace("\n", " ")
                    .Split(new[] {'.'}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 5)
                    .Take(3)
                    .ToArray();
                info.Description = sentences.Length > 0
                    ? string.Join(". ", sentences).Trim() + "."
                    : NormalizeSpaces(rawDesc);
            }

            // 3. Extract Tech Stack
            // Search for "TECH STACK" or "CÔNG NGHỆ" or "MÔI TRƯỜNG"
            var techMatch = Regex.Match(s, 
                @"(?:TECH\s*STACK|CÔNG\s*NGHỆ|MÔI\s*TRƯỜNG)\s*[:\.\-–]?\s*(.+?)(?=\n\s*(?:DANH\s*SÁCH|PHẠM\s*VI|YÊU\s*CẦU|MODULE|MÔ\s*TẢ)|\Z)", 
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (techMatch.Success)
            {
                var rawTech = techMatch.Groups[1].Value;
                // Split by newline or comma or bullet points
                var techs = rawTech.Split(new[] { '\n', ',', '-', '•' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(t => t.Trim())
                                   .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length > 1)
                                   .Distinct()
                                   .ToList();
                info.TechStack = techs;
            }

            // 4. Extract Modules
            info.Modules = ExtractModules(s);

            return info;
        }

        /// <summary>
        /// Extract modules from robust template scanning.
        /// </summary>
        public static List<ModuleInfo> ExtractModules(string content)
        {
            var modules = new List<ModuleInfo>();
            if (string.IsNullOrWhiteSpace(content)) return modules;

            var s = content.Replace("\r\n", "\n").Replace("\r", "\n");

            // Split by module/section headers — hỗ trợ nhiều format phổ biến:
            // "MODULE: Tên"  |  "1. MODULE: Tên"  |  "CHỨC NĂNG 1: Tên"  |  "PHẦN 1: Tên"
            // Cũng nhận các format có === separator trước header:
            // "========\nCHỨC NĂNG 1: Tên"
            var split = Regex.Split(s,
                @"(?im)(?:={3,}\s*\n)?[ \t]*(?:(?:\d+(?:\.\d+)*|[IVXivx]+)\.?\s+)?(?:MODULE|CHỨC\s*NĂNG|PHẦN|PHASE|EPIC|FEATURE)\s*(?:\d+\s*)?[:.]\s*",
                RegexOptions.IgnoreCase);

            if (split.Length <= 1) return modules;

            for (int i = 1; i < split.Length; i++)
            {
                var block = split[i].Trim();
                if (string.IsNullOrWhiteSpace(block)) continue;

                // Extract Name (first line or until Description header)
                var nameMatch = Regex.Match(block, @"^(?<name>.+?)(?=\n|(?:Mô\s*tả|Description|Giới\s*thiệu)\s*:|$)", RegexOptions.IgnoreCase);
                var name = nameMatch.Success ? NormalizeSpaces(nameMatch.Groups["name"].Value) : $"Module {i}";

                var body = block;
                var firstLine = block.Split('\n').FirstOrDefault() ?? "";
                
                // If the first line was strictly the name, remove it from body
                if (NormalizeSpaces(firstLine).Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    body = string.Join("\n", block.Split('\n').Skip(1)).Trim();
                }

                var info = new ModuleInfo { Name = name };
                
                // Parse Description
                var descMatch = Regex.Match(body, 
                    @"(?:Mô\s*tả|Description|Giới\s*thiệu)\s*[:\.\-–]\s*(.+?)(?=\s*(?:Chức\s+năng|Dữ\s+liệu|Ràng\s+buộc|Ước\s*lượng)\s*[:\.\-–]|\n\n|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    
                if (descMatch.Success) 
                {
                    info.Desc = NormalizeSpaces(descMatch.Groups[1].Value);
                }
                else
                {
                    // Fallback: take text until next known section
                    var head = Regex.Split(body, @"(?is)\n\s*(?:Chức\s*năng|Dữ\s*liệu|Ràng\s*buộc|Ước\s*lượng)\s*:", RegexOptions.IgnoreCase).FirstOrDefault();
                    info.Desc = NormalizeSpaces(head);
                }
                
                // Parse Hours — hỗ trợ cả "Ước lượng" và "Ước tính"
                var h = Regex.Match(body, @"(?i)(?:Ước\s*lượng|Ước\s*tính|Estimated)\s*[:\-]?\s*(\d{1,5})\s*(?:h|giờ|hours?)");
                if (h.Success && int.TryParse(h.Groups[1].Value, out var hv))
                {
                    info.Hours = hv;
                }
                else
                {
                    // Fallback: nhận dạng "X giờ" đưứng riêng như "320 giờ"
                    var hFallback = Regex.Match(body, @"\b(\d{2,5})\s*giờ\b");
                    if (hFallback.Success && int.TryParse(hFallback.Groups[1].Value, out var hf))
                        info.Hours = hf;
                    else
                        info.Hours = 80; // mặc định nếu không tìm được
                }

                // 5. Parse Features (yêu cầu chi tiết: 1.1, 1.2, ... hoặc - item)
                // ⭐ Tìm block "Yêu cầu chi tiết:" — regex linh hoạt hơn (không yêu cầu \n ngay sau :)
                var featuresBlock = "";
                var featBlockMatch = Regex.Match(body,
                    @"(?:Yêu\s*cầu\s*chi\s*tiết|Chức\s*năng\s*chi\s*tiết|Chi\s*tiết)\s*[:\.]?\s*\r?\n([\s\S]+?)(?=\r?\n\s*(?:Thời\s*gian|Ước\s*tính|Ước\s*lượng|={3,})|$)",
                    RegexOptions.IgnoreCase);

                if (featBlockMatch.Success)
                {
                    featuresBlock = featBlockMatch.Groups[1].Value;
                    Console.WriteLine($"  ✅ [ModuleExtractor] Tìm thấy Yêu cầu chi tiết block cho '{name}', length={featuresBlock.Length}");
                }
                else
                {
                    // Fallback: chỉ scan phần sau dòng "Mô tả:" để tránh lấy nhầm mô tả làm feature
                    Console.WriteLine($"  ⚠️ [ModuleExtractor] Không tìm thấy block Yêu cầu chi tiết cho '{name}', fallback scan body");
                    featuresBlock = body;
                }

                // Match dạng "1.1 Tên feature" — chỉ nhận N.N (có dấu chấm kép) để tránh match số thứ tự đơn
                var featMatches = Regex.Matches(featuresBlock,
                    @"(?im)^[ \t]*\d+\.\d+\s+(.+?)(?=\r?\n|$)");
                var features = featMatches
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value.Trim())
                    .Where(f => !string.IsNullOrWhiteSpace(f) && f.Length > 3
                             // Loại bỏ dòng metadata không phải feature thực
                             && !Regex.IsMatch(f, @"(?i)^(thời\s*gian|ước\s*tính|ước\s*lượng|mô\s*tả|yêu\s*cầu|module|chức\s*năng|phần\s*\d)"))
                    .Distinct()
                    .ToList();

                // Nếu không tìm thấy N.N, thử tìm "- item" hoặc "• item" (chỉ khi có block Yêu cầu chi tiết)
                if (features.Count == 0 && featBlockMatch.Success)
                {
                    var bulletMatches = Regex.Matches(featuresBlock,
                        @"(?im)^[ \t]*[-•*]\s+(.+?)(?=\r?\n|$)");
                    features = bulletMatches
                        .Cast<Match>()
                        .Select(m => m.Groups[1].Value.Trim())
                        .Where(f => !string.IsNullOrWhiteSpace(f) && f.Length > 3)
                        .ToList();
                }

                Console.WriteLine($"  📋 [ModuleExtractor] Module '{name}': {features.Count} features tìm được");
                info.Features = features;

                modules.Add(info);

            }
            
            // Filter garbage
            return modules
                .Where(m => !string.IsNullOrWhiteSpace(m.Name) && m.Name.Length >= 3)
                .ToList();
        }

        private static string NormalizeSpaces(string? s)
            => Regex.Replace((s ?? "").Replace("\n", " ").Trim(), @"\s+", " ");
    }
}
