using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Pages.Models;
using System.Net.Http.Json;
using QualityDoc.Helpers;
using Microsoft.Extensions.Configuration;
using QualityDoc.Services;

namespace QualityDoc.Pages.Documents
{
    public class ApprovalModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly DocumentVersionService _versionService;
        private readonly RolePermissionService _permissions;

        public Documento? Documento { get; set; }
        public List<Documento> VersionesAnteriores { get; set; } = new();
        public List<ApprovalHistory> Historial { get; set; } = new();
        public int CurrentUserId { get; set; }
        public bool CanApprove { get; set; } = false;
        public bool CanReviewerApprove { get; set; } = false;
        public bool CanAdminFinalize { get; set; } = false;
        public bool CanSendReview { get; set; } = false;
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

        public ApprovalModel(AppDbContext context, IWebHostEnvironment env, IConfiguration configuration, DocumentVersionService versionService, RolePermissionService permissions)
        {
            _context = context;
            _env = env;
            _configuration = configuration;
            _versionService = versionService;
            _permissions = permissions;
        }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Login");

            var rolName = HttpContext.Session.GetString("Rol") ?? "";
            if (_permissions.IsOperador(rolName))
            {
                return RedirectToPage("/Index");
            }

            CurrentUserId = userId.Value;

            if (id != null)
            {
                Documento = await _context.Documents
                    .Include(d => d.Author)
                    .Include(d => d.Company)
                    .Include(d => d.Status)
                    .Include(d => d.Department)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (Documento != null)
                {
                    var currentUser = await _context.Users.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == CurrentUserId);
                    if (currentUser == null || !_permissions.CanViewDocument(currentUser, rolName, Documento))
                    {
                        return RedirectToPage("/Index");
                    }

                    CanReviewerApprove = _permissions.CanReviewerApprove(currentUser, rolName, Documento);
                    CanAdminFinalize = _permissions.CanAdminFinalize(currentUser, rolName, Documento);
                    CanApprove = CanReviewerApprove || CanAdminFinalize;
                    CanSendReview = _permissions.CanSendToReview(currentUser, rolName, Documento);
                    CanEdit = _permissions.CanEditDraft(currentUser, rolName, Documento);

                    EditTitle = Documento.Title;
                    EditDescription = Documento.Description ?? "";
                    EditDepartmentId = Documento.DepartmentId ?? 0;

                    DepartmentsList = _permissions.IsSuperAdmin(rolName)
                        ? await _context.Departments.OrderBy(d => d.Name).ToListAsync()
                        : await _context.Departments
                            .Where(d => d.CompanyId == currentUser.CompanyId)
                            .OrderBy(d => d.Name)
                            .ToListAsync();

                    Historial = await _context.ApprovalHistory
                        .Include(h => h.User)
                        .Where(h => h.DocumentId == id)
                        .OrderByDescending(h => h.ActionDate)
                        .ToListAsync();

                    VersionesAnteriores = await _versionService.GetRealVersionHistoryAsync(Documento.DocumentCode, Documento.CompanyId, Documento.Id);
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSendReviewAsync(Guid id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Login");

            var result = await _versionService.AssignReviewVersionAsync(id, userId.Value);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;

            return RedirectToPage("/Documents/List");
        }

        public async Task<IActionResult> OnPostApproveAsync(Guid id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Login");

            var result = await _versionService.ReviewerApproveAsync(id, userId.Value);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;

            return RedirectToPage("/Documents/List");
        }

        public async Task<IActionResult> OnPostRejectAsync(Guid id, string rejectionComment)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Login");

            var doc = await _context.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
            var result = doc?.StatusId == DocumentWorkflowConstants.Status.Candidate
                ? await _versionService.AdminFinalizeRejectAsync(id, userId.Value, rejectionComment)
                : await _versionService.ReviewerRejectAsync(id, userId.Value, rejectionComment);

            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;

            return RedirectToPage("/Documents/List");
        }

        public async Task<IActionResult> OnPostFinalizeApproveAsync(Guid id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Login");

            var result = await _versionService.AdminFinalizeApproveAsync(id, userId.Value);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;

            return RedirectToPage("/Documents/List");
        }

        public async Task<IActionResult> OnPostSyncFirebaseAsync(Guid id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Login");

            var rolName = HttpContext.Session.GetString("Rol") ?? "";
            if (_permissions.IsOperador(rolName))
            {
                return BadRequest("El rol Operador no tiene permisos para realizar esta acción.");
            }

            var documento = _context.Documents.FirstOrDefault(d => d.Id == id);
            if (documento == null) return RedirectToPage();

            var usuario = await _context.Users.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == userId.Value);
            if (usuario == null || !_permissions.CanViewDocument(usuario, rolName, documento) || documento.StatusId != DocumentWorkflowConstants.Status.Active)
            {
                return BadRequest("No tienes permiso para sincronizar este documento.");
            }

            if (string.IsNullOrWhiteSpace(documento.DocumentCode))
            {
                TempData["ErrorMessage"] = "No se puede sincronizar con MongoDB: el documento no tiene codigo.";
                return RedirectToPage(new { id = id });
            }

            // Si ya está marcado como sincronizado, limpiar cualquier error residual y no repetir
            if (documento.SyncFirebase)
            {
                if (!string.IsNullOrEmpty(documento.LastErrorLog))
                {
                    documento.LastErrorLog = null;
                    await _context.SaveChangesAsync();
                }
                TempData["SuccessMessage"] = "Este documento ya estaba sincronizado con MongoDB.";
                return RedirectToPage(new { id = id });
            }

            var payload = DocumentSyncHelper.GenerateSyncPayload(documento, _env.WebRootPath);
            var mongoEndpoint = _configuration["MongoSync:Endpoint"] ?? "http://localhost:3000/api/documents";
            var mongoHealthEndpoint = _configuration["MongoSync:HealthEndpoint"]
                ?? DocumentSyncHelper.GetHealthEndpoint(mongoEndpoint);

            try
            {
                var client = new HttpClient();
                var healthResponse = await client.GetAsync(mongoHealthEndpoint);
                var healthBody = await healthResponse.Content.ReadAsStringAsync();

                if (!healthResponse.IsSuccessStatusCode)
                {
                    documento.LastErrorLog = healthBody;
                    TempData["ErrorMessage"] = "MongoDB API no respondio en /api/saludo: " + healthResponse.StatusCode;
                    await _context.SaveChangesAsync();
                    return RedirectToPage(new { id = id });
                }

                var response = await client.PostAsJsonAsync(mongoEndpoint, payload);

                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    documento.SyncFirebase = true;
                    documento.LastErrorLog = null;
                    TempData["SuccessMessage"] = "Sincronizado con MongoDB exitosamente.";
                }
                else
                {
                    // La API de MongoDB responde 409 cuando el id ya existe; para este flujo significa que ya esta sincronizado.
                    bool yaExiste = responseBody.Contains("ya existe", StringComparison.OrdinalIgnoreCase)
                                 || responseBody.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                                 || responseBody.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                                 || responseBody.Contains("duplicar", StringComparison.OrdinalIgnoreCase)
                                 || responseBody.Contains("No se permite", StringComparison.OrdinalIgnoreCase)
                                 || response.StatusCode == System.Net.HttpStatusCode.Conflict;

                    if (yaExiste)
                    {
                        documento.SyncFirebase = true;
                        documento.LastErrorLog = null;
                        TempData["SuccessMessage"] = "El documento ya estaba sincronizado con MongoDB.";
                    }
                    else
                    {
                        documento.LastErrorLog = responseBody;
                        TempData["ErrorMessage"] = "Error al sincronizar con MongoDB: " + response.StatusCode;
                    }
                }
            }
            catch (Exception ex)
            {
                documento.LastErrorLog = ex.Message;
                TempData["ErrorMessage"] = "Fallo de conexion al sincronizar con MongoDB.";
            }

            await _context.SaveChangesAsync();

            return RedirectToPage(new { id = id }); 
        }

        public async Task<IActionResult> OnPostSyncPostgreAsync(Guid id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Login");

            var rolName = HttpContext.Session.GetString("Rol") ?? "";
            if (rolName.Trim().Equals("Operador", StringComparison.OrdinalIgnoreCase) ||
                rolName.Trim().Equals("Operator", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("El rol Operador no tiene permisos para realizar esta acción.");
            }

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

            var rolName = HttpContext.Session.GetString("Rol") ?? "";
            if (_permissions.IsOperador(rolName))
            {
                return BadRequest("El rol Operador no tiene permisos para realizar esta acción.");
            }

            var documento = await _context.Documents.FirstOrDefaultAsync(d => d.Id == id);
            if (documento == null) return RedirectToPage();

            var currentUser = await _context.Users.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == userId);
            if (currentUser == null || !_permissions.CanEditDraft(currentUser, rolName, documento))
            {
                TempData["ErrorMessage"] = "No tienes permiso para editar este documento.";
                return RedirectToPage(new { id });
            }

            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.Id == EditDepartmentId && d.CompanyId == documento.CompanyId);
            if (department == null)
            {
                TempData["ErrorMessage"] = "El departamento seleccionado no pertenece a la empresa del documento.";
                return RedirectToPage(new { id });
            }

            documento.Title = EditTitle;
            documento.Description = EditDescription;
            documento.DepartmentId = department.Id;

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
