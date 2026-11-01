using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GimnasioFITLAB.Controllers;
using GimnasioFITLAB.Data;
using GimnasioFITLAB.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

int checks = 0;
void Check(bool ok, string description)
{
    if (!ok) throw new Exception("FAIL: " + description);
    checks++; Console.WriteLine("PASS: " + description);
}
var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
var logs = new AuditLogs();
builder.Logging.AddProvider(logs);
builder.WebHost.UseUrls("http://127.0.0.1:0");
string dbName = Guid.NewGuid().ToString();
builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
builder.Services.AddControllers().AddApplicationPart(typeof(MiembrosContactoController).Assembly);
await using var app = builder.Build();
app.MapControllers();
await app.StartAsync();
using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
try
{
    var registration = await client.PostAsJsonAsync("/api/miembros/registro", new { nombre = "Ana Prueba", cedula = "001-1234567-8", telefono = "809-555-0101", correo = "antes@example.invalid", empleadoRegistroId = 1 });
    Check(registration.StatusCode == HttpStatusCode.Created, "Registro de miembros conservado");
    var original = (await registration.Content.ReadFromJsonAsync<Miembro>())!;
    string route = $"/api/miembros/{original.Id}/contacto";
    var response = await client.PutAsJsonAsync(route, new { telefono = "829-555-0102", correo = "nuevo@example.invalid", empleadoModificacionId = 7 });
    Check(response.StatusCode == HttpStatusCode.NoContent, "Actualización válida: 204");
    var updated = (await client.GetFromJsonAsync<Miembro>($"/api/miembros/{original.Id}"))!;
    Check(updated.Telefono == "829-555-0102" && updated.Correo == "nuevo@example.invalid", "Perfil refleja el nuevo contacto desde otro contexto");
    Check(updated.Id == original.Id && updated.Cedula == original.Cedula && updated.CodigoUnico == original.CodigoUnico && updated.Nombre == original.Nombre && updated.FechaAlta == original.FechaAlta && updated.EmpleadoRegistroId == original.EmpleadoRegistroId, "Identidad, alta y empleado de registro intactos");
    Check(logs.Events.Count == 1 && logs.Events[0].Any(x => x.Key == "EmpleadoDeclaradoId" && Equals(x.Value, 7)) && logs.Events[0].Any(x => x.Key == "FechaModificacionUtc" && x.Value is DateTimeOffset d && d.Offset == TimeSpan.Zero), "Log de empleado declarado y fecha UTC");
    foreach (var phone in new[] { "8095550103", "8295550103", "8495550103" })
        Check((await client.PutAsJsonAsync(route, new { telefono = phone, correo = "nuevo@example.invalid", empleadoModificacionId = 7 })).StatusCode == HttpStatusCode.NoContent, "Teléfono válido sin guiones");
    var beforeFailures = await client.GetFromJsonAsync<Miembro>($"/api/miembros/{original.Id}");
    var badBodies = new object[] {
        new { telefono = "", correo = "nuevo@example.invalid", empleadoModificacionId = 7 },
        new { telefono = "123", correo = "nuevo@example.invalid", empleadoModificacionId = 7 },
        new { telefono = "2125550101", correo = "nuevo@example.invalid", empleadoModificacionId = 7 },
        new { telefono = "8095550101", correo = "", empleadoModificacionId = 7 },
        new { telefono = "8095550101", correo = "invalido", empleadoModificacionId = 7 },
        new { telefono = "8095550101", correo = "nuevo@example.invalid", empleadoModificacionId = 0 },
        new { telefono = "8095550101", correo = "nuevo@example.invalid" },
        new { correo = "nuevo@example.invalid", empleadoModificacionId = 7 },
        new { telefono = "8095550101", empleadoModificacionId = 7 }
    };
    int logsBeforeFailures = logs.Events.Count;
    foreach (var body in badBodies) Check((await client.PutAsJsonAsync(route, body)).StatusCode == HttpStatusCode.BadRequest, "Entrada inválida/incompleta: 400");
    foreach (string field in new[] { "cedula", "codigoUnico", "id", "nombre", "fechaAlta", "empleadoRegistroId" })
    {
        var body = new Dictionary<string, object> { ["telefono"] = "8095550101", ["correo"] = "nuevo@example.invalid", ["empleadoModificacionId"] = 7, [field] = "NO_CAMBIAR" };
        Check((await client.PutAsJsonAsync(route, body)).StatusCode == HttpStatusCode.BadRequest, "Campo ajeno rechazado: " + field);
    }
    Check((await client.PutAsJsonAsync("/api/miembros/999999/contacto", new { telefono = "8095550101", correo = "nuevo@example.invalid", empleadoModificacionId = 7 })).StatusCode == HttpStatusCode.NotFound, "Miembro inexistente: 404");
    Check((await client.PutAsJsonAsync("/api/miembros/0/contacto", new { telefono = "8095550101", correo = "nuevo@example.invalid", empleadoModificacionId = 7 })).StatusCode == HttpStatusCode.BadRequest, "Identificador inválido: 400");
    var afterFailures = await client.GetFromJsonAsync<Miembro>($"/api/miembros/{original.Id}");
    Check(JsonSerializer.Serialize(beforeFailures) == JsonSerializer.Serialize(afterFailures), "Rechazos no cambian ningún dato");
    Check(logs.Events.Count == logsBeforeFailures, "Rechazos no registran actualizaciones exitosas");
    var search = await client.GetFromJsonAsync<JsonElement>("/api/miembros/buscar?termino=Ana");
    Check(search.GetProperty("miembros")[0].GetProperty("correo").GetString() == "nuevo@example.invalid", "Búsqueda conserva ruta y refleja el contacto");
    Check((await client.GetAsync("/api/miembros")).StatusCode == HttpStatusCode.OK, "Listado conservado");
    Console.WriteLine($"RESULTADO: {checks} comprobaciones HTTP aprobadas con InMemory; sin SQL Server real ni auditoría persistente.");
}
finally { await app.StopAsync(); }

sealed class AuditLogs : ILoggerProvider
{
    public List<List<KeyValuePair<string, object?>>> Events { get; } = new();
    public ILogger CreateLogger(string categoryName) => new Capture(this);
    public void Dispose() { }
    private sealed class Capture(AuditLogs owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (eventId.Id == 2002 && state is IEnumerable<KeyValuePair<string, object?>> values)
                owner.Events.Add(values.ToList());
        }
    }
}
