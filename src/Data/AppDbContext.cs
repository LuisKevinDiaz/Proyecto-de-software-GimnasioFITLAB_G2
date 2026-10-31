using Microsoft.EntityFrameworkCore;
using TuProyecto.Models;

namespace TuProyecto.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Miembro> Miembros { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Garantiza que la BD rechace cédulas o códigos repetidos (Criterio 4)
            modelBuilder.Entity<Miembro>()
                .HasIndex(m => m.Cedula)
                .IsUnique();

            modelBuilder.Entity<Miembro>()
                .HasIndex(m => m.CodigoUnico)
                .IsUnique();
        }
    }
}