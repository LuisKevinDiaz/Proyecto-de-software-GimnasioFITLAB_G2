using System.Text;
using System.Text.RegularExpressions;
using GimnasioFITLAB.Data;
using GimnasioFITLAB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// Este programa no crea cuentas por defecto ni acepta contraseñas en argumentos.
// Ejecutarlo antes de grabar; únicamente modifica la base aislada FITLAB_Pruebas.
try
{
    if (args.Length != 1 || (args[0] != "crear-cuenta" && args[0] != "--verificar" && args[0] != "verificar-clave" && args[0] != "cambiar-clave"))
    {
        Console.WriteLine("Uso privado: EmpleadoPrivado crear-cuenta | --verificar");
        return 2;
    }
    var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
    if (string.IsNullOrWhiteSpace(connection)) throw new OperacionPrivadaException("Falta la conexión privada de FITLAB_Pruebas.");
    var parsed = new SqlConnectionStringBuilder(connection);
    var server = parsed.DataSource.Trim();
    if (server.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)) server = server[4..];
    server = server.Split(',')[0].Trim();
    if (!string.Equals(parsed.InitialCatalog, "FITLAB_Pruebas", StringComparison.Ordinal) ||
        !(server.Equals("localhost", StringComparison.OrdinalIgnoreCase) || server == "127.0.0.1" || server == "[::1]" || server == "::1"))
        throw new OperacionPrivadaException("La herramienta solo permite FITLAB_Pruebas en el servidor local.");
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
    if (!await db.Database.CanConnectAsync()) throw new OperacionPrivadaException("No se pudo acceder a FITLAB_Pruebas; comprueba Docker y la preparación privada.");
    if (args[0] == "--verificar")
    {
        Console.WriteLine($"FITLAB_Pruebas: {await db.EmpleadosCuentas.CountAsync()} cuenta(s) persistente(s), {await db.EmpleadosCuentas.CountAsync(e => e.Activo)} activa(s). No se muestran nombres, hashes ni sesiones.");
        return 0;
    }
    if (Console.IsInputRedirected)
        throw new OperacionPrivadaException("La creación exige una terminal interactiva. No envíes contraseñas por argumentos, archivos o tuberías.");
    Console.WriteLine("CONFIGURACIÓN PRIVADA — ejecutar antes de comenzar la grabación.");
    Console.Write("Nombre de usuario (3–64 letras, números, punto, guion o guion bajo): ");
    string username = (Console.ReadLine() ?? "").Trim();
    if (!Regex.IsMatch(username, @"^[A-Za-z0-9._-]{3,64}$")) throw new OperacionPrivadaException("El nombre de usuario no cumple el formato indicado.");
    var normalized = username.ToUpperInvariant();
    if (args[0] is "verificar-clave" or "cambiar-clave")
    {
        var current = await db.EmpleadosCuentas.SingleOrDefaultAsync(e => e.UsuarioNormalizado == normalized);
        if (current == null) throw new OperacionPrivadaException("La cuenta no existe. No se modificó nada.");
        var verifier = new PasswordHasher<EmpleadoCuenta>(Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = 210000 }));
        if (args[0] == "verificar-clave")
        {
            var candidate = ReadHidden("Contraseña actual, para comprobarla localmente: ");
            bool valid = verifier.VerifyHashedPassword(current, current.PasswordHash, candidate) != PasswordVerificationResult.Failed;
            candidate = string.Empty;
            Console.WriteLine(valid ? "La contraseña coincide con el hash de SQL Server. No se modificó la cuenta." : "La contraseña no coincide. La cuenta y los permisos siguen intactos.");
            return valid ? 0 : 1;
        }
        var replacement = ReadHidden("Contraseña nueva (mínimo 12 caracteres): ");
        if (replacement.Length < 12) throw new OperacionPrivadaException("Contraseña demasiado corta. No se modificó nada.");
        var repeat = ReadHidden("Repite la contraseña nueva: ");
        if (replacement != repeat) throw new OperacionPrivadaException("Las contraseñas no coinciden. No se modificó nada.");
        Console.Write("¿Actualizar solo la contraseña de esta cuenta, conservando ID y permisos? [s/N]: ");
        if (!Yes(Console.ReadLine())) { Console.WriteLine("Cancelado sin cambios."); return 0; }
        current.PasswordHash = verifier.HashPassword(current, replacement);
        replacement = string.Empty; repeat = string.Empty;
        foreach (var session in await db.SesionesEmpleados.Where(x => x.EmpleadoId == current.Id && x.ExpiraUtc > DateTimeOffset.UtcNow).ToListAsync()) session.ExpiraUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        Console.WriteLine("Contraseña actualizada. Se conservaron la cuenta y sus permisos; las sesiones anteriores quedaron vencidas.");
        return 0;
    }
    if (await db.EmpleadosCuentas.AnyAsync(e => e.UsuarioNormalizado == normalized))
    {
        Console.WriteLine("La cuenta ya existe. No se modificaron su contraseña, estado ni permisos.");
        return 0;
    }
    string password = ReadHidden("Contraseña (mínimo 12 caracteres; no se mostrará): ");
    if (password.Length < 12) throw new OperacionPrivadaException("La contraseña debe contener al menos 12 caracteres.");
    string confirmation = ReadHidden("Repite la contraseña: ");
    if (!string.Equals(password, confirmation, StringComparison.Ordinal)) throw new OperacionPrivadaException("Las contraseñas no coinciden.");
    Console.Write("¿Permitir actualizar el contacto de miembros? [s/N]: ");
    bool contacto = Yes(Console.ReadLine());
    Console.Write("¿Permitir dar de baja a miembros? [s/N]: ");
    bool baja = Yes(Console.ReadLine());
    Console.WriteLine($"Permisos seleccionados: actualizar contacto = {(contacto ? "sí" : "no")}; dar de baja = {(baja ? "sí" : "no")}.");
    Console.Write("¿Crear esta cuenta activa en FITLAB_Pruebas? [s/N]: ");
    if (!Yes(Console.ReadLine())) { Console.WriteLine("Cancelado. No se creó la cuenta."); return 0; }
    var employee = new EmpleadoCuenta
    {
        Usuario = username, UsuarioNormalizado = normalized, Activo = true,
        PuedeActualizarContacto = contacto, PuedeDarDeBaja = baja
    };
    var hasher = new PasswordHasher<EmpleadoCuenta>(Options.Create(new PasswordHasherOptions
    {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = 210000
    }));
    employee.PasswordHash = hasher.HashPassword(employee, password);
    password = string.Empty; confirmation = string.Empty;
    db.EmpleadosCuentas.Add(employee);
    await db.SaveChangesAsync();
    Console.WriteLine("Cuenta creada con contraseña hasheada y permisos elegidos explícitamente. No hay acceso por declarar un empleadoId.");
    return 0;
}
catch (DbUpdateException)
{
    Console.Error.WriteLine("No se pudo crear la cuenta. Puede existir ya o faltar la migración; no se sobrescribieron cuentas. Revisa la preparación privada.");
    return 1;
}
catch (OperacionPrivadaException error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
catch (Exception)
{
    Console.Error.WriteLine("La operación privada no se completó. Revisa Docker, la conexión y las migraciones; no se muestran detalles que puedan contener credenciales.");
    return 1;
}

static bool Yes(string? text) => string.Equals(text?.Trim(), "s", StringComparison.OrdinalIgnoreCase)
    || string.Equals(text?.Trim(), "si", StringComparison.OrdinalIgnoreCase)
    || string.Equals(text?.Trim(), "sí", StringComparison.OrdinalIgnoreCase);

static string ReadHidden(string prompt)
{
    Console.Write(prompt);
    var result = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return result.ToString(); }
        if (key.Key == ConsoleKey.Escape) { Console.WriteLine(); throw new OperacionPrivadaException("Cancelado sin crear la cuenta."); }
        if (key.Key == ConsoleKey.Backspace) { if (result.Length > 0) result.Length--; continue; }
        if (char.IsControl(key.KeyChar)) continue;
        if (result.Length >= 256) { Console.WriteLine(); throw new OperacionPrivadaException("La contraseña excede el máximo de 256 caracteres."); }
        result.Append(key.KeyChar);
    }
}

sealed class OperacionPrivadaException(string message) : Exception(message);
