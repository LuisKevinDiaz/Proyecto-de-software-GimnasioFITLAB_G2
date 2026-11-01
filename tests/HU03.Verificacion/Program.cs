using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GimnasioFITLAB.Controllers;
using GimnasioFITLAB.Data;
using GimnasioFITLAB.Models;
using GimnasioFITLAB.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-DO");
int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    checks++;
}
var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
string databaseName = Guid.NewGuid().ToString();
builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(databaseName));
builder.Services.AddScoped<PagoService>();
builder.Services.AddControllers().AddApplicationPart(typeof(PagosController).Assembly);
await using var app = builder.Build();
app.MapControllers();
await app.StartAsync();
using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Miembros.AddRange(new Miembro { Id = 1, Nombre = "Miembro prueba", Cedula = "00112345678", CodigoUnico = "TEST-1", Correo = "uno@example.invalid", Telefono = "8095550101", EmpleadoRegistroId = 1 },
            new Miembro { Id = 2, Nombre = "Sin pagos", Cedula = "00212345678", CodigoUnico = "TEST-2", Correo = "dos@example.invalid", Telefono = "8095550102", EmpleadoRegistroId = 1 });
        await db.SaveChangesAsync();
    }
    var key = Guid.NewGuid();
    RegistroPagoDto Dto(Guid? clave = null) => new() { MiembroId = 1, Importe = 123.45m, Moneda = "USD", MetodoPago = "METODO_DE_PRUEBA", EmpleadoRegistroId = 1, ClaveIdempotencia = clave ?? key };
    var response = await client.PostAsJsonAsync("/api/pagos", Dto());
    Check(response.StatusCode == HttpStatusCode.Created, "Registro válido HTTP 201");
    var original = (await response.Content.ReadFromJsonAsync<PagoDto>())!;
    Check(response.Headers.Location != null && original.Id != Guid.Empty, "Identificador y Location");
    Check(original.NumeroComprobante == $"PAGO-{original.Id:N}" && original.NombreMiembro == "Miembro prueba" && original.Importe == 123.45m && original.FechaRegistroUtc.Offset == TimeSpan.Zero, "Datos de comprobante y fecha UTC");
    response = await client.GetAsync(response.Headers.Location);
    Check(response.StatusCode == HttpStatusCode.OK && await response.Content.ReadFromJsonAsync<PagoDto>() == original, "Consulta individual reproduce comprobante");
    response = await client.PostAsJsonAsync("/api/pagos", Dto());
    Check(response.StatusCode == HttpStatusCode.OK && await response.Content.ReadFromJsonAsync<PagoDto>() == original, "Reintento idéntico devuelve el pago original");
    var normalized = Dto(); normalized.Moneda = "usd"; normalized.MetodoPago = " METODO_DE_PRUEBA "; normalized.ReferenciaExterna = " ";
    Check((await client.PostAsJsonAsync("/api/pagos", normalized)).StatusCode == HttpStatusCode.OK, "Reintento tras normalización");
    foreach (Action<RegistroPagoDto> mutate in new Action<RegistroPagoDto>[] {
        x => x.Importe = 124m, x => x.MiembroId = 2, x => x.Moneda = "EUR",
        x => x.MetodoPago = "OTRO_METODO", x => x.EmpleadoRegistroId = 2, x => x.ReferenciaExterna = "DISTINTA" })
    {
        var dto = Dto(); mutate(dto);
        Check((await client.PostAsJsonAsync("/api/pagos", dto)).StatusCode == HttpStatusCode.Conflict, "Clave reutilizada con datos distintos: 409");
    }
    foreach (decimal amount in new[] { 0m, -1m, 0.001m, 12.345m, 10000000000000000m })
    {
        var dto = Dto(Guid.NewGuid()); dto.Importe = amount;
        Check((await client.PostAsJsonAsync("/api/pagos", dto)).StatusCode == HttpStatusCode.BadRequest, $"Importe inválido {amount}: 400");
    }
    foreach (Action<RegistroPagoDto> mutate in new Action<RegistroPagoDto>[] {
        x => x.ClaveIdempotencia = Guid.Empty, x => x.Moneda = "", x => x.Moneda = "123",
        x => x.MetodoPago = " ", x => x.MetodoPago = new string('m', 51),
        x => x.ReferenciaExterna = new string('r', 101), x => x.MiembroId = 0, x => x.EmpleadoRegistroId = 0 })
    {
        var dto = Dto(Guid.NewGuid()); mutate(dto);
        Check((await client.PostAsJsonAsync("/api/pagos", dto)).StatusCode == HttpStatusCode.BadRequest, "Campos obligatorios/formato: 400");
    }
    var missing = Dto(Guid.NewGuid()); missing.MiembroId = 999;
    Check((await client.PostAsJsonAsync("/api/pagos", missing)).StatusCode == HttpStatusCode.NotFound, "Miembro inexistente: 404");
    Check((await client.GetAsync($"/api/pagos/{Guid.NewGuid()}")).StatusCode == HttpStatusCode.NotFound, "Pago inexistente: 404");
    Check((await client.GetAsync("/api/miembros/999/pagos")).StatusCode == HttpStatusCode.NotFound, "Historial de miembro inexistente: 404");
    var history = await client.GetFromJsonAsync<JsonElement>("/api/miembros/2/pagos");
    Check(history.GetProperty("pagos").GetArrayLength() == 0, "Miembro sin pagos: colección vacía");
    history = await client.GetFromJsonAsync<JsonElement>("/api/miembros/1/pagos");
    Check(history.GetProperty("pagos").GetArrayLength() == 1, "Reintentos y rechazos no insertan pagos");
    Check((await client.PostAsJsonAsync("/api/pagos", Dto(Guid.NewGuid()))).StatusCode == HttpStatusCode.Created, "Otra clave permite otra operación legítima");
    history = await client.GetFromJsonAsync<JsonElement>("/api/miembros/1/pagos?tamanoPagina=1");
    Check(history.GetProperty("pagos").GetArrayLength() == 1 && history.GetProperty("hayMasResultados").GetBoolean(), "Paginación y aviso de más resultados");
    var page2 = await client.GetFromJsonAsync<JsonElement>("/api/miembros/1/pagos?tamanoPagina=1&pagina=2");
    Check(page2.GetProperty("pagos")[0].GetProperty("id").GetGuid() != history.GetProperty("pagos")[0].GetProperty("id").GetGuid() && !page2.GetProperty("hayMasResultados").GetBoolean(), "Segunda página sin duplicación");
    foreach (var query in new[] { "pagina=0", "pagina=1000001", "tamanoPagina=0", "tamanoPagina=101" })
        Check((await client.GetAsync("/api/miembros/1/pagos?" + query)).StatusCode == HttpStatusCode.BadRequest, "Paginación inválida: 400");
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = await db.Miembros.FindAsync(1);
        member!.Nombre = "Nombre cambiado en prueba";
        await db.SaveChangesAsync();
    }
    var saved = await client.GetFromJsonAsync<PagoDto>($"/api/pagos/{original.Id}");
    Check(saved == original, "Comprobante conserva los datos históricos");
    Check((await client.GetAsync("/api/miembros/1")).StatusCode == HttpStatusCode.OK && (await client.GetAsync("/api/miembros")).StatusCode == HttpStatusCode.OK, "Perfil y listado de miembros conservados");
    var registration = await client.PostAsJsonAsync("/api/miembros/registro", new { nombre = "Nuevo miembro prueba", cedula = "003-1234567-8", telefono = "809-555-0103", correo = "tres@example.invalid", empleadoRegistroId = 1 });
    Check(registration.StatusCode == HttpStatusCode.Created, "Registro de miembros conservado");
    Check((await client.PostAsJsonAsync("/api/miembros/registro", new { nombre = "Duplicado", cedula = "00312345678", telefono = "8095550103", correo = "tres@example.invalid", empleadoRegistroId = 1 })).StatusCode == HttpStatusCode.Conflict, "Duplicados de miembros conservados");

    await using var sql = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=localhost;Database=HU03_NO_CONECTAR;Integrated Security=True;TrustServerCertificate=True").Options);
    Check(!sql.Database.HasPendingModelChanges(), "Migración y snapshot corresponden al modelo");
    string script = sql.GetService<IMigrator>().GenerateScript("20261031211726_InitialCreate");
    Check(script.Contains("CREATE TABLE [Pagos]") && script.Contains("CREATE UNIQUE INDEX [IX_Pagos_ClaveIdempotencia]") && script.Contains("FOREIGN KEY ([MiembroId])") && script.Contains("CHECK ([Importe] > 0)"), "Script SQL contiene tabla, FK, unicidad e importe positivo");
    Check(!script.Contains("DROP TABLE [Miembros]") && !script.Contains("ALTER TABLE [Miembros]") && !script.Contains("ON DELETE CASCADE"), "Migración conserva Miembros y no elimina pagos en cascada");
    string sqlQuery = sql.Pagos.AsNoTracking().Where(p => p.MiembroId == 1).OrderByDescending(p => p.FechaRegistroUtc).ThenByDescending(p => p.Id).Skip(20).Take(21).ToQueryString();
    Check(sqlQuery.Contains("WHERE") && sqlQuery.Contains("ORDER BY") && sqlQuery.Contains("OFFSET"), "Historial se traduce a SQL Server paginado");
    var paymentType = sql.Model.FindEntityType(typeof(Pago))!;
    Check(paymentType.GetForeignKeys().Single().DeleteBehavior == DeleteBehavior.Restrict, "Relación protege el historial ante borrado de miembro");
    await using var brokenDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(new FailingSave()).Options);
    // Una base aislada adicional evita depender del proveedor interno del host.
    brokenDb.Miembros.Add(new Miembro { Id = 1 });
    // El interceptor solo falla cuando se intenta insertar un Pago.
    await brokenDb.SaveChangesAsync();
    bool propagated = false;
    try { await new PagoService(brokenDb).RegistrarAsync(Dto(Guid.NewGuid())); }
    catch (DbUpdateException) { propagated = true; }
    Check(propagated, "Error genérico de persistencia no se transforma en duplicado");
    Console.WriteLine($"RESULTADO: {checks} comprobaciones aprobadas. Sin SQL Server real: concurrencia, FK e índices se inspeccionan pero no se prueban ejecutándolos.");
}
finally { await app.StopAsync(); }

sealed class FailingSave : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<Pago>().Any(e => e.State == EntityState.Added))
            throw new DbUpdateException("Fallo simulado no relacionado con unicidad");
        return ValueTask.FromResult(result);
    }
}
