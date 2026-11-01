using System;

namespace TuProyecto.Services;

public static class CalculoVigenciaService
{
    public static DateOnly CalcularVencimiento(
        DateOnly fechaBase,
        int meses)
    {
        if (meses <= 0)
        {
            throw new ArgumentException(
                "La cantidad de meses debe ser mayor que cero.");
        }

        return fechaBase.AddMonths(meses);
    }

    public static (DateOnly Inicio, DateOnly Vencimiento)
        CalcularPeriodo(
            DateOnly vencimientoAnterior,
            DateOnly fechaPago)
    {
        DateOnly inicio = fechaPago > vencimientoAnterior
            ? fechaPago
            : vencimientoAnterior;

        DateOnly vencimiento =
            CalcularVencimiento(inicio, 1);

        return (inicio, vencimiento);
    }
}
