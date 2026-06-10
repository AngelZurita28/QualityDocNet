using QualityDoc.Pages.Models;

namespace QualityDoc.Services
{
    public class RolePermissionService
    {
        public bool IsSuperAdmin(string? roleName)
        {
            var role = Normalize(roleName);
            return role == "superadmin" || role == "super admin" || role == "super administrador" || role == "súperadmin";
        }

        public bool IsAdmin(string? roleName)
        {
            var role = Normalize(roleName);
            return role == "admin" || role == "administrador";
        }

        public bool IsReviewer(string? roleName)
        {
            return Normalize(roleName) == "reviewer";
        }

        public bool IsRedacter(string? roleName)
        {
            var role = Normalize(roleName);
            return role == "redacter" || role == "redactor";
        }

        public bool IsOperador(string? roleName)
        {
            var role = Normalize(roleName);
            return role == "operador" || role == "operator";
        }

        public bool CanCreateDraft(string? roleName)
        {
            return IsRedacter(roleName);
        }

        public bool CanViewDocument(Usuario user, string? roleName, Documento doc)
        {
            if (IsOperador(roleName)) return false;
            if (IsSuperAdmin(roleName)) return true;
            if (IsRedacter(roleName)) return doc.AuthorId == user.Id;
            return user.CompanyId != null && doc.CompanyId == user.CompanyId;
        }

        public bool CanEditDraft(Usuario user, string? roleName, Documento doc)
        {
            if (IsOperador(roleName)) return false;
            if (doc.StatusId != DocumentWorkflowConstants.Status.Draft) return false;
            return doc.AuthorId == user.Id || IsSuperAdmin(roleName);
        }

        public bool CanDeleteDraft(Usuario user, string? roleName, Documento doc)
        {
            return CanEditDraft(user, roleName, doc);
        }

        public bool CanSendToReview(Usuario user, string? roleName, Documento doc)
        {
            if (IsOperador(roleName)) return false;
            return doc.AuthorId == user.Id && doc.StatusId == DocumentWorkflowConstants.Status.Draft;
        }

        public bool CanReviewerApprove(Usuario user, string? roleName, Documento doc)
        {
            if (IsOperador(roleName)) return false;
            if (doc.StatusId != DocumentWorkflowConstants.Status.InReview) return false;
            if (doc.AuthorId == user.Id) return false;
            if (IsSuperAdmin(roleName)) return true;
            return IsReviewer(roleName) && user.CompanyId != null && doc.CompanyId == user.CompanyId;
        }

        public bool CanAdminFinalize(Usuario user, string? roleName, Documento doc)
        {
            if (IsOperador(roleName)) return false;
            if (doc.StatusId != DocumentWorkflowConstants.Status.Candidate) return false;
            if (doc.AuthorId == user.Id) return false;
            if (IsSuperAdmin(roleName)) return true;
            return IsAdmin(roleName) && user.CompanyId != null && doc.CompanyId == user.CompanyId;
        }

        private static string Normalize(string? roleName)
        {
            return (roleName ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
