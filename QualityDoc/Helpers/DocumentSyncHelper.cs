using System.IO;
using System.Security.Cryptography;
using QualityDoc.Pages.Models;

namespace QualityDoc.Helpers
{
    public static class DocumentSyncHelper
    {
        public static object GenerateSyncPayload(Documento documento, string webRootPath)
        {
            var physicalPath = Path.Combine(webRootPath, documento.FilePath.TrimStart('/'));
            
            object metadata = null;

            if (File.Exists(physicalPath))
            {
                var fileInfo = new FileInfo(physicalPath);
                var extension = fileInfo.Extension.ToLower();
                var category = DetectCategory(extension);

                metadata = new
                {
                    fileSize = FormatBytes(fileInfo.Length),
                    mimeType = GetMimeType(extension),
                    extension = extension,
                    checksum = ComputeHash(physicalPath, MD5.Create()),
                    sha256 = ComputeHash(physicalPath, SHA256.Create()),
                    createdOnDisk = fileInfo.CreationTimeUtc.ToString("o"),
                    modifiedOnDisk = fileInfo.LastWriteTimeUtc.ToString("o"),
                    category = category,
                    specific = GenerateSpecificMetadata(category, extension)
                };
            }

            return new
            {
                Id = documento.Id.ToString().ToUpper(),
                Title = documento.Title,
                Description = documento.Description,
                FilePath = documento.FilePath,
                VersionNumber = documento.VersionNumber,
                CompanyId = documento.CompanyId,
                AuthorId = documento.AuthorId,
                StatusId = documento.StatusId,
                ParentId = documento.ParentId,
                IsLatest = documento.IsLatest,
                CreatedAt = documento.CreatedAt,
                metadata = metadata
            };
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
