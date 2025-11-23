using System.Net.Http;
using System.Text;
using ClosedXML.Excel;
using DocTask.Core.Interfaces.Services;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

public class ReportReaderService : IReportReaderService
{
  private readonly HttpClient _httpClient;
  private const string TesseractDataPath = "tessdata"; // Đường dẫn đến thư mục chứa dữ liệu ngôn ngữ của Tesseract
  private readonly IUploadFileService _uploadFileService;

  public ReportReaderService(HttpClient httpClient, IUploadFileService uploadFileService)
  {
    _httpClient = httpClient;
    _uploadFileService = uploadFileService;
  }

  public async Task<string> ExtractTextFromIdAsync(int fileId)
  {
    // Lấy metadata (để có FileName và loại file)
    var meta = await _uploadFileService.GetFileByIdAsync(fileId);
    if (meta == null)
      throw new FileNotFoundException($"File metadata not found for id {fileId}");

    // Tải file stream từ S3/MinIO
    using var stream = await _uploadFileService.DownloadFileAsync(fileId);
    if (stream == null)
      throw new FileNotFoundException($"File stream not found for id {fileId}");

    // Dò extension
    var ext = Path.GetExtension(meta.FileName)?.ToLowerInvariant();

    string text = ext switch
    {
      ".pdf" => ExtractFromPdf(stream),
      ".docx" => ExtractFromDocx(stream),
      ".txt" => await ExtractFromTxtAsync(stream),
      ".xlsx" => ExtractFromExcel(stream),
      _ => throw new NotSupportedException($"Unsupported file type: {ext}")
    };

    return text;
    // var stream = await _uploadFileService.DownloadFileAsync(fileId);

    // if (fileType.Equals("pdf", StringComparison.OrdinalIgnoreCase))
    // {
    //   using var pdf = PdfDocument.Open(stream);
    //   var text = new System.Text.StringBuilder();
    //   foreach (var page in pdf.GetPages())
    //     text.AppendLine(page.Text);
    //   return text.ToString();
    // }
    // else if (fileType.Equals("docx", StringComparison.OrdinalIgnoreCase))
    // {
    //   var tempFile = Path.GetTempFileName() + ".docx";
    //   using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
    //   {
    //     await stream.CopyToAsync(fs);
    //   }
    //   using var doc = DocX.Load(tempFile);
    //   return doc.Text;
    // }

    // return string.Empty;
  }
  private string ExtractFromPdf(Stream stream)
  {
    using var pdf = PdfDocument.Open(stream);
    var sb = new StringBuilder();
    foreach (var page in pdf.GetPages())
    {
      sb.AppendLine(page.Text);
    }
    return sb.ToString();
  }

  private string ExtractFromDocx(Stream stream)
  {
    // Copy stream ra memory để OpenXml có thể mở lại
    using var ms = new MemoryStream();
    stream.CopyTo(ms);
    ms.Position = 0;

    using var doc = WordprocessingDocument.Open(ms, false);
    var body = doc.MainDocumentPart?.Document.Body;

    if (body == null) return string.Empty;

    // Ghép toàn bộ text trong các paragraph
    return string.Join(Environment.NewLine,
        body.Elements<Paragraph>().Select(p => p.InnerText));
  }

  private async Task<string> ExtractFromTxtAsync(Stream stream)
  {
    using var reader = new StreamReader(stream);
    return await reader.ReadToEndAsync();
  }

  private string ExtractFromExcel(Stream stream)
  {
    using var workbook = new XLWorkbook(stream);
    var ws = workbook.Worksheets.First();
    var sb = new StringBuilder();
    foreach (var row in ws.RowsUsed())
    {
      sb.AppendLine(string.Join(" | ", row.Cells().Select(c => c.Value.ToString())));
    }
    return sb.ToString();
  }

}
