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
        Correo = Correo?.Trim().ToLower();

        var hash = PasswordHelper.HashPassword(Password);

        var usuario = _context.Users
             .Include(u => u.Rol)
             .Include(u => u.Company)
             .FirstOrDefault(u => u.Email == Correo
                      && u.PasswordHash == hash
                      && u.IsActive);


        if (usuario == null)
        {
            Error = "Correo o contraseña incorrectos";
            return Page();
        }

        HttpContext.Session.SetInt32("UserId", usuario.Id);
        HttpContext.Session.SetString("Usuario", usuario.FullName);
        HttpContext.Session.SetString("Rol", usuario.Rol?.Name ?? "");
        HttpContext.Session.SetString("Empresa", usuario.Company?.Name ?? "Sin Empresa");

        return RedirectToPage("/Index");
    }
}