namespace GimnasioFITLAB.Models;

public class AuditoriaContacto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int MiembroId { get; set; }
    public int EmpleadoId { get; set; }
    public DateTimeOffset FechaUtc { get; set; }
    public Guid VersionAnterior { get; set; }
    public Guid VersionNueva { get; set; }
}
