# Sprint 2 — Casos y Detalle

## Objetivo

Implementar el núcleo de Casos / Expedientes, relacionándolo con Clientes y con el contexto multi-tenant existente.

Al finalizar el sprint, cada organización podrá crear, consultar, editar, buscar, filtrar y cerrar/reabrir sus propios casos, con una pantalla de detalle preparada para Seguimientos, Tareas, Agenda y Documentos.

## Alcance

### Backend
- Crear entidad `Case`.
- Relación obligatoria con `Client`.
- Relación opcional con una `Membership` responsable.
- CRUD de casos.
- Búsqueda, filtros y paginación.
- Estados y prioridad.
- Cierre y reapertura sin eliminación física.
- Validación multi-tenant para cliente y responsable.
- Migración EF Core.
- Pruebas de integración.

### Frontend
- Activar módulo `Casos`.
- Listado con búsqueda, filtros y paginación.
- Alta y edición.
- Vista de detalle.
- Cierre/reapertura.
- Selección de cliente.
- Selección de responsable.
- Tabs de detalle preparados para Seguimientos, Tareas, Agenda y Documentos.

## Modelo inicial

```text
Case
----
Id
OrganizationId
ClientId
CaseNumber
Title
Description
CaseType
Status
Priority
ResponsibleMembershipId
OpenedAt
ClosedAt
Court
Jurisdiction
Counterparty
OpposingCounsel
Notes
CreatedAt
UpdatedAt
```

Valores:

```text
Status:
OPEN
IN_PROGRESS
SUSPENDED
CLOSED

Priority:
LOW
MEDIUM
HIGH
URGENT
```

## Reglas de negocio

1. `OrganizationId` siempre se obtiene del `TenantContext`.
2. Un caso pertenece a una sola organización.
3. Todo caso debe tener un cliente válido de la organización activa.
4. No se puede asociar un cliente de otro tenant.
5. El responsable, cuando exista, debe ser una `Membership` activa de la organización.
6. No se puede asignar como responsable una membresía de otra organización.
7. `CaseNumber` debe ser único dentro de la organización, no globalmente.
8. La misma numeración puede existir en organizaciones distintas.
9. No eliminar físicamente casos en el MVP.
10. Un caso cerrado conserva toda su información.
11. `ClosedAt` se establece al cerrar y se limpia al reabrir.
12. `OpenedAt` es obligatorio.
13. El detalle del caso debe quedar listo para extenderse en Sprint 3, 4 y 5.

## API mínima

```http
GET    /api/cases
GET    /api/cases/{id}
POST   /api/cases
PUT    /api/cases/{id}
PATCH  /api/cases/{id}/status
```

`GET /api/cases` debe soportar:

```text
search
clientId
status
priority
responsibleMembershipId
page
pageSize
```

La búsqueda debe considerar al menos:

- número de caso;
- título;
- nombre visible del cliente;
- contraparte;
- tribunal/juzgado.

Todos los resultados deben estar filtrados por el tenant activo.

## UX

### Listado de casos

- Buscar caso.
- Filtro por estado.
- Filtro por prioridad.
- Filtro por cliente.
- Filtro por responsable.
- Botón `Nuevo caso`.
- Tabla:
  - Número
  - Caso / título
  - Cliente
  - Tipo
  - Responsable
  - Estado
  - Prioridad
  - Fecha de apertura
  - Acción

### Crear / Editar

Campos principales:

- Número de caso / expediente
- Título
- Cliente
- Tipo de caso
- Descripción
- Estado
- Prioridad
- Responsable
- Fecha de apertura
- Tribunal / juzgado
- Jurisdicción
- Contraparte
- Abogado contrario
- Notas

### Detalle del caso

Cabecera:

- Número del caso
- Título
- Cliente
- Estado
- Prioridad
- Responsable
- Fecha de apertura/cierre

Resumen:

- descripción
- tribunal
- jurisdicción
- contraparte
- abogado contrario
- notas

Tabs visibles:

- `Resumen` — funcional en Sprint 2.
- `Seguimientos` — placeholder.
- `Tareas` — placeholder.
- `Agenda` — placeholder.
- `Documentos` — placeholder.

## Seguridad y multi-tenancy

- Reutilizar el `TenantContext` existente.
- Ningún endpoint debe confiar en `OrganizationId` recibido desde frontend.
- Validar el tenant antes de leer o modificar el caso.
- Validar también las relaciones `ClientId` y `ResponsibleMembershipId`.
- Un identificador válido de un caso de otra organización nunca debe exponer sus datos.
- Mantener la convención existente del proyecto para responder `403` o `404` a recursos de otro tenant.

## Pruebas obligatorias

1. `CreateCase_ShouldUseClientFromActiveTenant`
2. `CreateCase_ShouldRejectForeignTenantClient`
3. `CreateCase_ShouldAcceptResponsibleMembershipFromActiveTenant`
4. `CreateCase_ShouldRejectForeignTenantResponsibleMembership`
5. `GetCases_ShouldOnlyReturnActiveTenantCases`
6. `GetCase_ShouldReturnOwnTenantCase`
7. `GetCase_ShouldRejectForeignTenantCase`
8. `UpdateCase_ShouldUpdateOwnTenantCase`
9. `CloseCase_ShouldSetClosedAt`
10. `ReopenCase_ShouldClearClosedAt`
11. `CreateCase_ShouldRejectDuplicateCaseNumberInsideOrganization`
12. `CreateCase_ShouldAllowSameCaseNumberInDifferentOrganizations`

Además:
- las 18 pruebas existentes deben continuar pasando.

## Fuera de alcance

- Seguimientos reales.
- Tareas reales.
- Eventos de agenda.
- Documentos.
- Participantes múltiples / `CaseMember` avanzado.
- Facturación.
- Automatizaciones.
- IA.
- Integraciones judiciales externas.

## Definition of Done

- [ ] Entidad `Case` implementada.
- [ ] Migración creada y aplicada.
- [ ] Relación con `Client` validada por tenant.
- [ ] Responsable validado mediante `Membership`.
- [ ] CRUD funcionando.
- [ ] Búsqueda, filtros y paginación funcionando.
- [ ] Cierre y reapertura funcionando.
- [ ] Número de caso único por organización.
- [ ] Listado React terminado.
- [ ] Alta y edición terminadas.
- [ ] Detalle con tab `Resumen` funcional.
- [ ] Tabs futuras visibles como placeholders.
- [ ] Aislamiento multi-tenant cubierto por pruebas.
- [ ] Tests anteriores continúan en verde.
- [ ] Backend compila sin errores ni advertencias.
- [ ] Frontend compila sin errores.
- [ ] README actualizado únicamente si aparecen nuevos pasos de ejecución.

## Resultado esperado

Sprint 2 termina con el núcleo de Casos / Expedientes completamente usable y relacionado con Clientes, preparado para que Sprint 3 agregue Seguimientos y Tareas sin rediseñar la pantalla de detalle.

## Instrucción para Codex

Trabaja sobre el estado actual del repositorio `sgl-saas-app`.

Reglas:

- No reinicies ni reestructures la solución.
- Reutiliza `TenantContext`, autenticación, organizaciones, memberships y clientes existentes.
- No confíes en `OrganizationId` enviado por frontend.
- No avances a Seguimientos, Tareas, Agenda o Documentos.
- No introduzcas infraestructura nueva sin una necesidad concreta.
- No conviertas los placeholders de tabs futuros en funcionalidades reales.
- Mantén todos los tests existentes en verde.
- Conserva el diseño y la paleta azul legal ya establecida.
- Si detectas una decisión técnica que cambia el modelo acordado, detente y repórtala antes de implementarla.

Antes de finalizar ejecuta:

```bash
dotnet restore
dotnet build --no-restore
dotnet test
npm run build
git diff --check
```

También:
- crea y aplica la migración de Sprint 2;
- verifica que EF no reporte cambios pendientes de modelo;
- valida manualmente o mediante test el aislamiento de casos entre dos organizaciones.

Al finalizar entrega:

1. Resumen de cambios.
2. Migración creada.
3. Tests agregados.
4. Total de tests aprobados/fallidos.
5. Resultado de backend build.
6. Resultado de frontend build.
7. Flujo multi-tenant validado.
8. Pendientes, si existen.

No avances a Sprint 3.
