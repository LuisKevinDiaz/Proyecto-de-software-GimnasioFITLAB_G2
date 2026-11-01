using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GimnasioFITLAB.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ActualizarContactoDto
{
    [Required(ErrorMessage = "El teléfono es obligatorio")]
    [RegularExpression(@"^(809|829|849)-?\d{3}-?\d{4}$", ErrorMessage = "Teléfono inválido. Usa el formato 809-000-0000")]
    public string Telefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio")]
    [EmailAddress(ErrorMessage = "Formato de correo inválido")]
    public string Correo { get; set; } = string.Empty;

    // Declarado por el cliente hasta disponer de autenticación de empleados.
    [Range(1, int.MaxValue, ErrorMessage = "El empleado responsable es obligatorio")]
    public int EmpleadoModificacionId { get; set; }
}
