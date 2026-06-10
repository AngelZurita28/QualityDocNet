using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Services;

namespace QualityDoc.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly DocumentVersionService _versionService;
    private readonly AppDbContext _context;

    public DocumentsController(DocumentVersionService versionService, AppDbContext context)
    {
        _versionService = versionService;
        _context = context;
    }

    /// <summary>
    /// Dado un código de documento, devuelve el siguiente número de versión.
    /// GET /api/documents/version?code=DOC-XXXXXXXX
    /// </summary>
    [HttpGet("version")]
    public async Task<IActionResult> GetNextVersion([FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "Código requerido" });

        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
            return Unauthorized(new { error = "Sesion requerida" });

        var companyId = await _context.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => u.CompanyId)
            .FirstOrDefaultAsync();

        if (companyId == null)
            return BadRequest(new { error = "Usuario sin empresa asignada" });

        var nextVersion = await _versionService.PreviewNextReviewVersionAsync(code, companyId.Value);

        if (nextVersion == null)
            return NotFound(new { error = "Documento no encontrado" });

        return Ok(new { nextVersion = DocumentVersionService.FormatVersion(nextVersion) });
    }
}
