namespace GimnasioFITLAB.Models;

public class SesionEmpleado
{
    public string TokenHash { get; set; } = string.Empty;
    public int EmpleadoId { get; set; }
    public DateTimeOffset ExpiraUtc { get; set; }
    public EmpleadoCuenta Empleado { get; set; } = null!;
}
