using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QualityDoc.Data;
using QualityDoc.Pages.Models;
using System.Net.Http;
using System.Net.Http.Json;

namespace QualityDoc.Pages.Documents
{
    public class ListModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public List<Documento> Documentos { get; set; } = new();

        public int PageNumber { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 10; 

        public ListModel(AppDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult OnGet(int pageNumber = 1)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToPage("/Login");

            PageNumber = pageNumber;

            var query = _context.Documents.AsQueryable();

            int totalRecords = query.Count();

            TotalPages = (int)Math.Ceiling(totalRecords / (double)PageSize);

            Documentos = query
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

            return RedirectToPage(new { pageNumber = PageNumber }); 
        }
    }
}