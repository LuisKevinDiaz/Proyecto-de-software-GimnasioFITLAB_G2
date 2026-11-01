# HU04: cálculo de vigencia e integración pendiente

Historia: SCRUM-13. Rama: `feature/HU04-integracion`. PR: #6 hacia `develop`.

## Alcance implementado

`CalculoVigenciaService` calcula fechas; no registra pagos ni modifica la base de datos.

- `CalcularVencimiento(fechaBase, meses)` suma meses calendario y rechaza duraciones menores o iguales a cero.
- `CalcularPeriodo(vencimientoAnterior, fechaPago)` calcula una mensualidad.
- Si el pago es anterior al vencimiento o cae ese mismo día, el nuevo período comienza en el vencimiento anterior.
- Si el pago es posterior, el período comienza en la fecha del pago.
- Cuando el día no existe en el mes de destino, se usa el último día de ese mes.

Estas reglas proceden del prototipo HU04 de cálculo de vigencia. Los criterios completos de Jira deben contrastarse antes de dar la historia por terminada.

## Ejecutar las pruebas

Desde la raíz del repositorio, con el SDK de .NET 9:

```bash
dotnet restore tests/FITLAB-HU04-Tests/FITLAB-HU04-Tests.csproj
dotnet build tests/FITLAB-HU04-Tests/FITLAB-HU04-Tests.csproj --configuration Release --no-restore
dotnet test tests/FITLAB-HU04-Tests/FITLAB-HU04-Tests.csproj --configuration Release --no-build --no-restore
```

Hay 16 casos unitarios: los 11 originales y 5 adicionales para fin de mes, año bisiesto, duraciones de varios meses y renovaciónes que terminan en febrero. No necesitan SQL Server y no prueban todavía la persistencia ni la validación de pagos.

El workflow `.github/workflows/hu04-tests.yml` ejecuta compilación, pruebas y publicación de la API al actualizar esta rama o un PR hacia `develop`. Los resultados TRX quedan como artefacto de la ejecucion.

El proyecto principal excluye `tests/**` tanto de compilación como de contenido y archivos auxiliares. Esto impide copiar resultados de pruebas dentro de la API y volver a copiarlos de forma anidada en compilaciónes posteriores.

## Dependencia real para conectar la renovación

En las ramas publicadas revisadas el 31 de octubre de 2026 no existe aún el modulo de pagos/renovaciónes: `AppDbContext` solo expone `Miembros`; `Miembro` no contiene membresía o vigencia; el controlador solo registra y consulta miembros. La lógica de pagos HU04-T3 figura como dependencia en el prototipo.

Antes de crear entidades o endpoints hay que conocer el contrato de HU04-T3 para no duplicar su implementacion. La conexión debe usar datos consultados del pago y la membresía en el servidor. No debe aceptar del cliente una bandera `pagoValido` ni un vencimiento anterior como prueba de que la renovación esta autorizada.

Flujo de integración a implementar con el modulo real:

1. Consultar el pago y comprobar su existencia, miembro propietario y validez conforme a HU04-T3.
2. Comprobar que el pago no haya sido aplicado a otra renovación.
3. Consultar la vigencia persistida de la membresía. Para miembros sin vigencia previa falta confirmar el contrato de primera activacion.
4. Llamar a `CalculoVigenciaService.CalcularPeriodo(vencimientoAnterior, fechaPago)` con las fechas verificadas del servidor.
5. Guardar el período nuevo y la aplicación del pago como una sola operación atómica, con protección ante solicitudes concurrentes y pagos repetidos.
6. Devolver el período que se guardo y verificarlo leyendo desde un contexto de base de datos nuevo.

## Pruebas de integración pendientes

| Caso | Resultado a verificar con HU04-T3 y la base de datos |
| --- | --- |
| Pago válido antes del vencimiento | Empieza en el vencimiento anterior; conserva los días restántes |
| Pago válido el día del vencimiento | Empieza ese mismo día |
| Pago válido después del vencimiento | Empieza en la fecha del pago |
| Pago inexistente, inválido o de otro miembro | Rechazo sin cambiar la vigencia |
| Pago ya utilizado o enviado dos veces | Ninguna segunda extension de vigencia |
| Dos solicitudes simultaneas | Una sola aplicación del mismo pago |
| Falla al guardar | No queda el pago aplicado sin su período ni el período sin el pago |
| Nueva consulta a la base de datos | Recupera exactamente las fechas guardadas |
| Registro y consulta de miembros (HU01) | Conservan el comportamiento existente |

## Criterio de cierre

Mantener el PR en borrador hasta completar el contrato de HU04-T3, la conexión al flujo real y las pruebas con base de datos. Después registrar la evidencia en SCRUM-13 y sus subtareas correspondientes, y solicitar revisión al equipo. Tener pruebas unitarias aprobadas no completa por sí solo HU04.
