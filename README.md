# MicroserviceTemplate

Plantilla base para construir microservicios modernos usando **.NET 8 con C#**, aplicando **Arquitectura Hexagonal** combinada con **DDD** y el principio de **Inversión de Dependencias (DIP)**. Perfecto para escalar proyectos y mantener un código limpio y desacoplado.

---

## Características Principales
- ✅ Arquitectura Hexagonal (Domain, Application, Infrastructure, WebApi).
- ✅ CRUD genérico con `IBaseRepository`.
- ✅ `Records` para modelos de Request/Response.
- ✅ Configuración flexible con archivo `.env` y `appsettings.yml` (en desarrollo).
- ✅ Handlers con MediatR para aplicar el patrón CQRS.
- ✅ Soporte para Docker y despliegue contenedorizado.

---

## Estructura del template
```
/monitor_access_agent_ms
│
├── /src
│   ├── /Domain             # Entidades y contratos (interfaces)
│   │   ├── /Entities
│   │   └── /Repositories
│   │
│   ├── /Application         # Lógica de negocio y handlers MediatR
│   │   ├── /Commands
│   │   ├── /Queries
│   │   ├── /Handlers
│   │   ├── /Records
│   │   └── /Validators (opcional)
│   │
│   ├── /Infrastructure      # Acceso a datos y persistencia
│   │   ├── /Persistence
│   │   └── /Repositories
│   │
│   └── /WebApi              # API REST (Controllers, Program.cs, Startup)
│
├── /tests                  # Pruebas unitarias y de integración
│
├── .env                    # Variables de entorno
├── Dockerfile              # Imagen Docker
├── docker-compose.yml      # Orquestación Docker (si aplica)
├── monitor_access_agent_ms.sln        # Solución principal
└── template.json           # Metadatos del template (dotnet new)
```

---

## Requisitos
- [.NET 8 SDK](https://dotnet.microsoft.com/)
- SQL Server (local o remoto)
- Docker (opcional pero recomendado para despliegue)
- Visual Studio o Visual Studio Code
- Entity Framework Core

Instala los paquetes esenciales si no los hay instalados:
```bash
Install-Package Microsoft.EntityFrameworkCore.SqlServer
Install-Package Microsoft.EntityFrameworkCore.Tools
```

---

## Generar Entidades desde un Esquema Específico con EF Core

Para generar únicamente las entidades del esquema deseado desde una base de datos SQL Server, puedes usar el siguiente comando con `dotnet ef dbcontext scaffold`.

### Comando base para ejecutar el comando ef

```bash
dotnet tool install --global dotnet-ef
```
## Asegurarse de que se instaló correctamente

```bash
dotnet ef --version
```

## Ejecutar el comando para actualizar la migración de la base

```bash
dotnet ef dbcontext scaffold "Server=10.10.7.4;Database=ERP3000;User Id=sa;Password=Gapgr2011;TrustServerCertificate=True;" Microsoft.EntityFrameworkCore.SqlServer -o Domain/Entities -c ApplicationDbContext --context-dir Infrastructure/Persistence/Context --no-onconfiguring --no-pluralize --force --schema seguridades
```
## Estructura del comando

```
o Domain/Entities	Ruta de salida para las entidades generadas
-c ApplicationDbContext	Nombre de la clase del DbContext
--context-dir Infrastructure/Persistence/Context	Ruta donde se generará el DbContext
--no-onconfiguring	Evita que se genere el método OnConfiguring con la cadena de conexión
--force	Sobrescribe archivos existentes
--schema seguridades	Solo incluye tablas del esquema inventario
--table seguridades.nombre_tabla --table empresa.empresa  Para las tablas de la empresa
```




---

## Ejecutar Localmente
1. Asegúrate de tener el archivo `.env` con tus variables:
```env
DATABASE_HOST=localhost
DATABASE_NAME=YourDB
DATABASE_USER=sa
DATABASE_PASSWORD=YourPassword
DATABASE_SSL=false
```
2. Correr la aplicación:
```bash
dotnet run --project src/WebApi
```
3. Accede a Swagger:
```
http://localhost:tupuerto/swagger
```


## Estructura Hexagonal Explicada
- `Domain`: Entidades, interfaces de repositorios y reglas del negocio puras.
- `Application`: Lógica de aplicación con MediatR (CQRS), validaciones y records.
- `Infrastructure`: Implementación real de los contratos, acceso a datos.
- `WebApi`: Puntos de entrada para exponer la lógica (Controllers y Swagger).

---

## Comandos Útiles para el Template
### Crear nuevo proyecto basado en esta plantilla
```bash
dotnet new --install ./
```
### Actualizar la plantilla en caso de cambios
```bash
dotnet new --install ./ --force 
```

### Usar el template para crear microservicios
```bash
dotnet new gapms -n nombre-ms
```

---

## Licencia
Este proyecto está licenciado bajo la Licencia GAP Systems 2025©

---
