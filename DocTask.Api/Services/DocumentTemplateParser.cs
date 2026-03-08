using System.Text.RegularExpressions;
using DocTask.Core.Dtos.AiGeneration;

namespace DocTask.Api.Services;

/// <summary>
/// Parser deterministic — bóc tách nội dung file tài liệu yêu cầu theo template chuẩn.
/// Hoạt động hoàn toàn KHÔNG phụ thuộc AI/Ollama, dễ debug, không bị LLM "bịa".
///
/// Template chuẩn nhận vào (theo SampleProject.txt):
///   Tên dự án: ...
///   Thời gian: DD/MM/YYYY - DD/MM/YYYY
///   Ngân sách: ...
///   Công nghệ sử dụng: (list gạch đầu dòng)
///   CHỨC NĂNG X: TÊN CHỨC NĂNG
///     Mô tả: ...
///     Yêu cầu chi tiết:
///       X.1 ...
///       X.2 ...
///     Thời gian: ...
///     Ước tính: XXX giờ
///   YÊU CẦU PHI CHỨC NĂNG
///     - item1
///     - item2
/// </summary>
public class DocumentTemplateParser
{
    // ================================================================
    // ENTRY POINT
    // ================================================================

    /// <summary>
    /// Parse rawText từ file tài liệu thành DocumentExtractDto có cấu trúc.
    /// </summary>
    /// <param name="rawText">Nội dung thuần text của file upload</param>
    /// <param name="sourceFileName">Tên file gốc (dùng cho metadata)</param>
    public DocumentExtractDto Parse(string rawText, string? sourceFileName = null)
    {
        // Chuẩn hóa line endings
        var content = rawText
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Trim();

        var dto = new DocumentExtractDto
        {
            SourceFileName = sourceFileName
        };

        // Bóc lần lượt theo thứ tự ưu tiên
        dto.ProjectName         = ExtractProjectName(content, dto.ParseWarnings);
        (dto.StartDate, dto.EndDate) = ExtractProjectTimeline(content, dto.ParseWarnings);
        dto.Budget              = ExtractBudget(content);
        dto.ProjectDescription  = ExtractProjectDescription(content);
        dto.TechStack           = ExtractTechStack(content);
        dto.Functions           = ExtractFunctions(content, dto.ParseWarnings);
        dto.NonFunctionalRequirements = ExtractNFR(content);

        return dto;
    }

    // ================================================================
    // THÔNG TIN CƠ BẢN DỰ ÁN
    // ================================================================

    /// <summary>Parse "Tên dự án: ..."</summary>
    private static string ExtractProjectName(string content, List<string> warnings)
    {
        var m = Regex.Match(content,
            @"(?i)Tên\s+dự\s+án\s*:\s*(.+?)(?=\n|$)",
            RegexOptions.Singleline);

        if (m.Success)
        {
            var name = NormalizeSpaces(m.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }

        warnings.Add("Không tìm thấy 'Tên dự án' trong tài liệu — dùng giá trị mặc định.");
        return "Dự án mới";
    }

    /// <summary>Parse "Thời gian: DD/MM/YYYY - DD/MM/YYYY" ở cấp dự án</summary>
    private static (DateTime? start, DateTime? end) ExtractProjectTimeline(string content, List<string> warnings)
    {
        // Tìm dòng chứa "Thời gian:" không nằm trong block CHỨC NĂNG
        var m = Regex.Match(content,
            @"(?i)^Thời\s*gian\s*:\s*(\d{1,2})[/\-](\d{1,2})[/\-](\d{4})\s*[-–]\s*(\d{1,2})[/\-](\d{1,2})[/\-](\d{4})",
            RegexOptions.Multiline);

        if (!m.Success)
        {
            // Thử lấy bất kỳ "Thời gian:" đầu tiên
            m = Regex.Match(content,
                @"(?i)Thời\s*gian\s*:\s*(\d{1,2})[/\-](\d{1,2})[/\-](\d{4})\s*[-–]\s*(\d{1,2})[/\-](\d{1,2})[/\-](\d{4})");
        }

        if (m.Success)
        {
            try
            {
                var s = ParseDate(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
                var e = ParseDate(m.Groups[4].Value, m.Groups[5].Value, m.Groups[6].Value);
                if (s < e) return (s, e);
            }
            catch { /* fall through */ }
        }

        warnings.Add("Không parse được thời gian dự án — dùng ±6 tháng từ hôm nay.");
        var now = DateTime.Now;
        return (now, now.AddMonths(6));
    }

    /// <summary>Parse "Ngân sách: ..."</summary>
    private static string? ExtractBudget(string content)
    {
        var m = Regex.Match(content, @"(?i)Ngân\s*sách\s*:\s*(.+?)(?=\n|$)");
        return m.Success ? NormalizeSpaces(m.Groups[1].Value) : null;
    }

    /// <summary>Parse phần "MÔ TẢ DỰ ÁN" hoặc đoạn mô tả đầu tài liệu</summary>
    private static string? ExtractProjectDescription(string content)
    {
        // Thử lấy từ phần "MÔ TẢ DỰ ÁN" hoặc "MÔ TẢ TỔNG QUAN"
        var m = Regex.Match(content,
            @"(?is)MÔ\s*TẢ\s*(DỰ\s*ÁN|TỔNG\s*QUAN)\s*={0,50}\s*\n(.+?)(?=={4,}|CHỨC\s*NĂNG\s*\d|YÊU\s*CẦU\s*PHI|\Z)");

        if (m.Success)
        {
            var desc = NormalizeSpaces(m.Groups[2].Value);
            if (!string.IsNullOrWhiteSpace(desc))
                return desc.Length > 800 ? desc[..800] + "..." : desc;
        }

        return null;
    }

    // ================================================================
    // TECH STACK
    // ================================================================

    /// <summary>
    /// Parse phần "Công nghệ sử dụng:" — lấy từng dòng "- xxx"
    /// </summary>
    private static List<string> ExtractTechStack(string content)
    {
        var result = new List<string>();

        // Tìm block bắt đầu bằng "Công nghệ sử dụng:" cho đến khi gặp dòng trống hoặc section mới
        var m = Regex.Match(content,
            @"(?is)Công\s*nghệ\s*sử\s*dụng\s*:\s*\n((?:\s*[-–•]\s*.+\n?)+)");

        if (!m.Success)
        {
            // Fallback: "Tech stack:" / "Technology:"
            m = Regex.Match(content,
                @"(?is)(?:Tech\s*[Ss]tack|Technology)\s*:\s*\n((?:\s*[-–•]\s*.+\n?)+)");
        }

        if (m.Success)
        {
            var block = m.Groups[1].Value;
            foreach (Match item in Regex.Matches(block, @"(?m)^\s*[-–•]\s*(.+?)\s*$"))
            {
                var tech = NormalizeSpaces(item.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(tech) && tech.Length >= 2)
                    result.Add(tech);
            }
        }

        return result;
    }

    // ================================================================
    // CHỨC NĂNG (EPIC)
    // ================================================================

    /// <summary>
    /// Tách tài liệu thành các block theo "CHỨC NĂNG X: ..." rồi parse từng block.
    /// Mỗi block tương ứng 1 Epic trong Agile plan.
    /// </summary>
    private static List<FunctionExtractDto> ExtractFunctions(string content, List<string> warnings)
    {
        var functions = new List<FunctionExtractDto>();

        // Pattern tìm block: "====...====\nCHỨC NĂNG X: tên\n====...====\n...body..."
        // hoặc đơn giản hơn: "CHỨC NĂNG X: tên\n...body..."
        var blocks = Regex.Split(content,
            @"(?mi)^(?:={4,}\s*\n)?CHỨC\s*NĂNG\s+(\d+)\s*:\s*(.+?)(?:\s*\n={4,})?\s*\n");

        // Regex split tạo ra: [trước_block1, funcNum1, funcName1, body1, funcNum2, funcName2, body2 ...]
        // Nên ta cần parse theo 3 items / step
        if (blocks.Length <= 1)
        {
            // Thử regex match thay vì split
            return ExtractFunctionsByMatch(content, warnings);
        }

        // Parse theo kết quả split (index 0 = phần đầu, sau đó nhóm 3)
        // blocks[0] = text trước CHỨC NĂNG đầu tiên
        // blocks[1] = số (1), blocks[2] = tên, blocks[3] = body
        // blocks[4] = số (2), blocks[5] = tên, blocks[6] = body ...
        for (int i = 1; i + 2 < blocks.Length; i += 3)
        {
            if (!int.TryParse(blocks[i].Trim(), out int num)) continue;

            var funcName = NormalizeSpaces(blocks[i + 1]);
            var body     = blocks[i + 2];

            var func = ParseFunctionBlock(num, funcName, body, warnings);
            if (func != null) functions.Add(func);
        }

        if (!functions.Any())
        {
            warnings.Add("Không tìm thấy 'CHỨC NĂNG X:' trong tài liệu — danh sách chức năng rỗng.");
        }

        return functions;
    }

    /// <summary>Fallback: dùng Matches thay vì Split khi Split không hoạt động</summary>
    private static List<FunctionExtractDto> ExtractFunctionsByMatch(string content, List<string> warnings)
    {
        var functions = new List<FunctionExtractDto>();

        // Tìm vị trí bắt đầu của từng "CHỨC NĂNG X:"
        var headers = Regex.Matches(content,
            @"(?mi)^(?:=+\s*\n)?CHỨC\s*NĂNG\s+(\d+)\s*:\s*(.+?)(?:\s*=+)?\s*$");

        for (int i = 0; i < headers.Count; i++)
        {
            if (!int.TryParse(headers[i].Groups[1].Value, out int num)) continue;

            var funcName = NormalizeSpaces(headers[i].Groups[2].Value);

            // Body: từ sau header đến header tiếp hoặc YÊU CẦU PHI CHỨC NĂNG
            var bodyStart = headers[i].Index + headers[i].Length;
            var bodyEnd   = i + 1 < headers.Count
                ? headers[i + 1].Index
                : FindNFRStart(content, bodyStart);

            var body = content[bodyStart..bodyEnd].Trim();
            var func = ParseFunctionBlock(num, funcName, body, warnings);
            if (func != null) functions.Add(func);
        }

        if (!functions.Any())
            warnings.Add("Không tìm thấy 'CHỨC NĂNG X:' trong tài liệu.");

        return functions;
    }

    /// <summary>Tìm vị trí bắt đầu của phần NFR</summary>
    private static int FindNFRStart(string content, int fromIndex)
    {
        var m = Regex.Match(content[fromIndex..],
            @"(?mi)^YÊU\s*CẦU\s*PHI\s*CHỨC\s*NĂNG");
        return m.Success ? fromIndex + m.Index : content.Length;
    }

    /// <summary>Parse body của 1 block CHỨC NĂNG thành FunctionExtractDto</summary>
    private static FunctionExtractDto? ParseFunctionBlock(int num, string funcName, string body, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(funcName)) return null;

        var dto = new FunctionExtractDto
        {
            FunctionNumber = num,
            FunctionName   = funcName
        };

        // Mô tả
        var descMatch = Regex.Match(body, @"(?i)Mô\s*tả\s*:\s*(.+?)(?=\nYêu\s*cầu|\nThời\s*gian|\nƯớc|\z)",
            RegexOptions.Singleline);
        dto.Description = descMatch.Success
            ? NormalizeSpaces(descMatch.Groups[1].Value)
            : null;

        // Yêu cầu chi tiết: dòng "X.Y ..."
        var details = new List<string>();
        var detailPattern = $@"(?m)^\s*{num}\.(\d+)\s+(.+?)\s*$";
        foreach (Match dm in Regex.Matches(body, detailPattern))
        {
            var detail = NormalizeSpaces(dm.Groups[2].Value);
            if (!string.IsNullOrWhiteSpace(detail)) details.Add(detail);
        }

        // Nếu không có "X.Y" thì lấy các gạch đầu dòng trong "Yêu cầu chi tiết:"
        if (!details.Any())
        {
            var reqBlock = Regex.Match(body,
                @"(?is)Yêu\s*cầu\s*chi\s*tiết\s*:\s*\n((?:\s*[-–•\d.]+\s*.+\n?)+)");
            if (reqBlock.Success)
            {
                foreach (Match li in Regex.Matches(reqBlock.Groups[1].Value,
                    @"(?m)^\s*(?:[-–•]|\d+[.):])\s*(.+?)\s*$"))
                {
                    var detail = NormalizeSpaces(li.Groups[1].Value);
                    if (!string.IsNullOrWhiteSpace(detail)) details.Add(detail);
                }
            }
        }

        dto.Details = details;

        // Thời gian (riêng cho chức năng này)
        var timeMatch = Regex.Match(body,
            @"(?i)Thời\s*gian\s*:\s*(\d{1,2})[/\-](\d{1,2})[/\-](\d{4})\s*[-–]\s*(\d{1,2})[/\-](\d{1,2})[/\-](\d{4})");
        if (timeMatch.Success)
        {
            try
            {
                dto.StartDate = ParseDate(timeMatch.Groups[1].Value, timeMatch.Groups[2].Value, timeMatch.Groups[3].Value);
                dto.Deadline  = ParseDate(timeMatch.Groups[4].Value, timeMatch.Groups[5].Value, timeMatch.Groups[6].Value);
            }
            catch { /* ignore */ }
        }

        // Ước tính giờ
        var estMatch = Regex.Match(body,
            @"(?i)Ước\s*tính\s*:\s*(\d+(?:\.\d+)?)\s*(giờ|h|hours?)");
        if (estMatch.Success && decimal.TryParse(estMatch.Groups[1].Value, out var hrs))
            dto.EstimatedHours = hrs;

        return dto;
    }

    // ================================================================
    // YÊU CẦU PHI CHỨC NĂNG
    // ================================================================

    /// <summary>
    /// Parse phần "YÊU CẦU PHI CHỨC NĂNG" — lấy từng gạch đầu dòng.
    /// </summary>
    private static List<string> ExtractNFR(string content)
    {
        var result = new List<string>();
        var m = Regex.Match(content,
            @"(?is)YÊU\s*CẦU\s*PHI\s*CHỨC\s*NĂNG\s*(?:={4,})?\s*\n(.+?)(?:={4,}|\Z)");

        if (!m.Success) return result;

        var block = m.Groups[1].Value;
        foreach (Match li in Regex.Matches(block, @"(?m)^\s*[-–•]\s*(.+?)\s*$"))
        {
            var nfr = NormalizeSpaces(li.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(nfr)) result.Add(nfr);
        }

        return result;
    }

    // ================================================================
    // HELPERS
    // ================================================================

    /// <summary>Xóa khoảng trắng dư thừa, rút về 1 dòng</summary>
    private static string NormalizeSpaces(string s)
        => Regex.Replace((s ?? "").Replace("\n", " ").Trim(), @"\s+", " ");

    /// <summary>Tạo DateTime từ ngày/tháng/năm string</summary>
    private static DateTime ParseDate(string day, string month, string year)
        => new DateTime(int.Parse(year), int.Parse(month), int.Parse(day));
}
