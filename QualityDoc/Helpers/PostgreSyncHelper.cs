using System;
using QualityDoc.Pages.Models;

namespace QualityDoc.Helpers
{
    public static class PostgreSyncHelper
    {
        public static object GenerateApiPayload(Documento documento)
        {
            return new
            {
                Id = documento.Id.ToString().ToLowerInvariant(),
                DocumentCode = documento.DocumentCode,
                Title = documento.Title,
                Description = documento.Description,
                FilePath = documento.FilePath,
                VersionNumber = (int)Math.Truncate(documento.VersionNumber ?? 1m),
                IsLatest = documento.IsLatest,
                StatusName = documento.Status?.Name ?? "Activo",
                CompanyId = documento.CompanyId,
                CompanyName = documento.Company?.Name ?? "Empresa 1",
                AuthorId = documento.AuthorId,
                CreatedAt = documento.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
        }
    }
}
