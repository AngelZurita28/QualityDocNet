using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Pages.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using QualityDoc.Helpers;
using QualityDoc.Services;

namespace QualityDoc.Pages.Documents
{
    public class ListModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;
        private readonly RolePermissionService _permissions;

        public List<Documento> Documentos { get; set; } = new();

        public int PageNumber { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 15; 
        public string PageTitle { get; set; } = "Documentos";
        public string PageDescription { get; set; } = "Listado de documentos";
        public List<StatusOption> StatusOptions { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? StatusFilter { get; set; } 

        public class StatusOption
        {
            public int Id { get; set; }
            public string Label { get; set; } = string.Empty;
        }

        public ListModel(AppDbContext context, IHttpClientFactory httpClientFactory, IWebHostEnvironment env, IConfiguration configuration, RolePermissionService permissions)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _env = env;
            _configuration = configuration;
            _permissions = permissions;
        }

        public IActionResult OnGet(int pageNumber = 1)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToPage("/Login");

            var usuario = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (usuario == null) return RedirectToPage("/Login");

            var rolName = HttpContext.Session.GetString("Rol") ?? "";
            PageNumber = pageNumber;

            var query = _context.Documents.AsQueryable();
            List<int> allowedStatuses;
            int? defaultStatus = null;

            if (_permissions.IsSuperAdmin(rolName))
            {
                PageTitle = "Aprobacion de Documentos";
                PageDescription = "Candidatos pendientes y listas de versiones finales";
                allowedStatuses = new List<int>
                {
                    DocumentWorkflowConstants.Status.Candidate,
                    DocumentWorkflowConstants.Status.Active,
                    DocumentWorkflowConstants.Status.Rejected,
                    DocumentWorkflowConstants.Status.Obsolete
                };
                defaultStatus = DocumentWorkflowConstants.Status.Candidate;
            }
            else if (_permissions.IsRedacter(rolName))
            {
                PageTitle = "Mis Documentos";
                PageDescription = "Documentos redactados por ti";
                query = query.Where(d => d.AuthorId == userId.Value);
                allowedStatuses = new List<int>
                {
                    DocumentWorkflowConstants.Status.Draft,
                    DocumentWorkflowConstants.Status.InReview,
                    DocumentWorkflowConstants.Status.Candidate,
                    DocumentWorkflowConstants.Status.Rejected,
                    DocumentWorkflowConstants.Status.Active,
                    DocumentWorkflowConstants.Status.Obsolete
                };
            }
            else if (_permissions.IsOperador(rolName))
            {
                PageTitle = "Documentos";
                PageDescription = "El rol operador no tiene documentos asignados";
                query = query.Where(d => false);
                allowedStatuses = new List<int>();
            }
            else if (_permissions.IsReviewer(rolName))
            {
                PageTitle = "Revision de Documentos";
                PageDescription = "Documentos pendientes de revision y listas de consulta";
                query = query.Where(d => d.CompanyId == usuario.CompanyId);
                allowedStatuses = new List<int>
                {
                    DocumentWorkflowConstants.Status.InReview,
                    DocumentWorkflowConstants.Status.Active,
                    DocumentWorkflowConstants.Status.Rejected,
                    DocumentWorkflowConstants.Status.Obsolete
                };
                defaultStatus = DocumentWorkflowConstants.Status.InReview;
            }
            else
            {
                PageTitle = "Aprobacion de Documentos";
                PageDescription = "Documentos pendientes de aprobacion final y listas de consulta";
                query = query.Where(d => d.CompanyId == usuario.CompanyId);
                allowedStatuses = new List<int>
                {
                    DocumentWorkflowConstants.Status.Candidate,
                    DocumentWorkflowConstants.Status.Active,
                    DocumentWorkflowConstants.Status.Rejected,
                    DocumentWorkflowConstants.Status.Obsolete
                };
                defaultStatus = DocumentWorkflowConstants.Status.Candidate;
            }

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim();
                query = query.Where(d => d.Title.Contains(term) || (d.DocumentCode != null && d.DocumentCode.Contains(term)));
            }

            StatusOptions = BuildStatusOptions(allowedStatuses, rolName);

            if (StatusFilter.HasValue && !allowedStatuses.Contains(StatusFilter.Value))
            {
                StatusFilter = defaultStatus;
            }

            if (!StatusFilter.HasValue && defaultStatus.HasValue)
            {
                StatusFilter = defaultStatus;
            }

            if (allowedStatuses.Any())
            {
                query = query.Where(d => allowedStatuses.Contains(d.StatusId));
            }

            if (StatusFilter.HasValue && StatusFilter.Value > 0)
            {
                query = query.Where(d => d.StatusId == StatusFilter.Value);
            }

            int totalRecords = query.Count();

            TotalPages = (int)Math.Ceiling(totalRecords / (double)PageSize);
            if (TotalPages == 0) TotalPages = 1; // Prevent page 0

            Documentos = query
                .Include(d => d.Author)
                .Include(d => d.Company)
                .Include(d => d.Status)
                .OrderByDescending(d => d.CreatedAt) 
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            return Page();
        }

        private static List<StatusOption> BuildStatusOptions(List<int> statuses, string roleName)
        {
            bool isReviewer = roleName.Trim().Equals("Reviewer", StringComparison.OrdinalIgnoreCase);
            bool isAdminLike = roleName.Trim().Equals("Admin", StringComparison.OrdinalIgnoreCase)
                || roleName.Trim().Equals("Administrador", StringComparison.OrdinalIgnoreCase)
                || roleName.Trim().Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
                || roleName.Trim().Equals("Super Admin", StringComparison.OrdinalIgnoreCase)
                || roleName.Trim().Equals("Súperadmin", StringComparison.OrdinalIgnoreCase);

            return statuses.Select(status => new StatusOption
            {
                Id = status,
                Label = status switch
                {
                    DocumentWorkflowConstants.Status.Draft => "Borradores",
                    DocumentWorkflowConstants.Status.InReview => isReviewer ? "Pendientes de revision" : "En revision",
                    DocumentWorkflowConstants.Status.Candidate => isAdminLike ? "Pendientes de aprobacion" : "Candidatas",
                    DocumentWorkflowConstants.Status.Rejected => "Rechazados",
                    DocumentWorkflowConstants.Status.Active => "Vigentes",
                    DocumentWorkflowConstants.Status.Obsolete => "Obsoletos",
                    _ => "Estado"
                }
            }).ToList();
        }

        public async Task<IActionResult> OnPostSyncAsync(Guid id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToPage("/Login");

            var rolName = HttpContext.Session.GetString("Rol") ?? "";
            if (_permissions.IsOperador(rolName))
            {
                return BadRequest("El rol Operador no tiene permisos para sincronizar.");
            }

            var documento = _context.Documents.FirstOrDefault(d => d.Id == id);

            if (documento == null)
                return RedirectToPage();

            var usuario = await _context.Users.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == userId.Value);
            if (usuario == null || !_permissions.CanViewDocument(usuario, rolName, documento) || documento.StatusId != DocumentWorkflowConstants.Status.Active)
            {
                return BadRequest("No tienes permiso para sincronizar este documento.");
            }

            if (string.IsNullOrWhiteSpace(documento.DocumentCode))
            {
                TempData["ErrorMessage"] = "No se puede sincronizar con MongoDB: el documento no tiene codigo.";
                return RedirectToPage(new { pageNumber = PageNumber });
            }

            // Si ya esta sincronizado, no hacer nada (por si el boton se pica dos veces)
            if (documento.SyncFirebase)
            {
                TempData["SuccessMessage"] = "Este documento ya estaba sincronizado con MongoDB.";
                return RedirectToPage(new { pageNumber = PageNumber });
            }

            var payload = DocumentSyncHelper.GenerateSyncPayload(documento, _env.WebRootPath);
            var mongoEndpoint = _configuration["MongoSync:Endpoint"] ?? "http://localhost:3000/api/documents";
            var mongoHealthEndpoint = _configuration["MongoSync:HealthEndpoint"]
                ?? DocumentSyncHelper.GetHealthEndpoint(mongoEndpoint);

            try
            {
                var client = _httpClientFactory.CreateClient();

                var healthResponse = await client.GetAsync(mongoHealthEndpoint);
                var healthBody = await healthResponse.Content.ReadAsStringAsync();

                if (!healthResponse.IsSuccessStatusCode)
                {
                    documento.LastErrorLog = healthBody;
                    TempData["ErrorMessage"] = "MongoDB API no respondio en /api/saludo: " + healthResponse.StatusCode;
                    await _context.SaveChangesAsync();
                    return RedirectToPage(new { pageNumber = PageNumber });
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

            return RedirectToPage(new { pageNumber = PageNumber }); 

        }

        public async Task<IActionResult> OnPostSyncPostgreAsync(Guid id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToPage("/Login");

            var rolName = HttpContext.Session.GetString("Rol") ?? "";
            if (_permissions.IsOperador(rolName))
            {
                return BadRequest("El rol Operador no tiene permisos para sincronizar.");
            }

            var documento = _context.Documents
                .Include(d => d.Company)
                .Include(d => d.Status)
                .FirstOrDefault(d => d.Id == id);

            if (documento == null)
                return RedirectToPage();

            var usuario = await _context.Users.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == userId.Value);
            if (usuario == null || !_permissions.CanViewDocument(usuario, rolName, documento) || documento.StatusId != DocumentWorkflowConstants.Status.Active)
            {
                return BadRequest("No tienes permiso para sincronizar este documento.");
            }

            var connString = _configuration.GetConnectionString("PostgresConnection");
            if (string.IsNullOrEmpty(connString))
            {
                TempData["ErrorMessage"] = "Cadena de conexión a PostgreSQL no configurada.";
                return RedirectToPage(new { pageNumber = PageNumber });
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
            return RedirectToPage(new { pageNumber = PageNumber });
        }
    }
}
