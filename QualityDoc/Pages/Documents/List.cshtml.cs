using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
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
        public int PageSize { get; set; } = 15; 

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? StatusFilter { get; set; } 

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

            var query = _context.Documents.Where(d => d.AuthorId == userId.Value);

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