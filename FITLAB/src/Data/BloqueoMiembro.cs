using GimnasioFITLAB.Models;
using Microsoft.EntityFrameworkCore;

namespace GimnasioFITLAB.Data;

public static class BloqueoMiembro
{
    // Solo se usa dentro de una transacción. Baja y cobro bloquean la misma fila.
    // En SQL Server UPDLOCK + HOLDLOCK conserva el bloqueo hasta confirmar/deshacer.
    public static IQueryable<Miembro> Consulta(AppDbContext context, int id)
        => context.Database.IsSqlServer()
            ? context.Miembros.FromSqlInterpolated($"SELECT * FROM [Miembros] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
            : context.Miembros.Where(m => m.Id == id);
}
