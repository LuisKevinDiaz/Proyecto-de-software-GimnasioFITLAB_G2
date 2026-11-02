using GimnasioFITLAB.Security;
using Microsoft.EntityFrameworkCore;
using GimnasioFITLAB.Data;

var builder = WebApplication.CreateBuilder(args);

// Controladores
builder.Services.AddControllers();
builder.Services.AddAccesoEmpleadosSeguro(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<GimnasioFITLAB.Services.ContactoMiembroService>();
builder.Services.AddScoped<GimnasioFITLAB.Services.PagoService>();

// OpenAPI nativo de .NET 9 
builder.Services.AddOpenApi();

// Conexión a la base de datos (SQL Server)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<GimnasioFITLAB.Services.RenovacionService>();

builder.Services.AddScoped<GimnasioFITLAB.Services.MiembroBajaService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/estado", async (AppDbContext db, EstadoAutenticacionLocal estado) =>
    Results.Ok(new { aplicacion = "FITLAB", version = "entrega-local-v1", baseDatos = db.Database.GetDbConnection().Database,
        sqlServer = await db.Database.CanConnectAsync(), autenticacionLocal = estado.Habilitada }));

app.Run();



