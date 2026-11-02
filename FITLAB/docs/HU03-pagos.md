# HU03: registro y consulta de pagos

Base: `origin/main`, commit `142f452`. Rama independiente `feature/HU03-pagos`. No contiene el commit exclusivo de búsqueda de HU02 ni integra HU04.

## Contrato

- `POST /api/pagos`: registro nuevo 201 con Location; reintento idéntico 200 con el comprobante original; clave reutilizada con otros datos 409; miembro inexistente 404; validación fallida 400.
- `GET /api/pagos/{id:guid}`: pago individual 200 o 404.
- `GET /api/miembros/{miembroId:int}/pagos?pagina=1&tamanoPagina=20`: historial del miembro, ordenado por fecha UTC descendente e ID descendente. Tamaño entre 1 y 100; página entre 1 y 1.000.000 para mantener el desplazamiento acotado. Respuesta: `pagos`, `pagina`, `tamanoPagina`, `hayMasResultados`. Miembro existente sin pagos: 200 con lista vacía. Miembro inexistente: 404.

El cuerpo de registro contiene `miembroId`, `importe`, `moneda`, `metodoPago`, `empleadoRegistroId`, `claveIdempotencia` y opcionalmente `referenciaExterna`. Los ejemplos de Pagos.http son datos de prueba, no valores comerciales.

`importe` usa decimal(18,2), es positivo y admite hasta dos decimales, sin redondeo silencioso. Su máximo es el representable por la columna, no un límite comercial. `moneda` admite tres letras y se normaliza a mayúsculas. `metodoPago` es una declaración obligatoria de hasta 50 caracteres: no hay catálogo oficial para validarlo. La referencia externa admite hasta 100 caracteres. Se recortan espacios externos del método y la referencia; una referencia vacía se guarda como null. No se exige unicidad de referencia externa sin una regla comercial que la justifique.

La fecha UTC la genera el servidor. No se admite una fecha de pago retroactiva. La identidad del empleado llega en el cuerpo como entero positivo: **no está autenticada ni validada contra una tabla de empleados**, porque el proyecto no dispone de esas capacidades. Los endpoints no incorporan autenticación nueva. Cuando exista, el empleado deberá obtenerse de la identidad verificada, y deberá definirse quién puede consultar o registrar pagos.

## Comprobante interno

La respuesta incluye ID, número `PAGO-{Id sin guiones}`, importe, moneda, método, referencia, fecha UTC, identificador de empleado y copia histórica de nombre, cédula y código del miembro. El ID identifica de forma estable el comprobante. No es una numeración fiscal ni un comprobante fiscal. No genera PDF ni una pantalla; guarda los datos para hacerlo después.

El nombre/cédula/código se copian desde el miembro en el servidor. No se aceptan del cliente. Cambiar después los datos del miembro no altera el comprobante ya registrado.

## Idempotencia y persistencia

El cliente genera una UUID no vacía por operación y la conserva en los reintentos. La clave es única en toda la tabla Pagos; no debe reciclarse para otra operación. Una clave distinta representa una operación distinta aunque coincidan miembro e importe.

Se comparan miembro, importe, moneda normalizada, método y referencia recortados y empleado. El método y la referencia se comparan respetando mayúsculas/minúsculas; no se inventan equivalencias comerciales. La fecha y los datos históricos del comprobante no se recalculan al reintentar.

La consulta previa mejora el reintento normal, pero la garantía concurrente corresponde al índice único de SQL Server. Solo se capturan SqlException 2601/2627 dentro de DbUpdateException; después se busca la clave específica. Si no existe, se relanza el error original. Los demás errores de persistencia no se presentan como duplicados. Se utiliza un único SaveChanges por pago.

La FK obliga a tener un miembro existente y utiliza Restrict: no se borran pagos en cascada. Esto requiere que la eliminación futura de miembros gestione aquellos que ya tengan pagos; coordinar con Jeison. No se modificó su funcionalidad ni los endpoints existentes.

La migración `20261101014431_AgregarPagosHU03` crea Pagos, la restricción de importe positivo, el índice único de idempotencia y el índice de historial. El snapshot coincide con el modelo. La migración fue generada pero **no se aplicó a ninguna base**. No editar ni volver a crear la migración inicial. Antes de integrar, revisar migraciones añadidas por otros compañeros.

## Límite con HU04 y decisiones pendientes

HU03 no calcula vigencia, no cambia estados de membresía y no aplica el pago a una renovación. HU04 deberá consultar el pago y evitar que se aplique más de una vez, persistiendo su aplicación y la nueva vigencia de forma atómica. Esa garantía es diferente de evitar duplicar el registro del pago.

Pendientes del equipo: tarifa oficial y validación del importe contra ella, moneda(s) admitida(s), catálogo de métodos y reglas de referencia, autenticación del empleado, duración/plan cubierto por un pago y fecha efectiva usada para renovar. Nada de eso se presume a partir del importe ni de la fecha UTC registrada. No se ha incluido estado ficticio de pago o membresía.

## Pruebas reproducibles

`tests/HU03.Verificacion` es un ejecutable de comprobaciones HTTP y modelo; **no es un proyecto de dotnet test**. Sale con error si falla una comprobación. Arranca una API en loopback con puerto asignado automáticamente, usa EF Core InMemory y solo crea datos sintéticos. Incluye los controladores reales de la API mediante una referencia de proyecto. Genera/inspecciona SQL de SQL Server sin conectarse a él.

Para repetir en este equipo, desde el directorio externo que contiene el global.json de SDK 9.0.318:

```powershell
$repo = 'C:\Users\josti\Documents\Codex\2026-10-31\contexto-del-proyecto-gym-fitlab-revisi\work\HU03-pagos'
Push-Location 'C:\Users\josti\Documents\Codex\2026-10-31\contexto-del-proyecto-gym-fitlab-revisi\work\fitlab-sdk9'
try {
    dotnet --version
    dotnet build "$repo\tests\HU03.Verificacion\HU03.Verificacion.csproj" --artifacts-path .\hu03-artifacts --disable-build-servers
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación' }
    dotnet .\hu03-artifacts\bin\HU03.Verificacion\debug\HU03.Verificacion.dll
    if ($LASTEXITCODE -ne 0) { throw 'Falló una comprobación' }
}
finally { Pop-Location }
```

En la verificación inicial se utilizó `--source .\test-feed` para evitar bloqueos de NuGet, aprovechando la caché instalada y el paquete InMemory 9.0.0 de esa fuente local. Esto no modifica NuGet global. La exclusión `tests/**` del proyecto web evita compilar o publicar las pruebas dentro de la API.

Resultado inicial: 47 comprobaciones aprobadas y compilación con SDK 9.0.318 sin errores ni advertencias. Cubren registro, validaciones HTTP, miembro inexistente, reintento idéntico, cambios en cada dato de la misma clave, historial/paginación, comprobante histórico, regresión de registro/listado/perfil de miembros, modelo y script SQL, y propagación de errores genéricos de base de datos.

SQL Server local no permitió abrir sistema_miembros_db. Por eso quedan pendientes en una base SQL Server de pruebas autorizada: aplicar/revertir la nueva migración, comprobar FK/check/índice únicos realmente ejecutados, solicitudes concurrentes con igual clave y datos iguales/distintos, recuperación tras fallos y medición de tiempos con datos representativos. InMemory no demuestra ninguna de esas garantías de base de datos.

La paginación por desplazamiento no es una instantánea: nuevos pagos entre páginas pueden desplazar resultados. El orden incluye ID como desempate. Evaluar paginación por cursor si el volumen o el uso simultáneo lo requieren.
