using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GimnasioFITLAB.Data;
using GimnasioFITLAB.Models;

namespace GimnasioFITLAB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MiembrosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MiembrosController(AppDbContext context) => _context = context;

        [HttpPost("registro")]
        public async Task<IActionResult> Registrar([FromBody] RegistroMiembroDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Se guarda siempre sin guiones, así el índice único atrapa duplicados
            string cedula = dto.Cedula.Replace("-", "");

            var existente = await _context.Miembros.FirstOrDefaultAsync(m => m.Cedula == cedula);
            if (existente != null)
            {
                return Conflict(new { mensaje = "Ya existe un miembro con esta cédula.", perfil = existente });
            }

            var miembro = new Miembro
            {
                CodigoUnico = Guid.NewGuid().ToString("N")[..20], // temporal, se reemplaza abajo
                Nombre = dto.Nombre.Trim(),
                Cedula = cedula,
                Telefono = dto.Telefono.Trim(),
                Correo = dto.Correo.Trim(),
                EmpleadoRegistroId = dto.EmpleadoRegistroId,
                FechaAlta = DateTime.Now
            };

            try
            {
                _context.Miembros.Add(miembro);
                await _context.SaveChangesAsync();

                // Código basado en el Id: nunca se repite
                miembro.CodigoUnico = $"MBR-{DateTime.Now.Year}-{miembro.Id:D5}";
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Dos recepcionistas registrando la misma cédula a la vez
                var duplicado = await _context.Miembros.FirstOrDefaultAsync(m => m.Cedula == cedula);
                return Conflict(new { mensaje = "Ya existe un miembro con esta cédula.", perfil = duplicado });
            }

            return CreatedAtAction(nameof(ObtenerPorId), new { id = miembro.Id }, miembro);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> ObtenerPorId(int id)
        {
            var miembro = await _context.Miembros.FindAsync(id);
            return miembro == null ? NotFound() : Ok(miembro);
        }

        [HttpGet]
        public async Task<IActionResult> Listar() =>
            Ok(await _context.Miembros.Where(m => m.Activo).OrderByDescending(m => m.FechaAlta).ToListAsync());
    }
}
