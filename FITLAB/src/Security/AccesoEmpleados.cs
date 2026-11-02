using System.Security.Claims;
using System.Security.Cryptography;
using System.Globalization;
using System.Threading.RateLimiting;
using GimnasioFITLAB.Data;
using GimnasioFITLAB.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace GimnasioFITLAB.Security;

// Nombres de operaciones, no roles ni concesiones de acceso.
public enum OperacionEmpleado { ActualizarContacto, DarDeBaja }
public static class PoliticasEmpleado
{
    public const string Contacto = "Empleado.ActualizarContacto";
    public const string Baja = "Empleado.DarDeBaja";
}

/// <summary>
/// Verifica empleado existente/activo y permiso después de autenticar la identidad.
/// El proveedor local solo se habilita explícitamente en FITLAB_Pruebas.
/// No se confía en IDs, roles, headers ni permisos declarados por el cliente.
/// </summary>
public interface IAccesoEmpleado
{
    ValueTask<int?> AutorizarAsync(ClaimsPrincipal identidad, OperacionEmpleado operacion, CancellationToken ct);
}

public sealed class AccesoEmpleadoNoConfigurado : IAccesoEmpleado
{
    public ValueTask<int?> AutorizarAsync(ClaimsPrincipal identidad, OperacionEmpleado operacion, CancellationToken ct)
        => ValueTask.FromResult<int?>(null);
}

public sealed class AutenticacionNoConfiguradaHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(AuthenticateResult.NoResult());

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = 401, Title = "Se requiere identidad de empleado verificada.",
            Detail = "El proveedor de autenticación de empleados todavía no está configurado. La operación permanece bloqueada."
        }, Context.RequestAborted);
    }
}

public sealed record OperacionEmpleadoRequirement(OperacionEmpleado Operacion) : IAuthorizationRequirement;

public sealed class OperacionEmpleadoHandler(IAccesoEmpleado acceso)
    : AuthorizationHandler<OperacionEmpleadoRequirement>
{
    private sealed record EmpleadoServidor(int Id, OperacionEmpleado Operacion);
    private static readonly object ClaveEmpleado = new();

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OperacionEmpleadoRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true || context.Resource is not HttpContext http) return;
        int? id = await acceso.AutorizarAsync(context.User, requirement.Operacion, http.RequestAborted);
        if (id is not > 0) return;
        http.Items[ClaveEmpleado] = new EmpleadoServidor(id.Value, requirement.Operacion);
        context.Succeed(requirement);
    }

    public static int? ObtenerEmpleado(HttpContext http, OperacionEmpleado operacion)
        => http.Items.TryGetValue(ClaveEmpleado, out var value) && value is EmpleadoServidor e && e.Operacion == operacion
            ? e.Id : null;
}

public sealed record EstadoAutenticacionLocal(bool Habilitada);

public static class ProteccionBaseEmpleados
{
    public static bool EsBasePruebas(AppDbContext context)
        => context.Database.IsSqlServer() && string.Equals(context.Database.GetDbConnection().Database,
            "FITLAB_Pruebas", StringComparison.OrdinalIgnoreCase);
}

public static class TokensSesion
{
    public static bool FormatoValido(string token) => token.Length == 64 && token.All(Uri.IsHexDigit);
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Convert.FromHexString(token)));
}

public sealed class AutenticacionEmpleadoLocalHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    AppDbContext context, TimeProvider reloj)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Esquema = "EmpleadosLocales";
    public const string ClaimEmpleadoId = "fitlab:empleado_id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization;
        if (header.Count == 0) return AuthenticateResult.NoResult();
        if (Context.Connection.RemoteIpAddress is not { } remote || !System.Net.IPAddress.IsLoopback(remote))
            return AuthenticateResult.Fail("El acceso local solo admite conexiones de esta computadora.");
        if (header.Count != 1 || header[0] is not string value ||
            !value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.Fail("Credencial no válida.");
        var token = value[7..];
        if (!TokensSesion.FormatoValido(token) || !ProteccionBaseEmpleados.EsBasePruebas(context))
            return AuthenticateResult.Fail("Credencial no válida.");
        var hash = TokensSesion.Hash(token);
        var session = await context.SesionesEmpleados.AsNoTracking().Include(s => s.Empleado)
            .SingleOrDefaultAsync(s => s.TokenHash == hash, Context.RequestAborted);
        if (session == null || session.ExpiraUtc <= reloj.GetUtcNow() || !session.Empleado.Activo)
            return AuthenticateResult.Fail("Credencial no válida.");
        var id = session.EmpleadoId.ToString(CultureInfo.InvariantCulture);
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimEmpleadoId, id), new Claim(ClaimTypes.NameIdentifier, id) }, Esquema);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        Response.Headers.CacheControl = "no-store";
        await Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        { Status = 401, Title = "Se requiere una sesión de empleado válida." }, Context.RequestAborted);
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        await Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        { Status = 403, Title = "El empleado no tiene permiso para esta operación." }, Context.RequestAborted);
    }
}

public sealed class AccesoEmpleadoLocal(AppDbContext context) : IAccesoEmpleado
{
    public async ValueTask<int?> AutorizarAsync(ClaimsPrincipal identidad, OperacionEmpleado operacion, CancellationToken ct)
    {
        if (identidad.Identity?.IsAuthenticated != true ||
            identidad.Identity.AuthenticationType != AutenticacionEmpleadoLocalHandler.Esquema ||
            !ProteccionBaseEmpleados.EsBasePruebas(context)) return null;
        var ids = identidad.FindAll(AutenticacionEmpleadoLocalHandler.ClaimEmpleadoId).ToArray();
        if (ids.Length != 1 || !int.TryParse(ids[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id <= 0) return null;
        var empleado = await context.EmpleadosCuentas.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);
        if (empleado == null || !empleado.Activo) return null;
        bool permitido = operacion switch
        {
            OperacionEmpleado.ActualizarContacto => empleado.PuedeActualizarContacto,
            OperacionEmpleado.DarDeBaja => empleado.PuedeDarDeBaja,
            _ => false
        };
        return permitido ? empleado.Id : null;
    }
}

public static class AccesoEmpleadoExtensions
{
    public static IServiceCollection AddAccesoEmpleadosSeguro(this IServiceCollection services, IConfiguration? configuration = null)
    {
        bool local = configuration?.GetValue<bool>("Empleados:AutenticacionLocalHabilitada") == true;
        if (local)
        {
            string? raw = configuration!.GetConnectionString("DefaultConnection");
            var sql = string.IsNullOrWhiteSpace(raw) ? null : new SqlConnectionStringBuilder(raw);
            var host = sql?.DataSource.Replace("tcp:", "", StringComparison.OrdinalIgnoreCase).Split(',')[0].Trim();
            if (sql == null || host is not ("127.0.0.1" or "localhost" or "[::1]") || !string.Equals(sql.InitialCatalog,
                    "FITLAB_Pruebas", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La autenticación local solo puede habilitarse en FITLAB_Pruebas.");
        }
        services.AddSingleton(new EstadoAutenticacionLocal(local));
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.Configure<PasswordHasherOptions>(o =>
        {
            o.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
            o.IterationCount = 210_000;
        });
        services.AddScoped<IPasswordHasher<EmpleadoCuenta>, PasswordHasher<EmpleadoCuenta>>();
        if (local)
        {
            services.AddAuthentication(AutenticacionEmpleadoLocalHandler.Esquema)
                .AddScheme<AuthenticationSchemeOptions, AutenticacionEmpleadoLocalHandler>(AutenticacionEmpleadoLocalHandler.Esquema, _ => { });
            services.AddScoped<IAccesoEmpleado, AccesoEmpleadoLocal>();
        }
        else
        {
            services.AddAuthentication("EmpleadosNoConfigurados")
                .AddScheme<AuthenticationSchemeOptions, AutenticacionNoConfiguradaHandler>("EmpleadosNoConfigurados", _ => { });
            services.AddSingleton<IAccesoEmpleado, AccesoEmpleadoNoConfigurado>();
        }
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("LoginEmpleados", http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "sin_ip", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        });
        services.AddScoped<IAuthorizationHandler, OperacionEmpleadoHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(PoliticasEmpleado.Contacto, p => p.RequireAuthenticatedUser()
                .AddRequirements(new OperacionEmpleadoRequirement(OperacionEmpleado.ActualizarContacto)));
            options.AddPolicy(PoliticasEmpleado.Baja, p => p.RequireAuthenticatedUser()
                .AddRequirements(new OperacionEmpleadoRequirement(OperacionEmpleado.DarDeBaja)));
        });
        return services;
    }
}
