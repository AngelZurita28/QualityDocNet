using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Pages.Models;
using System.Net.Http;
using System.Net.Http.Json;
using QualityDoc.Helpers;

namespace QualityDoc.Pages.Documents
{
    public class ListModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebHostEnvironment _env;

        public List<Documento> Documentos { get; set; } = new();

        public int PageNumber { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 15; 

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? StatusFilter { get; set; } 

        public ListModel(AppDbContext context, IHttpClientFactory httpClientFactory, IWebHostEnvironment env)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _env = env;
        }

        public IActionResult OnGet(int pageNumber = 1)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToPage("/Login");

            var usuario = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (usuario == null) return RedirectToPage("/Login");

            var rolName = HttpContext.Session.GetString("Rol") ?? "";
            bool isSuperAdmin = rolName.Trim().Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) || 
                                rolName.Trim().Equals("Super Admin", StringComparison.OrdinalIgnoreCase) ||
                                rolName.Trim().Equals("Súperadmin", StringComparison.OrdinalIgnoreCase);
            bool isOperador = rolName.Trim().Equals("Operador", StringComparison.OrdinalIgnoreCase);

            PageNumber = pageNumber;

            var query = _context.Documents.AsQueryable();

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
                query = query.Where(d => d.Title.Contains(term) || d.DocumentCode.Contains(term));
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

        public async Task<IActionResult> OnPostSyncAsync(Guid id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToPage("/Login");

            var documento = _context.Documents.FirstOrDefault(d => d.Id == id);

            if (documento == null)
                return RedirectToPage();

            var payload = DocumentSyncHelper.GenerateSyncPayload(documento, _env.WebRootPath);

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

            return RedirectToPage(new { pageNumber = PageNumber }); 
        }
    }
}