using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using GimnasioFITLAB.Data;
using GimnasioFITLAB.Models;
using GimnasioFITLAB.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Esta herramienta solo añade datos ficticios a FITLAB_Pruebas. No borra ni migra bases.
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var results = new List<object>();
string phase = "configuración";
string? connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
string? token = Environment.GetEnvironmentVariable("FITLAB_ACCESS_TOKEN");
void Fase(string value)
{
    phase = value;
    if(Environment.GetEnvironmentVariable("FITLAB_INTERACTIVO")=="1") { Console.WriteLine("\nPulsa Intro para mostrar " + value + ":"); Console.ReadLine(); }
    Console.WriteLine("\n=== " + value + " ===");
}
string run = Guid.NewGuid().ToString("N")[..10];
const int actorFixture = 7001; // Parámetro directo del servicio; no es una cuenta o sesión.
int checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    checks++;
    results.Add(new { fase = phase, comprobacion = description, resultado = "APROBADA", proveedor = "SQL Server" });
    Console.WriteLine($"OK [{phase}] {description}");
}
void HttpStatus(HttpResponseMessage response, HttpStatusCode expected, string description)
    => Check(response.StatusCode == expected, $"{description}: HTTP {(int)response.StatusCode}, esperado {(int)expected}");

try
{
    if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Falta ConnectionStrings__DefaultConnection; no se imprime su valor.");
    var sqlConfig = new SqlConnectionStringBuilder(connection);
    if (!string.Equals(sqlConfig.InitialCatalog, "FITLAB_Pruebas", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("La herramienta solo acepta la base FITLAB_Pruebas.");
    var address = new Uri(Environment.GetEnvironmentVariable("FITLAB_API_URL") ?? "http://127.0.0.1:5266");
    if (!address.IsLoopback || address.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(address.UserInfo) || !string.IsNullOrEmpty(address.Query))
        throw new InvalidOperationException("FITLAB_API_URL debe ser una URL HTTP/HTTPS local sin credenciales ni parámetros.");
    var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
    AppDbContext Db() => new(options);
    using var client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { BaseAddress = address, Timeout = TimeSpan.FromSeconds(30) };
    if (!string.IsNullOrWhiteSpace(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    using var anonymousClient = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { BaseAddress = address };
    if (Environment.GetEnvironmentVariable("FITLAB_REQUIRE_HTTP") == "1" && string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Esta demostración exige una sesión real; no admite modo de servicios.");
    async Task<HttpResponseMessage> Protected(HttpMethod method, string path, object body, string? etag = null, bool authenticate = true)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        if (authenticate && !string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (etag != null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        return await (authenticate ? client : anonymousClient).SendAsync(request);
    }
    RegistroPagoDto Payment(int member, Guid? key = null, decimal amount = 500m) => new()
    {
        MiembroId = member, Importe = amount, Moneda = "DOP", MetodoPago = "PRUEBA_TERMINAL",
        ReferenciaExterna = "FICTICIO-" + run, EmpleadoRegistroId = 1, ClaveIdempotencia = key ?? Guid.NewGuid()
    };
    async Task<Miembro> Register(string suffix)
    {
        string cedula = "999" + RandomNumberGenerator.GetInt32(100_000_000).ToString("D8");
        var body = new { nombre = $"PRUEBA VIDEO {run} {suffix}", cedula, telefono = "8095550101", correo = $"fitlab-{run}-{suffix}@example.invalid", empleadoRegistroId = 1 };
        using var response = await client.PostAsJsonAsync("/api/miembros/registro", body);
        HttpStatus(response, HttpStatusCode.Created, "Registro de miembro completamente ficticio");
        var member = await response.Content.ReadFromJsonAsync<Miembro>() ?? throw new InvalidOperationException("Registro sin perfil de respuesta.");
        await using var db = Db();
        var persisted = await db.Miembros.AsNoTracking().SingleAsync(m => m.Id == member.Id);
        Check(persisted.Cedula == cedula && persisted.Activo, "Registro persistido y activo en SQL Server");
        return persisted;
    }

    Console.WriteLine("FITLAB — demostración con SQL Server y datos ficticios. Sin borrado de datos.");
    Console.WriteLine(string.IsNullOrWhiteSpace(token)
        ? "MODO MIXTO: HTTP para operaciones disponibles; contacto y baja por servicios directos con actor fixture. NO acredita autenticación real."
        : "MODO HTTP: se utiliza una credencial suministrada externamente, sin mostrarla. Solo se acreditará acceso si la API lo acepta.");
    Console.WriteLine("El servidor obtiene el empleado de la sesión real; el campo legacy no concede identidad ni permisos.");
    Fase("SQL Server");
    await using (var db = Db())
    {
        Check(db.Database.IsSqlServer() && await db.Database.CanConnectAsync(), "Conexión real con SQL Server");
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT DB_NAME(), CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion')), CONVERT(nvarchar(128),SERVERPROPERTY('Edition'))";
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        Check(reader.GetString(0).Equals("FITLAB_Pruebas", StringComparison.OrdinalIgnoreCase), "Base de datos efectiva FITLAB_Pruebas");
        Console.WriteLine(JsonSerializer.Serialize(new { motor = "SQL Server", version = reader.GetString(1), edicion = reader.GetString(2), baseDatos = "FITLAB_Pruebas" }));
    }
    await using (var db = Db())
    {
        Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Migraciones aplicadas; no se aplican cambios de esquema desde este runner");
        Check(!db.Database.HasPendingModelChanges(), "Snapshot y modelo EF consistentes");
    }

    Fase("HU01");
    using (var invalid = await client.PostAsJsonAsync("/api/miembros/registro", new { nombre = "", cedula = "incorrecta" }))
        HttpStatus(invalid, HttpStatusCode.BadRequest, "Registro inválido rechazado");
    var member = await Register("principal");
    Console.WriteLine(JsonSerializer.Serialize(new { miembroFicticio = member.Id, member.CodigoUnico, nombreFicticio = member.Nombre }));
    using (var duplicate = await client.PostAsJsonAsync("/api/miembros/registro", new { nombre = member.Nombre, cedula = member.Cedula, telefono = "8095550101", correo = member.Correo, empleadoRegistroId = 1 }))
        HttpStatus(duplicate, HttpStatusCode.Conflict, "Cédula duplicada rechazada");

    Fase("HU02");
    var search = await client.GetFromJsonAsync<JsonElement>("/api/miembros/buscar?termino=" + Uri.EscapeDataString(member.Cedula));
    Check(search.GetProperty("miembros").EnumerateArray().Any(m => m.GetProperty("id").GetInt32() == member.Id), "Búsqueda HTTP encuentra al miembro creado");
    using var profileResponse = await client.GetAsync($"/api/miembros/{member.Id}");
    HttpStatus(profileResponse, HttpStatusCode.OK, "Consulta HTTP del perfil");
    var etag = profileResponse.Headers.ETag?.Tag ?? throw new InvalidOperationException("El perfil no entregó ETag.");
    Check(etag == $"\"{member.VersionContacto:D}\"", "ETag del perfil coincide con la versión SQL");
    using (var anonymous = await Protected(HttpMethod.Put, $"/api/miembros/{member.Id}/contacto", new { telefono = "8295550102", correo = $"actualizado-{run}@example.invalid" }, etag, false))
        HttpStatus(anonymous, HttpStatusCode.Unauthorized, "Actualización sin identidad verificada rechazada");
    var contact = new ActualizarContactoDto { Telefono = "8295550102", Correo = $"actualizado-{run}@example.invalid" };
    if (string.IsNullOrWhiteSpace(token))
    {
        await using var db = Db();
        var changed = await new ContactoMiembroService(db, TimeProvider.System).ActualizarAsync(member.Id, contact, member.VersionContacto, actorFixture);
        Check(changed.Estado == EstadoContacto.Actualizado, "SERVICIO directo actualiza contacto; actor fixture, sin sesión");
    }
    else
    {
        using var missingVersion = await Protected(HttpMethod.Put, $"/api/miembros/{member.Id}/contacto", contact);
        HttpStatus(missingVersion, (HttpStatusCode)428, "Actualización autorizada exige versión");
        using var updated = await Protected(HttpMethod.Put, $"/api/miembros/{member.Id}/contacto", contact, etag);
        HttpStatus(updated, HttpStatusCode.NoContent, "Actualización HTTP con identidad validada por la API");
        using var obsolete = await Protected(HttpMethod.Put, $"/api/miembros/{member.Id}/contacto", contact, etag);
        HttpStatus(obsolete, HttpStatusCode.PreconditionFailed, "HTTP rechaza ETag obsoleto");
    }
    await using (var db = Db())
    {
        var saved = await db.Miembros.AsNoTracking().SingleAsync(m => m.Id == member.Id);
        var audit = await db.AuditoriasContacto.AsNoTracking().SingleAsync(a => a.MiembroId == member.Id);
        Check(saved.Telefono == contact.Telefono && saved.Correo == contact.Correo, "Contacto persistido en SQL Server desde contexto nuevo");
        Check(saved.Cedula == member.Cedula && saved.CodigoUnico == member.CodigoUnico && saved.EmpleadoRegistroId == member.EmpleadoRegistroId, "Identidad y empleado de alta conservados");
        Check(audit.EmpleadoId > 0 && audit.FechaUtc.Offset == TimeSpan.Zero && audit.VersionNueva == saved.VersionContacto, "Auditoría persistente con empleado, fecha UTC y versión");
        if (string.IsNullOrWhiteSpace(token)) Check(audit.EmpleadoId == actorFixture, "Auditoría identifica explícitamente el parámetro de fixture del servicio");
        Console.WriteLine(JsonSerializer.Serialize(new { auditoriaContacto = audit.Id, miembro = member.Id, audit.FechaUtc, modo = string.IsNullOrWhiteSpace(token) ? "SERVICIO_FIXTURE_NO_AUTENTICACION" : "HTTP_API_VERIFICO_CREDENCIAL" }));
    }

    Fase("HU03");
    var payment = Payment(member.Id);
    using (var invalid = await client.PostAsJsonAsync("/api/pagos", Payment(member.Id, amount: -1)))
        HttpStatus(invalid, HttpStatusCode.BadRequest, "Importe inválido rechazado");
    using var paid = await client.PostAsJsonAsync("/api/pagos", payment);
    HttpStatus(paid, HttpStatusCode.Created, "Pago registrado por HTTP");
    var receipt = await paid.Content.ReadFromJsonAsync<PagoDto>() ?? throw new InvalidOperationException("Pago sin comprobante.");
    using (var repeated = await client.PostAsJsonAsync("/api/pagos", payment))
    {
        HttpStatus(repeated, HttpStatusCode.OK, "Reintento idempotente");
        Check((await repeated.Content.ReadFromJsonAsync<PagoDto>())?.Id == receipt.Id, "Reintento devuelve el mismo pago");
    }
    using (var conflicting = await client.PostAsJsonAsync("/api/pagos", Payment(member.Id, payment.ClaveIdempotencia, 501m)))
        HttpStatus(conflicting, HttpStatusCode.Conflict, "Clave repetida con datos distintos rechazada");
    var history = await client.GetFromJsonAsync<JsonElement>($"/api/miembros/{member.Id}/pagos");
    Check(history.GetProperty("pagos").GetArrayLength() == 1, "Historial HTTP devuelve un único pago");
    await using (var db = Db()) Check(await db.Pagos.CountAsync(p => p.MiembroId == member.Id) == 1, "Un pago persistido sin duplicados en SQL Server");
    Console.WriteLine(JsonSerializer.Serialize(new { pago = receipt.Id, receipt.NumeroComprobante, receipt.Importe, receipt.Moneda }));

    Fase("HU04");
    using (var renewal = await client.PostAsync($"/api/renovaciones/{receipt.Id}", null))
    {
        HttpStatus(renewal, HttpStatusCode.OK, "Renovación aplica el pago");
        var value = await renewal.Content.ReadFromJsonAsync<JsonElement>();
        var begin = DateOnly.Parse(value.GetProperty("inicio").GetString()!);
        var end = DateOnly.Parse(value.GetProperty("vencimiento").GetString()!);
        Check(end == begin.AddMonths(1), "Vencimiento suma un mes calendario");
        await using var db = Db();
        var membership = await db.Membresias.AsNoTracking().SingleAsync(m => m.MiembroId == member.Id);
        Check(membership.Inicio == begin && membership.Vencimiento == end && (await db.Pagos.SingleAsync(p => p.Id == receipt.Id)).Aplicado, "Membresía y aplicación del pago persistidas en SQL Server");
        Console.WriteLine(JsonSerializer.Serialize(new { miembro = member.Id, inicio = begin, vencimiento = end }));
    }
    using (var repeated = await client.PostAsync($"/api/renovaciones/{receipt.Id}", null))
        HttpStatus(repeated, HttpStatusCode.Conflict, "Renovación duplicada rechazada");
    using (var missing = await client.PostAsync($"/api/renovaciones/{Guid.NewGuid()}", null))
        HttpStatus(missing, HttpStatusCode.NotFound, "Pago inexistente no renueva");
    using (var validity = await client.GetAsync($"/api/renovaciones/miembro/{member.Id}"))
        HttpStatus(validity, HttpStatusCode.OK, "Consulta HTTP de vigencia disponible");

    Fase("HU05");
    using (var anonymous = await Protected(HttpMethod.Post, $"/api/miembros/{member.Id}/baja", new { empleadoId = 1 }, authenticate: false))
        HttpStatus(anonymous, HttpStatusCode.Unauthorized, "Declarar empleadoId no autoriza una baja");
    if (string.IsNullOrWhiteSpace(token))
    {
        await using var db = Db();
        Check((await new MiembroBajaService(db).DarDeBajaAsync(member.Id, actorFixture)).Estado == EstadoBaja.Baja, "SERVICIO directo desactiva; actor fixture, sin sesión");
    }
    else
    {
        using var deactivated = await Protected(HttpMethod.Post, $"/api/miembros/{member.Id}/baja", new { });
        HttpStatus(deactivated, HttpStatusCode.OK, "Baja HTTP con identidad validada por la API");
    }
    await using (var db = Db())
    {
        var saved = await db.Miembros.AsNoTracking().SingleAsync(m => m.Id == member.Id);
        Check(!saved.Activo && saved.FechaBaja != null && saved.EmpleadoBajaId > 0, "Miembro inactivo y auditoría de baja persistidos en SQL Server");
        Check(await db.Pagos.CountAsync(p => p.MiembroId == member.Id) == 1 && await db.Membresias.CountAsync(m => m.MiembroId == member.Id) == 1, "Baja conserva pagos y membresía");
        Console.WriteLine(JsonSerializer.Serialize(new { miembro = saved.Id, saved.Activo, saved.FechaBaja, modo = string.IsNullOrWhiteSpace(token) ? "SERVICIO_FIXTURE_NO_AUTENTICACION" : "HTTP_API_VERIFICO_CREDENCIAL" }));
    }
    search = await client.GetFromJsonAsync<JsonElement>("/api/miembros/buscar?termino=" + Uri.EscapeDataString(member.Cedula));
    Check(search.GetProperty("miembros").GetArrayLength() == 0, "Búsqueda HTTP oculta al inactivo");
    var list = await client.GetFromJsonAsync<JsonElement>("/api/miembros");
    Check(list.EnumerateArray().All(m => m.GetProperty("id").GetInt32() != member.Id), "Listado HTTP excluye al inactivo");
    using (var blocked = await client.PostAsJsonAsync("/api/pagos", Payment(member.Id)))
        HttpStatus(blocked, HttpStatusCode.Conflict, "Nuevo pago de inactivo rechazado por HTTP");
    history = await client.GetFromJsonAsync<JsonElement>($"/api/miembros/{member.Id}/pagos");
    Check(history.GetProperty("pagos").GetArrayLength() == 1 && history.GetProperty("pagos")[0].GetProperty("id").GetGuid() == receipt.Id, "Historial HTTP conservado después de baja");
    using (var receiptResponse = await client.GetAsync($"/api/pagos/{receipt.Id}"))
        HttpStatus(receiptResponse, HttpStatusCode.OK, "Comprobante histórico conservado");

    if (Environment.GetEnvironmentVariable("FITLAB_SOLO_HISTORIAS") != "1")
    {
    Fase("Concurrencia SQL Server");
    var concurrentMember = await Register("concurrencia");
    using var start = new ManualResetEventSlim(false);
    Task<ResultadoContacto> Edit(string phone) => Task.Run(async () =>
    {
        start.Wait(); await using var db = Db();
        return await new ContactoMiembroService(db, TimeProvider.System).ActualizarAsync(concurrentMember.Id,
            new ActualizarContactoDto { Telefono = phone, Correo = $"concurrencia-{run}@example.invalid" }, concurrentMember.VersionContacto, actorFixture);
    });
    var one = Edit("8295550103"); var two = Edit("8495550104"); start.Set();
    var edits = await Task.WhenAll(one, two);
    Check(edits.Count(r => r.Estado == EstadoContacto.Actualizado) == 1 && edits.Count(r => r.Estado == EstadoContacto.VersionObsoleta) == 1, "SQL Server: dos escritores con la misma versión, solo uno confirma");
    await using (var db = Db()) Check(await db.AuditoriasContacto.CountAsync(a => a.MiembroId == concurrentMember.Id) == 1, "Concurrencia deja una sola auditoría confirmada");
    using var raceStart = new ManualResetEventSlim(false);
    var deactivationTask = Task.Run(async () => { raceStart.Wait(); await using var db = Db(); return await new MiembroBajaService(db).DarDeBajaAsync(concurrentMember.Id, actorFixture); });
    var paymentTask = Task.Run(async () => { raceStart.Wait(); await using var db = Db(); return await new PagoService(db).RegistrarAsync(Payment(concurrentMember.Id)); });
    raceStart.Set(); await Task.WhenAll(deactivationTask, paymentTask);
    Check(deactivationTask.Result.Estado == EstadoBaja.Baja && paymentTask.Result.Estado is EstadoRegistroPago.Creado or EstadoRegistroPago.MiembroInactivo, "SQL Server: carrera baja/cobro termina en un orden coherente");
    await using (var db = Db())
    {
        Check(await db.Pagos.CountAsync(p => p.MiembroId == concurrentMember.Id) == (paymentTask.Result.Estado == EstadoRegistroPago.Creado ? 1 : 0), "Pagos SQL coinciden con el resultado de la carrera");
        Check((await new PagoService(db).RegistrarAsync(Payment(concurrentMember.Id))).Estado == EstadoRegistroPago.MiembroInactivo, "Después de la baja no se acepta otro cobro");
    }
    }
    var summary = JsonSerializer.Serialize(new { resultado = "APROBADO", proveedor = "SQL Server", baseDatos = "FITLAB_Pruebas", ejecucion = run, miembroId = member.Id, pagoId = receipt.Id, comprobaciones = checks,
        modo = string.IsNullOrWhiteSpace(token) ? "MIXTO_HTTP_Y_SERVICIOS_FIXTURE" : "HTTP_CON_CREDENCIAL_EXTERNA", accesoHttpAutorizadoVerificado = !string.IsNullOrWhiteSpace(token),
        aclaracion = Environment.GetEnvironmentVariable("FITLAB_SOLO_HISTORIAS") == "1" ? "Las cinco historias se verifican por HTTP con sesión real. No se borraron datos." : "Las pruebas concurrentes adicionales invocan servicios directamente con parámetros de fixture. No se borraron datos.", resultados = results }, json);
    if (Environment.GetEnvironmentVariable("FITLAB_RESULTADO_JSON") is { Length: > 0 } output) await File.WriteAllTextAsync(output, summary);
    Console.WriteLine($"\nRESULTADO: {checks} comprobaciones aprobadas en SQL Server. Cinco historias verificadas por HTTP: {!string.IsNullOrWhiteSpace(token)}.");
    return 0;
}
catch (Exception error)
{
    // No se imprime la excepción completa: puede contener parámetros privados del proveedor.
    string detail = error is SqlException sql ? $"Error SQL {sql.Number}" : error is InvalidOperationException ? error.Message : error.GetType().Name;
    if (!string.IsNullOrEmpty(connection)) detail = detail.Replace(connection, "[CONEXION_OCULTA]");
    if (!string.IsNullOrEmpty(token)) detail = detail.Replace(token, "[CREDENCIAL_OCULTA]");
    Console.Error.WriteLine(JsonSerializer.Serialize(new { resultado = "NO_COMPLETADO", fase = phase, comprobacionesAprobadas = checks, detalle = detail, resultados = results }, json));
    return 1;
}
