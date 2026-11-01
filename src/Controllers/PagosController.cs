using System.ComponentModel.DataAnnotations;
using GimnasioFITLAB.Models;
using GimnasioFITLAB.Services;
using Microsoft.AspNetCore.Mvc;

namespace GimnasioFITLAB.Controllers;

[ApiController]
[Route("api/pagos")]
public class PagosController(PagoService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] RegistroPagoDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var resultado = await service.RegistrarAsync(dto, ct);
        return resultado.Estado switch
        {
            EstadoRegistroPago.Creado => CreatedAtAction(nameof(ObtenerPorId), new { id = resultado.Pago!.Id }, resultado.Pago),
            EstadoRegistroPago.Repetido => Ok(resultado.Pago),
            EstadoRegistroPago.Conflicto => Conflict(new { mensaje = "La clave de idempotencia ya fue utilizada con datos diferentes." }),
            EstadoRegistroPago.MiembroInexistente => NotFound(new { mensaje = "No se encontró el miembro." }),
            EstadoRegistroPago.MiembroInactivo => Conflict(new { mensaje = "El miembro esta inactivo." }),
            _ => throw new InvalidOperationException()
        };
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObtenerPorId(Guid id, CancellationToken ct)
    {
        var pago = await service.ObtenerAsync(id, ct);
        return pago == null ? NotFound() : Ok(pago);
    }

    [HttpGet("/api/miembros/{miembroId:int}/pagos")]
    public async Task<IActionResult> Historial(
        [Range(1, int.MaxValue)] int miembroId,
        [FromQuery, Range(1, 1_000_000)] int pagina = 1,
        [FromQuery, Range(1, 100)] int tamanoPagina = 20,
        CancellationToken ct = default)
    {
        var pagos = await service.HistorialAsync(miembroId, pagina, tamanoPagina, ct);
        if (pagos == null) return NotFound(new { mensaje = "No se encontró el miembro." });
        return Ok(new { pagos = pagos.Take(tamanoPagina), pagina, tamanoPagina, hayMasResultados = pagos.Count > tamanoPagina });
    }
}

