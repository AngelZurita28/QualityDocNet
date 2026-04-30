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

        public List<Documento> Documentos { get; set; }

        public ListModel(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToPage("/Login");

            Documentos = _context.Documents.ToList();

            return Page();
        }
        public async Task<IActionResult> OnPostSyncAsync(Guid id)
        {
            var documento = _context.Documents.FirstOrDefault(d => d.Id == id);

            if (documento == null)
                return Page();

            var payload = new
            {
                Id = documento.Id,
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
                    keywords = new string[] { },
                    checksum = "N/A"
                }
            };

            var client = new HttpClient();

            var response = await client.PostAsJsonAsync("http://localhost:3000/api/documents", payload);

            if (response.IsSuccessStatusCode)
            {
                documento.SyncFirebase = true;
                _context.SaveChanges();
            }
            else
            {
                documento.LastErrorLog = await response.Content.ReadAsStringAsync();
                _context.SaveChanges();
            }

            return RedirectToPage();
        }
    }
}
