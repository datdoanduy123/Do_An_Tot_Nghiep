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

            // 1. Extract Title
            // Match "Tên dự án: ..."
            var titleMatch = Regex.Match(s, @"(?i)(?:Tên\s+(?:dự\s+án|project)|Project\s*Name)\s*[:\.\-–]\s*(.+?)(?=\n|$)", RegexOptions.IgnoreCase);
            if (titleMatch.Success)
            {
                info.Title = NormalizeSpaces(titleMatch.Groups[1].Value);
            }

            // 2. Extract Overview Description
            // Search for "MÔ TẢ TỔNG QUAN" or "Giới thiệu:"
            // Simple heuristic: Text between "MÔ TẢ TỔNG QUAN" and "TECH STACK" or "DANH SÁCH MODULE" or "PHẠM VI"
            var descMatch = Regex.Match(s, 
                @"(?:MÔ\s*TẢ\s*TỔNG\s*QUAN|Giới\s*thiệu|Overview)\s*[:\.\-–]?\s*(.+?)(?=\n\s*(?:TECH\s*STACK|CÔNG\s*NGHỆ|DANH\s*SÁCH|PHẠM\s*VI|YÊU\s*CẦU|MODULE)|\Z)", 
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            if (descMatch.Success)
            {
                info.Description = NormalizeSpaces(descMatch.Groups[1].Value);
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

            // Split by "MODULE" headers
            // Supports: MODULE: | 1. MODULE: | I. MODULE
            var split = Regex.Split(s, @"(?is)(?:(?:^|[\n\s])(?:(?:\d+(?:\.\d+)*)\s*)?MODULE\s*:\s*)");

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
                
                // Parse Hours
                var h = Regex.Match(body, @"(?i)Ước\s*lượng\s*:\s*(\d{1,4})\s*(h|giờ|hours?)\b");
                if (h.Success && int.TryParse(h.Groups[1].Value, out var hv)) 
                {
                    info.Hours = hv;
                }

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
