# Sprint 1 — Gestión de Clientes

## Objetivo

Implementar el módulo de Clientes sobre la base multi-tenant ya validada en Sprint 0.

Al finalizar el sprint, cada organización debe poder crear, consultar, editar, buscar, filtrar y desactivar sus propios clientes sin visualizar clientes de otras organizaciones.

## Alcance

### Backend
- Crear entidad `Client`.
- Relacionar cada cliente con `OrganizationId`.
- Soportar `PERSON` y `COMPANY`.
- CRUD de clientes.
- Búsqueda, filtros y paginación.
- Estado `ACTIVE` / `INACTIVE`.
- Validación de identificación duplicada dentro de la misma organización.
- Migración EF Core.
- Pruebas de integración multi-tenant.

### Frontend
- Activar módulo Clientes.
- Listado de clientes.
- Buscar y filtrar.
- Crear cliente.
- Editar cliente.
- Ver detalle.
- Activar/desactivar cliente.
- Mantener la paleta azul legal y el layout existente.

## Modelo inicial

```text
Client
------
Id
OrganizationId
Type
DisplayName
IdentificationType
IdentificationNumber
Email
Phone
SecondaryPhone
Address
Notes
Status
CreatedAt
UpdatedAt

PERSON
------
FirstName
LastName

COMPANY
-------
LegalName
TradeName
ContactPerson
```

Valores:

```text
Type: PERSON | COMPANY
Status: ACTIVE | INACTIVE
```

## Reglas

1. `OrganizationId` se obtiene del `TenantContext`.
2. Nunca confiar en `OrganizationId` enviado en body/query.
3. Un cliente pertenece a una sola organización.
4. La identificación debe ser única dentro de la organización.
5. La misma identificación puede existir en organizaciones diferentes.
6. No eliminar físicamente clientes en este sprint; usar `INACTIVE`.
7. `DisplayName` debe ser útil para búsquedas y listados.

## API mínima

```http
GET    /api/clients
GET    /api/clients/{id}
POST   /api/clients
PUT    /api/clients/{id}
PATCH  /api/clients/{id}/status
```

`GET /api/clients` debe soportar:

```text
search
type
status
page
pageSize
```

Todos los resultados deben estar filtrados por el tenant activo.

## UX

### Listado
- Buscar por nombre, razón social, identificación, correo o teléfono.
- Filtro por tipo.
- Filtro por estado.
- Botón `Nuevo cliente`.
- Tabla:
  - Nombre / Razón social
  - Tipo
  - Identificación
  - Correo
  - Teléfono
  - Estado
  - Última actualización
  - Acción

### Crear / Editar

Campos comunes:
- Tipo
- Identificación
- Correo
- Teléfono
- Teléfono secundario
- Dirección
- Notas

Persona natural:
- Nombre
- Apellido

Persona jurídica:
- Razón social
- Nombre comercial
- Persona de contacto

## Pruebas obligatorias

1. `CreateClient_ShouldUseActiveTenant`
2. `GetClients_ShouldOnlyReturnActiveTenantClients`
3. `GetClient_ShouldReturnOwnTenantClient`
4. `GetClient_ShouldRejectForeignTenantClient`
5. `UpdateClient_ShouldUpdateOwnTenantClient`
6. `ChangeClientStatus_ShouldDeactivateClient`
7. `CreateClient_ShouldRejectDuplicateIdentificationInsideOrganization`
8. `CreateClient_ShouldAllowSameIdentificationInDifferentOrganizations`

Además:
- todos los tests del Sprint 0 deben continuar pasando.

## Fuera de alcance

- Casos.
- Contactos múltiples.
- Importación Excel/CSV.
- Portal cliente.
- Facturación.
- Campos personalizados.
- CRM avanzado.
- Automatizaciones.
- IA.

## Definition of Done

- [ ] Migración `Clients` creada y aplicada.
- [ ] Backend compila sin errores.
- [ ] Frontend compila sin errores.
- [ ] CRUD funcionando.
- [ ] Persona natural y jurídica funcionando.
- [ ] Búsqueda, filtros y paginación funcionando.
- [ ] Aislamiento multi-tenant cubierto por pruebas.
- [ ] Duplicados validados por organización.
- [ ] UI alineada con mockup y paleta azul legal.
- [ ] Tests Sprint 0 siguen en verde.
- [ ] README actualizado si se requieren nuevos pasos.

## Resultado esperado

Sprint 1 termina con un módulo de Clientes real, usable y aislado por organización, preparado para que Sprint 2 implemente Casos mediante `ClientId`.

## Instrucción para Codex

Trabaja sobre el estado actual del repositorio `sgl-saas-app`.

- No modifiques la arquitectura multi-tenant salvo necesidad real.
- Reutiliza `TenantContext`.
- No avances a Casos.
- No agregues infraestructura nueva sin justificación.
- Mantén los tests existentes en verde.
- Antes de finalizar ejecuta:
  - `dotnet build`
  - `dotnet test`
  - `npm run build`
- Entrega resumen de cambios, migración, tests y pendientes.
