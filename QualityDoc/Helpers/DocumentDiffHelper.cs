using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UglyToad.PdfPig;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using DiffPlex.Chunkers;

namespace QualityDoc.Helpers
{
    public class PdfLine
    {
        public int PageNumber { get; set; }
        public int LineNumber { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public class DiffResultLine
    {
        public int? OldPageNumber { get; set; }
        public int? OldLineNumber { get; set; }
        public int? NewPageNumber { get; set; }
        public int? NewLineNumber { get; set; }
        public string Content { get; set; } = string.Empty;
        public ChangeType Type { get; set; }
    }

    public static class DocumentDiffHelper
    {
        public static string ResolvePhysicalPath(string? filePath, string webRootPath)
        {
            if (string.IsNullOrEmpty(filePath)) return string.Empty;

            var physicalPath = Path.Combine(webRootPath, filePath.TrimStart('/'));
            if (File.Exists(physicalPath)) return physicalPath;

            var fileName = Path.GetFileName(filePath);
            var currentDir = Directory.GetCurrentDirectory();

            // Try 1: uploads_compartidos in current directory
            var potentialPath = Path.Combine(currentDir, "uploads_compartidos", fileName);
            if (File.Exists(potentialPath)) return potentialPath;

            // Try 2: uploads_compartidos in parent directory
            var parentDir = Directory.GetParent(currentDir)?.FullName;
            if (parentDir != null)
            {
                var potentialParentPath = Path.Combine(parentDir, "uploads_compartidos", fileName);
                if (File.Exists(potentialParentPath)) return potentialParentPath;
            }

            // Try 3: uploads_compartidos moving up from webRootPath
            var webRootParent = Directory.GetParent(webRootPath)?.FullName;
            if (webRootParent != null)
            {
                var webRootGrandParent = Directory.GetParent(webRootParent)?.FullName;
                if (webRootGrandParent != null)
                {
                    var potentialWebRootPath = Path.Combine(webRootGrandParent, "uploads_compartidos", fileName);
                    if (File.Exists(potentialWebRootPath)) return potentialWebRootPath;
                }
            }

            return physicalPath;
        }

        public static List<PdfLine> ExtractLines(string physicalPath)
        {
            var list = new List<PdfLine>();
            if (!File.Exists(physicalPath)) return list;

            try
            {
                using var pdf = PdfDocument.Open(physicalPath);
                foreach (var page in pdf.GetPages())
                {
                    var pageTextBuilder = new StringBuilder();
                    var words = page.GetWords();

                    double lastTop = -1;
                    foreach (var word in words)
                    {
                        double currentTop = (double)word.BoundingBox.Top;
                        if (lastTop != -1 && Math.Abs(currentTop - lastTop) > 5)
                        {
                            pageTextBuilder.AppendLine();
                        }
                        pageTextBuilder.Append(word.Text + " ");
                        lastTop = currentTop;
                    }

                    var text = pageTextBuilder.ToString();
                    var rawLines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    for (int i = 0; i < rawLines.Length; i++)
                    {
                        var content = rawLines[i].Replace("\r", "").Replace("\n", "").Trim();
                        list.Add(new PdfLine
                        {
                            PageNumber = page.Number,
                            LineNumber = i + 1,
                            Content = content
                        });
                    }
                }
            }
            catch
            {
                // Return empty list on failure or non-PDF
            }
            return list;
        }

        public static List<DiffResultLine> CompareDocuments(string oldPath, string newPath)
        {
            var oldLines = ExtractLines(oldPath);
            var newLines = ExtractLines(newPath);

            var oldText = string.Join("\n", oldLines.Select(l => l.Content));
            var newText = string.Join("\n", newLines.Select(l => l.Content));

            var diffBuilder = new InlineDiffBuilder(new Differ());
            var diffResult = diffBuilder.BuildDiffModel(oldText, newText, false, false, new LineChunker());

            var result = new List<DiffResultLine>();
            int oldLineIdx = 0;
            int newLineIdx = 0;

            foreach (var piece in diffResult.Lines)
            {
                int? oldPage = null;
                int? oldLine = null;
                int? newPage = null;
                int? newLine = null;

                if (piece.Type == ChangeType.Unchanged)
                {
                    if (oldLineIdx < oldLines.Count)
                    {
                        oldPage = oldLines[oldLineIdx].PageNumber;
                        oldLine = oldLines[oldLineIdx].LineNumber;
                        oldLineIdx++;
                    }
                    if (newLineIdx < newLines.Count)
                    {
                        newPage = newLines[newLineIdx].PageNumber;
                        newLine = newLines[newLineIdx].LineNumber;
                        newLineIdx++;
                    }
                }
                else if (piece.Type == ChangeType.Deleted)
                {
                    if (oldLineIdx < oldLines.Count)
                    {
                        oldPage = oldLines[oldLineIdx].PageNumber;
                        oldLine = oldLines[oldLineIdx].LineNumber;
                        oldLineIdx++;
                    }
                }
                else if (piece.Type == ChangeType.Inserted)
                {
                    if (newLineIdx < newLines.Count)
                    {
                        newPage = newLines[newLineIdx].PageNumber;
                        newLine = newLines[newLineIdx].LineNumber;
                        newLineIdx++;
                    }
                }

                result.Add(new DiffResultLine
                {
                    OldPageNumber = oldPage,
                    OldLineNumber = oldLine,
                    NewPageNumber = newPage,
                    NewLineNumber = newLine,
                    Content = piece.Text ?? string.Empty,
                    Type = piece.Type
                });
            }

            return result;
        }
    }
}
