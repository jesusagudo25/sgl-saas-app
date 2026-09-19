# SGL SaaS — Sprint 0

Base técnica del SaaS de gestión legal: autenticación, organizaciones, membresías, invitaciones y aislamiento multi-tenant. Este repositorio todavía no implementa Clientes, Casos, Agenda, Documentos, pagos ni IA.

## Requisitos

- .NET SDK 8 o posterior con soporte para `net8.0`.
- SQL Server 2019 o posterior. En Windows se puede usar SQL Server Express LocalDB.
- Node.js 20 o posterior y npm.
- PowerShell para los ejemplos de configuración en Windows.

## Estructura

```text
backend/
  src/LegalManagement.Api             API, middleware y autorización
  src/LegalManagement.Application     contratos y abstracciones
  src/LegalManagement.Domain          entidades y roles de membresía
  src/LegalManagement.Infrastructure  Identity, EF Core, JWT y migraciones
  tests/LegalManagement.IntegrationTests
frontend/legal-management-web         React + TypeScript + Vite
```

## Configuración local

La API carga `appsettings.Local.json` únicamente en `Development`. El archivo está ignorado por Git y permite trabajar localmente sin versionar secretos.

1. Copia el ejemplo:

   ```powershell
   Copy-Item backend/src/LegalManagement.Api/appsettings.Local.example.json `
     backend/src/LegalManagement.Api/appsettings.Local.json
   ```

2. Edita `appsettings.Local.json` y reemplaza `Jwt:SigningKey` por una cadena aleatoria de al menos 32 bytes. La clave debe ser exclusiva de tu entorno local.
3. Ajusta `ConnectionStrings:DefaultConnection` si no usas LocalDB.

El ejemplo documenta `Jwt:Issuer`, `Jwt:Audience`, duración de access y refresh tokens, `FrontendUrl` y `Cors:AllowedOrigins`. En despliegues se deben proporcionar estos valores mediante variables de entorno o el gestor de secretos de la plataforma. Las claves usan la forma `Jwt__SigningKey` y `ConnectionStrings__DefaultConnection` en variables de entorno.

## SQL Server y migraciones

La herramienta `dotnet-ef` está fijada localmente en `backend/.config/dotnet-tools.json`.

```powershell
Set-Location backend
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet tool run dotnet-ef database update `
  --project src/LegalManagement.Infrastructure/LegalManagement.Infrastructure.csproj `
  --startup-project src/LegalManagement.Api/LegalManagement.Api.csproj
```

La base se crea exclusivamente mediante migraciones. No se usa `EnsureCreated` en la aplicación. Para crear una migración futura:

```powershell
dotnet tool run dotnet-ef migrations add NombreMigracion `
  --project src/LegalManagement.Infrastructure/LegalManagement.Infrastructure.csproj `
  --startup-project src/LegalManagement.Api/LegalManagement.Api.csproj `
  --output-dir Migrations
```

## Ejecutar el backend

Después de configurar y migrar la base:

```powershell
Set-Location backend
dotnet restore
dotnet run --project src/LegalManagement.Api/LegalManagement.Api.csproj
```

La API escucha en `http://localhost:5080`. Swagger está disponible en `http://localhost:5080/swagger` durante Development.

El correo de desarrollo se conserva en memoria. Los enlaces recientes de recuperación e invitación pueden consultarse desde la misma máquina en `GET /api/development/emails`. Este endpoint no existe fuera de Development.

## Ejecutar el frontend

```powershell
Set-Location frontend/legal-management-web
npm install
npm run dev
```

Vite sirve la aplicación en `http://localhost:5173` y redirige `/api` hacia el backend local.

## Compilación y pruebas

```powershell
Set-Location backend
dotnet restore
dotnet build --no-restore
dotnet test --no-build --no-restore

Set-Location ../frontend/legal-management-web
npm install
npm run build
```

Las pruebas de integración levantan la API completa con `WebApplicationFactory`, aplican las migraciones a una base SQL Server LocalDB temporal y eliminan esa base al terminar. Validan Identity, JWT, middleware, autorización, persistencia e aislamiento entre organizaciones.

## Modelo multi-tenant

El JWT identifica al usuario y no contiene una organización fija. El frontend envía `X-Organization-Id` en endpoints tenant-aware. El middleware obtiene el usuario desde el JWT, exige que el identificador coincida con la organización de la ruta y consulta una `Membership` activa. El rol se toma exclusivamente de esa membresía.

Una petición con una organización ajena recibe `403 Forbidden`. La prueba `TenantIsolation_ShouldReturn403ForForeignOrganization` reproduce este escenario. `CompleteMultiTenantFlow_ShouldAllowMembershipSwitchAndDenyForeignTenant` valida además el flujo completo con dos usuarios y dos bufetes.

Para ejecutar solamente las pruebas de aislamiento:

```powershell
Set-Location backend
dotnet test --filter "TenantIsolation|CompleteMultiTenantFlow"
```
