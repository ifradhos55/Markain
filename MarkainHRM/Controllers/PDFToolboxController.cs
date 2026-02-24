using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace OzarkLMS.Controllers
{
    [Authorize]
    public class PDFToolboxController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly string _tempFolder;

        public PDFToolboxController(IWebHostEnvironment env)
        {
            _env = env;
            _tempFolder = Path.Combine(_env.WebRootPath, "temp");
            
            // Ensure temp directory exists
            if (!Directory.Exists(_tempFolder))
            {
                Directory.CreateDirectory(_tempFolder);
            }

            // Configure QuestPDF license
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public IActionResult Index()
        {
            // Clean old files (older than 1 hour)
            CleanOldFiles();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MergePDFs(List<IFormFile> files)
        {
            if (files == null || files.Count < 2)
            {
                TempData["Error"] = "Please upload at least 2 PDF files to merge.";
                return RedirectToAction("Index");
            }

            try
            {
                // Validate all files are PDFs
                foreach (var file in files)
                {
                    if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        TempData["Error"] = $"File '{file.FileName}' is not a PDF. Only PDF files can be merged.";
                        return RedirectToAction("Index");
                    }
                }

                // Create output document
                var outputDocument = new PdfDocument();

                // Merge all PDFs
                foreach (var file in files)
                {
                    using (var stream = file.OpenReadStream())
                    {
                        var inputDocument = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
                        foreach (PdfPage page in inputDocument.Pages)
                        {
                            outputDocument.AddPage(page);
                        }
                    }
                }

                // Save merged PDF
                var fileName = $"merged_{Guid.NewGuid()}.pdf";
                var filePath = Path.Combine(_tempFolder, fileName);
                outputDocument.Save(filePath);
                outputDocument.Close();

                TempData["Success"] = "PDFs merged successfully! Click below to download.";
                TempData["DownloadFile"] = fileName;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error merging PDFs: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConvertToPDF(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please upload a file to convert.";
                return RedirectToAction("Index");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            try
            {
                if (extension != ".txt")
            {
                TempData["Error"] = $"Unsupported file type: {extension}. Only .txt files are supported.";
                return RedirectToAction("Index");
            }

            return await ConvertTextToPDF(file);    }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error converting file: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        private async Task<IActionResult> ConvertTextToPDF(IFormFile file)
        {
            // Read text content
            string textContent;
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                textContent = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(textContent))
            {
                TempData["Error"] = "The text file is empty.";
                return RedirectToAction("Index");
            }

            // Generate PDF using QuestPDF
            var fileName = $"converted_{Path.GetFileNameWithoutExtension(file.FileName)}_{Guid.NewGuid()}.pdf";
            var filePath = Path.Combine(_tempFolder, fileName);

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header()
                        .Text($"Converted from: {file.FileName}")
                        .SemiBold().FontSize(14).FontColor(Colors.Blue.Darken4);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(col =>
                        {
                            col.Item().Text(textContent);
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                });
            }).GeneratePdf(filePath);

            TempData["Success"] = "Text file converted to PDF successfully! Click below to download.";
            TempData["DownloadFile"] = fileName;

            return RedirectToAction("Index");
        }

        private async Task<IActionResult> ConvertDocxToPDF(IFormFile file)
        {
            try
            {
                // Save uploaded file temporarily
                var tempInputPath = Path.Combine(_tempFolder, $"input_{Guid.NewGuid()}.docx");
                using (var stream = new FileStream(tempInputPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Extract text from DOCX
                string extractedText;
                using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(tempInputPath, false))
                {
                    var body = wordDoc.MainDocumentPart?.Document?.Body;
                    if (body == null)
                    {
                        TempData["Error"] = "Unable to read Word document content.";
                        System.IO.File.Delete(tempInputPath);
                        return RedirectToAction("Index");
                    }
                    extractedText = body.InnerText;
                }

                // Clean up input file
                System.IO.File.Delete(tempInputPath);

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    TempData["Error"] = "The Word document appears to be empty.";
                    return RedirectToAction("Index");
                }

                // Generate PDF using QuestPDF
                var fileName = $"converted_{Path.GetFileNameWithoutExtension(file.FileName)}_{Guid.NewGuid()}.pdf";
                var filePath = Path.Combine(_tempFolder, fileName);

                QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                        page.Header()
                            .Text($"Converted from: {file.FileName}")
                            .SemiBold().FontSize(14).FontColor(Colors.Blue.Darken4);

                        page.Content()
                            .PaddingVertical(1, Unit.Centimetre)
                            .Column(col =>
                            {
                                col.Item().Text(extractedText);
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                            });
                    });
                }).GeneratePdf(filePath);

                TempData["Success"] = "Word document converted to PDF successfully! Note: Complex formatting may not be preserved.";
                TempData["DownloadFile"] = fileName;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error converting Word document: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExportTextToTxt(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["Error"] = "Cannot export empty content.";
                return RedirectToAction("Index");
            }

            var fileName = $"exported_text_{Guid.NewGuid()}.txt";
            var filePath = Path.Combine(_tempFolder, fileName);
            
            System.IO.File.WriteAllText(filePath, content);

            TempData["Success"] = "Text exported to TXT successfully! Click below to download.";
            TempData["DownloadFile"] = fileName;

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExportTextToPdf(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["Error"] = "Cannot export empty content.";
                return RedirectToAction("Index");
            }

            try
            {
                var fileName = $"exported_text_{Guid.NewGuid()}.pdf";
                var filePath = Path.Combine(_tempFolder, fileName);

                QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                        page.Header()
                            .Text("Exported Text")
                            .SemiBold().FontSize(14).FontColor(Colors.Purple.Darken2);

                        page.Content()
                            .PaddingVertical(1, Unit.Centimetre)
                            .Column(col =>
                            {
                                col.Item().Text(content);
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                            });
                    });
                }).GeneratePdf(filePath);

                TempData["Success"] = "Text exported to PDF successfully! Click below to download.";
                TempData["DownloadFile"] = fileName;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error exporting PDF: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        public IActionResult DownloadFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return NotFound();
            }

            var filePath = Path.Combine(_tempFolder, fileName);
            
            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "File not found or has expired.";
                return RedirectToAction("Index");
            }

            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "application/pdf", fileName);
        }

        private void CleanOldFiles()
        {
            try
            {
                var files = Directory.GetFiles(_tempFolder);
                var threshold = DateTime.Now.AddHours(-1);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < threshold)
                    {
                        System.IO.File.Delete(file);
                    }
                }
            }
            catch
            {
                // Silently fail cleanup
            }
        }
    }
}
