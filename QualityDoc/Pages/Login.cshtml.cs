using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Helpers;

namespace QualityDoc.Pages; 

public class LoginModel : PageModel
{
    private readonly AppDbContext _context;

    [BindProperty]
    public string Correo { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string Error { get; set; } = string.Empty;

    public LoginModel(AppDbContext context)
    {
        _context = context;
    }

    public void OnGet() { }

    public IActionResult OnPost()
    {
        Correo = Correo?.Trim();

        var hash = PasswordHelper.HashPassword(Password);

        var usuario = _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefault(u => u.Correo == Correo
                              && u.PasswordHash == hash
                              && u.Activo == true); 

        if (usuario == null)
        {
            Error = "Correo o contraseña incorrectos";
            return Page();
        }

        // Guardar sesión
        HttpContext.Session.SetString("Usuario", usuario.Nombre);
        HttpContext.Session.SetString("Rol", usuario.Rol.Nombre);

        return RedirectToPage("/Index");
    }
}