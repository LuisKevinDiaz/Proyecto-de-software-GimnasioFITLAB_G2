using System.ComponentModel.DataAnnotations;
using GimnasioFITLAB.Data;
using GimnasioFITLAB.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GimnasioFITLAB.Services;

public enum EstadoRegistroPago { Creado, Repetido, Conflicto, MiembroInexistente, MiembroInactivo }
public record ResultadoRegistroPago(EstadoRegistroPago Estado, PagoDto? Pago = null);

public class PagoService(AppDbContext context)
{
    public async Task<ResultadoRegistroPago> RegistrarAsync(RegistroPagoDto dto, CancellationToken ct = default)
    {
        // Protege también a los consumidores del servicio que no pasan por MVC.
        Validator.ValidateObject(dto, new ValidationContext(dto), validateAllProperties: true);
        string moneda = dto.Moneda.ToUpperInvariant();
        string metodo = dto.MetodoPago.Trim();
        string? referencia = string.IsNullOrWhiteSpace(dto.ReferenciaExterna) ? null : dto.ReferenciaExterna.Trim();

        ResultadoRegistroPago ResolverReintento(Pago existente)
        {
            bool iguales = existente.MiembroId == dto.MiembroId && existente.Importe == dto.Importe
                && existente.Moneda == moneda && existente.MetodoPago == metodo
                && existente.ReferenciaExterna == referencia && existente.EmpleadoRegistroId == dto.EmpleadoRegistroId;
            return iguales
                ? new(EstadoRegistroPago.Repetido, PagoDto.Desde(existente))
                : new(EstadoRegistroPago.Conflicto);
        }

        var existente = await context.Pagos.AsNoTracking()
            .SingleOrDefaultAsync(p => p.ClaveIdempotencia == dto.ClaveIdempotencia, ct);
        if (existente != null) return ResolverReintento(existente);

        var miembro = await context.Miembros.AsNoTracking().SingleOrDefaultAsync(m => m.Id == dto.MiembroId, ct);
        if (miembro == null) return new(EstadoRegistroPago.MiembroInexistente);
        if (!miembro.Activo) return new(EstadoRegistroPago.MiembroInactivo);

        var pago = new Pago
        {
            Id = Guid.NewGuid(), ClaveIdempotencia = dto.ClaveIdempotencia,
            MiembroId = miembro.Id, Importe = dto.Importe, Moneda = moneda,
            MetodoPago = metodo, ReferenciaExterna = referencia,
            EmpleadoRegistroId = dto.EmpleadoRegistroId, FechaRegistroUtc = DateTimeOffset.UtcNow,
            NombreMiembro = miembro.Nombre, CedulaMiembro = miembro.Cedula, CodigoMiembro = miembro.CodigoUnico
        };
        context.Pagos.Add(pago);
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Solo una colisión de unicidad de SQL Server puede ser un reintento concurrente.
            context.Entry(pago).State = EntityState.Detached;
            existente = await context.Pagos.AsNoTracking()
                .SingleOrDefaultAsync(p => p.ClaveIdempotencia == dto.ClaveIdempotencia, ct);
            if (existente == null) throw; // Otra restricción única: no ocultar el fallo.
            return ResolverReintento(existente);
        }
        return new(EstadoRegistroPago.Creado, PagoDto.Desde(pago));
    }

    public async Task<PagoDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var pago = await context.Pagos.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        return pago == null ? null : PagoDto.Desde(pago);
    }

    public async Task<IReadOnlyList<PagoDto>?> HistorialAsync(int miembroId, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        if (pagina < 1 || pagina > 1_000_000 || tamanoPagina < 1 || tamanoPagina > 100)
            throw new ArgumentOutOfRangeException(nameof(pagina));
        if (!await context.Miembros.AsNoTracking().AnyAsync(m => m.Id == miembroId, ct)) return null;
        var pagos = await context.Pagos.AsNoTracking().Where(p => p.MiembroId == miembroId)
            .OrderByDescending(p => p.FechaRegistroUtc).ThenByDescending(p => p.Id)
            .Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina + 1).ToListAsync(ct);
        return pagos.Select(PagoDto.Desde).ToList();
    }
}

