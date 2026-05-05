using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Pages.Models;
using QualityDoc.Helpers;

namespace QualityDoc.Pages
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;

        public List<Documento> Documentos { get; set; } = new();
        
        public int PageNumber { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 15;

        public IndexModel(AppDbContext context, IWebHostEnvironment env, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _env = env;
            _httpClientFactory = httpClientFactory;
        }

        [BindProperty]
        public string Title { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty]
        public string Description { get; set; } = string.Empty;

        [BindProperty]
        public IFormFile UploadFile { get; set; }

        [BindProperty]
        public bool EsNuevaVersion { get; set; }

        [BindProperty]
        public string? CodigoExistente { get; set; }

        public async Task<IActionResult> OnGetAsync(int pageNumber = 1)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Login");

            var usuario = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (usuario == null) return RedirectToPage("/Login");

            var rolName = HttpContext.Session.GetString("Rol") ?? "";
            bool isSuperAdmin = rolName.Trim().Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) || 
                                rolName.Trim().Equals("Super Admin", StringComparison.OrdinalIgnoreCase) ||
                                rolName.Trim().Equals("Súperadmin", StringComparison.OrdinalIgnoreCase);
            bool isOperador = rolName.Trim().Equals("Operador", StringComparison.OrdinalIgnoreCase);

            PageNumber = pageNumber;

            IQueryable<Documento> query = _context.Documents.Where(d => d.IsLatest);

            if (isSuperAdmin)
            {
                // Súperadmin ve los de cualquier empresa
            }
            else if (isOperador)
            {
                // Operador solo ve sus propios documentos en el Dashboard
                query = query.Where(d => d.AuthorId == userId.Value);
            }
            else
            {
                // Aprobadores y Admins ven los de su empresa
                query = query.Where(d => d.CompanyId == usuario.CompanyId);
            }

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim();
                query = query.Where(d => d.Title.Contains(term) || 
                                         d.DocumentCode.Contains(term) ||
                                         (d.Description != null && d.Description.Contains(term)));
            }

            int totalRecords = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(totalRecords / (double)PageSize);

            Documentos = await query
                .Include(d => d.Author)
                .Include(d => d.Company)
                .OrderByDescending(d => d.CreatedAt)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Unauthorized();

            var usuario = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (usuario == null || usuario.CompanyId == null) 
                return BadRequest("Usuario sin empresa asignada.");

            string filePath = "";
            if (UploadFile != null)
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(UploadFile.FileName);
                var fullPath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await UploadFile.CopyToAsync(stream);
                }
                filePath = "/uploads/" + fileName;
            }

            string documentCode;
            int version = 1;
            Guid? parentId = null;

            if (EsNuevaVersion && !string.IsNullOrEmpty(CodigoExistente))
            {
                var docBase = await _context.Documents
                    .Where(d => d.DocumentCode == CodigoExistente)
                    .OrderByDescending(d => d.VersionNumber)
                    .FirstOrDefaultAsync();

                if (docBase == null) return BadRequest("Documento base no encontrado.");

                parentId = docBase.Id;

                bool existeVersionSiguiente = await _context.Documents.AnyAsync(d => d.ParentId == parentId);
                if (existeVersionSiguiente)
                {
                    return BadRequest("Ya existe una nueva versión en progreso o aprobada derivada de este documento base.");
                }

                documentCode = docBase.DocumentCode!;
                version = docBase.VersionNumber + 1;

                var anteriores = _context.Documents.Where(d => d.DocumentCode == documentCode);
                foreach (var d in anteriores) d.IsLatest = false;
            }
            else
            {
                documentCode = "DOC-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
            }

            var documento = new Documento
            {
                Id = Guid.NewGuid(),
                DocumentCode = documentCode,
                Title = Title,
                Description = Description,
                FilePath = filePath,
                VersionNumber = version,
                IsLatest = true,
                ParentId = parentId,
                AuthorId = usuario.Id,
                CompanyId = (int)usuario.CompanyId,
                StatusId = 1,
                CreatedAt = DateTime.Now
            };

            _context.Documents.Add(documento);

            var history = new ApprovalHistory
            {
                DocumentId = documento.Id,
                UserId = usuario.Id,
                Action = EsNuevaVersion ? "Creado (Nueva Versión)" : "Creado",
                Comment = "Documento redactado y guardado como borrador.",
                ActionDate = DateTime.Now
            };
            _context.ApprovalHistory.Add(history);

            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostSyncAsync(Guid id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Login");

            var documento = await _context.Documents.FirstOrDefaultAsync(d => d.Id == id);
            if (documento == null) return RedirectToPage();

            var payload = DocumentSyncHelper.GenerateSyncPayload(documento, _env.WebRootPath);

            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.PostAsJsonAsync("http://localhost:3000/api/documents", payload);
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
            return RedirectToPage();
        }
    }
}