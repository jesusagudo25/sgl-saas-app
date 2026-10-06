# Sprint 3 — Seguimientos y Tareas — Codex

## Objetivo
Implementar Seguimientos y Tareas dentro del detalle del Caso sobre el estado ACTUAL de `sgl-saas-app`.

- No rehacer Sprint 0, 1 o 2.
- No romper multi-tenancy.
- No implementar comportamientos no definidos de las capturas de referencia.
- Reutilizar catálogos, `SearchableSelect`, patrones responsive y políticas existentes.

## 1. Auditoría previa
Antes de tocar código revisar:
- modelo actual de `Case`;
- tabs del detalle;
- `CaseStatus`, `CaseType`, `Court`, `Jurisdiction`;
- `SettingsCatalogs`;
- `SearchableSelect`;
- responsive actual;
- políticas de roles;
- tests y migraciones existentes.

Presentar un resumen técnico corto antes de implementar.

## 2. Seguimientos

### Modelo
```text
CaseFollowUp
------------
Id
OrganizationId
CaseId
CaseStatusId
CompetenceId
CompetenceDetailId
Description
OccurredAt
CreatedByMembershipId
CreatedAt
UpdatedAt
```

### Reglas
- `OrganizationId` sale de `TenantContext`.
- `CaseId` debe pertenecer al tenant activo.
- `CaseStatusId` debe pertenecer al tenant activo.
- Competencia y detalle deben pertenecer al tenant activo.
- El detalle debe ser hijo de la competencia seleccionada.
- `CreatedByMembershipId` debe corresponder al usuario autenticado dentro del tenant.
- No eliminar físicamente seguimientos.
- Ordenar más reciente primero.
- Mantener trazabilidad histórica.

### Importante
Las capturas muestran acciones de **Enviar/Compartir** y otro botón de historial/reversión, pero no se documentó su comportamiento.

NO implementarlas por inferencia.

Tampoco asumir que crear un Seguimiento actualiza automáticamente el estado actual del Caso. Por ahora:
- guardar `CaseStatusId` en el seguimiento;
- mostrarlo como snapshot histórico;
- NO modificar `Case.CaseStatusId` automáticamente salvo regla ya aprobada en el repositorio.

## 3. Competencias administrables

Agregar a Configuración → Tablas administrables:

```text
Competencias
```

Modelo jerárquico:

```text
LegalCompetence
---------------
Id
OrganizationId
Name
ParentId
IsActive
SortOrder
CreatedAt
UpdatedAt
```

Interpretación:

```text
ParentId = NULL → Competencia General
ParentId = X    → Competencia Detalle hija de X
```

Reglas:
- solo `ADMIN` administra;
- aislamiento por organización;
- activos/inactivos;
- no eliminar físicamente si está referenciada;
- al seleccionar una Competencia General, cargar solo detalles activos hijos;
- no inventar detalles si una competencia no tiene hijos.

## 4. API Seguimientos

Ejemplo:

```http
GET  /api/cases/{caseId}/follow-ups
POST /api/cases/{caseId}/follow-ups
GET  /api/cases/{caseId}/follow-ups/{id}
```

El listado debe devolver como mínimo:

```text
Id
OccurredAt
Status
Competence
CompetenceDetail
Description
CreatedBy
CreatedAt
```

## 5. UI Seguimientos

Dentro del detalle:

```text
[Resumen] [Seguimientos] [Tareas] [Agenda] [Documentos]
```

Activar `Seguimientos`.

### Listado
Columnas principales:

```text
Fecha/Hora
Estado
Competencia
Descripción
Registrado por
```

Responsive:
- sin scroll horizontal como patrón normal;
- ocultar secundarios por breakpoint;
- expansión solo si aporta información adicional;
- no duplicar datos visibles.

### Alta
Botón:

```text
+ Agregar seguimiento
```

Abrir modal o drawer.

Campos:

```text
Estado *
Fecha / Hora *
Competencia General *
Competencia Detalle *
Descripción *
```

Usar `SearchableSelect` cuando aplique, selector dependiente General → Detalle, loading, empty state y feedback de éxito/error.

## 6. Tareas

### Modelo

```text
CaseTask
--------
Id
OrganizationId
CaseId
Title
Description
AssignedMembershipId
Priority
Status
DueAt
CompletedAt
CreatedByMembershipId
CreatedAt
UpdatedAt
```

Estados iniciales:

```text
PENDING
IN_PROGRESS
COMPLETED
CANCELLED
```

Prioridad:

```text
LOW
MEDIUM
HIGH
URGENT
```

No convertir estados/prioridades en catálogos en este Sprint salvo necesidad real encontrada durante auditoría.

## 7. Reglas de Tareas
- Caso del tenant activo.
- Responsable debe ser `Membership` activa del tenant.
- Creador debe pertenecer al tenant.
- `DueAt` nullable si el dominio actual lo permite.
- Al completar: `Status = COMPLETED` y `CompletedAt = ahora`.
- Al reabrir desde COMPLETED, limpiar `CompletedAt` si se soporta.
- Cancelar: `Status = CANCELLED`.
- No eliminar físicamente en MVP.

## 8. API Tareas

Ejemplo:

```http
GET   /api/cases/{caseId}/tasks
POST  /api/cases/{caseId}/tasks
PUT   /api/cases/{caseId}/tasks/{id}
PATCH /api/cases/{caseId}/tasks/{id}/status
```

Filtros útiles:

```text
status
priority
assignedMembershipId
dueFrom
dueTo
```

## 9. UI Tareas

Activar tab `Tareas`.

Mostrar:

```text
Título
Responsable
Prioridad
Estado
Vencimiento
Acciones
```

Acciones:
- crear;
- editar;
- marcar en progreso;
- completar;
- cancelar.

UX:
- vencidas identificables sin depender solo de color;
- próximas a vencer diferenciables;
- responsive;
- sin scroll horizontal normal.

Alta/edición en modal o drawer.

Campos:

```text
Título *
Descripción
Responsable
Prioridad *
Estado
Fecha/hora de vencimiento
```

## 10. Preparación para Dashboard futuro
NO implementar Dashboard nuevo.

Solo asegurar que el modelo permita consultar posteriormente:
- tareas vencidas;
- tareas para hoy;
- tareas próximas;
- casos sin seguimiento reciente;
- último seguimiento por caso.

## 11. Multi-tenancy obligatorio
Probar explícitamente:
- seguimiento propio;
- seguimiento de otro tenant rechazado;
- estado de otro tenant rechazado;
- competencia de otro tenant rechazada;
- detalle hijo incorrecto rechazado;
- tarea propia;
- tarea de otro tenant rechazada;
- responsable de otro tenant rechazado.

## 12. Migración
Crear migración incremental para:

```text
CaseFollowUps
LegalCompetences
CaseTasks
```

No modificar migraciones previas.

Usar FKs y `DeleteBehavior.Restrict` donde proteja histórico.

Índices recomendados:

```text
CaseFollowUps(OrganizationId, CaseId, OccurredAt)
CaseTasks(OrganizationId, CaseId, Status, DueAt)
LegalCompetences(OrganizationId, ParentId, IsActive)
```

## 13. Tests mínimos

### Seguimientos
- crear válido;
- listar;
- caso de otro tenant rechazado;
- estado de otro tenant rechazado;
- competencia de otro tenant rechazada;
- detalle fuera de competencia rechazado;
- histórico conserva referencias;
- orden cronológico correcto.

### Tareas
- crear;
- responsable válido;
- responsable ajeno rechazado;
- completar y registrar `CompletedAt`;
- reabrir y limpiar `CompletedAt` si se soporta;
- cancelar;
- filtros;
- aislamiento multi-tenant.

### Competencias
- crear raíz;
- crear hijo;
- listar solo tenant activo;
- desactivar;
- inactivo no aparece en selector operativo;
- relación histórica permanece.

Todos los tests anteriores deben seguir pasando.

## 14. Responsive
Validar:

```text
360
390
768
1024
1280
1440
```

Pantallas:
- detalle Caso;
- Seguimientos;
- nuevo Seguimiento;
- Tareas;
- nueva Tarea;
- Configuración → Competencias.

Criterios:
- sin overflow innecesario;
- controles no se superponen;
- selects dependientes funcionan;
- descripción usable;
- acciones accesibles.

## 15. Fuera de alcance
NO implementar todavía:
- Enviar/Compartir Seguimiento;
- reversión/historial especial;
- correos;
- WhatsApp;
- push;
- recordatorios automáticos;
- Agenda real;
- Documentos;
- Dashboard final;
- IA.

## 16. Definition of Done
- [ ] `CaseFollowUp` implementado.
- [ ] `LegalCompetence` implementado.
- [ ] Competencias administrables.
- [ ] API Seguimientos.
- [ ] UI Seguimientos.
- [ ] Alta Seguimiento.
- [ ] Listado cronológico responsive.
- [ ] Estado histórico visible.
- [ ] Competencia General/Detalle dependiente.
- [ ] `CaseTask` implementado.
- [ ] API Tareas.
- [ ] UI Tareas.
- [ ] Crear/editar/completar/cancelar Tarea.
- [ ] Validación de responsable.
- [ ] Migración aplicada.
- [ ] Multi-tenancy probado.
- [ ] Responsive validado.
- [ ] Backend Release compila.
- [ ] Frontend compila.
- [ ] Tests previos + nuevos pasan.
- [ ] `git diff --check` correcto.
- [ ] No se implementaron funcionalidades inferidas.

## 17. Validación final

Backend:

```bash
cd backend
dotnet restore
dotnet build --configuration Release
dotnet test
dotnet ef database update \
  --project src/LegalManagement.Infrastructure \
  --startup-project src/LegalManagement.Api
```

Frontend:

```bash
cd frontend/legal-management-web
npm run build
```

Repositorio:

```bash
git diff --check
```

## 18. Entrega esperada

```text
Estado Sprint 3

Resumen
- porcentaje
- estado
- pendientes

Seguimientos
- modelo
- API
- UI
- validaciones
- responsive

Competencias
- jerarquía
- administración
- activos/inactivos

Tareas
- modelo
- API
- UI
- estados
- prioridades

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

Pendientes funcionales no implementados
- Enviar/Compartir
- Historial/Reversión
- actualización automática del estado del Caso desde Seguimiento, salvo confirmación explícita
```
