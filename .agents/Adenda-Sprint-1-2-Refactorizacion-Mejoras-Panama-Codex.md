# Adenda Sprint 1–2 — Refactorización y mejoras para contexto legal Panamá

## Objetivo

Aplicar mejoras posteriores a Sprint 1 (Clientes) y Sprint 2 (Casos) antes de iniciar Sprint 3.

La adenda parte de requerimientos funcionales ya definidos para el producto:

- mejorar responsive del layout;
- evitar tablas con scroll horizontal como patrón principal;
- adaptar identificación de clientes al contexto de Panamá;
- convertir estados, tipos de caso, tribunales y jurisdicciones en catálogos administrables por organización;
- agregar `SituationDate`;
- utilizar autocomplete en selectores relevantes;
- mantener el sistema dinámico, flexible y multi-tenant.

## Reglas generales para Codex

Trabaja sobre el estado ACTUAL de `sgl-saas-app`.

- NO reinicies ni reestructures la solución.
- NO avances a Sprint 3.
- NO elimines funcionalidad ya validada.
- Conserva `TenantContext`, Identity, Memberships, Organizations, Clients y Cases.
- Usa migraciones incrementales. NO reescribas migraciones anteriores.
- NO confíes en `OrganizationId` enviado desde frontend.
- Todo catálogo administrable debe estar aislado por organización.
- No agregues Redis, MongoDB, colas o microservicios.
- Mantén la paleta azul legal.
- Conserva todos los tests existentes en verde.
- Si una decisión requiere inventar una regla legal/documental que no está especificada, detente y repórtala.

---

# Bloque 1 — Responsive transversal

## 1.1 Sidebar / menú

Corregir el layout para que el menú sea realmente responsive.

### Desktop
- Sidebar visible.
- Área de contenido utiliza el ancho restante.
- No generar espacios muertos innecesarios.

### Tablet / móvil
- Sidebar colapsable tipo drawer/off-canvas.
- Debe poder abrirse/cerrarse fácilmente.
- El contenido no debe quedar comprimido por un sidebar fijo.
- Topbar, organización activa y acciones deben seguir siendo utilizables.

## 1.2 Tablas responsive

NO utilizar scroll horizontal como solución principal.

Crear un patrón reutilizable, por ejemplo:

```text
ResponsiveDataTable
```

### Desktop
Mostrar columnas principales y secundarias según espacio disponible.

### Tablet/móvil
Mostrar columnas esenciales.

Cada fila debe tener una acción de expansión:

```text
▶ Nombre | Identificación | Estado
  └── Correo
      Teléfono
      Tipo
      Responsable
      Fecha
      ...
```

Aplicar al menos a Clientes y Casos.

## 1.3 Formularios

Corregir:
- Nuevo Cliente.
- Editar Cliente.
- Nuevo Caso.
- Editar Caso.

Usar layout responsive:

```text
desktop  → grids de 2 o más columnas cuando tenga sentido
tablet   → reducción progresiva
mobile   → una columna
```

Evitar anchos fijos que dejen grandes espacios vacíos.

---

# Bloque 2 — Identificación de Clientes

## Requerimiento funcional

### Persona Natural

```text
CEDULA
PASSPORT
```

### Persona Jurídica

```text
RUC
```

La UI debe modificar las opciones disponibles al cambiar `Client.Type`.

```text
PERSON
 ├─ CEDULA
 └─ PASSPORT

COMPANY
 └─ RUC
```

## Backend

Centralizar:
- tipo permitido según tipo de cliente;
- normalización;
- validación;
- mensajes de error.

Frontend puede validar para UX, pero backend es autoridad.

## IMPORTANTE: reglas de formato Panamá

La adenda exige formato correcto para Cédula, Pasaporte y RUC, pero la especificación suministrada no documenta las reglas formales exactas.

Por tanto:

**NO inventar regex o reglas fiscales/documentales.**

Si el repositorio no contiene una especificación previamente aprobada:

1. implementar la estructura de validación;
2. mantener los códigos correctos de tipo;
3. documentar cuáles reglas exactas faltan;
4. detener la implementación de regex estrictos hasta recibir definición.

No utilizar una expresión regular obtenida arbitrariamente de Internet.

## Unicidad

Revisar la restricción existente.

Como mínimo:

```text
OrganizationId
IdentificationNumber
```

Si el dominio actual permite el mismo número bajo tipos distintos, evaluar:

```text
OrganizationId
IdentificationType
IdentificationNumber
```

sin cambiar la regla actual sin revisar primero los tests de Sprint 1.

---

# Bloque 3 — Infraestructura de Catálogos Administrables

Crear:

```text
Configuración
└── Tablas administrables
```

Catálogos:
- Estados de Caso.
- Tipos de Caso.
- Tribunales / Juzgados.
- Jurisdicciones.

Principios comunes:

```text
Id
OrganizationId
Name
IsActive
SortOrder
CreatedAt
UpdatedAt
```

Reglas:
- pertenecen a una organización;
- creación, edición, activación/desactivación;
- no borrar físicamente si ya fueron utilizados;
- selectores operativos muestran solo activos;
- datos históricos conservan referencias;
- aislamiento multi-tenant.

---

# Bloque 4 — Estados de Caso administrables

Refactorizar estados rígidos hacia catálogo por organización.

## Modelo propuesto

```text
CaseStatus
----------
Id
OrganizationId
Name
Code
IsActive
IsOpen
IsClosed
IsInnocent
IsGuilty
SortOrder
CreatedAt
UpdatedAt
```

NO inferir significado por nombre.

Incorrecto:

```csharp
status.Name == "ASEG. INOCENTE - CERRADO"
```

Usar propiedades analíticas:

```text
IsOpen
IsClosed
IsInnocent
IsGuilty
```

Los estados mostrados en la referencia de seguros son solo contexto, no catálogo universal obligatorio.

---

# Bloque 5 — Tipos de Caso administrables

Modelo:

```text
CaseType
--------
Id
OrganizationId
Name
IsActive
SortOrder
CreatedAt
UpdatedAt
```

El Caso debe usar:

```text
CaseTypeId
```

---

# Bloque 6 — Información judicial administrable

## Tribunal / Juzgado

```text
Court
-----
Id
OrganizationId
Name
IsActive
SortOrder
CreatedAt
UpdatedAt
```

Caso:

```text
CourtId
```

## Jurisdicción

```text
Jurisdiction
------------
Id
OrganizationId
Name
IsActive
SortOrder
CreatedAt
UpdatedAt
```

Caso:

```text
JurisdictionId
```

Todo selector operativo debe mostrar solo registros activos.

---

# Bloque 7 — Fecha de situación

Agregar:

```text
SituationDate
```

Diferenciar:

```text
SituationDate
→ fecha en que ocurre el hecho/situación que origina o contextualiza el caso.

OpenedAt
→ fecha en que se abre el expediente/proceso.

ClosedAt
→ fecha de cierre.
```

Debe estar en:
- crear Caso;
- editar Caso;
- detalle;
- DTOs;
- API;
- persistencia;
- tests.

---

# Bloque 8 — Autocomplete / searchable selects

Crear o reutilizar componente de búsqueda/autocomplete para:

- Cliente.
- Responsable.
- Estado de Caso.
- Tipo de Caso.
- Tribunal / Juzgado.
- Jurisdicción.

Debe soportar:
- búsqueda mientras escribe;
- teclado;
- loading;
- sin resultados;
- limpiar selección cuando aplique;
- valor actual al editar;
- solo activos para nuevas selecciones.

No agregar dependencia pesada sin necesidad.

---

# Bloque 9 — Refactor del modelo Case

Modelo objetivo aproximado:

```text
Case
----
Id
OrganizationId
ClientId
CaseNumber
Title
Description

CaseStatusId
CaseTypeId
CourtId
JurisdictionId

ResponsibleMembershipId

SituationDate
OpenedAt
ClosedAt

Counterparty
OpposingCounsel
Notes

CreatedAt
UpdatedAt
```

Revisar primero el modelo real e implementar incrementalmente.

---

# Bloque 10 — Migración de datos

NO borrar información existente de Sprint 2.

Si existen:

```text
Status
CaseType
Court
Jurisdiction
```

como enum/string:

1. crear tablas nuevas;
2. crear registros por organización desde valores actuales;
3. mapear cada Case hacia el nuevo ID;
4. verificar integridad;
5. solo después retirar o deprecar campos anteriores;
6. no perder valores no reconocidos.

Índices a evaluar:

```text
CaseStatus(OrganizationId, Name)
CaseType(OrganizationId, Name)
Court(OrganizationId, Name)
Jurisdiction(OrganizationId, Name)
```

---

# Bloque 11 — APIs

Ejemplo:

```http
GET    /api/settings/case-statuses
POST   /api/settings/case-statuses
PUT    /api/settings/case-statuses/{id}
PATCH  /api/settings/case-statuses/{id}/status

GET    /api/settings/case-types
POST   /api/settings/case-types
PUT    /api/settings/case-types/{id}
PATCH  /api/settings/case-types/{id}/status

GET    /api/settings/courts
POST   /api/settings/courts
PUT    /api/settings/courts/{id}
PATCH  /api/settings/courts/{id}/status

GET    /api/settings/jurisdictions
POST   /api/settings/jurisdictions
PUT    /api/settings/jurisdictions/{id}
PATCH  /api/settings/jurisdictions/{id}/status
```

Adaptar nombres al patrón existente.

Todos requieren TenantContext.

Inicialmente la gestión puede restringirse a ADMIN reutilizando políticas existentes.

---

# Bloque 12 — Pantalla Configuración

Agregar:

```text
Configuración
└── Tablas administrables
    ├── Estados de Caso
    ├── Tipos de Caso
    ├── Tribunales / Juzgados
    └── Jurisdicciones
```

Cada catálogo:
- listado;
- nuevo;
- editar;
- activar/desactivar;
- feedback;
- responsive.

---

# Bloque 13 — Tests obligatorios

## Clientes
- PERSON permite Cédula.
- PERSON permite Pasaporte.
- COMPANY permite RUC.
- combinaciones inválidas rechazadas.
- aislamiento multi-tenant.
- unicidad sigue funcionando.

No probar regex exactos hasta tener reglas explícitamente aprobadas.

## Catálogos
Para cada catálogo:
- crear en tenant activo;
- listar solo tenant activo;
- actualizar propio;
- rechazar recurso de otro tenant;
- desactivar;
- inactivos no aparecen en selector operativo.

## CaseStatus
- `IsOpen`;
- `IsClosed`;
- `IsInnocent`;
- `IsGuilty`;
- estado de otro tenant rechazado.

## Case
- CaseType propio válido.
- CaseType ajeno rechazado.
- Court propio válido.
- Court ajeno rechazado.
- Jurisdiction propia válida.
- Jurisdiction ajena rechazada.
- SituationDate persiste.
- SituationDate actualiza.
- referencias históricas sobreviven a inactivación.

Todos los tests anteriores deben continuar en verde.

---

# Bloque 14 — Validación responsive

Validar al menos:

```text
360 px
390 px
768 px
1024 px
1440 px
```

Revisar:
- sidebar;
- Clientes listado;
- Clientes nuevo/editar;
- Casos listado;
- Casos nuevo/editar;
- detalle Caso;
- Configuración;
- tablas administrables;
- autocomplete.

Criterio:

No aceptar una pantalla que requiera scroll horizontal para utilizar normalmente una tabla.

---

# Definition of Done

- [ ] Sidebar responsive.
- [ ] Tablas responsive con expansión.
- [ ] Clientes sin scroll horizontal como patrón principal.
- [ ] Casos sin scroll horizontal como patrón principal.
- [ ] Formularios Cliente usan correctamente el ancho.
- [ ] Formularios Caso usan correctamente el ancho.
- [ ] Tipos de identificación dependen de PERSON / COMPANY.
- [ ] Validación de identificación centralizada.
- [ ] No se inventaron regex legales no especificados.
- [ ] CaseStatus administrable por organización.
- [ ] CaseStatus incluye atributos analíticos.
- [ ] CaseType administrable.
- [ ] Court administrable.
- [ ] Jurisdiction administrable.
- [ ] Catálogos tienen `IsActive`.
- [ ] Catálogos inactivos no aparecen en nuevas selecciones.
- [ ] Registros históricos conservan relaciones.
- [ ] SituationDate implementado.
- [ ] Autocomplete reutilizable.
- [ ] Cliente usa autocomplete.
- [ ] Responsable usa autocomplete.
- [ ] Catálogos usan autocomplete/búsqueda.
- [ ] Migración incremental aplicada sin pérdida de datos.
- [ ] Aislamiento multi-tenant probado.
- [ ] Responsive validado.
- [ ] Backend compila.
- [ ] Frontend compila.
- [ ] Tests anteriores + nuevos pasan.
- [ ] `git diff --check` correcto.
- [ ] No se implementó Sprint 3.

---

# Orden de ejecución recomendado

```text
1. Auditar modelo actual Sprint 1/2
2. Responsive base + componentes reutilizables
3. Identificación Clientes
4. Infraestructura catálogos
5. CaseStatus
6. CaseType
7. Court
8. Jurisdiction
9. Migración de Case
10. SituationDate
11. Autocomplete
12. Configuración UI
13. Regression tests
14. Responsive validation
15. Build/test final
```

---

# Validación final

Backend:

```bash
cd backend
dotnet restore
dotnet build --no-restore
dotnet test
dotnet tool restore

dotnet ef migrations list   --project src/LegalManagement.Infrastructure   --startup-project src/LegalManagement.Api
```

Aplicar migración a SQL Server local y verificar nuevas tablas, FKs, índices y conservación de datos.

Frontend:

```bash
cd frontend/legal-management-web
npm install
npm run build
```

Finalmente:

```bash
git diff --check
```

---

# Entrega esperada de Codex

## Estado Adenda Sprint 1–2

### Resumen
- estado:
- porcentaje:
- pendientes:

### Backend
- entidades nuevas:
- endpoints:
- cambios Case/Client:
- validaciones:
- migraciones:

### Frontend
- responsive:
- tablas:
- formularios:
- autocompletes:
- configuración:

### Migración de datos
Explicar cómo se conservaron datos existentes.

### Tests
- tests previos:
- tests nuevos:
- total:
- passed:
- failed:

### Builds
- backend:
- frontend:
- `git diff --check`:

### Multi-tenancy
Explicar pruebas realizadas.

### Responsive
Indicar viewports validados.

### Pendientes
Solo pendientes reales de la adenda.

NO iniciar Sprint 3.


Continúa la Adenda Sprint 1–2 desde el estado ACTUAL.

NO reinicies ni reestructures el proyecto.
NO avances a Sprint 3.
NO rehagas funcionalidades ya implementadas.

El estado actual es aproximadamente 82% y existen 36 pruebas aprobadas.

Quiero cerrar únicamente los pendientes reales de la adenda.

# 1. Tablas responsive con expansión explícita

Actualmente en móvil se muestran columnas esenciales, pero falta el comportamiento solicitado de expansión por fila.

Implementa un patrón reutilizable para Clientes y Casos:

- mostrar columnas esenciales;
- botón/ícono para expandir;
- al expandir mostrar campos secundarios debajo de la fila;
- sin scroll horizontal como patrón principal;
- usable por teclado;
- indicador visual claro de expandido/colapsado.

Debe funcionar al menos en:

- Clientes;
- Casos.

Evita duplicar lógica si puede resolverse con un componente reutilizable simple.

# 2. Edición UI de catálogos

La API de edición ya existe.

Completa la edición frontend para:

- Estados de Caso;
- Tipos de Caso;
- Tribunales / Juzgados;
- Jurisdicciones.

Debe permitir:

- abrir registro existente;
- editar nombre;
- editar orden si aplica;
- editar propiedades analíticas de CaseStatus:
  - IsOpen
  - IsClosed
  - IsInnocent
  - IsGuilty
- guardar;
- mostrar feedback de éxito/error;
- conservar activación/desactivación ya existente.

No crear endpoints nuevos si los actuales ya soportan esto.

# 3. Completar matriz de tests

Agregar pruebas faltantes para los catálogos administrables.

Para CADA catálogo:

- crear en tenant A;
- listar únicamente tenant A;
- actualizar propio;
- rechazar actualización de tenant B;
- desactivar;
- confirmar que un inactivo no aparece en opciones operativas.

Para relaciones Case:

- rechazar CaseStatus de otro tenant;
- rechazar CaseType de otro tenant;
- rechazar Court de otro tenant;
- rechazar Jurisdiction de otro tenant;
- comprobar que referencias históricas sobreviven a inactivación.

Mantener todos los tests anteriores en verde.

# 4. Loading del autocomplete

Mejorar `SearchableSelect` sin sobrediseñar.

Debe diferenciar como mínimo:

- loading;
- sin resultados;
- resultado disponible;
- error si aplica.

Evitar parpadeos o mensaje “sin resultados” mientras todavía está cargando.

Mantener navegación por teclado.

# 5. Validación responsive

Validar:

- 360 px
- 390 px
- 768 px
- 1024 px
- 1440 px

Pantallas:

- layout/sidebar;
- Clientes listado;
- Nuevo Cliente;
- Editar Cliente;
- Casos listado;
- Nuevo Caso;
- Editar Caso;
- Detalle Caso;
- Configuración;
- Tablas administrables;
- autocompletes.

Criterios:

- no overflow horizontal funcional;
- formularios aprovechan el ancho;
- sidebar funciona correctamente;
- tablas permiten expandir información secundaria;
- botones y acciones siguen siendo utilizables.

Si hay infraestructura Playwright utilizable, puedes automatizar validaciones razonables.
No agregues una infraestructura E2E compleja únicamente para este punto.

# 6. Cédula / Pasaporte / RUC Panamá

NO implementar regex inventadas.

Mantener:

- PERSON → CEDULA / PASSPORT
- COMPANY → RUC
- validación estructural centralizada existente.

Documentar como dependencia funcional pendiente:

“Definir reglas formales aprobadas de validación documental para Panamá”.

Esto NO debe impedir cerrar técnicamente la adenda.

# Validación final

Ejecuta:

Backend:

dotnet restore
dotnet build --configuration Release
dotnet test

Frontend:

npm run build

Repositorio:

git diff --check

Verificar además:

- migraciones aplicadas;
- EF sin cambios de modelo pendientes;
- Sprint 3 no implementado.

# Entrega final

Entrégame:

## Estado final Adenda Sprint 1–2

### Resumen
- porcentaje:
- estado:

### Pendientes cerrados
...

### Responsive
- viewports validados:
- tablas expandibles:
- formularios:
- sidebar:

### Catálogos
- edición UI:
- multi-tenancy:
- activos/inactivos:

### Autocomplete
- loading:
- empty:
- teclado:

### Tests
- anteriores:
- nuevos:
- total:
- passed:
- failed:

### Builds
- backend:
- frontend:
- git diff --check:

### Dependencias externas pendientes
Debe aparecer únicamente aquello que realmente dependa de una definición externa, por ejemplo las reglas formales de Cédula/Pasaporte/RUC.

NO iniciar Sprint 3.