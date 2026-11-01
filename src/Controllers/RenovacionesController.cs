using GimnasioFITLAB.Services;
using Microsoft.AspNetCore.Mvc;

namespace GimnasioFITLAB.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RenovacionesController(RenovacionService service) : ControllerBase
{
    // Aplica un pago pendiente a la renovacion del miembro
    [HttpPost("{pagoId:guid}")]
    public async Task<IActionResult> Aplicar(Guid pagoId, CancellationToken ct)
    {
        var r = await service.AplicarPagoAsync(pagoId, ct);
        return r.Estado switch
        {
            EstadoRenovacion.Aplicada => Ok(new
            {
                miembroId = r.Membresia!.MiembroId,
                inicio = r.Membresia.Inicio,
                vencimiento = r.Membresia.Vencimiento
            }),
            EstadoRenovacion.PagoInexistente => NotFound(new { mensaje = "El pago no existe." }),
            _ => Conflict(new { mensaje = "Este pago ya fue aplicado." })
        };
    }

    // Consulta la vigencia de un miembro
    [HttpGet("miembro/{miembroId:int}")]
    public async Task<IActionResult> Vigencia(int miembroId, CancellationToken ct)
    {
        var m = await service.ObtenerPorMiembroAsync(miembroId, ct);
        if (m == null) return NotFound(new { mensaje = "El miembro no tiene membresia." });

        bool vigente = m.Vencimiento > DateOnly.FromDateTime(DateTime.Now);
        return Ok(new { inicio = m.Inicio, vencimiento = m.Vencimiento, vigente });
    }
}
