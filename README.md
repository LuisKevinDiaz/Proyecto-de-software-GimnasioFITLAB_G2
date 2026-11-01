# Proyecto de Software - Gimnasio FITLAB (Grupo 2)

API REST en C# (.NET 9) con Entity Framework Core y SQL Server para la gestión del gimnasio. Por ahora está terminada la HU01 (registro rápido de nuevos miembros), en su parte de backend y base de datos.

## 1. Requisitos previos

Antes de clonar el proyecto, cada integrante debe tener instalado el SDK de .NET 9 o superior, SQL Server (puede ser la edición Developer, Express o LocalDB), Git y un editor como Visual Studio Code. Para probar los endpoints también hace falta Postman, Thunder Client o simplemente curl.

Para comprobar que .NET quedó bien instalado, ejecuta el siguiente comando. Debe mostrar una versión que empiece por 9:

```bash
dotnet --version
```

## 2. Instalación paso a paso

### Paso 1: Clonar el repositorio

Clona el proyecto y ubícate en la rama develop, que es donde se integra el trabajo del equipo:

```bash
git clone https://github.com/LuisKevinDiaz/Proyecto-de-software-GimnasioFITLAB_G2.git
cd Proyecto-de-software-GimnasioFITLAB_G2
git checkout develop
git pull origin develop
```

### Paso 2: Instalar la herramienta de migraciones

Esto se hace una sola vez por computadora. La herramienta dotnet-ef es la que crea las tablas en la base de datos a partir del código:

```bash
dotnet tool install --global dotnet-ef
```

Si te dice que ya está instalada, no pasa nada y puedes seguir. Si quieres actualizarla, usa `dotnet tool update --global dotnet-ef`.

### Paso 3: Restaurar paquetes y compilar

Los paquetes de Entity Framework Core para SQL Server ya están declarados en el archivo TuProyecto.csproj, así que no hay que instalarlos a mano. Basta con restaurar y compilar:

```bash
dotnet restore
dotnet build
```

El build debe terminar con 0 errores.

### Paso 4: Configurar la conexión a SQL Server

Abre el archivo appsettings.json y revisa la cadena de conexión. Por defecto apunta a un SQL Server instalado en localhost, con autenticación de Windows:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=sistema_miembros_db;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Si tu instalación es distinta, cambia únicamente el valor de Server. Para SQL Server Express usa `localhost\\SQLEXPRESS`, y para LocalDB (que viene con Visual Studio) usa `(localdb)\\MSSQLLocalDB`. En un archivo JSON la barra invertida se escribe doble. Para SQL Server completo, déjalo en `localhost`.

Para saber qué tienes instalado, puedes probar estos dos comandos. Si el primero lista MSSQLLocalDB, tienes LocalDB. Si el segundo responde con una versión, tienes SQL Server en localhost:

```bash
sqllocaldb info
sqlcmd -S localhost -Q "select @@version"
```

Importante: si cambias el Server solo para tu computadora, no subas ese cambio al repositorio. Mira la sección de problemas comunes, más abajo, para usar tu propia conexión sin afectar al resto del equipo.

### Paso 5: Crear la base de datos y las tablas

Las migraciones ya están en el repositorio, dentro de la carpeta Migrations. Este comando crea la base de datos sistema_miembros_db y la tabla Miembros automáticamente:

```bash
dotnet ef database update
```

Debe terminar con la palabra Done. No ejecutes `dotnet ef migrations add InitialCreate`, porque la migración inicial ya existe y duplicarla causa errores.

### Paso 6: Correr la API

```bash
dotnet run
```

La consola muestra el puerto en que quedó escuchando, por ejemplo `http://localhost:5266`. Usa ese puerto en tus pruebas, porque puede variar de una computadora a otra.

## 3. Endpoints disponibles

### Registrar un miembro (HU01)

El registro se hace con una petición POST a `/api/miembros/registro`, enviando un JSON como este:

```json
{
  "nombre": "Juan Perez",
  "cedula": "001-1234567-8",
  "telefono": "809-555-0199",
  "correo": "juan@correo.com",
  "empleadoRegistroId": 1
}
```

Si todo está correcto, la API responde 201 Created con el perfil del miembro, que incluye un código único (por ejemplo MBR-2026-00001) y la fecha de alta. Si faltan campos o alguno tiene un formato inválido, responde 400 Bad Request e indica exactamente qué campo debe corregirse. Si ya existe un miembro con la misma cédula, responde 409 Conflict y devuelve el perfil que ya estaba registrado.

### Consultar miembros

Con una petición GET a `/api/miembros` se obtiene la lista de todos los miembros, con los más recientes primero. Con GET a `/api/miembros/{id}` se obtiene un miembro específico por su Id.

### Reglas de validación

Nombre, cédula, teléfono y correo son obligatorios. La cédula debe tener el formato 000-0000000-0, aunque los guiones son opcionales. El teléfono debe ser dominicano, es decir, empezar por 809, 829 u 849 seguido de siete dígitos, por ejemplo 809-555-0199. El correo debe tener un formato válido.

La cédula se guarda siempre sin guiones (por ejemplo 00112345678), así que quien desarrolle el frontend debe darle formato al mostrarla. Tampoco se permite registrar dos miembros con la misma cédula, sin importar si se escribe con o sin guiones.

### Pruebas rápidas con curl

Para registrar un miembro (debe responder 201):

```bash
curl -i -X POST http://localhost:5266/api/miembros/registro -H "Content-Type: application/json" -d '{"nombre":"Juan Perez","cedula":"001-1234567-8","telefono":"809-555-0199","correo":"juan@correo.com","empleadoRegistroId":1}'
```

Para listar los miembros registrados:

```bash
curl http://localhost:5266/api/miembros
```

## 4. Estructura del proyecto

```
/
├── Migrations/              Migraciones de Entity Framework (no editar a mano)
├── Properties/              Configuración de arranque (launchSettings.json)
├── docs/                    Documentación y diagramas
├── src/
│   ├── Controllers/         Endpoints de la API
│   ├── Data/                AppDbContext (acceso a la base de datos)
│   ├── Models/              Entidades y DTOs
│   └── Services/            Lógica de negocio
├── Program.cs               Punto de entrada y configuración
├── appsettings.json         Configuración y cadena de conexión
├── TuProyecto.csproj        Proyecto y paquetes NuGet
└── TuProyecto.http          Pruebas de endpoints desde VS Code
```

## 5. Flujo de trabajo con Git

Siempre se parte de develop actualizada. Antes de empezar una tarea, ejecuta:

```bash
git checkout develop
git pull origin develop
```

Luego crea una rama por cada historia de usuario, con un nombre que incluya el código de la historia:

```bash
git checkout -b feature/HU02-nombre-corto
```

Cuando avances, haz commits con mensajes claros, usando prefijos como feat, fix, chore o docs, y sube la rama:

```bash
git add .
git commit -m "feat(HU02): descripción breve"
git push -u origin feature/HU02-nombre-corto
```

Después abre un Pull Request en GitHub hacia develop (no hacia main) y pide a un compañero que lo revise. La rama main solo debe recibir lo que ya esté probado, al cerrar un sprint o una entrega.

### Si cambias el modelo de datos

Cuando modifiques una entidad dentro de src/Models o el AppDbContext, genera una migración nueva y aplícala:

```bash
dotnet ef migrations add NombreDelCambio
dotnet ef database update
```

Sube la nueva migración al repositorio. Tus compañeros solo tendrán que hacer `git pull` y correr `dotnet ef database update` para tener la base de datos actualizada.

## 6. Problemas comunes

Si el comando dotnet ef no se reconoce, instala la herramienta con `dotnet tool install --global dotnet-ef` y vuelve a abrir la terminal.

Si aparece el error "Login failed for user" o "Cannot open database", casi siempre significa que el valor de Server en appsettings.json no coincide con tu instalación de SQL Server. Revisa el Paso 4.

Si aparece "There is already an object named 'Miembros'", quedó una tabla de un intento anterior en tu base local. Soluciónalo con los siguientes comandos, que borran únicamente tu base de datos local y la vuelven a crear:

```bash
dotnet ef database drop --force
dotnet ef database update
```

Si aparece "A network-related or instance-specific error", SQL Server no está corriendo o el nombre de la instancia es otro. Si el build da errores de EntityFrameworkCore, ejecuta `dotnet restore` y luego `dotnet build`. Y si las pruebas no responden, verifica que estés usando el puerto que muestra `dotnet run` en tu consola.

### Usar una conexión distinta sin tocar el archivo compartido

Si tu SQL Server es diferente al de los demás, no modifiques appsettings.json. En su lugar, crea o edita el archivo appsettings.Development.json con tu propia cadena de conexión. Ese archivo sobrescribe al principal cuando la API corre en modo desarrollo:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=sistema_miembros_db;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

## 7. Pendientes conocidos

Por ahora el empleado que registra al miembro llega en el body de la petición (empleadoRegistroId). Cuando exista el login, ese dato se tomará del token del usuario. El formulario de registro (frontend) corresponde a otra tarea. Además, el nombre TuProyecto es provisional y aparece en los namespaces y en el archivo .csproj, así que si el equipo decide cambiarlo, conviene hacerlo pronto.

## Equipo

Grupo 2 - Proyecto de Software, Gimnasio FITLAB.
