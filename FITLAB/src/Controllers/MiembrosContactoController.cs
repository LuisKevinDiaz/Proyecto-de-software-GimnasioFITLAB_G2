using System.ComponentModel.DataAnnotations;
using GimnasioFITLAB.Models;
using GimnasioFITLAB.Security;
using GimnasioFITLAB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GimnasioFITLAB.Controllers;

[ApiController]
[Route("api/miembros")]
[Authorize(Policy = PoliticasEmpleado.Contacto)]
public class MiembrosContactoController(ContactoMiembroService service) : ControllerBase
{
    [HttpPut("{id:int}/contacto")]
    public async Task<IActionResult> Actualizar([Range(1, int.MaxValue)] int id,
        [FromBody] ActualizarContactoDto dto, CancellationToken cancellationToken)
    {
        var empleadoId = OperacionEmpleadoHandler.ObtenerEmpleado(HttpContext, OperacionEmpleado.ActualizarContacto);
        if (empleadoId is null) return Forbid();
        if (!Request.Headers.TryGetValue("If-Match", out var values))
            return StatusCode(428, new { mensaje = "Consulta el perfil y envía su ETag en If-Match." });
        var header = values.Count == 1 ? values[0] : null;
        if (header is null || header.Length != 38 || header[0] != '"' || header[^1] != '"' ||
            !Guid.TryParseExact(header[1..^1], "D", out var version))
            return BadRequest(new { mensaje = "If-Match debe contener un único ETag fuerte del perfil." });
        var result = await service.ActualizarAsync(id, dto, version, empleadoId.Value, cancellationToken);
        if (result.Estado == EstadoContacto.NoExiste) return NotFound(new { mensaje = "No se encontró el miembro." });
        if (result.Estado == EstadoContacto.VersionObsoleta)
            return StatusCode(412, new { mensaje = "El contacto cambió. Consulta el perfil antes de volver a editar." });
        Response.Headers.ETag = $"\"{result.Version:D}\"";
        return NoContent();
    }
}
