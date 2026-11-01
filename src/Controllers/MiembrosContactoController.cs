using System.ComponentModel.DataAnnotations;
using GimnasioFITLAB.Data;
using GimnasioFITLAB.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GimnasioFITLAB.Controllers;

[ApiController]
[Route("api/miembros")]
public class MiembrosContactoController(AppDbContext context, ILogger<MiembrosContactoController> logger) : ControllerBase
{
    [HttpPut("{id:int}/contacto")]
    public async Task<IActionResult> Actualizar(
        [Range(1, int.MaxValue)] int id,
        [FromBody] ActualizarContactoDto dto,
        CancellationToken cancellationToken)
    {
        var miembro = await context.Miembros.SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (miembro == null) return NotFound(new { mensaje = "No se encontró el miembro." });

        // Asignación explícita: no adjuntar una entidad recibida del cliente ni marcarla completa como modificada.
        miembro.Telefono = dto.Telefono.Trim();
        miembro.Correo = dto.Correo.Trim();
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await context.Miembros.AsNoTracking().AnyAsync(m => m.Id == id, cancellationToken))
                return NotFound(new { mensaje = "No se encontró el miembro." });
            return Conflict(new { mensaje = "El miembro cambió durante la actualización. Consulta sus datos e intenta nuevamente." });
        }

        // Log operativo; no sustituye una auditoría persistente ni autentica al empleado.
        logger.LogInformation(new EventId(2002, "ContactoActualizado"),
            "Contacto actualizado. MiembroId={MiembroId}, EmpleadoDeclaradoId={EmpleadoDeclaradoId}, FechaModificacionUtc={FechaModificacionUtc}",
            id, dto.EmpleadoModificacionId, DateTimeOffset.UtcNow);
        return NoContent();
    }
}
