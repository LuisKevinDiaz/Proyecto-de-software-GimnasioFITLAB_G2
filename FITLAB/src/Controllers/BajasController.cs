using System.Text.Json.Serialization;
using GimnasioFITLAB.Security;
using GimnasioFITLAB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GimnasioFITLAB.Controllers;

// La identidad nunca se recibe en el cuerpo. Enviar {} al confirmar.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class BajaDto { }

[ApiController]
[Route("api/miembros")]
[Authorize(Policy = PoliticasEmpleado.Baja)]
public class BajasController(MiembroBajaService service) : ControllerBase
{
    [HttpPost("{id:int}/baja")]
    public async Task<IActionResult> Baja(int id, [FromBody] BajaDto dto, CancellationToken ct)
    {
        var empleadoId = OperacionEmpleadoHandler.ObtenerEmpleado(HttpContext, OperacionEmpleado.DarDeBaja);
        if (empleadoId is null) return Forbid();
        var r = await service.DarDeBajaAsync(id, empleadoId.Value, ct);
        return r.Estado switch
        {
            EstadoBaja.Baja => Ok(new { id = r.Miembro!.Id, codigo = r.Miembro.CodigoUnico,
                activo = r.Miembro.Activo, fechaBaja = r.Miembro.FechaBaja, empleadoBajaId = r.Miembro.EmpleadoBajaId }),
            EstadoBaja.NoExiste => NotFound(new { mensaje = "No se encontró el miembro." }),
            _ => Conflict(new { mensaje = "El miembro ya está inactivo." })
        };
    }
}
