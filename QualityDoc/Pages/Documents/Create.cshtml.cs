using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QualityDoc.Data;
using QualityDoc.Pages.Models;

namespace QualityDoc.Pages.Documents
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public CreateModel(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [BindProperty]
        public string Title { get; set; } = string.Empty;

        [BindProperty]
        public string Description { get; set; } = string.Empty;

        [BindProperty]
        public IFormFile File { get; set; }

        public string Error { get; set; } = string.Empty;

        [BindProperty]
        public bool EsNuevaVersion { get; set; }

        [BindProperty]
        public string? CodigoExistente { get; set; }

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToPage("/Login");

            return Page();
        }

        public IActionResult OnPost()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToPage("/Login");

            var usuario = _context.Users
                .FirstOrDefault(u => u.Id == userId);

            if (usuario == null)
                return RedirectToPage("/Login");

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

            string documentCode;
            int version = 1;
            Guid? parentId = null;

            if (EsNuevaVersion && !string.IsNullOrEmpty(CodigoExistente))
            {
                var documentoBase = _context.Documents
                    .Where(d => d.DocumentCode == CodigoExistente)
                    .OrderByDescending(d => d.VersionNumber)
                    .FirstOrDefault();

                if (documentoBase == null)
                {
                    Error = "No existe un documento con ese código";
                    return Page();
                }

                documentCode = documentoBase.DocumentCode!;
                version = documentoBase.VersionNumber + 1;
                parentId = documentoBase.ParentId ?? documentoBase.Id;

                var anteriores = _context.Documents
                    .Where(d => d.DocumentCode == documentCode);

                foreach (var doc in anteriores)
                {
                    doc.IsLatest = false;
                }
            }
            else
            {
                documentCode = "DOC-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
            }

            var documento = new Documento
            {
                DocumentCode = documentCode,
                ParentId = parentId,
                VersionNumber = version,
                IsLatest = true,

                Title = Title,
                Description = Description,
                FilePath = filePath,
                AuthorId = usuario.Id,
                CompanyId = usuario.CompanyId,
                StatusId = 1,
                CreatedAt = DateTime.Now
            };

            _context.Documents.Add(documento);
            _context.SaveChanges();

            return RedirectToPage("/Documents/List");
        }
    }
}