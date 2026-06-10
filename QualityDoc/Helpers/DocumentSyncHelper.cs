using System.IO;
using System.Security.Cryptography;
using System.Text;
using QualityDoc.Pages.Models;
using UglyToad.PdfPig;

namespace QualityDoc.Helpers
{
    public static class DocumentSyncHelper
    {
        public static string GetHealthEndpoint(string documentsEndpoint)
        {
            if (Uri.TryCreate(documentsEndpoint, UriKind.Absolute, out var uri))
            {
                return $"{uri.Scheme}://{uri.Authority}/api/saludo";
            }

            return "http://localhost:3000/api/saludo";
        }

        public static object GenerateSyncPayload(Documento documento, string webRootPath)
        {
            var physicalPath = Path.Combine(webRootPath, (documento.FilePath ?? string.Empty).TrimStart('/'));
            
            // Robust fallback for development on host vs container
            if (!File.Exists(physicalPath) && !string.IsNullOrEmpty(documento.FilePath))
            {
                var fileName = Path.GetFileName(documento.FilePath);
                
                // Try 1: uploads_compartidos in current directory
                var currentDir = Directory.GetCurrentDirectory();
                var potentialPath = Path.Combine(currentDir, "uploads_compartidos", fileName);
                if (File.Exists(potentialPath))
                {
                    physicalPath = potentialPath;
                }
                else
                {
                    // Try 2: uploads_compartidos in parent directory
                    var parentDir = Directory.GetParent(currentDir)?.FullName;
                    if (parentDir != null)
                    {
                        var potentialParentPath = Path.Combine(parentDir, "uploads_compartidos", fileName);
                        if (File.Exists(potentialParentPath))
                        {
                            physicalPath = potentialParentPath;
                        }
                    }
                }
                
                // Try 3: uploads_compartidos moving up from webRootPath
                if (!File.Exists(physicalPath) && !string.IsNullOrEmpty(webRootPath))
                {
                    var webRootParent = Directory.GetParent(webRootPath)?.FullName;
                    if (webRootParent != null)
                    {
                        var webRootGrandParent = Directory.GetParent(webRootParent)?.FullName;
                        if (webRootGrandParent != null)
                        {
                            var potentialWebRootPath = Path.Combine(webRootGrandParent, "uploads_compartidos", fileName);
                            if (File.Exists(potentialWebRootPath))
                            {
                                physicalPath = potentialWebRootPath;
                            }
                        }
                    }
                }
            }

            var fallbackExtension = Path.GetExtension(documento.FilePath ?? string.Empty).ToLowerInvariant();
            var fallbackCategory = DetectCategory(fallbackExtension);

            var metadata = BuildBaseMetadata(fallbackExtension, fallbackCategory);

            if (File.Exists(physicalPath))
            {
                var fileInfo = new FileInfo(physicalPath);
                var extension = fileInfo.Extension.ToLowerInvariant();
                var category = DetectCategory(extension);

                metadata = BuildBaseMetadata(extension, category);
                metadata["fileSize"] = FormatBytes(fileInfo.Length);
                metadata["checksum"] = ComputeHash(physicalPath, MD5.Create());
                metadata["sha256"] = ComputeHash(physicalPath, SHA256.Create());
                metadata["createdOnDisk"] = fileInfo.CreationTimeUtc.ToString("o");
                metadata["modifiedOnDisk"] = fileInfo.LastWriteTimeUtc.ToString("o");

                if (extension == ".pdf")
                {
                    AddPdfTextMetadata(metadata, physicalPath);
                }
            }
            else
            {
                metadata["fileNotFoundError"] = $"No se encontro el archivo fisico en la ruta principal ni en las alternativas. Ruta buscada: {physicalPath}";
            }

            return new
            {
                id = documento.Id.ToString().ToUpperInvariant(),
                documentCode = documento.DocumentCode,
                title = documento.Title,
                description = documento.Description,
                filePath = documento.FilePath,
                versionNumber = documento.VersionNumber,
                companyId = documento.CompanyId,
                authorId = documento.AuthorId,
                statusId = documento.StatusId,
                parentId = documento.ParentId,
                isLatest = documento.IsLatest,
                createdAt = documento.CreatedAt,
                metadata = metadata
            };
        }

        private static Dictionary<string, object?> BuildBaseMetadata(string extension, string category)
        {
            return new Dictionary<string, object?>
            {
                ["fileSize"] = "0B",
                ["mimeType"] = GetMimeType(extension),
                ["extension"] = extension,
                ["category"] = category,
                ["specific"] = GenerateSpecificMetadata(category, extension)
            };
        }

        private static void AddPdfTextMetadata(Dictionary<string, object?> metadata, string physicalPath)
        {
            try
            {
                using var pdf = PdfDocument.Open(physicalPath);
                var text = new StringBuilder();
                var hasImages = false;

                foreach (var page in pdf.GetPages())
                {
                    if (text.Length > 0)
                    {
                        text.AppendLine();
                        text.AppendLine();
                    }

                    text.AppendLine(page.Text);
                    hasImages = hasImages || page.GetImages().Any();
                }

                var fullText = text.ToString().Trim();
                metadata["fullText"] = fullText;
                metadata["texto"] = fullText;
                metadata["extractedText"] = fullText;
                metadata["documentText"] = fullText;
                metadata["specific"] = new
                {
                    pageCount = pdf.NumberOfPages,
                    hasImages = hasImages,
                    language = "es"
                };
            }
            catch (Exception ex)
            {
                metadata["textExtractionError"] = ex.Message;
            }
        }

        private static string DetectCategory(string extension)
        {
            return extension switch
            {
                ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" => "document",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => "image",
                ".dwg" or ".dxf" => "cad",
                _ => "other"
            };
        }

        private static string GetMimeType(string extension)
        {
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".dwg" => "application/acad",
                ".dxf" => "application/dxf",
                _ => "application/octet-stream"
            };
        }

        private static object GenerateSpecificMetadata(string category, string extension)
        {
            if (category == "document")
            {
                return new
                {
                    pageCount = 1, // Simulacion
                    hasImages = true,
                    language = "es"
                };
            }
            if (category == "image")
            {
                return new
                {
                    dimensions = "1920x1080", // Simulacion
                    width = 1920,
                    height = 1080,
                    colorSpace = "srgb"
                };
            }
            if (category == "cad")
            {
                return new
                {
                    softwareVersion = "AutoCAD 2018+",
                    layers = new[] { "MUROS", "DEFAULT" }
                };
            }
            return new { };
        }

        private static string ComputeHash(string filePath, HashAlgorithm hashAlgorithm)
        {
            using (hashAlgorithm)
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = hashAlgorithm.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return string.Format("{0:n1}{1}", number, suffixes[counter]);
        }
    }
}
