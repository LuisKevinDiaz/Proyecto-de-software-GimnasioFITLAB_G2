# HU02: actualización de contacto

`PUT /api/miembros/{id:int}/contacto` reemplaza teléfono y correo de un miembro existente. Ambos campos son obligatorios. El DTO acepta exclusivamente `telefono`, `correo` y `empleadoModificacionId`; cualquier propiedad adicional, incluida cédula, ID, nombre o código único, produce 400.

Se conservan las reglas del registro: teléfono de diez dígitos con prefijo 809, 829 o 849, con guiones opcionales; correo validado con EmailAddress. El ID del miembro y el empleado declarado deben ser positivos. Respuestas: 204 actualizado; 400 entrada inválida; 404 miembro inexistente. Una eliminación detectada durante el guardado produce 404; otro fallo de concurrencia detectado por EF produce 409. El modelo no tiene token de concurrencia: dos cambios simultáneos de contacto pueden terminar con el último guardado. No se garantiza detección de actualizaciones perdidas.

El controlador nuevo recibe AppDbContext e ILogger por inyección de dependencias. Solo asigna Telefono y Correo; no modifica la cédula, código, nombre, fecha de alta ni EmpleadoRegistroId. El registro, búsqueda, listado y consulta originales no se editaron. No se incorporó código de HU03 ni HU05.

## Responsable y fecha

Tras guardar, se emite el evento de log 2002 (ContactoActualizado), con MiembroId, EmpleadoDeclaradoId y FechaModificacionUtc. No se registran teléfono ni correo en ese evento. El identificador del empleado lo declara el cliente: no es una identidad autenticada. El sistema no tiene autenticación ni una tabla de empleados para verificarlo.

El modelo actual no tiene campos ni tabla de auditoría de modificaciones. Este log operativo **no es una auditoría persistente y atómica con la actualización**; su retención depende del proveedor de logging. Queda pendiente acordar una migración/modelo de auditoría y obtener el responsable de una identidad verificada. No se reutiliza el empleado de registro para simular auditoría. No se crearon ni aplicaron migraciones.

## Verificaciones

`tests/HU02.Contacto.Verificacion` es un ejecutable, no una suite de dotnet test. Levanta una API HTTP en loopback y puerto automático con los controladores reales y EF Core InMemory. Crea únicamente datos sintéticos. Ejecuta 29 comprobaciones de contacto válido, prefijos telefónicos, entradas inválidas/ausentes, campos prohibidos, miembro inexistente, datos inalterados tras rechazos, log, registro, consulta, listado y búsqueda.

Desde una carpeta externa con global.json que seleccione SDK 9.0.318, indicar la ruta absoluta del worktree en `$repo`:

```powershell
dotnet build "$repo\tests\HU02.Contacto.Verificacion\HU02.Contacto.Verificacion.csproj" --artifacts-path .\hu02-contacto-artifacts --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw 'Compilación fallida' }
dotnet .\hu02-contacto-artifacts\bin\HU02.Contacto.Verificacion\debug\HU02.Contacto.Verificacion.dll
if ($LASTEXITCODE -ne 0) { throw 'Verificaciones fallidas' }
```

No se probó persistencia en SQL Server real, eliminación simultánea ni actualizaciones concurrentes. Antes de integrar se requiere una prueba sobre base SQL Server autorizada. La auditoría persistente y el estado de membresía siguen pendientes: esta ampliación no completa por sí sola todos los criterios de HU02.
