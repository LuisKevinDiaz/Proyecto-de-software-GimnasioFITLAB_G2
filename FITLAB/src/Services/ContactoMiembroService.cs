using System.ComponentModel.DataAnnotations;
using GimnasioFITLAB.Data;
using GimnasioFITLAB.Models;
using Microsoft.EntityFrameworkCore;

namespace GimnasioFITLAB.Services;

public enum EstadoContacto { Actualizado, NoExiste, VersionObsoleta }
public record ResultadoContacto(EstadoContacto Estado, Guid? Version = null);

public class ContactoMiembroService(AppDbContext context, TimeProvider reloj)
{
    // Solo lo invoca el controlador con el empleado resuelto por la autorización del servidor.
    public async Task<ResultadoContacto> ActualizarAsync(int id, ActualizarContactoDto dto, Guid versionEsperada,
        int empleadoId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(empleadoId);
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        // Mismo orden de bloqueo que baja/cobro. Evita conversiones de bloqueo de FK
        // durante inserciones de auditoría simultáneas en SQL Server.
        await using var tx = context.Database.IsSqlServer() ? await context.Database.BeginTransactionAsync(ct) : null;
        var miembro = await BloqueoMiembro.Consulta(context, id).SingleOrDefaultAsync(ct);
        if (miembro == null) return new(EstadoContacto.NoExiste);
        if (miembro.VersionContacto != versionEsperada) return new(EstadoContacto.VersionObsoleta);
        var versionNueva = Guid.NewGuid();
        miembro.Telefono = dto.Telefono.Trim();
        miembro.Correo = dto.Correo.Trim();
        miembro.VersionContacto = versionNueva;
        context.AuditoriasContacto.Add(new AuditoriaContacto
        {
            MiembroId = id, EmpleadoId = empleadoId, FechaUtc = reloj.GetUtcNow(),
            VersionAnterior = versionEsperada, VersionNueva = versionNueva
        });
        try
        {
            // Una sola transacción relacional: contacto, versión y auditoría se confirman juntos.
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return new(await context.Miembros.AnyAsync(m => m.Id == id, ct)
                ? EstadoContacto.VersionObsoleta : EstadoContacto.NoExiste);
        }
        if (tx != null) await tx.CommitAsync(ct);
        return new(EstadoContacto.Actualizado, versionNueva);
    }
}
