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
        public string Nombre { get; set; } = string.Empty;

        [BindProperty]
        public string Correo { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public int RolId { get; set; }

        public string Error { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;

        public RegisterModel(AppDbContext context)
        {
            _context = context;
        }

        public void OnGet() { }

        public IActionResult OnPost()
        {
            var existe = _context.Usuarios.Any(u => u.Correo == Correo);

            if (existe)
            {
                Error = "El correo ya está registrado";
                return Page();
            }

            var hash = PasswordHelper.HashPassword(Password);

            var usuario = new Usuario
            {
                Nombre = Nombre,
                Correo = Correo,
                PasswordHash = hash,
                RolId = RolId,
                Activo = true,
                FechaCreacion = DateTime.Now
            };

            _context.Usuarios.Add(usuario);
            _context.SaveChanges();

            Mensaje = "Usuario registrado correctamente";

            return RedirectToPage("/Index");
        }
    }
}
