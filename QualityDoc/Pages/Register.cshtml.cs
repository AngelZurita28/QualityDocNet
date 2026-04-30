using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QualityDoc.Data;
using QualityDoc.Helpers;
using QualityDoc.Pages.Models;

namespace QualityDoc.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly AppDbContext _context;

        [BindProperty]
        public string FullName { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public int RoleId { get; set; }

        public string Error { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;

        public RegisterModel(AppDbContext context)
        {
            _context = context;
        }

        public void OnGet() { }

        public IActionResult OnPost()
        {
            var existe = _context.Users.Any(u => u.Email == Email);

            if (existe)
            {
                Error = "El correo ya está registrado";
                return Page();
            }

            var hash = PasswordHelper.HashPassword(Password);

            var usuario = new Usuario
            {
                FullName = FullName,
                Email = Email,
                PasswordHash = hash,
                RoleId = RoleId,
                IsActive = true,
                CreatedAt = DateTime.Now,
                CompanyId = null 
            };

            _context.Users.Add(usuario);
            _context.SaveChanges();

            HttpContext.Session.SetInt32("UserId", usuario.Id);
            HttpContext.Session.SetString("Usuario", usuario.FullName);
            HttpContext.Session.SetString("Rol", usuario.RoleId.ToString());

            return RedirectToPage("/Index");
        }
    }
}