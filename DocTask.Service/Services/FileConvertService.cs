using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using DocTask.Core.Interfaces.Services;
using iText = iTextSharp.text;
using iTextPdf = iTextSharp.text.pdf;
using PdfPig = UglyToad.PdfPig;
using Doc = DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml;
using ClosedXML;
using Tesseract;
using System.Text.RegularExpressions;

namespace DocTask.Service.Services
{
    public class FileConvertService : IFileConvertService
    {
        private readonly HttpClient _httpClient;
        public FileConvertService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task<string> GetFileContentAsync(string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                throw new ArgumentException("File URL is null or empty", nameof(fileUrl));
            }

            // Nếu fileUrl không phải URL tuyệt đối, ghép BaseAddress của MinIO
            if (!Uri.IsWellFormedUriString(fileUrl, UriKind.Absolute))
            {
                string baseAddress = "https://minio-production-15d9.up.railway.app/doctask/";
                fileUrl = $"{baseAddress}{Uri.EscapeDataString(fileUrl)}";
            }

            // Chuyển fileUrl thành URI hợp lệ
            Uri fileUri;
            try
            {
                var uri = new Uri(fileUrl);

                // Encode các ký tự không hợp lệ trong path
                var encodedPath = string.Join(
                    "/",
                    uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                        .Select(segment => Uri.EscapeDataString(segment))
                );

                encodedPath = "/" + encodedPath; // thêm / đầu tiên nếu cần

                // Bao gồm query nếu có
                var encodedUriString = $"{uri.Scheme}://{uri.Host}{encodedPath}{uri.Query}";
                fileUri = new Uri(encodedUriString);
            }
            catch (UriFormatException ex)
            {
                throw new InvalidOperationException($"Invalid file URL: {fileUrl}", ex);
            }

            var fileBytes = await _httpClient.GetByteArrayAsync(fileUrl);
            var extension = Path.GetExtension(fileUrl).ToLowerInvariant();
            return ParseFileContent(fileBytes, extension);
        }

        private string ParseFileContent(byte[] file, string extension)
        {
            return extension switch
            {
                ".txt" => Encoding.UTF8.GetString(file),
                ".pdf" => ParsePdf(file),
                ".doc" or ".docx" => ParseWord(file),
                ".xls" or ".xlsx" => ParseExcel(file),
                ".png" or ".jpg" or ".jpeg" or ".gif" => ParseImage(file),
                _ => throw new NotSupportedException($"File format {extension} not supported")
            };
        }

        private string ParsePdf(byte[] file)
        {
            using var pdf = PdfPig.PdfDocument.Open(file);
            var sb = new StringBuilder();

            foreach (var page in pdf.GetPages())
            {
                sb.AppendLine(page.Text);
            }

            return sb.ToString();
        }

        private string ParseWord(byte[] file)
        {
            using var stream = new MemoryStream(file);
            using var doc = WordprocessingDocument.Open(stream, false);
            return doc.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
        }

        private string ParseExcel(byte[] file)
        {
            using var stream = new MemoryStream(file);
            using var xls = new ClosedXML.Excel.XLWorkbook(stream);
            var sb = new StringBuilder();

            foreach (var worksheet in xls.Worksheets)
            {
                sb.AppendLine($"--- Sheet:{worksheet.Name} ---");

                foreach (var row in worksheet.RowsUsed())
                {
                    foreach (var cell in row.CellsUsed())
                    {
                        sb.Append(cell.Value.ToString());
                        sb.Append("\t");
                    }

                    sb.AppendLine();
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string ParseImage(byte[] file)
        {
            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");

            if (!Directory.Exists(dataPath))
            {
                throw new DirectoryNotFoundException($"Te$$Data not found: {dataPath}");
            }

            using var engine = new TesseractEngine(dataPath, "eng+vie", EngineMode.Default);
            using var img = Pix.LoadFromMemory(file);
            using var page = engine.Process(img);
            return page.GetText();
        }

        public async Task<(byte[] fileContent, string contentType)> ConvertFileFormatAsync(
            string fileContent,
            string fileName,
            string format
            )
        {
            return format.ToLower() switch
            {
                "pdf" => await ConvertPdfAsync(fileContent, fileName),
                "docx" => await ConvertWordAsync(fileContent, fileName),
                "txt" => await ConvertTextAsync(fileContent, fileName),
                _ => throw new NotSupportedException($"Format {format} không được hỗ trợ")
            };
        }

        private async Task<(byte[] fileContent, string contentType)> ConvertPdfAsync(string fileContent, string fileName)
        {
            using var ms = new MemoryStream();
            using (var document = new iText.Document(iText.PageSize.A4, 50, 50, 50, 50))
            {
                var writer = iTextPdf.PdfWriter.GetInstance(document, ms);
                document.Open();

                var fontBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Asset/fonts/BeVietnamPro");
                if (!Directory.Exists(fontBasePath))
                {
                    throw new DirectoryNotFoundException($"Font not found: {fontBasePath}");
                }

                var fontLight = iTextPdf.BaseFont.CreateFont(Path.Combine(fontBasePath, "BeVietnamPro-Light.ttf"), iTextPdf.BaseFont.IDENTITY_H, iTextPdf.BaseFont.EMBEDDED);
                var fontMedium = iTextPdf.BaseFont.CreateFont(Path.Combine(fontBasePath, "BeVietnamPro-Medium.ttf"), iTextPdf.BaseFont.IDENTITY_H, iTextPdf.BaseFont.EMBEDDED);
                var fontBold = iTextPdf.BaseFont.CreateFont(Path.Combine(fontBasePath, "BeVietnamPro-Bold.ttf"), iTextPdf.BaseFont.IDENTITY_H, iTextPdf.BaseFont.EMBEDDED);

                var contentFont = new iText.Font(fontLight, 11);
                var headerFont = new iText.Font(fontMedium, 14);
                var titleFont = new iText.Font(fontBold, 18);

                var lines = fileContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                var firstContentIndex = Array.FindIndex(lines, l => !string.IsNullOrWhiteSpace(l));

                if (firstContentIndex < 0)
                {
                    throw new ArgumentException("No content available to convert to PDF.");
                }

                var titleText = lines[firstContentIndex].Trim();
                if (string.IsNullOrWhiteSpace(titleText))
                {
                    titleText = Path.GetFileNameWithoutExtension(fileName);
                }

                var titleParagraph = new iText.Paragraph(titleText, titleFont)
                {
                    Alignment = iText.Element.ALIGN_CENTER,
                    SpacingAfter = 15f,
                };
                document.Add(titleParagraph);

                for (int i = firstContentIndex + 1; i < lines.Length; i++)
                {
                    var rawLine = lines[i];
                    if (string.IsNullOrWhiteSpace(rawLine))
                    {
                        document.Add(new iText.Paragraph(" ", contentFont) { SpacingAfter = 7f });
                        continue;
                    }

                    var trimmedLine = rawLine.Trim();
                    iText.Paragraph paragraph = Regex.IsMatch(trimmedLine, @"^\d+\.|^[A-Z].*:$")
                        ? new iText.Paragraph(trimmedLine, headerFont)
                        : new iText.Paragraph(trimmedLine, contentFont);

                    paragraph.Alignment = iText.Element.ALIGN_JUSTIFIED;
                    paragraph.SpacingAfter = 7f;
                    document.Add(paragraph);
                }

                document.Close();

                // // Add title
                // var title = new iText.Paragraph(fileName, titleFont)
                // {
                //     Alignment = iText.Element.ALIGN_CENTER,
                //     SpacingAfter = 15f,
                // };
                // document.Add(title);

                // // Add content
                // var content = fileContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                // if (content.Length == 0)
                // {
                //     throw new ArgumentException("Không có nội dung để chuyển đổi dữ liệu FILE sang PDF.");
                // }
                // else
                // {
                //     foreach (var line in content)
                //     {
                //         iText.Paragraph p;

                //         if (Regex.IsMatch(line.Trim(), @"^\d+\.|^[A-ZĐ].*:$"))
                //         {
                //             p = new iText.Paragraph(line.Trim(), headerFont);
                //         }
                //         else
                //         {
                //             p = new iText.Paragraph(line.Trim(), contentFont);
                //         }

                //         p.Alignment = iText.Element.ALIGN_JUSTIFIED;
                //         p.SpacingAfter = 7f;
                //         document.Add(p);
                //     }
                // }

                // document.Close();
            }

            return (ms.ToArray(), "application/pdf");
        }

        private async Task<(byte[] fileContent, string contentType)> ConvertWordAsync(string fileContent, string fileName)
        {
            using var ms = new MemoryStream();
            using (var document = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
            {
                var mainPart = document.AddMainDocumentPart();
                mainPart.Document = new Doc.Document();
                var body = new Doc.Body();

                // Chia nội dung theo dòng
                var lines = fileContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

                foreach (var rawLine in lines)
                {
                    var line = rawLine.TrimEnd(); // Giữ đầu dòng, bỏ cuối dòng trống

                    // Bỏ qua dòng trống => tạo đoạn trống trong Word
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        body.Append(new Doc.Paragraph(new Doc.Run(new Doc.Text(""))));
                        continue;
                    }

                    // ===== Quy tắc nhận dạng đoạn =====
                    bool isTitle = Regex.IsMatch(line, @"^[A-ZĐÂÊÔƯĂ].+$") && !line.StartsWith("-");
                    bool isNumbered = Regex.IsMatch(line, @"^\d+\.");
                    bool isBullet = line.StartsWith("-");

                    // ===== Định dạng font cơ bản =====
                    var runProps = new Doc.RunProperties(
                        new Doc.RunFonts()
                        {
                            Ascii = "Times New Roman",
                            HighAnsi = "Times New Roman",
                            EastAsia = "Times New Roman",
                            ComplexScript = "Times New Roman",
                        },
                        new Doc.FontSize()
                        {
                            Val = "24"
                        }
                    );

                    // ===== Định dạng tiêu đề =====
                    if (isTitle)
                    {
                        runProps.Append(new Doc.Bold());
                        runProps.Append(new Doc.FontSize()
                        {
                            Val = "28"
                        });
                    }

                    // ===== Gắn text =====
                    var run = new Doc.Run(
                        runProps,
                        new Doc.Text(line)
                        {
                            Space = SpaceProcessingModeValues.Preserve
                        });

                    // ===== Căn lề =====
                    var paragraphProps = new Doc.ParagraphProperties(
                        new Doc.SpacingBetweenLines()
                        {
                            Line = "276",
                            LineRule = Doc.LineSpacingRuleValues.Auto
                        },
                        new Doc.Justification()
                        {
                            Val = Doc.JustificationValues.Both
                        }
                    );

                    // Gạch đầu dòng (-) => thụt vào
                    if (isBullet)
                    {
                        paragraphProps.Append(
                            new Doc.Indentation()
                            {
                                Left = "720"
                            }
                        );
                    }

                    // ===== Tạo đoạn và thêm vào body =====
                    var paragraph = new Doc.Paragraph(paragraphProps, run);
                    body.Append(paragraph);
                }

                mainPart.Document.Append(body);
                mainPart.Document.Save();
            }

            return (ms.ToArray(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        }

        private async Task<(byte[] fileContent, string contentType)> ConvertTextAsync(string fileContent, string fileName)
        {
            return (Encoding.UTF8.GetBytes(fileContent), "text/plain");
        }
    }
}
