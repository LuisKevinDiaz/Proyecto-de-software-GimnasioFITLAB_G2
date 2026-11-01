namespace GimnasioFITLAB.Models;

public class Pago
{
    public Guid Id { get; set; }
    public Guid ClaveIdempotencia { get; set; }
    public int MiembroId { get; set; }
    public Miembro Miembro { get; set; } = null!;
    public decimal Importe { get; set; }
    public string Moneda { get; set; } = string.Empty;
    public string MetodoPago { get; set; } = string.Empty;
    public string? ReferenciaExterna { get; set; }
    public int EmpleadoRegistroId { get; set; }
    public DateTimeOffset FechaRegistroUtc { get; set; }
    // Datos históricos del comprobante: no cambian si luego se edita el miembro.
    public string NombreMiembro { get; set; } = string.Empty;
    public string CedulaMiembro { get; set; } = string.Empty;
    public string CodigoMiembro { get; set; } = string.Empty;
}
