using GimnasioFITLAB.Data;
using GimnasioFITLAB.Models;
using Microsoft.EntityFrameworkCore;

namespace GimnasioFITLAB.Services;

public enum EstadoRenovacion { Aplicada, PagoInexistente, PagoYaAplicado }
public record ResultadoRenovacion(EstadoRenovacion Estado, Membresia? Membresia = null);

public class RenovacionService(AppDbContext context)
{
    public async Task<ResultadoRenovacion> AplicarPagoAsync(Guid pagoId, CancellationToken ct = default)
    {
        var pago = await context.Pagos.AsNoTracking().SingleOrDefaultAsync(p => p.Id == pagoId, ct);
        if (pago == null) return new(EstadoRenovacion.PagoInexistente);
        if (pago.Aplicado) return new(EstadoRenovacion.PagoYaAplicado);

        // Todo va en una transaccion: si algo falla, no se cambia nada
        using var tx = await context.Database.BeginTransactionAsync(ct);

        // Marca el pago como aplicado solo si todavia no lo estaba
        int filas = await context.Pagos.Where(p => p.Id == pagoId && !p.Aplicado)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Aplicado, true)
                .SetProperty(p => p.FechaAplicacionUtc, DateTimeOffset.UtcNow), ct);
        if (filas == 0) return new(EstadoRenovacion.PagoYaAplicado);

        var membresia = await context.Membresias.SingleOrDefaultAsync(m => m.MiembroId == pago.MiembroId, ct);
        var fechaPago = DateOnly.FromDateTime(pago.FechaRegistroUtc.LocalDateTime);

        // Si no tiene membresia, el periodo empieza el dia del pago
        var vencimientoAnterior = membresia == null ? DateOnly.MinValue : membresia.Vencimiento;
        var (inicio, vencimiento) = CalculoVigenciaService.CalcularPeriodo(vencimientoAnterior, fechaPago);

        if (membresia == null)
        {
            membresia = new Membresia { MiembroId = pago.MiembroId };
            context.Membresias.Add(membresia);
        }
        membresia.Inicio = inicio;
        membresia.Vencimiento = vencimiento;

        await context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(EstadoRenovacion.Aplicada, membresia);
    }

    public async Task<Membresia?> ObtenerPorMiembroAsync(int miembroId, CancellationToken ct = default)
    {
        return await context.Membresias.AsNoTracking().SingleOrDefaultAsync(m => m.MiembroId == miembroId, ct);
    }
}
