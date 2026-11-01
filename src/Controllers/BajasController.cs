using System.ComponentModel.DataAnnotations;
using GimnasioFITLAB.Services;
using Microsoft.AspNetCore.Mvc;

namespace GimnasioFITLAB.Controllers;

public class BajaDto
{
    [Range(1, int.MaxValue, ErrorMessage = "El empleado es obligatorio")]
    public int EmpleadoId { get; set; }
}

[ApiController]
[Route("api/miembros")]
public class BajasController(MiembroBajaService service) : ControllerBase
{
    // Da de baja (logica) a un miembro
    [HttpPost("{id:int}/baja")]
    public async Task<IActionResult> Baja(int id, [FromBody] BajaDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var r = await service.DarDeBajaAsync(id, dto.EmpleadoId, ct);
        return r.Estado switch
        {
            EstadoBaja.Baja => Ok(new
            {
                id = r.Miembro!.Id,
                codigo = r.Miembro.CodigoUnico,
                activo = r.Miembro.Activo,
                fechaBaja = r.Miembro.FechaBaja,
                empleadoBajaId = r.Miembro.EmpleadoBajaId
            }),
            EstadoBaja.NoExiste => NotFound(new { mensaje = "No se encontro el miembro." }),
            _ => Conflict(new { mensaje = "El miembro ya esta inactivo." })
        };
    }
}
