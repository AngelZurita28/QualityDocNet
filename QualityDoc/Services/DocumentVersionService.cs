using System.Data;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Pages.Models;

namespace QualityDoc.Services
{
    public class DocumentVersionService
    {
        private const decimal VersionStep = 0.01m;

        private readonly AppDbContext _context;
        private readonly RolePermissionService _permissions;

        public DocumentVersionService(AppDbContext context, RolePermissionService permissions)
        {
            _context = context;
            _permissions = permissions;
        }

        public async Task<WorkflowResult> AssignReviewVersionAsync(Guid documentId, int userId)
        {
            var user = await LoadUserAsync(userId);
            if (user == null) return WorkflowResult.Fail("Usuario no encontrado.");

            var doc = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
            if (doc == null) return WorkflowResult.Fail("Documento no encontrado.");

            if (!_permissions.CanSendToReview(user, user.Rol?.Name, doc))
            {
                return WorkflowResult.Fail("No tienes permiso para enviar este documento a revision.");
            }

            if (string.IsNullOrWhiteSpace(doc.DocumentCode))
            {
                return WorkflowResult.Fail("El documento no tiene codigo asignado.");
            }

            var versions = await _context.Documents
                .Where(d => d.DocumentCode == doc.DocumentCode && d.CompanyId == doc.CompanyId)
                .Select(d => d.VersionNumber)
                .ToListAsync();

            var completeVersions = versions
                .Where(v => IsCompleteVersion(v))
                .Select(v => v!.Value)
                .ToList();

            var baseVersion = completeVersions.Any() ? completeVersions.Max() : 0m;

            var intermediateVersions = versions
                .Where(v => v.HasValue && v.Value > baseVersion && v.Value < baseVersion + 1m)
                .Select(v => v!.Value)
                .ToList();

            var latestIntermediate = intermediateVersions.Any() ? intermediateVersions.Max() : baseVersion;

            var nextVersion = decimal.Round(latestIntermediate + VersionStep, 2);
            if (nextVersion >= baseVersion + 1m)
            {
                return WorkflowResult.Fail("No hay mas versiones intermedias disponibles para esta version base.");
            }

            doc.VersionNumber = nextVersion;
            doc.StatusId = DocumentWorkflowConstants.Status.InReview;
            doc.IsLatest = false;

            AddHistory(doc.Id, user.Id, "Enviado a Revision", $"Version intermedia asignada: v{FormatVersion(nextVersion)}.");
            await _context.SaveChangesAsync();

            return WorkflowResult.Ok("El documento fue enviado a revision.");
        }

        public async Task<WorkflowResult> ReviewerApproveAsync(Guid documentId, int reviewerId)
        {
            var user = await LoadUserAsync(reviewerId);
            if (user == null) return WorkflowResult.Fail("Usuario no encontrado.");

            var doc = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
            if (doc == null) return WorkflowResult.Fail("Documento no encontrado.");

            if (!_permissions.CanReviewerApprove(user, user.Rol?.Name, doc))
            {
                return WorkflowResult.Fail("No tienes permiso para aprobar esta revision.");
            }

            doc.StatusId = DocumentWorkflowConstants.Status.Candidate;
            AddHistory(doc.Id, user.Id, "Aprobado por Reviewer", "El documento queda como candidato para aprobacion final.");

            await _context.SaveChangesAsync();
            return WorkflowResult.Ok("El documento fue aprobado por reviewer y queda como candidato.");
        }

        public async Task<WorkflowResult> ReviewerRejectAsync(Guid documentId, int reviewerId, string? comment)
        {
            var user = await LoadUserAsync(reviewerId);
            if (user == null) return WorkflowResult.Fail("Usuario no encontrado.");

            var doc = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
            if (doc == null) return WorkflowResult.Fail("Documento no encontrado.");

            if (!_permissions.CanReviewerApprove(user, user.Rol?.Name, doc))
            {
                return WorkflowResult.Fail("No tienes permiso para rechazar esta revision.");
            }

            doc.StatusId = DocumentWorkflowConstants.Status.Rejected;
            doc.IsLatest = false;
            AddHistory(doc.Id, user.Id, "Rechazado por Reviewer", CleanComment(comment, "Revision rechazada."));

            await _context.SaveChangesAsync();
            return WorkflowResult.Ok("El documento fue rechazado.");
        }

        public async Task<WorkflowResult> AdminFinalizeApproveAsync(Guid documentId, int adminId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var user = await LoadUserAsync(adminId);
            if (user == null) return WorkflowResult.Fail("Usuario no encontrado.");

            var doc = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
            if (doc == null) return WorkflowResult.Fail("Documento no encontrado.");

            if (!_permissions.CanAdminFinalize(user, user.Rol?.Name, doc))
            {
                return WorkflowResult.Fail("No tienes permiso para aprobar esta candidata.");
            }

            if (doc.StatusId == DocumentWorkflowConstants.Status.Rejected)
            {
                return WorkflowResult.Fail("Esta candidata ya fue rechazada y no puede activarse.");
            }

            if (string.IsNullOrWhiteSpace(doc.DocumentCode))
            {
                return WorkflowResult.Fail("El documento no tiene codigo asignado.");
            }

            var relatedDocs = await _context.Documents
                .Where(d => d.DocumentCode == doc.DocumentCode && d.CompanyId == doc.CompanyId)
                .ToListAsync();

            var fullVersions = relatedDocs
                .Where(d => IsCompleteVersion(d.VersionNumber))
                .Select(d => d.VersionNumber!.Value)
                .ToList();

            var nextFullVersion = fullVersions.Any() ? decimal.Truncate(fullVersions.Max()) + 1m : 1m;

            foreach (var versionedDoc in relatedDocs)
            {
                if (versionedDoc.Id == doc.Id) continue;

                if (IsCompleteVersion(versionedDoc.VersionNumber))
                {
                    versionedDoc.StatusId = DocumentWorkflowConstants.Status.Obsolete;
                    versionedDoc.IsLatest = false;
                }
                else if (versionedDoc.VersionNumber.HasValue &&
                         versionedDoc.StatusId != DocumentWorkflowConstants.Status.Rejected)
                {
                    versionedDoc.StatusId = DocumentWorkflowConstants.Status.Rejected;
                    versionedDoc.IsLatest = false;
                }
            }

            try
            {
                await _context.SaveChangesAsync();

                doc.VersionNumber = decimal.Round(nextFullVersion, 2);
                doc.StatusId = DocumentWorkflowConstants.Status.Active;
                doc.IsLatest = true;
                doc.SyncFirebase = false;
                doc.SyncPostgre = false;
                doc.LastErrorLog = null;

                AddHistory(doc.Id, user.Id, "Aprobacion Final", $"Documento activado como version v{FormatVersion(doc.VersionNumber)}.");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return WorkflowResult.Fail("Esta candidata ya no puede aprobarse porque otra version fue activada.");
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                return WorkflowResult.Fail("Esta candidata ya no puede aprobarse porque otra version fue activada.");
            }

            return WorkflowResult.Ok("La candidata fue aprobada como nueva version activa.");
        }

        public async Task<WorkflowResult> AdminFinalizeRejectAsync(Guid documentId, int adminId, string? comment)
        {
            var user = await LoadUserAsync(adminId);
            if (user == null) return WorkflowResult.Fail("Usuario no encontrado.");

            var doc = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
            if (doc == null) return WorkflowResult.Fail("Documento no encontrado.");

            if (!_permissions.CanAdminFinalize(user, user.Rol?.Name, doc))
            {
                return WorkflowResult.Fail("No tienes permiso para rechazar esta candidata.");
            }

            doc.StatusId = DocumentWorkflowConstants.Status.Rejected;
            doc.IsLatest = false;
            AddHistory(doc.Id, user.Id, "Rechazado por Admin", CleanComment(comment, "Candidata rechazada en aprobacion final."));

            await _context.SaveChangesAsync();
            return WorkflowResult.Ok("La candidata fue rechazada.");
        }

        public async Task<List<Documento>> GetRealVersionHistoryAsync(string? documentCode, int? companyId, Guid currentDocumentId)
        {
            if (string.IsNullOrWhiteSpace(documentCode) || companyId == null) return new List<Documento>();

            var docs = await _context.Documents
                .Include(d => d.Status)
                .Where(d => d.DocumentCode == documentCode && d.CompanyId == companyId && d.Id != currentDocumentId)
                .ToListAsync();

            return docs
                .Where(d => IsCompleteVersion(d.VersionNumber))
                .OrderByDescending(d => d.VersionNumber)
                .ToList();
        }

        public async Task<decimal?> PreviewNextReviewVersionAsync(string documentCode, int companyId)
        {
            if (string.IsNullOrWhiteSpace(documentCode)) return null;

            var versions = await _context.Documents
                .Where(d => d.DocumentCode == documentCode && d.CompanyId == companyId)
                .Select(d => d.VersionNumber)
                .ToListAsync();

            if (!versions.Any()) return null;

            var completeVersions = versions
                .Where(v => IsCompleteVersion(v))
                .Select(v => v!.Value)
                .ToList();

            var baseVersion = completeVersions.Any() ? completeVersions.Max() : 0m;

            var intermediateVersions = versions
                .Where(v => v.HasValue && v.Value > baseVersion && v.Value < baseVersion + 1m)
                .Select(v => v!.Value)
                .ToList();

            var latestIntermediate = intermediateVersions.Any() ? intermediateVersions.Max() : baseVersion;

            var nextVersion = decimal.Round(latestIntermediate + VersionStep, 2);
            return nextVersion < baseVersion + 1m ? nextVersion : null;
        }

        public static bool IsCompleteVersion(decimal? version)
        {
            return version.HasValue && version.Value == decimal.Truncate(version.Value);
        }

        public static string FormatVersion(decimal? version)
        {
            if (!version.HasValue) return "Sin version";
            return IsCompleteVersion(version) ? version.Value.ToString("0.0") : version.Value.ToString("0.00");
        }

        private async Task<Usuario?> LoadUserAsync(int userId)
        {
            return await _context.Users
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        private void AddHistory(Guid documentId, int userId, string action, string comment)
        {
            _context.ApprovalHistory.Add(new ApprovalHistory
            {
                DocumentId = documentId,
                UserId = userId,
                Action = action,
                Comment = comment,
                ActionDate = DateTime.Now
            });
        }

        private static string CleanComment(string? comment, string fallback)
        {
            return string.IsNullOrWhiteSpace(comment) ? fallback : comment.Trim();
        }
    }
}
