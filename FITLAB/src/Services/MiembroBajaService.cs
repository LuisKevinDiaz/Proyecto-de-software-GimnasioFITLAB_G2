using GimnasioFITLAB.Data;
using GimnasioFITLAB.Models;
using Microsoft.EntityFrameworkCore;

namespace GimnasioFITLAB.Services;

public enum EstadoBaja { Baja, NoExiste, YaInactivo }
public record ResultadoBaja(EstadoBaja Estado, Miembro? Miembro = null);

public class MiembroBajaService(AppDbContext context)
{
    public async Task<ResultadoBaja> DarDeBajaAsync(int id, int empleadoId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(empleadoId);
        await using var tx = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(ct) : null;
        var actual = await BloqueoMiembro.Consulta(context, id).AsNoTracking().SingleOrDefaultAsync(ct);
        if (actual == null) return new(EstadoBaja.NoExiste);
        if (!actual.Activo) return new(EstadoBaja.YaInactivo, actual);
        // La decisión usa el estado leído bajo bloqueo, no una entidad local obsoleta.
        var local = context.Miembros.Local.FirstOrDefault(m => m.Id == id);
        if (local != null)
        {
            if (context.Entry(local).State != EntityState.Unchanged)
                throw new InvalidOperationException("La baja requiere confirmar primero los cambios pendientes del miembro.");
            // Actualiza también cualquier token de concurrencia añadido por otras historias.
            await context.Entry(local).ReloadAsync(ct);
        }
        var miembro = local ?? actual;
        if (context.Entry(miembro).State == EntityState.Detached) context.Attach(miembro);

        // Solo se cambia el estado, no se borra nada
        miembro.Activo = false;
        miembro.FechaBaja = DateTime.UtcNow;
        miembro.EmpleadoBajaId = empleadoId;

        await context.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);
        return new(EstadoBaja.Baja, miembro);
    }
}
