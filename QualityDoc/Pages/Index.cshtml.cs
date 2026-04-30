using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QualityDoc.Data;
using QualityDoc.Pages.Models;

namespace QualityDoc.Pages
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public List<Documento> Documentos { get; set; } = new();

        public IndexModel(AppDbContext context)
        {
            _context = context;
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

    }
}