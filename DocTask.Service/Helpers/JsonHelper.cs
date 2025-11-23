using System;
using System.Text.Json;

namespace DocTask.Service.Helpers
{
    public static class JsonHelper
    {
        // Cố gắng parse chuỗi JSON, nếu lỗi thì trả null hoặc chuỗi gốc.
        public static object TryParseJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var clean = input.Trim();

            if (clean.StartsWith("{") || clean.StartsWith("["))
            {
                try
                {
                    using var parsed = JsonDocument.Parse(clean);
                    return parsed.RootElement.Clone();
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"JSON parse error: {ex.Message}");
                }
            }

            try
            {
                var unescaped = JsonSerializer.Deserialize<string>(clean);
                
                if (!string.IsNullOrEmpty(unescaped) &&
                    (unescaped.TrimStart().StartsWith("{") || unescaped.TrimStart().StartsWith("[")))
                {
                    using var parsed = JsonDocument.Parse(unescaped);
                    return parsed.RootElement.Clone();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unescape JSON failed: {ex.Message}");
            }

            // Trường hợp JSON bị cắt (Gemini hết token)
            var fixedJson = TryFixTruncatedJson(clean);

            if (fixedJson != null)
            {
                try
                {
                    using var parsed = JsonDocument.Parse(fixedJson);
                    return parsed.RootElement.Clone();
                }
                catch { /* ignore */ }
            }

            //Không phải JSON
            return clean;
        }

        // Thử tự động thêm dấu đóng khi JSON bị cắt giữa chừng.
        private static string? TryFixTruncatedJson(string raw)
        {
            try
            {
                int openCurly = raw.Count(c => c == '{');
                int closeCurly = raw.Count(c => c == '}');
                int openSquare = raw.Count(c => c == '[');
                int closeSquare = raw.Count(c => c == ']');

                while (closeCurly < openCurly)
                {
                    raw += "}";
                    closeCurly++;
                }

                while (closeSquare < openSquare)
                {
                    raw += "]";
                    closeSquare++;
                }

                return raw;
            }
            catch
            {
                return null;
            }
        }
    }
}
