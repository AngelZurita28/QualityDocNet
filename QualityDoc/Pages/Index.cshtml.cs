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

        public int CountTotal { get; set; }
        public int CountBorrador { get; set; }
        public int CountRevision { get; set; }
        public int CountAprobado { get; set; }
        public int CountRechazado { get; set; }

        public List<DepartmentStat> DeptStats { get; set; } = new();
        public List<Department> DepartmentsList { get; set; } = new();

        public class DepartmentStat
        {
            public string Name { get; set; } = "";
            public int Count { get; set; }
            public int Borrador { get; set; }
            public int Revision { get; set; }
            public int Aprobado { get; set; }
            public int Rechazado { get; set; }
        }

        public IndexModel(AppDbContext context, IWebHostEnvironment env, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _env = env;
            _httpClientFactory = httpClientFactory;
        }

        [BindProperty]
        public string Title { get; set; } = string.Empty;



        [BindProperty]
        public string Description { get; set; } = string.Empty;

        [BindProperty]
        public IFormFile UploadFile { get; set; }

        [BindProperty]
        public bool EsNuevaVersion { get; set; }

        [BindProperty]
        public string? CodigoExistente { get; set; }

        [BindProperty]
        public int SelectedDepartmentId { get; set; }

        public async Task<IActionResult> OnGetAsync()
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

            CountTotal = await query.CountAsync();
            CountBorrador = await query.CountAsync(d => d.StatusId == 1);
            CountRevision = await query.CountAsync(d => d.StatusId == 2);
            CountAprobado = await query.CountAsync(d => d.StatusId == 3);
            CountRechazado = await query.CountAsync(d => d.StatusId == 4);

            DepartmentsList = await _context.Departments.ToListAsync();

            var docs = await query.Include(d => d.Department).ToListAsync();
            DeptStats = DepartmentsList.Select(dept => new DepartmentStat
            {
                Name = dept.Name,
                Count = docs.Count(d => d.DepartmentId == dept.Id),
                Borrador = docs.Count(d => d.DepartmentId == dept.Id && d.StatusId == 1),
                Revision = docs.Count(d => d.DepartmentId == dept.Id && d.StatusId == 2),
                Aprobado = docs.Count(d => d.DepartmentId == dept.Id && d.StatusId == 3),
                Rechazado = docs.Count(d => d.DepartmentId == dept.Id && d.StatusId == 4)
            }).ToList();

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
                DepartmentId = SelectedDepartmentId,
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


    }
}