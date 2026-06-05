using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Pages.Models;
using System.Net.Http.Json;
using QualityDoc.Helpers;
using Microsoft.Extensions.Configuration;

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
        public bool CanEdit { get; set; } = false;

        [BindProperty]
        public string EditTitle { get; set; } = string.Empty;
        [BindProperty]
        public string EditDescription { get; set; } = string.Empty;
        [BindProperty]
        public IFormFile? NewFile { get; set; }
        [BindProperty]
        public int EditDepartmentId { get; set; }

        public List<Department> DepartmentsList { get; set; } = new();

        private readonly IConfiguration _configuration;

        public ApprovalModel(AppDbContext context, IWebHostEnvironment env, IConfiguration configuration)
        {
            _context = context;
            _env = env;
            _configuration = configuration;
        }

        private bool CheckApprovalPermissions(Documento doc, int userId)
        {
            var currentUser = _context.Users.Include(u => u.Rol).FirstOrDefault(u => u.Id == userId);
            if (currentUser == null || doc == null) return false;

            bool isAuthor = doc.AuthorId == userId;
            string rolName = currentUser.Rol?.Name?.Trim() ?? "";

            bool isAdmin = rolName.Equals("Admin", StringComparison.OrdinalIgnoreCase) || 
                           rolName.Equals("Administrador", StringComparison.OrdinalIgnoreCase);

            bool isSuperAdmin = rolName.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) || 
                                rolName.Equals("Super Admin", StringComparison.OrdinalIgnoreCase) ||
                                rolName.Equals("Súperadmin", StringComparison.OrdinalIgnoreCase);

            bool isSameCompany = doc.CompanyId == currentUser.CompanyId;

            // El autor NUNCA puede aprobar su propio documento.
            // Solo Admins de la misma empresa o SuperAdmins globales pueden aprobar.
            // Esto excluye automáticamente al rol "Operador".
            if (isAuthor) return false;
            
            return (isAdmin && isSameCompany) || isSuperAdmin;
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
                    .Include(d => d.Department)
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

                    bool isAuthor = Documento.AuthorId == CurrentUserId;

                    // SuperAdmin edita todo. Autor edita lo suyo (según instrucción "y si no esta también hazlo").
                    CanEdit = isSuperAdmin || isAuthor;

                    EditTitle = Documento.Title;
                    EditDescription = Documento.Description ?? "";
                    EditDepartmentId = Documento.DepartmentId ?? 0;

                    DepartmentsList = _context.Departments.OrderBy(d => d.Name).ToList();

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

            // Si ya está marcado como sincronizado, limpiar cualquier error residual y no repetir
            if (documento.SyncFirebase)
            {
                if (!string.IsNullOrEmpty(documento.LastErrorLog))
                {
                    documento.LastErrorLog = null;
                    await _context.SaveChangesAsync();
                }
                TempData["SuccessMessage"] = "Este documento ya estaba sincronizado con Firebase.";
                return RedirectToPage(new { id = id });
            }

            var payload = DocumentSyncHelper.GenerateSyncPayload(documento, _env.WebRootPath);

            try
            {
                var client = new HttpClient();
                var response = await client.PostAsJsonAsync(
                    "http://localhost:3000/api/documents",
                    payload
                );

                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    documento.SyncFirebase = true;
                    documento.LastErrorLog = null;
                    TempData["SuccessMessage"] = "Sincronizado con Firebase exitosamente.";
                }
                else
                {
                    // Si el documento ya existe en Firestore, se trata como éxito
                    bool yaExiste = responseBody.Contains("ya existe", StringComparison.OrdinalIgnoreCase)
                                 || responseBody.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                                 || responseBody.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                                 || responseBody.Contains("duplicar", StringComparison.OrdinalIgnoreCase)
                                 || responseBody.Contains("No se permite", StringComparison.OrdinalIgnoreCase);

                    if (yaExiste)
                    {
                        documento.SyncFirebase = true;
                        documento.LastErrorLog = null;
                        TempData["SuccessMessage"] = "El documento ya estaba sincronizado con Firebase.";
                    }
                    else
                    {
                        documento.LastErrorLog = responseBody;
                        TempData["ErrorMessage"] = "Error al sincronizar: " + response.StatusCode;
                    }
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

        public async Task<IActionResult> OnPostSyncPostgreAsync(Guid id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Login");

            var documento = _context.Documents
                .Include(d => d.Company)
                .Include(d => d.Status)
                .FirstOrDefault(d => d.Id == id);

            if (documento == null) return RedirectToPage();

            var connString = _configuration.GetConnectionString("PostgresConnection");
            if (string.IsNullOrEmpty(connString))
            {
                TempData["ErrorMessage"] = "Cadena de conexión a PostgreSQL no configurada.";
                return RedirectToPage(new { id = id });
            }

            try
            {
                bool success = await PostgreSyncHelper.SyncToPostgreAsync(documento, connString);
                if (success)
                {
                    documento.SyncPostgre = true;
                    documento.LastErrorLog = null;
                    TempData["SuccessMessage"] = "Sincronizado con PostgreSQL de manera exitosa.";
                }
                else
                {
                    TempData["ErrorMessage"] = "No se pudieron realizar cambios en PostgreSQL.";
                }
            }
            catch (Exception ex)
            {
                documento.LastErrorLog = "Error Postgres: " + ex.Message;
                TempData["ErrorMessage"] = "Fallo al sincronizar con PostgreSQL: " + ex.Message;
            }

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id = id });
        }

        public async Task<IActionResult> OnPostEditAsync(Guid id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Login");

            var documento = await _context.Documents.FirstOrDefaultAsync(d => d.Id == id);
            if (documento == null) return RedirectToPage();

            var currentUser = await _context.Users.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == userId);
            bool isSuperAdmin = currentUser?.Rol?.Name?.Trim().Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) == true 
                             || currentUser?.Rol?.Name?.Trim().Equals("Super Admin", StringComparison.OrdinalIgnoreCase) == true
                             || currentUser?.Rol?.Name?.Trim().Equals("Súperadmin", StringComparison.OrdinalIgnoreCase) == true;
            bool isAuthor = documento.AuthorId == userId;

            if (!isSuperAdmin && !isAuthor)
            {
                TempData["ErrorMessage"] = "No tienes permiso para editar este documento.";
                return RedirectToPage(new { id });
            }

            documento.Title = EditTitle;
            documento.Description = EditDescription;
            documento.DepartmentId = EditDepartmentId > 0 ? EditDepartmentId : (int?)null;

            string editComment = "Metadatos del documento actualizados.";

            if (NewFile != null)
            {
                var fileName = $"{Guid.NewGuid()}_{NewFile.FileName}";
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                
                var filePath = Path.Combine(uploadsFolder, fileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await NewFile.CopyToAsync(fileStream);
                }

                documento.FilePath = "/uploads/" + fileName;
                editComment = "Documento y metadatos actualizados.";
            }

            _context.ApprovalHistory.Add(new ApprovalHistory
            {
                DocumentId = documento.Id,
                UserId = userId.Value,
                Action = "Editado",
                Comment = editComment,
                ActionDate = DateTime.Now
            });

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Documento actualizado correctamente.";

            return RedirectToPage(new { id });
        }
    }
}