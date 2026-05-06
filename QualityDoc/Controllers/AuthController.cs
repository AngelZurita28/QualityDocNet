using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Helpers;
using QualityDoc.Pages.Models;

namespace QualityDoc.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { message = "Email y contraseña son requeridos." });
            }

            var hash = PasswordHelper.HashPassword(request.Password);
            var usuario = await _context.Users
                .Include(u => u.Rol)
                .Include(u => u.Company)
                .Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower() && u.PasswordHash == hash && u.IsActive);

            if (usuario == null)
            {
                return Unauthorized(new { message = "Credenciales incorrectas o usuario inactivo." });
            }

            // Nota: El modelo Usuario no tiene Departamento asignado directamente según el esquema actual.
            // Se retorna "N/A" o se podría buscar si tiene algún documento con departamento.
            return Ok(new
            {
                id = usuario.Id,
                nombre = usuario.FullName,
                usuario = usuario.Email,
                empresa = usuario.Company?.Name ?? "Sin Empresa",
                rol = usuario.Rol?.Name ?? "Sin Rol",
                departamento = usuario.Department?.Name ?? "No Asignado" 
            });
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            // Este es un ejemplo de cómo se manejaría si el cliente envía un email tras validar con Google.
            // En una implementación real, se recibiría un ID Token y se validaría con Google.Apis.Auth.
            if (string.IsNullOrEmpty(request.Email))
            {
                return BadRequest(new { message = "Email es requerido." });
            }

            var usuario = await _context.Users
                .Include(u => u.Rol)
                .Include(u => u.Company)
                .Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower() && u.IsActive);

            if (usuario == null)
            {
                return NotFound(new { message = "El usuario de Google no está registrado." });
            }

            return Ok(new
            {
                id = usuario.Id,
                nombre = usuario.FullName,
                usuario = usuario.Email,
                empresa = usuario.Company?.Name ?? "Sin Empresa",
                rol = usuario.Rol?.Name ?? "Sin Rol",
                departamento = usuario.Department?.Name ?? "No Asignado"
            });
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class GoogleLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string IdToken { get; set; } = string.Empty;
    }
}
