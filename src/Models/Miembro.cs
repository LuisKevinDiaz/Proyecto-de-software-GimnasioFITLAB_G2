using System;
using System.ComponentModel.DataAnnotations;

namespace TuProyecto.Models
{
    public class Miembro
    {
        [Key]
        public int Id { get; set; }

        // Se genera automáticamente, no se pide al usuario
        public string CodigoUnico { get; set; } = string.Empty; 

        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "La cédula es obligatoria")]
        [RegularExpression(@"^\d{3}-?\d{7}-?\d{1}$", ErrorMessage = "Cédula inválida. Usa el formato 000-0000000-0")]
        public string Cedula { get; set; } = string.Empty;

        [Required(ErrorMessage = "El teléfono es obligatorio")]
        [Phone(ErrorMessage = "Formato de teléfono inválido")]
        public string Telefono { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido")]
        public string Correo { get; set; } = string.Empty;

        // Idealmente esto se extrae del JWT del empleado logueado
        public int EmpleadoRegistroId { get; set; } 
        
        public DateTime FechaAlta { get; set; } = DateTime.Now;
    }
}