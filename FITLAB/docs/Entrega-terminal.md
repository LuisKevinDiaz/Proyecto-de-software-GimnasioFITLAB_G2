# FITLAB — guía de grabación por terminal

## Estado verificable

- Motor real: SQL Server 2022 Developer, versión 16.0.4185.3, contenedor sqlserver2022, puerto local 1433.
- Base aislada: FITLAB_Pruebas. Las migraciones de miembros, pagos, membresías, baja, auditoría y sesiones están aplicadas. No se borraron otras bases ni datos.
- Copia local: `/Users/angelurenavalenzuela/Documents/Codex/2026-10-31/referenced-chatgpt-conversation-this-is-an/work/fitlab-entrega`. Rama `integracion/entrega-terminal`, desde main 812075b. No se publicaron cambios.
- API: http://127.0.0.1:5088. El inicio de sesión está limitado a esta computadora y esta base de pruebas.
- La primera cuenta debe crearla personalmente el usuario. No existen usuarios ni contraseñas predeterminadas.

**Verificación final completada:** 18 comprobaciones reales de seguridad, 47 de recorrido HTTP con concurrencia SQL y ensayo final interactivo de 40 comprobaciones. Se usó una sesión real de la cuenta personal y se comprobó la persistencia directamente en SQL Server. La cuenta y sus permisos se conservaron.

El último ensayo completo creó el miembro ficticio 6, registró su pago, renovó hasta 2026-12-01 y lo dejó inactivo sin borrar su historial. Cada nueva ejecución crea su propio miembro ficticio. Las fechas de auditoría se guardan en UTC (cuatro horas por delante de República Dominicana).

## 1. Abrir el proyecto

Abre Visual Studio Code y selecciona Archivo → Abrir carpeta:

`/Users/angelurenavalenzuela/Documents/Codex/2026-10-31/referenced-chatgpt-conversation-this-is-an/work/fitlab-entrega`

Usa Terminal → Nueva terminal. También sirve la aplicación Terminal de macOS. Los comandos se escriben en esa terminal, no en el chat.

```bash
cd "/Users/angelurenavalenzuela/Documents/Codex/2026-10-31/referenced-chatgpt-conversation-this-is-an/work/fitlab-entrega"
```

Docker Desktop debe estar funcionando. El contenedor sqlserver2022 ya está configurado; no cambies ni elimines sus volúmenes.

## 2. Iniciar la API

En una primera terminal:

```bash
python3 scripts/entrega.py api
```

Debe aparecer: `FITLAB listo: http://127.0.0.1:5088 | SQL Server: FITLAB_Pruebas | acceso de empleados habilitado.`

Déjala abierta. Si indica que ya está funcionando, puedes continuar. Para comprobarlo desde otra terminal:

```bash
python3 scripts/entrega.py estado
```

Debe mostrar `sqlServer: true`, `autenticacionLocal: true` y `baseDatos: FITLAB_Pruebas`.

## 3. Acceso privado antes de grabar

Tu cuenta angel_admin ya existe y fue utilizada en las pruebas; no hace falta recrearla. Si la sesión venció, ejecuta directamente entrar.

Solo para crear otra cuenta voluntariamente, en privado:

```bash
python3 scripts/entrega.py crear-cuenta
```

Elige personalmente tu usuario y una contraseña de al menos 12 caracteres. La contraseña no se muestra al escribirla ni se recibe como argumento. Para demostrar las cinco historias, responde `s` a los permisos de actualizar contacto y dar de baja, y confirma la creación. Estos permisos se guardan realmente en SQL Server. Ningún ID fijo concede acceso.

Si la cuenta ya existe, este comando no la sobrescribe. No pegues la contraseña en el chat, en archivos del proyecto o en comandos. No grabes esta preparación.

Después:

```bash
python3 scripts/entrega.py entrar
```

Escribe tus credenciales en privado. Debe indicar `Ambos permisos confirmados. Ya puedes iniciar la grabación.` La sesión dura 30 minutos, se guarda con permisos restringidos fuera del repositorio y nunca se imprime. Si vence, repite entrar fuera de la grabación.

## 4. Verificación previa a grabar

```bash
python3 scripts/entrega.py seguridad
python3 scripts/entrega.py demostrar
```

El primer comando comprueba la sesión real, tokens inválidos, cuenta desactivada, sesión vencida, 403 sin permisos y éxito con los permisos restaurados. Cambia temporalmente los permisos de tu cuenta únicamente para probar el rechazo y los restaura al terminar. No introduce cuentas de demostración.

El segundo crea un miembro completamente ficticio nuevo y verifica por HTTP las cinco historias, consultando SQL Server desde conexiones independientes. No admite simular éxito ni recurrir a servicios cuando falla la autenticación. Si algún paso falla, devuelve error y detiene la demostración.

La verificación adicional de concurrencia real SQL, fuera del video, se puede ejecutar así:

```bash
python3 scripts/entrega.py demostrar --concurrencia
```

Los controles concurrentes adicionales invocan los servicios directamente y están rotulados como tales; el recorrido normal de las cinco historias usa HTTP y la sesión real.

## 5. Orden exacto para grabar

No muestres la terminal usada para crear la cuenta o entrar. No abras archivos de la carpeta privada ni inspecciones las variables de entorno del contenedor.

En una terminal limpia, dentro del proyecto, inicia la grabación y ejecuta:

```bash
python3 scripts/entrega.py estado
python3 scripts/entrega.py demostrar --pausas
python3 scripts/entrega.py datos
```

El segundo comando espera Intro antes de cada fase:

1. **SQL Server:** conexión real, versión del motor, base FITLAB_Pruebas y migraciones consistentes.
2. **HU01:** registro inválido 400; registro ficticio 201; recuperación de la fila SQL; duplicado 409.
3. **HU02:** búsqueda y perfil 200; actualización sin sesión 401; sin versión 428; actualización autorizada 204; versión obsoleta 412; contacto y auditoría persistidos.
4. **HU03:** importe inválido 400; pago 201; reintento devuelve el mismo comprobante 200; datos incompatibles 409; historial y una sola fila SQL.
5. **HU04:** renovación 200; vencimiento suma un mes; membresía y pago aplicado persistidos; repetición 409; pago inexistente 404.
6. **HU05:** el ID declarado no autoriza 401; baja con sesión válida 200; SQL confirma inactivo y empleado real; búsqueda lo oculta; nuevo pago 409; historial y comprobante conservados.

Al final debe aparecer un resultado APROBADO y `Cinco historias verificadas por HTTP: True`. Si aparece error, no lo presentes como aprobado: el registro identifica la fase.

`datos` consulta directamente el contenedor SQL Server y muestra únicamente las filas ficticias del último recorrido: miembro, pago, membresía y auditoría. No muestra contraseñas, hashes de cuentas ni sesiones.

## 6. Después de grabar

```bash
python3 scripts/entrega.py salir
```

Esto vence tu sesión en SQL Server y elimina la copia privada local de la credencial. Los miembros ficticios permanecen como evidencia; no se borran bases ni datos.

## Evidencias y límites

Las ejecuciones nuevas se guardan en `/Users/angelurenavalenzuela/Documents/Codex/2026-10-31/referenced-chatgpt-conversation-this-is-an/outputs/FITLAB-entrega`:

- ultima-demostracion.log y ultima-demostracion.json: prueba HTTP y persistencia SQL del último recorrido.
- seguridad-sqlserver.log: comprobaciones reales de identidad y permisos, una vez ejecutadas con tu sesión.
- sqlserver-inicial-servicios.log: prueba inicial de 46 comprobaciones, anterior al login; modo mixto HTTP/servicios, **no acredita autenticación**.
- migraciones-sqlserver.log y migracion-identidad-sqlserver.log: migraciones realmente aplicadas a SQL Server.

Las 80 comprobaciones previas de SQLite no se presentan como pruebas SQL Server. Esta preparación utiliza el motor del contenedor real.

El proveedor local es una solución de cuentas reales para FITLAB_Pruebas. Su uso institucional en producción, roles del equipo, administración de cuentas y despliegue HTTPS externo deben acordarse y revisarse antes de publicarlo. Esta API solo admite el proveedor local en el servidor y base autorizados. No se modificaron los PR #6/#8.

## Diagnóstico del acceso

La API real responde en /api/estado. Abrir la raíz / devuelve 404 porque no existe una página web; esto no impide usar la API desde la terminal. El fallo histórico de login no se atribuyó a un defecto criptográfico: el hash guardado era válido y el nuevo acceso personal funcionó sin recrear ni cambiar la contraseña.

Si volver a entrar falla, el lanzador distingue 401 de credenciales y 429 por límite de intentos. `python3 scripts/entrega.py verificar-clave` comprueba la clave privadamente sin cambios. `cambiar-clave` solo actúa si tú eliges personalmente otra contraseña y confirmas; conserva ID y permisos. No es necesario para esta grabación.
