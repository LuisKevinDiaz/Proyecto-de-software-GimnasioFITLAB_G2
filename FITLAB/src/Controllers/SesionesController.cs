using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using GimnasioFITLAB.Data;
using GimnasioFITLAB.Models;
using GimnasioFITLAB.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GimnasioFITLAB.Controllers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class IniciarSesionDto
{
    [Required, StringLength(128)] public string Usuario { get; set; } = string.Empty;
    [Required, StringLength(512)] public string Clave { get; set; } = string.Empty;
}

[ApiController]
[Route("api/sesion")]
public class SesionesController(AppDbContext context, IPasswordHasher<EmpleadoCuenta> hasher,
    EstadoAutenticacionLocal estado, TimeProvider reloj) : ControllerBase
{
    [HttpGet("actual")]
    [Authorize]
    public async Task<IActionResult> Actual(CancellationToken ct)
    {
        if (!estado.Habilitada) return NotFound();
        var value = User.FindFirst(AutenticacionEmpleadoLocalHandler.ClaimEmpleadoId)?.Value;
        if (!int.TryParse(value, out var id)) return Unauthorized();
        var empleado = await context.EmpleadosCuentas.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id && e.Activo, ct);
        if (empleado == null) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(new { empleadoId = id, empleado.PuedeActualizarContacto, empleado.PuedeDarDeBaja });
    }

    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> Salir(CancellationToken ct)
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return Unauthorized();
        var hash = TokensSesion.Hash(header[7..]);
        var session = await context.SesionesEmpleados.SingleOrDefaultAsync(s => s.TokenHash == hash, ct);
        if (session != null) { session.ExpiraUtc = reloj.GetUtcNow(); await context.SaveChangesAsync(ct); }
        return NoContent();
    }

    private static readonly byte[] SalComparacion = RandomNumberGenerator.GetBytes(16);

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("LoginEmpleados")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Iniciar(IniciarSesionDto dto, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        if (!estado.Habilitada) return NotFound();
        if (HttpContext.Connection.RemoteIpAddress is not { } remote || !System.Net.IPAddress.IsLoopback(remote))
            return StatusCode(403, new { mensaje = "Este inicio de sesión está limitado a la computadora local." });
        if (!ProteccionBaseEmpleados.EsBasePruebas(context))
            return StatusCode(503, new { mensaje = "La autenticación local está restringida a la base de pruebas." });
        var usuario = dto.Usuario.Trim().ToUpperInvariant();
        var cuenta = await context.EmpleadosCuentas.SingleOrDefaultAsync(e => e.UsuarioNormalizado == usuario, ct);
        if (cuenta == null)
        {
            // Coste comparable a IdentityV3 sin crear una cuenta o credencial ficticia.
            _ = Rfc2898DeriveBytes.Pbkdf2(dto.Clave, SalComparacion, 210_000, HashAlgorithmName.SHA512, 32);
            return Unauthorized(new { mensaje = "Credenciales no válidas." });
        }
        PasswordVerificationResult verification;
        try { verification = hasher.VerifyHashedPassword(cuenta, cuenta.PasswordHash, dto.Clave); }
        catch (FormatException) { verification = PasswordVerificationResult.Failed; }
        if (!cuenta.Activo || verification == PasswordVerificationResult.Failed)
            return Unauthorized(new { mensaje = "Credenciales no válidas." });
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            cuenta.PasswordHash = hasher.HashPassword(cuenta, dto.Clave);
        // Se emite únicamente después de comprobar la contraseña real de la cuenta activa.
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expiry = reloj.GetUtcNow().AddMinutes(30);
        context.SesionesEmpleados.Add(new SesionEmpleado
        {
            TokenHash = TokensSesion.Hash(token), EmpleadoId = cuenta.Id, ExpiraUtc = expiry
        });
        await context.SaveChangesAsync(ct);
        return Ok(new { accessToken = token, expiraUtc = expiry, empleadoId = cuenta.Id });
    }
}
