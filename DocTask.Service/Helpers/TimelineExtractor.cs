using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DocTask.Service.Helpers
{
    /// <summary>
    /// Helper class để extract timeline dự án từ nội dung file bằng regex (rule-based).
    /// 
    /// ⚠️ QUAN TRỌNG: LLM không thể tin cậy 100% cho constraint-critical data.
    /// Class này đảm bảo timeline được extract chính xác bằng rule-based system,
    /// không phụ thuộc vào AI.
    /// </summary>
    public static class TimelineExtractor
    {
        // Các keyword để tìm timeline trong văn bản
        private static readonly string[] TimelineKeywords = new[]
        {
            "Thời gian thực hiện",
            "Thời gian dự án",
            "Thời gian triển khai",
            "Timeline",
            "Duration",
            "Project timeline",
            "Thời gian",
            "Khoảng thời gian"
        };

        // Regex pattern để match date format dd/MM/yyyy (Vietnamese style)
        // Hỗ trợ các separator: - – — (hyphen, en-dash, em-dash)
        private static readonly Regex DateRangePattern = new Regex(
            @"(\d{1,2})[\/\-\.](\d{1,2})[\/\-\.](\d{4})\s*[-–—]\s*(\d{1,2})[\/\-\.](\d{1,2})[\/\-\.](\d{4})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        /// <summary>
        /// Extract project timeline từ file content.
        /// </summary>
        /// <param name="content">Nội dung file đã được parse</param>
        /// <returns>
        /// Tuple (startDate, endDate):
        /// - Nếu tìm thấy timeline hợp lệ → (DateTime, DateTime)
        /// - Nếu không tìm thấy hoặc invalid → (null, null)
        /// </returns>
        public static (DateTime? startDate, DateTime? endDate) ExtractProjectTimeline(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return (null, null);
            }

            // Bước 1: Tìm các keyword trong content
            foreach (var keyword in TimelineKeywords)
            {
                var keywordIndex = content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (keywordIndex == -1) continue;

                // Bước 2: Extract context gần keyword (200 ký tự sau keyword)
                var contextStart = keywordIndex;
                var contextLength = Math.Min(200, content.Length - contextStart);
                var contextWindow = content.Substring(contextStart, contextLength);

                // Bước 3: Tìm date range trong context window
                var match = DateRangePattern.Match(contextWindow);
                if (!match.Success) continue;

                // Bước 4: Parse dates
                try
                {
                    // Parse start date (group 1, 2, 3)
                    var startDay = int.Parse(match.Groups[1].Value);
                    var startMonth = int.Parse(match.Groups[2].Value);
                    var startYear = int.Parse(match.Groups[3].Value);
                    var startDate = new DateTime(startYear, startMonth, startDay);

                    // Parse end date (group 4, 5, 6)
                    var endDay = int.Parse(match.Groups[4].Value);
                    var endMonth = int.Parse(match.Groups[5].Value);
                    var endYear = int.Parse(match.Groups[6].Value);
                    var endDate = new DateTime(endYear, endMonth, endDay);

                    // Bước 5: Validate - startDate phải <= endDate
                    if (startDate > endDate)
                    {
                        Console.WriteLine($"[TimelineExtractor] Invalid date range: {startDate:dd/MM/yyyy} > {endDate:dd/MM/yyyy}");
                        continue; // Thử keyword tiếp theo
                    }

                    // Bước 6: Success - log và return
                    Console.WriteLine($"[TimelineExtractor] ✓ Found project timeline: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}");
                    return (startDate, endDate);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TimelineExtractor] Failed to parse dates from match: {match.Value} - {ex.Message}");
                    continue; // Thử keyword tiếp theo
                }
            }

            // Không tìm thấy timeline hợp lệ
            Console.WriteLine("[TimelineExtractor] ⚠ No valid project timeline found in content");
            return (null, null);
        }

        /// <summary>
        /// Helper method: Format timeline cho prompt injection.
        /// </summary>
        /// <param name="startDate">Start date</param>
        /// <param name="endDate">End date</param>
        /// <returns>Formatted string để inject vào prompt</returns>
        public static string FormatForPrompt(DateTime? startDate, DateTime? endDate)
        {
            if (startDate == null || endDate == null)
            {
                return string.Empty;
            }

            return $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";
        }
    }
}
