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
        var miembro = await context.Miembros.SingleOrDefaultAsync(m => m.Id == id, ct);
        if (miembro == null) return new(EstadoBaja.NoExiste);
        if (!miembro.Activo) return new(EstadoBaja.YaInactivo, miembro);

        // Solo se cambia el estado, no se borra nada
        miembro.Activo = false;
        miembro.FechaBaja = DateTime.Now;
        miembro.EmpleadoBajaId = empleadoId;

        await context.SaveChangesAsync(ct);
        return new(EstadoBaja.Baja, miembro);
    }
}
