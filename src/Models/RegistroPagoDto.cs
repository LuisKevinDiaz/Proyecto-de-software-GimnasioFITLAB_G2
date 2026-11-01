using System.ComponentModel.DataAnnotations;

namespace GimnasioFITLAB.Models;

public class RegistroPagoDto : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int MiembroId { get; set; }

    // Límite de representación de decimal(18,2), no una tarifa comercial.
    [Range(typeof(decimal), "0.01", "9999999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Importe { get; set; }

    [Required, RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "La moneda debe contener tres letras.")]
    public string Moneda { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string MetodoPago { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ReferenciaExterna { get; set; }

    [Range(1, int.MaxValue)]
    public int EmpleadoRegistroId { get; set; }

    public Guid ClaveIdempotencia { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ClaveIdempotencia == Guid.Empty)
            yield return new ValidationResult("La clave de idempotencia es obligatoria.", new[] { nameof(ClaveIdempotencia) });
        if (decimal.Round(Importe, 2) != Importe)
            yield return new ValidationResult("El importe admite hasta dos decimales.", new[] { nameof(Importe) });
    }
}
