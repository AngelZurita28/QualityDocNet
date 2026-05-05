using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Helpers;
using QualityDoc.Pages.Models;

namespace QualityDoc.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly AppDbContext _context;

        public RegisterModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public string FullName { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public int RoleId { get; set; }

        [BindProperty]
        public int CompanyId { get; set; }

        public SelectList Empresas { get; set; }
        public SelectList Roles { get; set; }

        public string Error { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            await LoadData();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (CompanyId == 0)
            {
                ModelState.AddModelError("CompanyId", "Debe seleccionar una empresa válida.");
                await LoadData();
                return Page();
            }

            if (RoleId == 0)
            {
                ModelState.AddModelError("RoleId", "Debe seleccionar un rol válido.");
                await LoadData();
                return Page();
            }

            Email = Email?.Trim().ToLower() ?? string.Empty;
            FullName = FullName?.Trim() ?? string.Empty;

            var existe = await _context.Users.AnyAsync(u => u.Email == Email);
            if (existe)
            {
                Error = "El correo ya está registrado";
                await LoadData();
                return Page();
            }

            var hash = PasswordHelper.HashPassword(Password);

            var usuario = new Usuario
            {
                FullName = FullName,
                Email = Email,
                PasswordHash = hash,
                RoleId = RoleId,
                CompanyId = CompanyId,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(usuario);
            await _context.SaveChangesAsync();

            var role = await _context.Roles.FindAsync(RoleId);
            var roleName = role?.Name ?? "";
            
            var company = await _context.Companies.FindAsync(CompanyId);
            var companyName = company?.Name ?? "Sin Empresa";

            HttpContext.Session.SetInt32("UserId", usuario.Id);
            HttpContext.Session.SetString("Usuario", usuario.FullName);
            HttpContext.Session.SetString("Rol", roleName);
            HttpContext.Session.SetString("Empresa", companyName);

            return RedirectToPage("/Index");
        }

        private async Task LoadData()
        {
            var companies = await _context.Companies.Where(c => c.IsActive).ToListAsync();
            Empresas = new SelectList(companies, "Id", "Name");

            var roles = await _context.Roles.ToListAsync();
            Roles = new SelectList(roles, "Id", "Name");
        }
    }
}