using Microsoft.EntityFrameworkCore;
using GimnasioFITLAB.Models;

namespace GimnasioFITLAB.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Miembro> Miembros { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        public DbSet<Membresia> Membresias { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Garantiza que la BD rechace cédulas o códigos repetidos (Criterio 4)
            modelBuilder.Entity<Miembro>()
                .HasIndex(m => m.Cedula)
                .IsUnique();

            modelBuilder.Entity<Miembro>()
                .HasIndex(m => m.CodigoUnico)
                .IsUnique();

            var membresia = modelBuilder.Entity<Membresia>();
            membresia.HasKey(m => m.Id);
            membresia.HasIndex(m => m.MiembroId).IsUnique();
            membresia.HasOne(m => m.Miembro).WithMany().HasForeignKey(m => m.MiembroId)
                .OnDelete(DeleteBehavior.Restrict);
    
            var pago = modelBuilder.Entity<Pago>();
            pago.HasKey(p => p.Id);
            pago.Property(p => p.Id).ValueGeneratedNever();
            pago.HasIndex(p => p.ClaveIdempotencia).IsUnique();
            pago.HasIndex(p => new { p.MiembroId, p.FechaRegistroUtc, p.Id });
            pago.HasOne(p => p.Miembro).WithMany().HasForeignKey(p => p.MiembroId)
                .OnDelete(DeleteBehavior.Restrict);
            pago.Property(p => p.Importe).HasPrecision(18, 2);
            pago.Property(p => p.Moneda).HasMaxLength(3);
            pago.Property(p => p.MetodoPago).HasMaxLength(50);
            pago.Property(p => p.ReferenciaExterna).HasMaxLength(100);
            pago.Property(p => p.CedulaMiembro).HasMaxLength(450);
            pago.Property(p => p.CodigoMiembro).HasMaxLength(450);
            pago.ToTable("Pagos", t => t.HasCheckConstraint("CK_Pagos_ImportePositivo", "[Importe] > 0"));
        }
    }
}


