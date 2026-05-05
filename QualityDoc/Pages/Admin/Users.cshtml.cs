using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Pages.Models;

namespace QualityDoc.Pages.Admin
{
    public class UsersModel : PageModel
    {
        private readonly AppDbContext _context;

        public UsersModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Usuario> Usuarios { get; set; } = new();
        public SelectList RolesList { get; set; }
        public SelectList CompaniesList { get; set; }
        public string MensajeExito { get; set; } = string.Empty;

        public int PageNumber { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 10;

        public async Task<IActionResult> OnGetAsync(string? msj, int pageNumber = 1)
        {
            var rol = HttpContext.Session.GetString("Rol");
            if (rol == null) return RedirectToPage("/Login");

            bool isSuperAdmin = rol.Trim().Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) || 
                                rol.Trim().Equals("Super Admin", StringComparison.OrdinalIgnoreCase) || 
                                rol.Trim().Equals("Súperadmin", StringComparison.OrdinalIgnoreCase);
            bool isAdmin = rol.Trim().Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                           rol.Trim().Equals("Administrador", StringComparison.OrdinalIgnoreCase);

            if (!isSuperAdmin && !isAdmin)
            {
                return RedirectToPage("/Index");
            }

            if (!string.IsNullOrEmpty(msj))
            {
                MensajeExito = msj;
            }

            PageNumber = pageNumber;
            var query = _context.Users.AsQueryable();

            int totalRecords = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(totalRecords / (double)PageSize);

            Usuarios = await query
                .Include(u => u.Rol)
                .Include(u => u.Company)
                .OrderBy(u => u.FullName)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            RolesList = new SelectList(await _context.Roles.ToListAsync(), "Id", "Name");
            CompaniesList = new SelectList(await _context.Companies.ToListAsync(), "Id", "Name");

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateUserAsync(int userId, int roleId, int companyId, bool isActive)
        {
            var rol = HttpContext.Session.GetString("Rol");
            bool isSuperAdmin = rol?.Trim().Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) == true || 
                                rol?.Trim().Equals("Super Admin", StringComparison.OrdinalIgnoreCase) == true ||
                                rol?.Trim().Equals("Súperadmin", StringComparison.OrdinalIgnoreCase) == true;
            bool isAdmin = rol?.Trim().Equals("Admin", StringComparison.OrdinalIgnoreCase) == true ||
                           rol?.Trim().Equals("Administrador", StringComparison.OrdinalIgnoreCase) == true;

            if (!isSuperAdmin && !isAdmin) return Unauthorized();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                user.RoleId = roleId;
                user.CompanyId = companyId;
                user.IsActive = isActive;
                await _context.SaveChangesAsync();
            }

            return RedirectToPage(new { msj = "Usuario actualizado correctamente." });
        }
    }
}
