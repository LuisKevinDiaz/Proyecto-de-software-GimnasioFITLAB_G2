using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace GimnasioFITLAB.Models
{
    public class RegistroMiembroDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "La cédula es obligatoria")]
        [RegularExpression(@"^\d{3}-?\d{7}-?\d{1}$", ErrorMessage = "Cédula inválida. Usa el formato 000-0000000-0")]
        public string Cedula { get; set; } = string.Empty;

        [Required(ErrorMessage = "El teléfono es obligatorio")]
        [RegularExpression(@"^(809|829|849)-?\d{3}-?\d{4}$", ErrorMessage = "Teléfono inválido. Usa el formato 809-000-0000")]
        public string Telefono { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Formato de correo inválido")]
        public string Correo { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "El empleado que registra es obligatorio")]
        [JsonIgnore, ValidateNever] // Exclusivamente asignado desde la sesión del servidor.
    public int EmpleadoRegistroId { get; set; }
    }
}