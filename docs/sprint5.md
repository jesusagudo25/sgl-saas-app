# Sprint 5: Documentos y Dashboard

Estado: completado (100%). Sin pendientes del alcance solicitado.

## Documentos

`CaseDocument` conserva metadatos, organización, caso, proveedor, clave privada y auditoría de carga/eliminación. Los binarios no se guardan en SQL Server. Las relaciones usan Restrict para proteger el histórico; la eliminación lógica también tiene control de concurrencia.

`IDocumentStorage` define SaveAsync, OpenReadAsync y DeleteAsync. `LocalDocumentStorage` genera claves UUID, rechaza claves con rutas, escribe con CreateNew y limita bytes durante la copia. La carpeta se configura con `Documents:RootPath`, relativa al ContentRoot de la API o absoluta. Por defecto: `.local/documents`, fuera de wwwroot e ignorada por Git. No configurar esta carpeta como recurso estático. El usuario del proceso necesita acceso de lectura/escritura. Para producción, configurar un volumen privado persistente y respaldos; Blob/S3 requieren una implementación del contrato y configuración real. No se implementó proveedor cloud.

Configuración en appsettings.json (también admite variables de entorno):

- `Documents:Provider`: Local, único proveedor implementado.
- `Documents:RootPath`: .local/documents.
- `Documents:MaxBytes`: 10485760 (10 MiB); admite hasta 100 MiB. `Documents__MaxBytes` permite cambiarlo por entorno.

API autorizada mediante OrganizationMember y TenantRequired:

- GET `/api/cases/{caseId}/documents`
- POST `/api/cases/{caseId}/documents` (multipart: file, documentType, notes)
- GET `/api/cases/{caseId}/documents/{id}/download`
- DELETE `/api/cases/{caseId}/documents/{id}`

Organización y actor se resuelven desde TenantContext y membresía activa; no se reciben del formulario. Caso ajeno y documento ajeno devuelven 404; organización sin membresía devuelve 403. Ningún DTO contiene StoredFileName, StorageKey o rutas físicas. La descarga usa autenticación y el nombre original saneado. Se permiten PDF, DOC/DOCX, XLS/XLSX, TXT, CSV, JPEG y PNG con MIME correspondiente. Se valida extensión/MIME declarado; no se realiza análisis de contenido, antivirus ni OCR. Se rechazan archivos vacíos, tipos inválidos, notas excesivas y tamaños superiores al límite. El nombre original pierde componentes de ruta, caracteres de control y caracteres de nombre inseguros.

DELETE conserva el binario: IsDeleted, DeletedAt UTC y DeletedByMembershipId. No aparece ni se descarga posteriormente. DeleteAsync del storage se usa únicamente para compensar un fallo al guardar metadatos; no elimina documentos históricos. No existe purga automática en el MVP.

La pestaña Documentos ofrece listado, descarga, alta con modal y eliminación confirmada; incluye estados vacío, carga, error y éxito. En pantallas pequeñas cada documento se reorganiza sin desplazamiento horizontal.

## Dashboard

GET `/api/dashboard?timeZone=America/Panama` devuelve cards, listas, generatedAt, staleCaseDays y upcomingDays. La UI envía la zona IANA del navegador, coherente con Sprint 4; omitirla usa America/Panama. Las horas se almacenan en UTC y los eventos de todo el día se comparan como fechas civiles, con fin inclusivo. Hoy usa intervalo [medianoche local, siguiente medianoche); los eventos cronometrados cuentan si se solapan con ese día. Eventos completados/cancelados se excluyen de las cards y próximas actividades.

- Casos activos: CaseStatus.IsOpen, incluso con catálogo inactivo. Casos antiguos sin referencia usan códigos OPEN/IN_PROGRESS/SUSPENDED, coherentes con la equivalencia existente.
- Tareas vencidas: DueAt < ahora, excluyendo COMPLETED y CANCELLED.
- Tareas para hoy: vencimiento dentro del día local, excluyendo COMPLETED y CANCELLED. Puede solaparse con vencidas.
- Próximas tareas: desde ahora hasta medianoche local dentro de UpcomingDays (fin exclusivo). Incluye hoy; excluye sin vencimiento y tareas terminadas/canceladas.
- Próximos eventos: programados, en curso o futuros dentro de esa ventana; fechas civiles para todo el día.
- Casos sin seguimiento: activos cuyo último OccurredAt, o CreatedAt si nunca tuvieron seguimiento, es anterior a ahora menos StaleCaseDays. Los casos nuevos sin seguimiento esperan ese umbral. Es una referencia operativa, no una regla legal.
- Últimos seguimientos: OccurredAt descendente, desempate por Id.

`Dashboard:UpcomingDays` = 7 (1–31), `Dashboard:StaleCaseDays` = 30 (1–3650), `Dashboard:ListLimit` = 10 (1–50). Los valores se acotan a esos rangos. Todas las listas usan Take, proyecciones y AsNoTracking; los totales usan CountAsync. No hay consultas por cada elemento ni cargas completas de entidades. No se agregó Redis.

Los accesos rápidos crean caso/cliente, abren la creación de evento o permiten seleccionar un caso antes de abrir Nueva tarea. Los elementos del Dashboard enlazan con la pestaña correspondiente del caso.

## Permisos auditados

Deuda preexistente: OrganizationMember permite escrituras a cualquier membresía activa, incluida READONLY, en clientes, casos, seguimientos, tareas y agenda. Sprint 5 conserva esa misma política para documentos; READONLY puede subir y eliminar. No se agregó ni cambió una matriz de permisos. Una prueba explicita esta compatibilidad y verifica auditoría con uploader/deleter distintos. Una restricción futura de escrituras requiere decisión expresa y revisión conjunta de los módulos existentes. La administración de catálogos mantiene OrganizationAdmin.

## Migración y validación

Migración incremental: `20261018221917_AddCaseDocuments`, aplicada a la base local configurada en Development. No se modificaron migraciones anteriores. EF: sin cambios pendientes. Índices (OrganizationId, CaseId, IsDeleted, UploadedAt) y (OrganizationId, UploadedAt), además de los índices de claves foráneas.

Pruebas: 81 anteriores + 16 nuevas = 97; 97 passed, 0 failed, 0 skipped. Se ejecutan con API real, SQL Server LocalDB y migraciones. Las nuevas cubren ciclo documental, metadatos privados, validaciones, auditoría, histórico, tenant/caso ajeno, nombres maliciosos, storage, semántica de estado, dataset vacío, límites exactos de fechas y Take. El Dashboard usa un reloj fijo en integración para evitar resultados dependientes de la hora de ejecución. Cada fixture usa base y carpeta documental temporal independientes.

Build backend Release y frontend: correctos. `git diff --check`: correcto.

`sprint5-visual-check.mjs` valida Dashboard normal/vacío/abundante/carga/error y documentos con nombre largo, descarga, alta, error, eliminación y vacío en 360, 390, 768, 1024, 1280 y 1440. Comprueba overflow de página y modal; guarda capturas en `%TEMP%/sgl-sprint5-visual`. Los cuatro scripts visuales (Sprint 5, Sprint 4, Sprint 3 y visual-check) pasaron. Sprint 4 también verifica fechas civiles en Pacific/Kiritimati y Etc/GMT+12. Las pruebas visuales usan API simulada; la integración de API/persistencia se valida mediante los tests .NET.

## Fuera de alcance

Sin IA/RAG, OCR, extracción automática, firma, versionado complejo, enlaces públicos, portal cliente, integraciones externas, proveedor cloud real, facturación, pagos, automatizaciones ni notificaciones nuevas. Las restricciones preexistentes para desplegar en producción (por ejemplo IEmailSender) siguen aplicando.
