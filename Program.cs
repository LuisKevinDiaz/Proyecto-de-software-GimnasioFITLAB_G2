using Microsoft.EntityFrameworkCore;
using GimnasioFITLAB.Data;

var builder = WebApplication.CreateBuilder(args);

// Controladores
builder.Services.AddControllers();
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
app.UseAuthorization();
app.MapControllers();

app.Run();



