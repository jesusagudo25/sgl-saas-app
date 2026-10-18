# Sprint 5 — Documentos y Dashboard final — Codex

## Objetivo
Cerrar el MVP operativo de `sgl-saas-app` con gestión documental segura por Caso y un Dashboard final orientado al trabajo diario.

No rehacer sprints anteriores. No romper multi-tenancy. No implementar IA, OCR, firma electrónica ni integraciones externas.

## 1. Auditoría previa
Revisar antes de cambiar código:
- `Case` y tab Documentos;
- Dashboard actual;
- `CaseTask`, `CaseFollowUp`, `CalendarEvent`;
- membresías, políticas y `TenantContext`;
- patrones responsive/modal;
- tests, migraciones y configuración;
- cualquier storage ya existente.

Presentar un resumen técnico corto antes de implementar.

## 2. CaseDocument
```text
Id
OrganizationId
CaseId
OriginalFileName
StoredFileName
Extension
ContentType
SizeBytes
DocumentType
Notes
StorageProvider
StorageKey
UploadedByMembershipId
UploadedAt
IsDeleted
DeletedAt?
DeletedByMembershipId?
```

Tipos iniciales:
```text
PLEADING
RESOLUTION
EVIDENCE
CONTRACT
IDENTIFICATION
CORRESPONDENCE
OTHER
```

## 3. Seguridad documental
- `OrganizationId` desde `TenantContext`.
- Caso y usuarios deben pertenecer al tenant activo.
- Generar nombre físico seguro/único.
- Evitar path traversal.
- No guardar en `wwwroot`.
- No exponer rutas físicas, `StoredFileName` ni `StorageKey`.
- Tamaño máximo configurable.
- Validar extensión y MIME razonablemente.
- Descarga autorizada por tenant/caso.
- Soft delete con auditoría.
- Documento eliminado no aparece ni descarga.

## 4. Storage
Crear abstracción:
```csharp
public interface IDocumentStorage
{
    Task<StoredDocumentResult> SaveAsync(...);
    Task<Stream> OpenReadAsync(...);
    Task DeleteAsync(...);
}
```

Development:
- `LocalDocumentStorage`
- carpeta configurable fuera de `wwwroot`.

Producción:
- dejar contrato listo para Blob/S3.
- no implementar cloud sin configuración real.
- no guardar binarios en SQL Server.

## 5. API documentos
```http
GET    /api/cases/{caseId}/documents
POST   /api/cases/{caseId}/documents
GET    /api/cases/{caseId}/documents/{id}/download
DELETE /api/cases/{caseId}/documents/{id}
```

POST con `multipart/form-data`:
```text
file
documentType
notes
```

Listado:
```text
Id
OriginalFileName
Extension
ContentType
SizeBytes
DocumentType
Notes
UploadedBy
UploadedAt
```

## 6. UI Documentos
Activar tab:
```text
[Resumen] [Seguimientos] [Tareas] [Agenda] [Documentos]
```

Listado:
```text
Documento
Tipo
Tamaño
Subido por
Fecha
Acciones
```

Acciones:
- Descargar.
- Eliminar con confirmación.

Alta modal/drawer:
```text
Archivo *
Tipo *
Observaciones
```

Responsive sin scroll horizontal normal.

## 7. Eliminación
DELETE = soft delete:
```text
IsDeleted = true
DeletedAt = now UTC
DeletedByMembershipId = membership actual
```

No borrar físicamente en MVP salvo criterio documentado y seguro.

## 8. Dashboard final
Debe responder:
> ¿Qué debo atender hoy?

Cards:
```text
Casos activos
Tareas vencidas
Tareas para hoy
Eventos de hoy
```

Bloques:
```text
Próximas tareas
Próximos eventos
Casos sin seguimiento reciente
Últimos seguimientos
Accesos rápidos
```

Accesos rápidos sugeridos:
```text
Nuevo caso
Nuevo cliente
Nueva tarea
Nuevo evento
```

No agregar charts o KPIs sin utilidad operativa clara.

## 9. Definiciones
### Casos activos
Usar semántica existente de estado (`IsOpen` o equivalente), no texto.

### Tareas vencidas
```text
DueAt < ahora
Status != COMPLETED
Status != CANCELLED
```

### Tareas para hoy / Eventos de hoy
Respetar convención de timezone existente de Sprint 4.

### Próximos
Usar una ventana corta documentada, por ejemplo 7 días, si no existe otra convención.

### Casos sin seguimiento reciente
Usar umbral configurable/documentado; recomendación MVP: 30 días, sin presentarlo como regla legal.

### Últimos seguimientos
Orden descendente por `OccurredAt`.

## 10. API Dashboard
Ruta sugerida:
```http
GET /api/dashboard
```

DTO agregado:
```text
activeCasesCount
overdueTasksCount
todayTasksCount
todayEventsCount
upcomingTasks[]
upcomingEvents[]
staleCases[]
latestFollowUps[]
generatedAt
staleCaseDays
upcomingDays
```

## 11. Rendimiento
Evitar N+1 y cargas completas.

Preferir:
```text
AsNoTracking
CountAsync
Select
OrderBy
Take
```

No introducir Redis.

## 12. Roles
Preservar políticas existentes. No ampliar permisos silenciosamente.

Auditar especialmente READONLY y documentar cualquier deuda de permisos encontrada; no cambiarla dentro de Sprint 5 sin decisión explícita.

## 13. Migración
Crear migración incremental para `CaseDocuments`.

Índices sugeridos:
```text
CaseDocuments(OrganizationId, CaseId, IsDeleted, UploadedAt)
CaseDocuments(OrganizationId, UploadedAt)
```

No modificar migraciones previas.

## 14. Tests documentos
Cubrir:
- subir válido;
- metadata;
- listar;
- descargar;
- soft delete;
- eliminado no visible/no descargable;
- tenant/caso ajeno;
- tamaño excesivo;
- extensión inválida;
- nombre malicioso/path traversal neutralizado;
- uploader/deleter correctos;
- histórico protegido.

## 15. Tests Dashboard
Cubrir:
- casos activos;
- tareas vencidas;
- tareas hoy;
- eventos hoy;
- próximos;
- casos sin seguimiento;
- últimos seguimientos;
- aislamiento multi-tenant;
- dataset vacío;
- límites de fecha;
- máximos `Take(n)`.

Todos los 81 tests previos deben seguir en verde.

## 16. Responsive
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
- Dashboard normal/vacío/con datos abundantes.
- Documentos.
- Carga.
- Eliminación.
- Error.
- Nombre de archivo largo.

Si la estrategia actual lo permite, crear `sprint5-visual-check.mjs`.

## 17. Fuera de alcance
No implementar:
- OCR;
- IA/RAG;
- resumen/extracción automática;
- firma electrónica;
- versionado complejo;
- enlaces públicos;
- portal cliente;
- Google Drive/OneDrive/Dropbox;
- proveedor cloud real sin configuración;
- facturación/pagos;
- nuevas automatizaciones/notificaciones.

## 18. Definition of Done
- [ ] `CaseDocument`.
- [ ] `IDocumentStorage`.
- [ ] Storage local configurable.
- [ ] Migración aplicada.
- [ ] EF sin cambios pendientes.
- [ ] API listar/subir/descargar/eliminar.
- [ ] Soft delete + auditoría.
- [ ] Validaciones de seguridad.
- [ ] Tab Documentos.
- [ ] Dashboard final.
- [ ] Cards operativas.
- [ ] Próximas tareas/eventos.
- [ ] Casos sin seguimiento.
- [ ] Últimos seguimientos.
- [ ] Accesos rápidos.
- [ ] Multi-tenancy probado.
- [ ] Responsive validado.
- [ ] Backend Release.
- [ ] Frontend build.
- [ ] Tests previos + nuevos.
- [ ] `git diff --check`.

## 19. Validación final
```bash
cd backend
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release

dotnet tool run dotnet-ef database update   --project src/LegalManagement.Infrastructure   --startup-project src/LegalManagement.Api

dotnet tool run dotnet-ef migrations has-pending-model-changes   --project src/LegalManagement.Infrastructure   --startup-project src/LegalManagement.Api
```

```bash
cd frontend/legal-management-web
npm run build
```

Si existen:
```bash
node sprint5-visual-check.mjs
node sprint4-visual-check.mjs
node sprint3-visual-check.mjs
node visual-check.mjs
```

```bash
git diff --check
```

## 20. Entrega esperada
```text
Estado Sprint 5

Resumen
- porcentaje
- estado
- pendientes

Documentos
- modelo
- storage
- seguridad
- API
- UI
- soft delete

Dashboard
- cards
- tareas
- eventos
- casos sin seguimiento
- últimos seguimientos
- accesos rápidos
- rendimiento

Migración
- nombre
- aplicada
- pending model changes

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

Fuera de alcance confirmado
```
