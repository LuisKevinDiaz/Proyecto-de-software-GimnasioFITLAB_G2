using System.ComponentModel.DataAnnotations;

namespace GimnasioFITLAB.Models;

// Cuenta privada de la base de pruebas. No se generan filas ni permisos por defecto.
public class EmpleadoCuenta
{
    public int Id { get; set; }
    [MaxLength(128)] public string Usuario { get; set; } = string.Empty;
    [MaxLength(128)] public string UsuarioNormalizado { get; set; } = string.Empty;
    [MaxLength(1024)] public string PasswordHash { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public bool PuedeActualizarContacto { get; set; }
    public bool PuedeDarDeBaja { get; set; }
}
