namespace GimnasioFITLAB.Models;

public class Membresia
{
    public int Id { get; set; }
    public int MiembroId { get; set; }
    public Miembro Miembro { get; set; } = null!;
    public DateOnly Inicio { get; set; }
    public DateOnly Vencimiento { get; set; }
}
