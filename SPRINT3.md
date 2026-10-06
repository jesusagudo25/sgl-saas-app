# Estado Sprint 3 — Seguimientos y Tareas

Entrega validada el 5 de octubre de 2026 (America/Panama).

## Resumen

- Avance: 100% del alcance solicitado; sin pendientes del MVP.
- Seguimientos y Tareas activos dentro del detalle existente del caso.
- Competencias incorporadas a Configuración → Tablas administrables.
- Migración aplicada a la base local configurada; migraciones anteriores intactas.

## Auditoría previa y decisiones

Se revisaron `Case`, sus pestañas, los cuatro catálogos, `SettingsCatalogs`, `SearchableSelect`, las hojas responsive, middleware de tenancy, políticas, tests y migraciones.

La implementación reutiliza `TenantContext`, filtrado explícito por organización y las políticas existentes: `OrganizationMember` para operaciones de casos y opciones operativas; `OrganizationAdmin` para administración de competencias. Como en `CasesController`, la política existente de miembros no distingue permisos de escritura entre LAWYER, ASSISTANT y READONLY. Este sprint preserva esa política.

Responsable y vencimiento de tarea son opcionales, coherentes con el modelo existente de casos. Estados y prioridades son valores fijos del dominio. Las fechas de los formularios se convierten a UTC y se presentan en la zona local del navegador.

## Seguimientos

`CaseFollowUp` incluye los identificadores, descripción, fecha de actuación, creador y fechas de auditoría solicitados. También guarda los nombres del estado, competencia y detalle al registrar la actuación; los cambios posteriores de nombre no alteran ese snapshot.

| Método | Ruta | Comportamiento |
| --- | --- | --- |
| GET | `/api/cases/{caseId}/follow-ups` | Lista del tenant, orden descendente por actuación, registro e identificador |
| POST | `/api/cases/{caseId}/follow-ups` | Valida relaciones activas y registra al creador autenticado |
| GET | `/api/cases/{caseId}/follow-ups/{id}` | Consulta propia del tenant y del caso |

El API devuelve estado histórico, competencia, detalle, descripción, autor, fecha de actuación y fecha de registro. No hay edición ni eliminación física de seguimientos. El caso, estado y ambas competencias deben pertenecer a la organización activa; el detalle debe ser hijo de la raíz seleccionada.

La UI incorpora listado responsive, alta en modal, loading, estados vacíos y mensajes de éxito/error. General → Detalle usa `SearchableSelect`, limpia el detalle al cambiar la raíz y consulta únicamente hijos activos. Una raíz sin detalles muestra una explicación y bloquea el alta; no inventa detalles. Crear un seguimiento no modifica `Case.CaseStatusId`.

## Competencias

`LegalCompetence` reutiliza el catálogo organizacional y añade `ParentId`. Sólo se permiten dos niveles: general y detalle. La API rechaza padres ajenos, nietos, autorreferencias y cambios de jerarquía de registros con hijos o historial.

| Método | Ruta |
| --- | --- |
| GET / POST | `/api/settings/competences` |
| PUT | `/api/settings/competences/{id}` |
| PATCH | `/api/settings/competences/{id}/status` |
| GET | `/api/cases/competences?parentId={id}` |

Las rutas de configuración exigen ADMIN. El selector operativo es accesible a miembros y excluye registros inactivos, también cuando su raíz está inactiva. Configuración permite crear raíces e hijos, editar nombre y orden y activar/desactivar. Conserva los registros históricos; no expone eliminación física. Los nombres son únicos por tenant y nivel/padre.

`SearchableSelect` valida que el texto corresponda a un resultado, evitando que un nombre inválido se convierta silenciosamente en una selección vacía.

## Tareas

`CaseTask` contiene todos los campos solicitados y referencias al caso, organización, creador y responsable.

| Método | Ruta | Comportamiento |
| --- | --- | --- |
| GET | `/api/cases/{caseId}/tasks` | Lista con filtros combinables |
| POST | `/api/cases/{caseId}/tasks` | Crea tarea con responsable activo del tenant o sin asignar |
| PUT | `/api/cases/{caseId}/tasks/{id}` | Edita tarea propia y valida responsable |
| PATCH | `/api/cases/{caseId}/tasks/{id}/status` | Cambia estado y mantiene `CompletedAt` |

Filtros: `status`, `priority`, `assignedMembershipId`, `dueFrom`, `dueTo`. Un rango invertido o valor de estado/prioridad inválido recibe 400. Recursos de otro tenant o caso reciben 404, sin revelar su existencia.

- Estados: PENDING, IN_PROGRESS, COMPLETED, CANCELLED.
- Prioridades: LOW, MEDIUM, HIGH, URGENT.
- Completar registra `CompletedAt` en UTC; repetir COMPLETED conserva la fecha original.
- Reabrir limpia `CompletedAt`; cancelar conserva la tarea y su trazabilidad de creación.
- La UI permite crear, editar, iniciar, completar, reabrir y cancelar.
- Vencidas y próximas a vencer se identifican con texto e indicador además del color. La ventana visual de proximidad es de 48 horas; no genera recordatorios.
- Recargas posteriores a una actualización usan los filtros actuales; las respuestas anteriores de filtros no sobrescriben solicitudes más recientes.

## Migración e integridad

Migración: `20261006031003_AddCaseFollowUpsCompetencesAndTasks`.

Aplicada correctamente a la base local configurada. EF Core confirma que no hay cambios del modelo pendientes desde la migración. Los tests crean bases LocalDB temporales y aplican la cadena completa de migraciones.

Se crean `CaseFollowUps`, `LegalCompetences` y `CaseTasks` con claves foráneas `Restrict`. Índices:

- `CaseFollowUps(OrganizationId, CaseId, OccurredAt)`.
- `CaseTasks(OrganizationId, CaseId, Status, DueAt)`.
- `CaseTasks(OrganizationId, Status, DueAt)` para consultas futuras de vencimientos.
- `LegalCompetences(OrganizationId, ParentId, IsActive)`.
- Unicidad de nombres de competencias por organización/padre.

El modelo permite consultar último seguimiento, casos sin actividad reciente y tareas vencidas, de hoy y próximas sin agregar un dashboard.

## Tests

| Suite | Total | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| Tests anteriores | 48 | 48 | 0 | 0 |
| Sprint 3 (`CaseActivityTests`) | 12 | 12 | 0 | 0 |
| Total | 60 | 60 | 0 | 0 |

Los tests nuevos cubren alta/listado y orden cronológico de seguimientos, creador autenticado, caso/estado/competencia/detalle ajenos, detalle de raíz incorrecta, snapshot y referencias tras desactivación, bloqueo de eliminación referenciada y protección de jerarquía histórica. Tareas: creación, edición, responsable válido/ajeno/inactivo, completar, idempotencia, reabrir, cancelar, filtros e aislamiento. Competencias: raíz/hijo, jerarquía, tenant, desactivación, selector operativo y ADMIN frente a los demás roles.

## Validación visual y responsive

| Anchura | Detalle | Seguimientos y alta | Tareas y alta | Competencias y alta |
| --- | --- | --- | --- | --- |
| 360 | PASS | PASS | PASS | PASS |
| 390 | PASS | PASS | PASS | PASS |
| 768 | PASS | PASS | PASS | PASS |
| 1024 | PASS | PASS | PASS | PASS |
| 1280 | PASS | PASS | PASS | PASS |
| 1440 | PASS | PASS | PASS | PASS |

`sprint3-visual-check.mjs` verifica siete vistas por anchura, overflow del documento y modal, superposición de campos, selectores dependientes y raíz sin hijos, creación, precarga de edición, completar/reabrir/cancelar, filtros y estado vacío. Comprueba alertas de vencida/próxima y ausencia de errores JavaScript. Genera 42 capturas en `%TEMP%/sgl-sprint3-visual`; se inspeccionaron capturas de móvil, tablet y escritorio.

Esta comprobación de navegador usa respuestas API simuladas para reproducir estados de forma determinista. La persistencia, autorización y aislamiento se prueban por separado contra la API real con SQL Server LocalDB.

La regresión visual anterior (`visual-check.mjs`) también pasa: ocho vistas en cada una de las seis anchuras, menú móvil y sidebar colapsable. Se corrigió su selector de cierre para apuntar al botón del menú, en lugar del fondo cuya zona central está cubierta por el sidebar.

## Build y reproducción

Backend Release: PASS, sin advertencias ni errores. Frontend TypeScript/Vite: PASS. `git diff --check`: PASS.

Desde `backend`:

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
dotnet tool run dotnet-ef database update --project src/LegalManagement.Infrastructure --startup-project src/LegalManagement.Api
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/LegalManagement.Infrastructure --startup-project src/LegalManagement.Api
```

Desde `frontend/legal-management-web`, con Vite en `http://127.0.0.1:5173` para las comprobaciones visuales:

```powershell
npm run build
node sprint3-visual-check.mjs
node visual-check.mjs
```

## Funcionalidades expresamente fuera de alcance

No se implementaron Enviar/Compartir, historial/reversión especial ni actualización automática del estado del caso desde un seguimiento. Tampoco correos, WhatsApp, push, recordatorios automáticos, Agenda real, Documentos, dashboard nuevo o IA.
