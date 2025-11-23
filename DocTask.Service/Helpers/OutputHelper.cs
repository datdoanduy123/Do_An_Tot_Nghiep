using System.Text.RegularExpressions;

namespace DocTask.Service.Helpers
{
    public static class OutputHelper
    {
        // chỉ dùng để sửa chuỗi json thuần (thiếu dấu, cắt giữa chừng)
        public static string FixJson(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return output;

            var cleaned = output
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            // Xóa ký tự điều khiển
            cleaned = Regex.Replace(cleaned, @"[\u0000-\u001F]+", "");

            // Vá lỗi JSON phổ biến
            cleaned = Regex.Replace(cleaned, @"\\(\s*?\r?\n)", ""); // bỏ escape thừa
            cleaned = Regex.Replace(cleaned, @"(^|[^{])\\\""(?![:,}\]])", "$1\""); // unescape lỗi

            // Đảm bảo JSON đóng đúng
            if (cleaned.StartsWith("{") && !cleaned.EndsWith("}"))
                cleaned += "}";

            return cleaned;
        }
    }
}
