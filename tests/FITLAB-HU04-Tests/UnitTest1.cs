
using System;
using TuProyecto.Services;
using Xunit;

public class CalculoVigenciaTests
{
    // Pruebas de meses calendario
    [Theory]
    [InlineData("2024-01-31", 1, "2024-02-29")]
    [InlineData("2025-01-31", 1, "2025-02-28")]
    [InlineData("2025-03-31", 1, "2025-04-30")]
    [InlineData("2025-04-30", 1, "2025-05-30")]
    [InlineData("2025-12-31", 1, "2026-01-31")]
    [InlineData("2026-02-28", 12, "2027-02-28")]
    [InlineData("2027-01-31", 1, "2027-02-28")]
    public void CalculaVencimientoCorrectamente(
        string fecha, int meses, string esperado)
    {
        var resultado = CalculoVigenciaService
            .CalcularVencimiento(DateOnly.Parse(fecha), meses);

        Assert.Equal(DateOnly.Parse(esperado), resultado);
    }

    // 2 pruebas de duraciones inválidas
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RechazaDuracionesInvalidas(int meses)
    {
        Assert.Throws<ArgumentException>(() =>
            CalculoVigenciaService.CalcularVencimiento(
                new DateOnly(2026, 10, 31), meses));
    }

    // Renovación anticipada
    [Fact]
    public void RenovacionAnticipadaSegunCriterioDeJira()
    {
        var resultado = CalculoVigenciaService.CalcularPeriodo(
            new DateOnly(2026, 11, 10),
            new DateOnly(2026, 11, 5));

        Assert.Equal(new DateOnly(2026, 11, 10), resultado.Inicio);
        Assert.Equal(new DateOnly(2026, 12, 10), resultado.Vencimiento);
    }

    // Renovación el día del vencimiento
    [Fact]
    public void RenovacionDiaVencimiento()
    {
        var resultado = CalculoVigenciaService.CalcularPeriodo(
            new DateOnly(2026, 11, 10),
            new DateOnly(2026, 11, 10));

        Assert.Equal(new DateOnly(2026, 11, 10), resultado.Inicio);
        Assert.Equal(new DateOnly(2026, 12, 10), resultado.Vencimiento);
    }

    // Renovación después del vencimiento
    [Fact]
    public void RenovacionVencida()
    {
        var resultado = CalculoVigenciaService.CalcularPeriodo(
            new DateOnly(2026, 11, 10),
            new DateOnly(2026, 11, 15));

        Assert.Equal(new DateOnly(2026, 11, 15), resultado.Inicio);
        Assert.Equal(new DateOnly(2026, 12, 15), resultado.Vencimiento);
    }
}
