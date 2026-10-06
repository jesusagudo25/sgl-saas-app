# Sprint 4 — Agenda y Calendario — Codex

## Objetivo
Implementar una Agenda jurídica multi-tenant integrada con Casos y responsables sobre el estado actual de `sgl-saas-app`.

- No rehacer sprints anteriores.
- No romper multi-tenancy.
- No implementar recurrencias complejas, recordatorios ni sincronización externa.

## 1. Auditoría previa
Revisar antes de tocar código:
- detalle actual de `Case` y tab `Agenda`;
- `CaseTask`;
- membresías/responsables;
- `SearchableSelect`;
- layout responsive;
- políticas actuales;
- tests y migraciones;
- dependencias de calendario existentes.

Presentar un resumen técnico corto antes de implementar.

## 2. Modelo

```text
CalendarEvent
-------------
Id
OrganizationId
CaseId?
Title
Description
EventType
Status
AssignedMembershipId?
StartsAt
EndsAt
AllDay
Location
MeetingUrl?
CreatedByMembershipId
CreatedAt
UpdatedAt
```

Estados:
```text
SCHEDULED
COMPLETED
CANCELLED
```

Tipos:
```text
HEARING
MEETING
DEADLINE
CALL
VISIT
OTHER
```

Mantener estados y tipos como valores fijos del dominio en Sprint 4.

## 3. Reglas
- `OrganizationId` desde `TenantContext`.
- `CaseId`, si existe, debe pertenecer al tenant activo.
- `AssignedMembershipId`, si existe, debe ser membership activa del tenant.
- `CreatedByMembershipId` corresponde al usuario autenticado.
- `EndsAt >= StartsAt`.
- Eventos cancelados se conservan.
- Fechas persistidas en UTC.
- UI muestra timezone local del navegador.
- Manejar `AllDay` sin desplazar la fecha por UTC.
- No aceptar referencias cross-tenant.
- Sin recurrencias en este sprint.

## 4. API

```http
GET    /api/calendar/events
GET    /api/calendar/events/{id}
POST   /api/calendar/events
PUT    /api/calendar/events/{id}
PATCH  /api/calendar/events/{id}/status
```

Filtros:
```text
from
to
caseId
assignedMembershipId
eventType
status
```

Rango invertido, tipo o estado inválido → 400.

## 5. Agenda global

Ruta sugerida:

```text
/app/calendar
```

Debe soportar:
- vista mensual;
- semanal;
- diaria;
- Hoy;
- anterior/siguiente;
- periodo actual;
- Nuevo evento;
- click en evento para abrir/editar.

Si no existe librería, usar una madura y compatible con React + TypeScript.

## 6. Filtros

```text
Responsable
Caso
Tipo
Estado
```

Reutilizar `SearchableSelect` para Caso y Responsable.

## 7. Crear / Editar

Modal o drawer consistente con el sistema.

Campos:

```text
Título *
Caso
Tipo *
Estado
Responsable
Inicio *
Fin *
Todo el día
Ubicación
URL de reunión
Descripción
```

Con:
- labels;
- validación;
- loading;
- error/success;
- cancelar;
- mantener valores al editar.

## 8. Todo el día
Cuando `AllDay = true`:
- usar controles de fecha apropiados;
- evitar desplazamientos por timezone;
- definir correctamente inicio/fin según la librería;
- agregar pruebas específicas.

## 9. Agenda dentro del Caso
Activar tab:

```text
[Resumen] [Seguimientos] [Tareas] [Agenda] [Documentos]
```

Mostrar solo eventos del Caso.

Desde allí:
- ver eventos;
- crear evento con Caso preseleccionado;
- editar eventos del Caso.

## 10. Tareas vs Eventos
Mantener conceptos separados:

```text
CaseTask     → obligación pendiente
CalendarEvent → hecho agendado en tiempo
```

NO crear uno automáticamente desde el otro.

## 11. Responsive
Validar:

```text
360
390
768
1024
1280
1440
```

Revisar:
- mes;
- semana;
- día;
- filtros;
- modal/drawer;
- tab Agenda del Caso.

No aceptar overflow horizontal innecesario.

## 12. Migración
Crear migración incremental para `CalendarEvents`.

No modificar migraciones previas.

Índices sugeridos:

```text
CalendarEvents(OrganizationId, StartsAt, EndsAt)
CalendarEvents(OrganizationId, CaseId, StartsAt)
CalendarEvents(OrganizationId, AssignedMembershipId, StartsAt)
CalendarEvents(OrganizationId, Status, StartsAt)
```

## 13. Tests mínimos
- crear;
- obtener;
- editar;
- completar;
- cancelar;
- caso ajeno rechazado;
- responsable ajeno rechazado;
- evento ajeno no visible;
- `EndsAt < StartsAt` → 400;
- filtros from/to;
- filtro por caso;
- filtro por responsable;
- tipo;
- estado;
- AllDay conserva fecha esperada;
- todos los tests previos continúan en verde.

## 14. Validación visual automatizada
Si ya existen scripts tipo `visual-check.mjs`, crear `sprint4-visual-check.mjs` si es razonable.

Validar:
- month;
- week;
- day;
- alta;
- edición;
- cancelación;
- filtros;
- tab de Caso;
- responsive;
- ausencia de errores JS.

## 15. Fuera de alcance
NO implementar:
- Google Calendar;
- Outlook;
- ICS;
- recurrencias complejas;
- recordatorios;
- push;
- correo;
- WhatsApp;
- invitaciones externas;
- disponibilidad avanzada;
- Dashboard final;
- Documentos;
- IA.

## 16. Definition of Done
- [ ] `CalendarEvent` implementado.
- [ ] Migración aplicada.
- [ ] API CRUD.
- [ ] Filtros.
- [ ] Vista mes.
- [ ] Vista semana.
- [ ] Vista día.
- [ ] Crear.
- [ ] Editar.
- [ ] Completar.
- [ ] Cancelar.
- [ ] AllDay correcto.
- [ ] Caso validado por tenant.
- [ ] Responsable validado por tenant.
- [ ] Tab Agenda del Caso funcional.
- [ ] Responsive validado.
- [ ] Multi-tenancy probado.
- [ ] Backend Release compila.
- [ ] Frontend compila.
- [ ] Tests previos + nuevos pasan.
- [ ] `git diff --check` correcto.

## 17. Validación final

Backend:

```bash
cd backend
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release

dotnet tool run dotnet-ef database update \
  --project src/LegalManagement.Infrastructure \
  --startup-project src/LegalManagement.Api

dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project src/LegalManagement.Infrastructure \
  --startup-project src/LegalManagement.Api
```

Frontend:

```bash
cd frontend/legal-management-web
npm run build
```

Si existe:

```bash
node sprint4-visual-check.mjs
```

Luego:

```bash
git diff --check
```

## 18. Entrega esperada

```text
Estado Sprint 4

Resumen
- porcentaje
- estado
- pendientes

Agenda
- modelo
- API
- vistas
- filtros
- responsive

Eventos
- crear
- editar
- completar
- cancelar
- todo el día

Integración Caso
- tab Agenda
- creación contextual

Migración
- nombre
- aplicada
- integridad

Tests
- anteriores
- nuevos
- total
- passed
- failed

Validación visual
- 360
- 390
- 768
- 1024
- 1280
- 1440

Build
- backend
- frontend
- git diff --check
```
