Proyecto de Software - Gimnasio FITLAB (Grupo 2)

API REST en C# (.NET 9) con Entity Framework Core y SQL Server para la gestión del gimnasio.

Estado actual
Historia	Descripción	Estado
HU01	Registro rápido de nuevos miembros (backend y base de datos)	Hecho
1. Requisitos previos

Instala esto antes de clonar el proyecto:

Herramienta	Versión	Para qué
.NET SDK	9.0 o superior	Compilar y correr la API
SQL Server (Developer, Express o LocalDB)	Cualquiera reciente	Base de datos
Git	Cualquiera	Control de versiones
Visual Studio Code	Cualquiera	Editor (recomendado)
Postman, Thunder Client o curl	Cualquiera	Probar los endpoints

Comprueba que .NET quedó instalado:

bash
dotnet --version

Debe mostrar 9.x.x.

2. Instalación paso a paso
Paso 1: Clonar el repositorio
bash
git clone https://github.com/LuisKevinDiaz/Proyecto-de-software-GimnasioFITLAB_G2.git
cd Proyecto-de-software-GimnasioFITLAB_G2
git checkout develop
git pull origin develop
Paso 2: Instalar la herramienta de migraciones (una sola vez por PC)
bash
dotnet tool install --global dotnet-ef

Si dice que ya está instalada, sigue al siguiente paso. Para actualizarla:

bash
dotnet tool update --global dotnet-ef
Paso 3: Restaurar paquetes

Los paquetes NuGet (Entity Framework Core para SQL Server) ya están declarados en TuProyecto.csproj. No hay que instalarlos a mano:

bash
dotnet restore
dotnet build

El build debe terminar con 0 errores.

Paso 4: Configurar la conexión a SQL Server

Abre appsettings.json y revisa la cadena de conexión. Por defecto apunta a SQL Server en localhost con autenticación de Windows:

json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=sistema_miembros_db;Trusted_Connection=True;TrustServerCertificate=True;"
}

Si tu instalación es distinta, cambia solo el valor de Server:

Tienes	Valor de Server
SQL Server completo (Developer)	localhost
SQL Server Express	localhost\\SQLEXPRESS
LocalDB (viene con Visual Studio)	(localdb)\\MSSQLLocalDB

En JSON la barra invertida se escribe doble (\\).

No subas tu cambio de Server al repositorio si es solo para tu PC. Cada quien puede usar appsettings.Development.json (ver sección 6).

Para saber qué tienes instalado:

bash
sqllocaldb info

Si lista MSSQLLocalDB, tienes LocalDB.

bash
sqlcmd -S localhost -Q "select @@version"

Si responde, tienes SQL Server en localhost.

Paso 5: Crear la base de datos y las tablas

Las migraciones ya están en el repositorio (carpeta Migrations/). Este comando crea la base sistema_miembros_db y la tabla Miembros automáticamente:

bash
dotnet ef database update

Debe terminar con Done.. No ejecutes dotnet ef migrations add InitialCreate: la migración inicial ya existe.

Paso 6: Correr la API
bash
dotnet run

La consola muestra el puerto, por ejemplo Now listening on: http://localhost:5266. Usa ese puerto en las pruebas (puede variar en cada PC).

3. Endpoints
Registrar un miembro (HU01)
POST /api/miembros/registro
Content-Type: application/json

Body:

json
{
  "nombre": "Juan Perez",
  "cedula": "001-1234567-8",
  "telefono": "809-555-0199",
  "correo": "juan@correo.com",
  "empleadoRegistroId": 1
}
Código	Significado
201 Created	Miembro creado. Devuelve el perfil con codigoUnico (ej. MBR-2026-00001) y fechaAlta.
400 Bad Request	Faltan campos o tienen formato inválido. Detalla qué campo corregir.
409 Conflict	Ya existe un miembro con esa cédula. Devuelve el perfil existente.
Consultar miembros
GET /api/miembros          Lista todos los miembros (más recientes primero)
GET /api/miembros/{id}     Devuelve un miembro por su Id
Reglas de validación
Nombre, cédula, teléfono, correo son obligatorios.
Cédula: formato 000-0000000-0 (los guiones son opcionales).
Teléfono: formato dominicano 809, 829 o 849 seguido de 7 dígitos (ej. 809-555-0199).
Correo: formato de correo válido.
La cédula se guarda sin guiones (00112345678). El frontend debe formatearla al mostrarla.
No se permiten dos miembros con la misma cédula, escrita con o sin guiones.
Pruebas rápidas con curl

Registro válido (debe dar 201):

bash
curl -i -X POST http://localhost:5266/api/miembros/registro -H "Content-Type: application/json" -d '{"nombre":"Juan Perez","cedula":"001-1234567-8","telefono":"809-555-0199","correo":"juan@correo.com","empleadoRegistroId":1}'

Listar miembros:

bash
curl http://localhost:5266/api/miembros
4. Estructura del proyecto
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
5. Flujo de trabajo con Git
Parte siempre de develop actualizada:
bash
   git checkout develop
   git pull origin develop
Crea una rama por historia de usuario:
bash
   git checkout -b feature/HU02-nombre-corto
Haz commits claros (feat:, fix:, chore:, docs:):
bash
   git add .
   git commit -m "feat(HU02): descripción breve"
   git push -u origin feature/HU02-nombre-corto
Abre un Pull Request hacia develop (no hacia main) y pide revisión a un compañero.
main solo recibe lo que ya está probado, al cerrar un sprint o una entrega.
Si cambias el modelo de datos

Cuando modifiques una entidad en src/Models/ o el AppDbContext:

bash
dotnet ef migrations add NombreDelCambio
dotnet ef database update

Sube la nueva migración al repositorio. Tus compañeros solo tendrán que correr dotnet ef database update después de hacer git pull.

6. Problemas comunes
Problema	Solución
dotnet ef no se reconoce	dotnet tool install --global dotnet-ef y reabre la terminal
Login failed for user o Cannot open database	Server incorrecto en appsettings.json; revisa el Paso 4
There is already an object named 'Miembros'	Quedó una tabla de un intento anterior. Corre dotnet ef database drop --force y luego dotnet ef database update (borra solo tu base local)
A network-related or instance-specific error	SQL Server no está corriendo o el nombre de la instancia es otro
Build con errores de EntityFrameworkCore	Corre dotnet restore y luego dotnet build
El puerto de las pruebas no responde	Usa el puerto que muestra dotnet run en tu consola
Usar una conexión distinta sin tocar el archivo compartido

Crea o edita appsettings.Development.json con tu propia cadena. Esta sobrescribe a appsettings.json cuando corres en modo desarrollo:

json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=sistema_miembros_db;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
7. Pendientes conocidos
Autenticación: por ahora el empleado que registra llega en el body (empleadoRegistroId). Cuando exista login, se tomará del token del usuario.
Formulario de registro (frontend): corresponde a otra tarea.
Nombre del proyecto: TuProyecto es provisional y aparece en los namespaces y el .csproj.
