using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QualityDoc.Data;
using QualityDoc.Pages.Models;
using System.Net.Http.Json;

namespace QualityDoc.Pages
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;

        public List<Documento> Documentos { get; set; } = new();

        public IndexModel(AppDbContext context, IWebHostEnvironment env, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _env = env;
            _httpClientFactory = httpClientFactory;
        }

        [BindProperty]
        public string Title { get; set; }

        [BindProperty]
        public string Description { get; set; }

        [BindProperty]
        public IFormFile File { get; set; }

        [BindProperty]
        public bool EsNuevaVersion { get; set; }

        [BindProperty]
        public string? CodigoExistente { get; set; }

        public async Task<IActionResult> OnPostSyncAsync(Guid id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Login");

            var documento = _context.Documents.FirstOrDefault(d => d.Id == id);
            if (documento == null) return RedirectToPage();

            var payload = new
            {
                Id = documento.Id.ToString().ToUpper(),
                ParentId = documento.ParentId,
                VersionNumber = documento.VersionNumber,
                IsLatest = documento.IsLatest,
                Title = documento.Title,
                Description = documento.Description,
                FilePath = documento.FilePath,
                AuthorId = documento.AuthorId,
                StatusId = documento.StatusId,
                CompanyId = documento.CompanyId,
                CreatedAt = documento.CreatedAt,
                metadata = new
                {
                    fileSize = "N/A",
                    pages = 0,
                    checksum = "N/A"
                }
            };

            try
            {
                var client = _httpClientFactory.CreateClient(); 
                var response = await client.PostAsJsonAsync(
                    "http://localhost:3000/api/documents",
                    payload
                );

                if (response.IsSuccessStatusCode)
                {
                    documento.SyncFirebase = true;
                    documento.LastErrorLog = null;
                }
                else
                {
                    documento.LastErrorLog = await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                documento.LastErrorLog = ex.Message;
            }

            await _context.SaveChangesAsync();

            return RedirectToPage(); 
        }

        public IActionResult OnGet()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToPage("/Login");

            var usuario = _context.Users
                .FirstOrDefault(u => u.Id == userId);

            if (usuario == null)
                return RedirectToPage("/Login");

            ViewData["Empresa"] = usuario.CompanyId;

            if (usuario.CompanyId == null)
            {
                Documentos = _context.Documents
                    .Where(d => d.IsLatest)
                    .ToList();
            }
            else
            {
                Documentos = _context.Documents
                    .Where(d => d.CompanyId == usuario.CompanyId && d.IsLatest)
                    .ToList();
            }

            return Page();
        }

        public IActionResult OnPostCreate()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToPage("/Login");

            var usuario = _context.Users
                .FirstOrDefault(u => u.Id == userId);

            if (usuario == null)
                return RedirectToPage("/Login");

            if (EsNuevaVersion && string.IsNullOrEmpty(CodigoExistente))
            {
                ModelState.AddModelError("", "Debes ingresar el código del documento base.");
                return Page();
            }

            string filePath = "";

            if (File != null)
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads");

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(File.FileName);
                var fullPath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    File.CopyTo(stream);
                }

                filePath = "/uploads/" + fileName;
            }

            int version = 1;

            if (EsNuevaVersion)
            {
                var docBase = _context.Documents
                    .FirstOrDefault(d => d.Id.ToString() == CodigoExistente);

                if (docBase != null)
                {
                    version = docBase.VersionNumber + 1;
                    docBase.IsLatest = false;
                }
            }

            var documento = new Documento
            {
                Title = Title,
                Description = Description,
                FilePath = filePath,
                AuthorId = usuario.Id,
                CompanyId = usuario.CompanyId,
                StatusId = 1,
                VersionNumber = version,
                IsLatest = true,
                CreatedAt = DateTime.Now
            };

            _context.Documents.Add(documento);
            _context.SaveChanges();

            return RedirectToPage();
        }
    }
}