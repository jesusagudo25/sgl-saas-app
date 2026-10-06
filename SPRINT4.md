# Estado Sprint 4 — Agenda y Calendario

Entrega validada el 5 de octubre de 2026 (America/Panama).

## Resumen

- Avance: 100% del alcance solicitado.
- Estado: implementación, migración y validaciones completadas.
- Pendientes funcionales del sprint: ninguno.
- Agenda global en `/app/calendar`, con acceso desde el menú.
- Pestaña Agenda funcional en el detalle del Caso.

## Auditoría previa

Se revisaron el detalle de `Case`, su pestaña Agenda, `CaseTask`, las membresías y selección de responsables, `SearchableSelect`, layout responsive, políticas, pruebas y migraciones. No existía dependencia de calendario. Agenda era un placeholder en el caso y un botón deshabilitado en el menú.

Se reutilizan `TenantContext`, el filtrado explícito por organización, los modales nativos y la política `OrganizationMember`, como en los módulos de Casos y Tareas. Esta política existente permite operaciones a miembros activos, incluidos LAWYER, ASSISTANT y READONLY; Sprint 4 preserva esa política y prueba su comportamiento.

Se incorporó FullCalendar 6.1.21: core, React, daygrid y timegrid, con locale español. La dependencia se carga mediante un chunk diferido al abrir Agenda, para mantener el bundle principal por debajo de 500 kB. Sus convenciones de fechas y fin exclusivo están documentadas en [FullCalendar: Event Parsing](https://fullcalendar.io/docs/event-parsing).

## Agenda

`CalendarEvent` incluye los campos solicitados: organización, caso opcional, título, descripción, tipo, estado, responsable opcional, inicio, fin, todo el día, ubicación, URL de reunión, creador y fechas de auditoría.

Tipos fijos: HEARING, MEETING, DEADLINE, CALL, VISIT, OTHER. Estados fijos: SCHEDULED, COMPLETED, CANCELLED. No se agregaron catálogos para estos valores.

| Método | Ruta | Comportamiento |
| --- | --- | --- |
| GET | `/api/calendar/events` | Eventos propios con filtros combinables y orden por inicio |
| GET | `/api/calendar/events/{id}` | Obtener evento del tenant activo |
| POST | `/api/calendar/events` | Crear con referencias validadas y creador autenticado |
| PUT | `/api/calendar/events/{id}` | Editar conservando creador y fecha de creación |
| PATCH | `/api/calendar/events/{id}/status` | Completar o cancelar conservando el evento |
| GET | `/api/calendar/options` | Casos propios y membresías activas para selectores |

Filtros: `from`, `to`, `caseId`, `assignedMembershipId`, `eventType`, `status`. La consulta incluye eventos que se solapan con el intervalo, sin limitarse a aquellos cuyo inicio cae dentro del periodo. El límite superior `to` es exclusivo, compatible con FullCalendar. Un evento de duración cero en `from` se conserva en la consulta.

Rango invertido, tipo/estado inválido o referencias inválidas reciben 400. Obtener, editar o cambiar el estado de un evento ajeno recibe 404. El listado y las opciones nunca incluyen datos de otras organizaciones. Se valida que el responsable asignado esté activo. El creador se obtiene de la membresía del usuario autenticado y la organización se obtiene exclusivamente del tenant resuelto.

La URL de reunión es opcional y acepta HTTP/HTTPS. Inicio y fin son obligatorios, con `EndsAt >= StartsAt`. La consulta de DTO proyecta los nombres de caso y responsable junto con los eventos, sin realizar una consulta adicional por cada fila.

## Vistas y formularios

- Vistas Mes, Semana y Día.
- Hoy, periodo anterior/siguiente y título del periodo actual.
- Filtros por Caso, Responsable, Tipo y Estado; Caso y Responsable usan `SearchableSelect`.
- Click en evento para abrir el formulario de edición.
- Lista de eventos del periodo con botones accesibles y títulos completos, útil cuando las celdas del calendario son estrechas.
- Modal para alta/edición: todos los campos solicitados, validación, loading, error/success y cierre sin guardar.
- Acciones explícitas Completar evento y Cancelar evento; el estado también puede editarse en el formulario.
- Eventos cancelados permanecen consultables y muestran su estado mediante texto y color.
- Eventos simultáneos se presentan sin superposición horizontal en las vistas de tiempo.

No se agregaron eliminación física, recurrencias ni conversión automática entre tareas y eventos.

## Todo el día y timezone

Eventos con hora: el formulario usa `datetime-local`, convierte al guardar a UTC y muestra los instantes en la zona local del navegador. La API usa `DateTimeOffset` en sus contratos y normaliza los valores a UTC; las respuestas incluyen offset UTC explícito.

Eventos de todo el día: Inicio y Fin usan controles `date`. El fin del formulario es el último día incluido. Las fechas civiles se persisten como medianoche UTC de esas mismas fechas y no se convierten según el offset del navegador. Por ejemplo:

```text
Formulario: Inicio 2026-10-05; Fin 2026-10-07; Todo el día
Persistencia: StartsAt 2026-10-05T00:00:00Z; EndsAt 2026-10-07T00:00:00Z
FullCalendar: start "2026-10-05"; end "2026-10-08"; allDay true
```

Al editar se recuperan las fechas civiles desde los primeros diez caracteres del valor ISO. Para FullCalendar, el adaptador añade un día al fin inclusivo y envía cadenas de fecha sin timezone. El filtrado de eventos de todo el día usa las fechas civiles del rango, conservando el último día incluido. Si `to` tiene hora posterior a medianoche, se incluye el día civil que contiene esa hora.

Las pruebas de API verifican esta convención con offsets UTC−12, UTC y UTC+14. El navegador se validó en America/Panama, Pacific/Kiritimati y Etc/GMT+12. La fecha de todo el día permanece en el día esperado al visualizar, editar y guardar.

## Integración con Caso

La pestaña Agenda usa el mismo componente que la agenda global, con `caseId` contextual fijo. Consulta únicamente eventos de ese caso, permite ver/editarlos y crea eventos con el caso preseleccionado. El formulario contextual muestra el caso como campo de lectura para conservar la asociación.

`CaseTask` y `CalendarEvent` siguen siendo entidades independientes. Una prueba verifica que registrar un evento no crea tareas; los handlers de tareas no se modificaron para crear eventos.

## Migración e integridad

Migración: `20261006035248_AddCalendarEvents`.

- Aplicada a la base local configurada.
- Migraciones anteriores intactas; snapshot actualizado mediante EF Core.
- `has-pending-model-changes`: sin cambios pendientes.
- Claves foráneas `Restrict` para organización, caso, responsable y creador.
- Restricción SQL `CK_CalendarEvents_Dates`: `EndsAt >= StartsAt`.
- Sin endpoint DELETE; cancelación conservando registros.

Índices:

```text
CalendarEvents(OrganizationId, StartsAt, EndsAt)
CalendarEvents(OrganizationId, CaseId, StartsAt)
CalendarEvents(OrganizationId, AssignedMembershipId, StartsAt)
CalendarEvents(OrganizationId, Status, StartsAt)
```

## Tests

| Suite | Total | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| Sprints anteriores | 60 | 60 | 0 | 0 |
| Sprint 4 (`CalendarTests`) | 21 | 21 | 0 | 0 |
| Total | 81 | 81 | 0 | 0 |

Los nuevos tests cubren crear/obtener/editar, creador y organización correctos, normalización UTC, completar/cancelar sin borrado, caso ajeno, responsable ajeno/inactivo, evento ajeno no visible ni modificable, opciones aisladas, fechas ausentes/invertidas, tipo/estado inválidos, URL inválida, filtros individuales y combinados, solapamiento y límites de rango, instantes de duración cero, todo el día con offsets extremos, política existente de miembros y protección de referencias históricas.

Las pruebas de integración levantan la API real y aplican todas las migraciones en bases temporales SQL Server LocalDB.

## Validación visual automatizada

| Anchura | Mes | Semana | Día | Alta/edición | Filtros/cancelación | Agenda del Caso / AllDay |
| --- | --- | --- | --- | --- | --- | --- |
| 360 | PASS | PASS | PASS | PASS | PASS | PASS |
| 390 | PASS | PASS | PASS | PASS | PASS | PASS |
| 768 | PASS | PASS | PASS | PASS | PASS | PASS |
| 1024 | PASS | PASS | PASS | PASS | PASS | PASS |
| 1280 | PASS | PASS | PASS | PASS | PASS | PASS |
| 1440 | PASS | PASS | PASS | PASS | PASS | PASS |

`sprint4-visual-check.mjs` comprueba navegación, creación con conversión a UTC, precarga de edición, completar/cancelar, filtros combinados, estado vacío, error de guardado sin pérdida de valores, agenda contextual, controles AllDay y conservación de fechas en zonas extremas. Verifica overflow del documento, calendario y modal, campos superpuestos, eventos simultáneos sin superposición, cabeceras correctas del mes y ausencia de errores JavaScript.

Genera ocho capturas por anchura y dos adicionales para los extremos de timezone: 50 capturas en `%TEMP%/sgl-sprint4-visual`. Se inspeccionaron capturas de móvil, tablet y escritorio.

Esta validación del navegador usa API simulada para reproducir estados de forma determinista. La persistencia, tenancy y autorización se verifican por separado mediante los tests de integración contra API real y LocalDB.

Regresiones visuales: `visual-check.mjs` y `sprint3-visual-check.mjs` pasan en las seis anchuras. En el script de Sprint 3 se sincronizó la selección del detalle con la carga de sus opciones y se comprobó el identificador seleccionado antes de guardar, evitando una carrera del propio test al simular la entrada.

## Build y reproducción

- Backend Release: PASS, sin advertencias ni errores.
- Restore: correcto durante los builds/tests de .NET.
- Frontend TypeScript/Vite: PASS; calendario en chunk diferido, sin advertencia de bundle mayor de 500 kB.
- Modelo/migración: sin cambios pendientes.
- `git diff --check`: PASS.

Desde `backend`:

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
dotnet tool run dotnet-ef database update --project src/LegalManagement.Infrastructure --startup-project src/LegalManagement.Api
dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/LegalManagement.Infrastructure --startup-project src/LegalManagement.Api
```

Desde `frontend/legal-management-web`, con Vite en `http://127.0.0.1:5173` para los scripts del navegador:

```powershell
npm run build
node sprint4-visual-check.mjs
node sprint3-visual-check.mjs
node visual-check.mjs
```

El nuevo script permite cambiar la URL del servidor mediante `SGL_VISUAL_URL`.

## Fuera de alcance

No se implementaron Google Calendar, Outlook, ICS, recurrencias, recordatorios, push, correo, WhatsApp, invitaciones externas, disponibilidad avanzada, dashboard final, Documentos o IA.
