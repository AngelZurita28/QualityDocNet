using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QualityDoc.Data;
using QualityDoc.Pages.Models;
using System.Net.Http.Json;

namespace QualityDoc.Pages.Documents
{
    public class ApprovalModel : PageModel
    {
        private readonly AppDbContext _context;

        public Documento Documento { get; set; }

        public ApprovalModel(AppDbContext context)
        {
            _context = context;
        }

        public void OnGet(Guid? id)
        {
            if (id != null)
            {
                Documento = _context.Documents.FirstOrDefault(d => d.Id == id);
            }
        }

        public IActionResult OnPostApprove(Guid id)
        {
            var doc = _context.Documents.FirstOrDefault(d => d.Id == id);

            if (doc != null)
            {
                doc.StatusId = 2; 
                _context.SaveChanges();
            }

            return RedirectToPage(new { id = id }); 
        }

        public IActionResult OnPostReject(Guid id)
        {
            var doc = _context.Documents.FirstOrDefault(d => d.Id == id);

            if (doc != null)
            {
                doc.StatusId = 4; 
                _context.SaveChanges();
            }

            return RedirectToPage(new { id = id }); 
        }

        public async Task<IActionResult> OnPostSyncFirebaseAsync(Guid id)
        {
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
                CreatedAt = documento.CreatedAt
            };

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

            return RedirectToPage(new { id = id }); 
        }
    }
}