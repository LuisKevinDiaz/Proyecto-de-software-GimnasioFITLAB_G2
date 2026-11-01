using TuProyecto.Services;

public class CalculoVigenciaCalendarioTests
{
    [Theory]
    [InlineData("2024-02-29", 12, "2025-02-28")]
    [InlineData("2025-11-30", 3, "2026-02-28")]
    [InlineData("2026-08-31", 1, "2026-09-30")]
    public void RespetaElCalendarioEnPeriodosDeVariosMesesYAnos(
        string fecha, int meses, string esperado)
    {
        var vencimiento = CalculoVigenciaService.CalcularVencimiento(
            DateOnly.Parse(fecha), meses);

        Assert.Equal(DateOnly.Parse(esperado), vencimiento);
    }

    [Theory]
    [InlineData("2024-01-31", "2024-01-20", "2024-01-31", "2024-02-29")]
    [InlineData("2025-01-20", "2025-01-31", "2025-01-31", "2025-02-28")]
    public void RenovacionConservaLosDiasPagadosYRespetaElFinDeMes(
        string anterior, string pago, string inicioEsperado, string vencimientoEsperado)
    {
        var periodo = CalculoVigenciaService.CalcularPeriodo(
            DateOnly.Parse(anterior), DateOnly.Parse(pago));

        Assert.Equal(DateOnly.Parse(inicioEsperado), periodo.Inicio);
        Assert.Equal(DateOnly.Parse(vencimientoEsperado), periodo.Vencimiento);
    }
}
