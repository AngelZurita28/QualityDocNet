using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Helpers;
using QualityDoc.Pages.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using System.Security.Claims;

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
        Correo = Correo?.Trim().ToLower() ?? string.Empty;

        var hash = PasswordHelper.HashPassword(Password);

        var usuario = _context.Users
             .Include(u => u.Rol)
             .Include(u => u.Company)
             .Include(u => u.Department)
             .FirstOrDefault(u => u.Email == Correo
                      && u.PasswordHash == hash
                      && u.IsActive);


        if (usuario == null)
        {
            Error = "Correo o contraseña incorrectos";
            return Page();
        }

        SetUserSession(usuario);
        return RedirectToPage("/Index");
    }

    public IActionResult OnGetGoogleLogin()
    {
        var properties = new AuthenticationProperties { RedirectUri = Url.Page("./Login", pageHandler: "GoogleResponse") };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    public async Task<IActionResult> OnGetGoogleResponse()
    {
        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded) return RedirectToPage("./Login");

        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email)) return RedirectToPage("./Login");

        var usuario = await _context.Users
            .Include(u => u.Rol)
            .Include(u => u.Company)
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Email == email.ToLower() && u.IsActive);

        if (usuario == null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Error = "El correo de Google no está registrado en el sistema.";
            return Page();
        }

        SetUserSession(usuario);
        return RedirectToPage("/Index");
    }

    private void SetUserSession(Usuario usuario)
    {
        HttpContext.Session.SetInt32("UserId", usuario.Id);
        HttpContext.Session.SetString("Usuario", usuario.FullName);
        HttpContext.Session.SetString("Rol", usuario.Rol?.Name ?? "");
        HttpContext.Session.SetString("Empresa", usuario.Company?.Name ?? "Sin Empresa");
        HttpContext.Session.SetString("Departamento", usuario.Department?.Name ?? "Sin Área");
    }
}