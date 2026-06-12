using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Pages.Models;
using QualityDoc.Helpers;
using QualityDoc.Services;

namespace QualityDoc.Pages
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly RolePermissionService _permissions;

        public int CountTotal { get; set; }
        public int CountBorrador { get; set; }
        public int CountRevision { get; set; }
        public int CountCandidata { get; set; }
        public int CountRechazado { get; set; }
        public int CountActivo { get; set; }
        public int CountObsoleto { get; set; }
        public bool CanCreateDraft { get; set; }

        public List<DepartmentStat> DeptStats { get; set; } = new();
        public List<Department> DepartmentsList { get; set; } = new();

        public class DepartmentStat
        {
            public string Name { get; set; } = "";
            public int Count { get; set; }
            public int Borrador { get; set; }
            public int Revision { get; set; }
            public int Candidata { get; set; }
            public int Rechazado { get; set; }
            public int Activo { get; set; }
            public int Obsoleto { get; set; }
        }

        public IndexModel(AppDbContext context, IWebHostEnvironment env, IHttpClientFactory httpClientFactory, RolePermissionService permissions)
        {
            _context = context;
            _env = env;
            _httpClientFactory = httpClientFactory;
            _permissions = permissions;
        }

        [BindProperty]
        public string Title { get; set; } = string.Empty;



        [BindProperty]
        public string Description { get; set; } = string.Empty;

        [BindProperty]
        public IFormFile? UploadFile { get; set; }

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
            CanCreateDraft = _permissions.CanCreateDraft(rolName);

            IQueryable<Documento> query = _context.Documents;

            if (_permissions.IsSuperAdmin(rolName))
            {
                // Súperadmin ve los de cualquier empresa
            }
            else if (_permissions.IsRedacter(rolName))
            {
                // Redacter solo ve sus propios documentos
                query = query.Where(d => d.AuthorId == userId.Value);
            }
            else if (_permissions.IsOperador(rolName))
            {
                // Operador no puede ver nada
                query = query.Where(d => false);
            }
            else
            {
                // Aprobadores y Admins ven los de su empresa
                query = query.Where(d => d.CompanyId == usuario.CompanyId);
            }

            CountTotal = await query.CountAsync();
            CountBorrador = await query.CountAsync(d => d.StatusId == DocumentWorkflowConstants.Status.Draft);
            CountRevision = await query.CountAsync(d => d.StatusId == DocumentWorkflowConstants.Status.InReview);
            CountCandidata = await query.CountAsync(d => d.StatusId == DocumentWorkflowConstants.Status.Candidate);
            CountRechazado = await query.CountAsync(d => d.StatusId == DocumentWorkflowConstants.Status.Rejected);
            CountActivo = await query.CountAsync(d => d.StatusId == DocumentWorkflowConstants.Status.Active);
            CountObsoleto = await query.CountAsync(d => d.StatusId == DocumentWorkflowConstants.Status.Obsolete);

            DepartmentsList = _permissions.IsSuperAdmin(rolName)
                ? await _context.Departments.ToListAsync()
                : await _context.Departments.Where(d => d.CompanyId == usuario.CompanyId).ToListAsync();

            var docs = await query.Include(d => d.Department).ToListAsync();
            DeptStats = DepartmentsList.Select(dept => new DepartmentStat
            {
                Name = dept.Name,
                Count = docs.Count(d => d.DepartmentId == dept.Id),
                Borrador = docs.Count(d => d.DepartmentId == dept.Id && d.StatusId == DocumentWorkflowConstants.Status.Draft),
                Revision = docs.Count(d => d.DepartmentId == dept.Id && d.StatusId == DocumentWorkflowConstants.Status.InReview),
                Candidata = docs.Count(d => d.DepartmentId == dept.Id && d.StatusId == DocumentWorkflowConstants.Status.Candidate),
                Rechazado = docs.Count(d => d.DepartmentId == dept.Id && d.StatusId == DocumentWorkflowConstants.Status.Rejected),
                Activo = docs.Count(d => d.DepartmentId == dept.Id && d.StatusId == DocumentWorkflowConstants.Status.Active),
                Obsoleto = docs.Count(d => d.DepartmentId == dept.Id && d.StatusId == DocumentWorkflowConstants.Status.Obsolete)
            }).ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Unauthorized();

            var rolName = HttpContext.Session.GetString("Rol") ?? "";
            if (!_permissions.CanCreateDraft(rolName))
            {
                return BadRequest("Tu rol no tiene permisos para crear borradores.");
            }

            var usuario = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (usuario == null || usuario.CompanyId == null) 
                return BadRequest("Usuario sin empresa asignada.");

            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.Id == SelectedDepartmentId && d.CompanyId == usuario.CompanyId);
            if (department == null)
            {
                return BadRequest("El departamento seleccionado no pertenece a tu empresa.");
            }

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
            decimal? version = null;
            Guid? parentId = null;

            if (EsNuevaVersion && !string.IsNullOrEmpty(CodigoExistente))
            {
                var docBase = await _context.Documents
                    .Where(d => d.DocumentCode == CodigoExistente && d.CompanyId == usuario.CompanyId)
                    .OrderByDescending(d => d.CreatedAt)
                    .FirstOrDefaultAsync();

                if (docBase == null) return BadRequest("Documento base no encontrado.");

                parentId = docBase.Id;
                documentCode = docBase.DocumentCode!;
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
                IsLatest = false,
                ParentId = parentId,
                AuthorId = usuario.Id,
                CompanyId = (int)usuario.CompanyId,
                DepartmentId = SelectedDepartmentId,
                StatusId = DocumentWorkflowConstants.Status.Draft,
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
