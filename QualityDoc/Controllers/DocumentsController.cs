using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;

namespace QualityDoc.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public DocumentsController(AppDbContext context)
    {
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

        var maxVersion = await _context.Documents
            .Where(d => d.DocumentCode == code)
            .MaxAsync(d => (int?)d.VersionNumber);

        if (maxVersion == null)
            return NotFound(new { error = "Documento no encontrado" });

        return Ok(new { nextVersion = maxVersion.Value + 1 });
    }
}
