using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Pages.Models;
using System.Net.Http.Json;
using QualityDoc.Helpers;

namespace QualityDoc.Pages.Documents
{
    public class ApprovalModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public Documento? Documento { get; set; }
        public List<Documento> VersionesAnteriores { get; set; } = new();
        public List<ApprovalHistory> Historial { get; set; } = new();
        public int CurrentUserId { get; set; }
        public bool CanApprove { get; set; } = false;

        public ApprovalModel(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private bool CheckApprovalPermissions(Documento doc, int userId)
        {
            var currentUser = _context.Users.Include(u => u.Rol).FirstOrDefault(u => u.Id == userId);
            if (currentUser == null || doc == null) return false;

            bool isAuthor = doc.AuthorId == userId;
            bool isOperador = currentUser.Rol?.Name?.Trim().Equals("Operador", StringComparison.OrdinalIgnoreCase) == true;
            bool isSuperAdmin = currentUser.Rol?.Name?.Trim().Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) == true 
                             || currentUser.Rol?.Name?.Trim().Equals("Super Admin", StringComparison.OrdinalIgnoreCase) == true
                             || currentUser.Rol?.Name?.Trim().Equals("Súperadmin", StringComparison.OrdinalIgnoreCase) == true;
            bool isSameCompany = doc.CompanyId == currentUser.CompanyId;

            return !isAuthor && !isOperador && (isSameCompany || isSuperAdmin);
        }

        public IActionResult OnGet(Guid? id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Login");

            CurrentUserId = userId.Value;

            if (id != null)
            {
                Documento = _context.Documents
                    .Include(d => d.Author)
                    .Include(d => d.Company)
                    .Include(d => d.Status)
                    .FirstOrDefault(d => d.Id == id);

                if (Documento != null)
                {
                    var currentUser = _context.Users.Include(u => u.Rol).FirstOrDefault(u => u.Id == CurrentUserId);
                    bool isSuperAdmin = currentUser?.Rol?.Name?.Trim().Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) == true
                                     || currentUser?.Rol?.Name?.Trim().Equals("Super Admin", StringComparison.OrdinalIgnoreCase) == true
                                     || currentUser?.Rol?.Name?.Trim().Equals("Súperadmin", StringComparison.OrdinalIgnoreCase) == true;
                                     
                    if (Documento.CompanyId != currentUser?.CompanyId && !isSuperAdmin)
                    {
                        return RedirectToPage("/Index");
                    }

                    CanApprove = CheckApprovalPermissions(Documento, CurrentUserId);

                    Historial = _context.ApprovalHistory
                        .Include(h => h.User)
                        .Where(h => h.DocumentId == id)
                        .OrderByDescending(h => h.ActionDate)
                        .ToList();

                    VersionesAnteriores = _context.Documents
                        .Where(d => d.DocumentCode == Documento.DocumentCode && d.Id != Documento.Id)
                        .OrderByDescending(d => d.VersionNumber)
                        .ToList();
                }
            }

            return Page();
        }

        public IActionResult OnPostSendReview(Guid id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Login");

            var doc = _context.Documents.FirstOrDefault(d => d.Id == id);
            if (doc != null && doc.AuthorId == userId.Value && (doc.StatusId == 1 || doc.StatusId == 4))
            {
                doc.StatusId = 2; // En Revisión

                _context.ApprovalHistory.Add(new ApprovalHistory
                {
                    DocumentId = doc.Id,
                    UserId = userId.Value,
                    Action = "Enviado a Revisión",
                    Comment = "El autor envió el documento para su revisión.",
                    ActionDate = DateTime.Now
                });

                _context.SaveChanges();
                TempData["SuccessMessage"] = "El documento ha sido enviado a revisión exitosamente.";
            }
            else
            {
                TempData["ErrorMessage"] = "No tienes permiso para realizar esta acción.";
            }

            return RedirectToPage("/Documents/List");
        }

        public IActionResult OnPostApprove(Guid id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Login");

            var doc = _context.Documents.FirstOrDefault(d => d.Id == id);

            if (doc != null && doc.StatusId == 2 && CheckApprovalPermissions(doc, userId.Value))
            {
                doc.StatusId = 3; // Aprobado

                _context.ApprovalHistory.Add(new ApprovalHistory
                {
                    DocumentId = doc.Id,
                    UserId = userId.Value,
                    Action = "Aprobado",
                    Comment = "El documento ha sido aprobado y es la versión vigente.",
                    ActionDate = DateTime.Now
                });

                _context.SaveChanges();
                TempData["SuccessMessage"] = "Documento aprobado correctamente.";
            }
            else
            {
                TempData["ErrorMessage"] = "No tienes permiso para aprobar este documento.";
            }

            return RedirectToPage("/Documents/List");
        }

        public IActionResult OnPostReject(Guid id, string rejectionComment)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Login");

            var doc = _context.Documents.FirstOrDefault(d => d.Id == id);

            if (doc != null && doc.StatusId == 2 && CheckApprovalPermissions(doc, userId.Value))
            {
                doc.StatusId = 4; // Rechazado

                string finalComment = string.IsNullOrWhiteSpace(rejectionComment) 
                    ? "El documento ha sido devuelto al autor para correcciones." 
                    : rejectionComment;

                _context.ApprovalHistory.Add(new ApprovalHistory
                {
                    DocumentId = doc.Id,
                    UserId = userId.Value,
                    Action = "Rechazado",
                    Comment = finalComment,
                    ActionDate = DateTime.Now
                });

                _context.SaveChanges();
                TempData["SuccessMessage"] = "El documento ha sido rechazado y devuelto al autor.";
            }
            else
            {
                TempData["ErrorMessage"] = "No tienes permiso para rechazar este documento.";
            }

            return RedirectToPage("/Documents/List");
        }

        public async Task<IActionResult> OnPostSyncFirebaseAsync(Guid id)
        {
            var documento = _context.Documents.FirstOrDefault(d => d.Id == id);
            if (documento == null) return RedirectToPage();

            var payload = DocumentSyncHelper.GenerateSyncPayload(documento, _env.WebRootPath);

            try
            {
                var client = new HttpClient();
                var response = await client.PostAsJsonAsync(
                    "http://localhost:3000/api/documents",
                    payload
                );

                if (response.IsSuccessStatusCode)
                {
                    documento.SyncFirebase = true;
                    documento.LastErrorLog = null;
                    TempData["SuccessMessage"] = "Sincronizado con Firebase exitosamente.";
                }
                else
                {
                    documento.LastErrorLog = await response.Content.ReadAsStringAsync();
                    TempData["ErrorMessage"] = "Error al sincronizar: " + response.StatusCode;
                }
            }
            catch (Exception ex)
            {
                documento.LastErrorLog = ex.Message;
                TempData["ErrorMessage"] = "Fallo de conexión al sincronizar.";
            }

            await _context.SaveChangesAsync();

            return RedirectToPage(new { id = id }); 
        }
    }
}