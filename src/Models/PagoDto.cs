namespace GimnasioFITLAB.Models;

public record PagoDto(
    Guid Id, string NumeroComprobante, int MiembroId, decimal Importe,
    string Moneda, string MetodoPago, string? ReferenciaExterna,
    int EmpleadoRegistroId, DateTimeOffset FechaRegistroUtc,
    string NombreMiembro, string CedulaMiembro, string CodigoMiembro)
{
    public static PagoDto Desde(Pago pago) => new(
        pago.Id, $"PAGO-{pago.Id:N}", pago.MiembroId, pago.Importe,
        pago.Moneda, pago.MetodoPago, pago.ReferenciaExterna,
        pago.EmpleadoRegistroId, pago.FechaRegistroUtc,
        pago.NombreMiembro, pago.CedulaMiembro, pago.CodigoMiembro);
}
