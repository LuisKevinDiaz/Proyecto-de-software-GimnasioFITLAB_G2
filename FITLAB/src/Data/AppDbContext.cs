using Microsoft.EntityFrameworkCore;
using GimnasioFITLAB.Models;

namespace GimnasioFITLAB.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<EmpleadoCuenta> EmpleadosCuentas { get; set; }
        public DbSet<SesionEmpleado> SesionesEmpleados { get; set; }

        public DbSet<AuditoriaContacto> AuditoriasContacto { get; set; }

        public DbSet<Miembro> Miembros { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        public DbSet<Membresia> Membresias { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var empleado = modelBuilder.Entity<EmpleadoCuenta>();
            empleado.ToTable("EmpleadosCuentas");
            empleado.HasKey(e => e.Id);
            empleado.Property(e => e.Usuario).HasMaxLength(128).IsRequired();
            empleado.Property(e => e.UsuarioNormalizado).HasMaxLength(128).IsRequired();
            empleado.Property(e => e.PasswordHash).HasMaxLength(1024).IsRequired();
            empleado.HasIndex(e => e.UsuarioNormalizado).IsUnique();
            var sesion = modelBuilder.Entity<SesionEmpleado>();
            sesion.ToTable("SesionesEmpleados");
            sesion.HasKey(s => s.TokenHash);
            sesion.Property(s => s.TokenHash).HasMaxLength(64).IsUnicode(false).IsFixedLength();
            sesion.HasIndex(s => s.ExpiraUtc);
            sesion.HasOne(s => s.Empleado).WithMany().HasForeignKey(s => s.EmpleadoId).OnDelete(DeleteBehavior.Restrict);

            // Garantiza que la BD rechace cédulas o códigos repetidos (Criterio 4)
            modelBuilder.Entity<Miembro>()
                .HasIndex(m => m.Cedula)
                .IsUnique();

            modelBuilder.Entity<Miembro>()
                .HasIndex(m => m.CodigoUnico)
                .IsUnique();

            // Los miembros que ya existen quedan activos
            modelBuilder.Entity<Miembro>().Property(m => m.Activo).HasDefaultValue(true);

            modelBuilder.Entity<Miembro>().Property(m => m.VersionContacto).IsConcurrencyToken();
            var auditoria = modelBuilder.Entity<AuditoriaContacto>();
            auditoria.ToTable("AuditoriasContacto", t => t.HasCheckConstraint("CK_AuditoriasContacto_EmpleadoPositivo", "[EmpleadoId] > 0"));
            auditoria.HasKey(a => a.Id);
            auditoria.Property(a => a.Id).ValueGeneratedNever();
            auditoria.HasIndex(a => new { a.MiembroId, a.FechaUtc });
            auditoria.HasOne<Miembro>().WithMany().HasForeignKey(a => a.MiembroId).OnDelete(DeleteBehavior.Restrict);

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



