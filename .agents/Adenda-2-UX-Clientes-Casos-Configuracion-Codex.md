# Adenda 2 — UX/UI, Clientes, Casos y Configuración

## Contexto

Esta adenda continúa el trabajo de Sprint 1 y Sprint 2. **No iniciar Sprint 3**.

El objetivo es corregir problemas reales de UX/UI detectados durante validación manual y completar mejor los módulos de Clientes, Casos y Configuración antes de seguir construyendo nuevas funcionalidades.

Las capturas proporcionadas por el usuario son referencia visual y funcional. No deben copiarse literalmente ni convertir el producto en un ERP. Deben usarse para entender el nivel de completitud esperado en un sistema profesional utilizado en Panamá.

## Principios obligatorios

- Trabajar sobre el estado ACTUAL de `sgl-saas-app`.
- No rehacer arquitectura que ya funciona.
- No romper multi-tenancy.
- No avanzar a Sprint 3.
- No gastar tiempo en cambios cosméticos que no resuelvan el problema funcional.
- Antes de modificar una pantalla, revisar cómo está implementada actualmente.
- Cuando un bug se corrija, validar visualmente que realmente desapareció.
- No considerar resuelto un problema únicamente porque el proyecto compila.
- Mantener todos los tests existentes en verde.
- No usar scroll horizontal como solución normal para tablas de Clientes y Casos.
- Mantener la identidad visual azul legal existente.

---

# 1. Clientes — ampliar información del cliente

Ruta:

```text
/app/clients
```

El modelo actual de Cliente es demasiado mínimo para un sistema legal profesional en Panamá.

La referencia mostrada contiene muchos datos típicos de un ERP. **No debemos copiar campos de crédito, puntos, listas de precios, vendedor u otros conceptos comerciales que no correspondan a un sistema legal.**

Sí debemos enriquecer el cliente con información útil para un bufete.

## 1.1 Modelo funcional esperado

### Campos comunes

```text
Client
------
Id
OrganizationId
Type
IdentificationType
IdentificationNumber
DisplayName

Email
Phone
SecondaryPhone
MobilePhone
Website

Address
Country
ProvinceOrState
City

Notes
Status

CreatedAt
UpdatedAt
```

### Persona Natural

```text
FirstName
LastName
```

### Persona Jurídica

```text
LegalName
TradeName
Ruc
Dv
ContactPersonName
ContactPersonIdentification
ContactPersonEmail
ContactPersonPhone
```

## 1.2 Consideraciones

- Para `PERSON`, mantener:
  - CEDULA
  - PASSPORT
- Para `COMPANY`, mantener:
  - RUC
- No inventar validaciones regulatorias de Panamá que no estén documentadas.
- Mantener la validación estructural existente.
- `RUC` y `DV` deben poder manejarse de manera clara para Persona Jurídica.
- Dirección debe poder almacenar información completa sin forzarla a una sola línea.
- País, provincia/estado y ciudad deben estar claramente separados en UI.
- No convertir este módulo en CRM o ERP.

## 1.3 Migración

Si se agregan campos:

- crear migración incremental;
- mantener nullable aquellos campos que no puedan derivarse de datos históricos;
- no perder registros actuales;
- no modificar migraciones anteriores.

---

# 2. Clientes — rediseño de filtros

La barra actual con:

```text
Búsqueda general
Tipo
Estado
Filtrar
```

no es suficiente.

Se requiere una sección de filtros más clara y orientada a campos.

## 2.1 Diseño esperado

Crear un panel de filtros del estilo:

```text
Filtros
------------------------------------------------
Nombre / Razón social     [_____________]

Identificación            [_____________]

Correo                    [_____________]

Teléfono                  [_____________]

Tipo                      [Todos       v]

Estado                    [Todos       v]

                        [Limpiar] [Aplicar filtros]
```

Campos mínimos:

- Nombre / Razón social
- Identificación
- Correo
- Teléfono
- Tipo
- Estado

## 2.2 Responsive

### Desktop
Puede mostrarse como panel/grid de varias columnas.

### Tablet/móvil
Debe apilarse correctamente o abrirse en un panel/drawer de filtros.

No permitir:

- controles superpuestos;
- filtros cortados;
- botones fuera del contenedor;
- overflow horizontal.

## 2.3 Backend/API

No depender únicamente de un `search` global.

Agregar parámetros individuales cuando no existan, por ejemplo:

```text
name
identification
email
phone
type
status
page
pageSize
```

Mantener paginación.

Si se conserva `search`, puede seguir existiendo como búsqueda rápida, pero los filtros por campo deben ser independientes.

---

# 3. Clientes — tabla responsive y collapse correcto

Problema actual:

El collapse muestra campos que ya aparecen en la fila principal y todavía existe scroll horizontal.

Esto debe corregirse.

## 3.1 Regla principal

La fila normal muestra solamente los campos principales.

Ejemplo desktop:

```text
> | Nombre / Razón social | Identificación | Teléfono | Estado | Acción
```

Según el ancho disponible se puede mostrar además:

```text
Tipo
Correo
```

## 3.2 Fila expandida

Al expandir, mostrar **únicamente información adicional que no está visible arriba**.

Ejemplo:

```text
Correo
Teléfono secundario
Móvil
Dirección
Ciudad
Provincia
País
Sitio web
Contacto principal (si es empresa)
Última actualización
```

NO repetir:

- Nombre si ya está arriba.
- Identificación si ya está arriba.
- Estado si ya está arriba.
- Teléfono si ya está arriba.

## 3.3 Sin scroll horizontal

No aceptar `overflow-x: auto` como solución principal de la tabla.

La tabla debe adaptar columnas por breakpoint.

La información que no cabe debe migrar al panel expandible de la fila.

## 3.4 Componente reutilizable

Si ya existe un componente responsive, corregirlo.

Si no existe, crear uno reutilizable para Clientes y Casos.

Debe soportar:

- expanded/collapsed;
- teclado;
- `aria-expanded`;
- indicador visual;
- filas sin duplicación;
- responsive real.

---

# 4. Casos — rediseño completo de filtros

Ruta:

```text
/app/cases
```

Problema actual:

Los filtros se desbordan y se superponen visualmente.

No parchear únicamente CSS del contenedor actual. Reorganizar el bloque de filtros.

## 4.1 Filtros esperados

Como mínimo:

```text
Número de caso
Título
Cliente
Estado
Tipo de caso
Prioridad
Responsable
Tribunal / Juzgado
Fecha de situación desde
Fecha de situación hasta
```

Cliente, Estado, Tipo, Responsable y Tribunal deben utilizar los componentes de búsqueda/autocomplete existentes cuando aplique.

## 4.2 Layout

### Desktop

Usar grid responsive de 2–4 columnas según ancho.

Ejemplo:

```text
Número       Título
Cliente      Estado
Tipo         Prioridad
Responsable  Tribunal
Desde        Hasta

[Limpiar] [Aplicar filtros]
```

### Tablet/móvil

Una o dos columnas según espacio.

Nunca superponer controles.

Nunca cortar placeholders, labels o botones.

## 4.3 API

Agregar/ajustar filtros individuales si actualmente solo existen búsquedas generales.

Mantener:

- tenant activo;
- paginación;
- filtros combinables.

---

# 5. Casos — tabla responsive y expansión

Aplicar la misma regla conceptual que Clientes.

## 5.1 Campos principales

La fila principal debe priorizar:

```text
Número
Título
Cliente
Estado
Responsable
```

Dependiendo del ancho:

```text
Prioridad
Fecha de situación
```

## 5.2 Campos secundarios en expandido

Mostrar solo información que NO se ve en la fila principal:

```text
Tipo de caso
Tribunal
Jurisdicción
Fecha de apertura
Fecha de cierre
Contraparte
Abogado contrario
Notas resumidas
```

No repetir campos ya visibles.

## 5.3 Sin scroll horizontal

No debe existir scroll horizontal en el uso normal del listado de Casos.

Si aparece en 1024/768/390/360, se considera bug no resuelto.

---

# 6. Sidebar responsive / collapsable

El sidebar actual ocupa demasiado espacio cuando baja el ancho.

Debe tener tres modos.

## 6.1 Desktop amplio

```text
Sidebar expandido
Icono + texto
Selector de organización completo
```

## 6.2 Desktop pequeño / tablet

Permitir modo colapsado:

```text
solo iconos
```

Debe existir botón claro para expandir/colapsar.

Cuando está colapsado:

- tooltip o equivalente para cada icono;
- no romper navegación;
- contenido central gana ancho;
- selector de organización debe seguir siendo accesible.

Puede recordar la preferencia localmente si es simple.

## 6.3 Móvil

Usar drawer/off-canvas.

No mantener sidebar fijo consumiendo ancho.

Debe cerrar al navegar o mediante botón explícito.

---

# 7. Configuración — rediseño de Tablas Administrables

Problema actual:

- Los catálogos aparecen como botones/tabs horizontales.
- Crear/editar ocurre en el mismo bloque superior.
- Visualmente no escala si se agregan más tablas.
- El concepto de “Tablas administrables” debe ser un módulo reutilizable y escalable.

## 7.1 Arquitectura de la pantalla

Mantener:

```text
Configuración
└── Tablas administrables
```

Pero dentro utilizar un selector de catálogo.

Ejemplo:

```text
Tabla a administrar
[ Estados de caso                         v ]
```

Opciones actuales:

```text
Estados de caso
Tipos de caso
Tribunales / Juzgados
Jurisdicciones
```

El diseño debe permitir agregar más catálogos en el futuro sin llenar la pantalla de botones.

## 7.2 Listado

Luego de seleccionar una tabla:

```text
Estados de caso                    [+ Nuevo]

Buscar [________________]

------------------------------------------------
Nombre       Código       Estado       Acciones
------------------------------------------------
Abierto      OPEN         Activo       Editar
Cerrado      CLOSED       Activo       Editar
```

## 7.3 Crear / Editar

NO mostrar permanentemente el formulario de alta encima del listado.

Usar uno de estos patrones:

- modal;
- drawer lateral;
- pantalla secundaria.

Preferencia: **drawer o modal**, consistente con el resto del producto.

Flujo:

```text
Nuevo
  ↓
abre formulario
  ↓
guardar
  ↓
cerrar
  ↓
refrescar listado
```

Editar:

```text
Editar
  ↓
abre formulario con valores actuales
  ↓
guardar cambios
```

## 7.4 CaseStatus

Al editar Estado de Caso debe permitir:

```text
Nombre
Código
Orden
Activo

Es abierto
Es cerrado
Es inocente
Es culpable
```

La UI debe explicar estos flags de forma entendible.

Ejemplo:

```text
Clasificación del estado

[x] Cuenta como caso activo
[ ] Cuenta como caso cerrado
[ ] Resultado inocente
[ ] Resultado culpable
```

## 7.5 Otros catálogos

Tipo de Caso:

```text
Nombre
Orden
Activo
```

Tribunal:

```text
Nombre
Orden
Activo
```

Jurisdicción:

```text
Nombre
Orden
Activo
```

## 7.6 UX

Agregar:

- loading;
- empty state;
- confirmación antes de desactivar;
- feedback éxito/error;
- cancelar edición;
- focus correcto;
- navegación por teclado;
- responsive.

---

# 8. UX/UI transversal

Aplicar criterios consistentes:

- Labels visibles, no depender solo de placeholder.
- Espaciado consistente.
- Botones primario/secundario coherentes.
- No cortar texto.
- No superponer controles.
- No dejar grandes espacios sin propósito.
- Inputs con alturas coherentes.
- Empty states claros.
- Loading visible.
- Errores cerca del campo afectado cuando aplique.
- No usar rojo como borde permanente de campos válidos.
- Mantener azul legal como identidad principal.

---

# 9. Validación visual obligatoria

Esta adenda NO se considera terminada solo porque `npm run build` funciona.

Validar visualmente:

```text
360 px
390 px
768 px
1024 px
1280 px
1440 px
```

Pantallas mínimas:

```text
/app/clients
Nuevo Cliente
Editar Cliente

/app/cases
Nuevo Caso
Editar Caso
Detalle Caso

Configuración
Tablas administrables
Nuevo catálogo
Editar catálogo
```

Revisar en cada viewport:

- sidebar;
- filtros;
- tablas;
- expansión;
- formularios;
- acciones;
- modales/drawers;
- autocomplete.

## Criterio estricto

Si una pantalla presenta:

- scroll horizontal innecesario;
- controles superpuestos;
- campos cortados;
- collapse con información duplicada;
- sidebar inutilizable;

el punto NO está terminado.

---

# 10. Backend / tests

Preservar todas las pruebas actuales.

Agregar pruebas para cualquier nueva lógica backend introducida por:

- nuevos campos de Cliente;
- filtros individuales;
- RUC/DV;
- nuevos filtros de Casos.

No duplicar pruebas de UI como tests backend.

Si existe Playwright en el proyecto y ya es utilizable, agregar smoke tests visuales/funcionales razonables para:

- Clientes;
- Casos;
- Configuración.

No crear infraestructura E2E compleja si no existe.

---

# 11. Definition of Done

- [ ] Cliente ampliado con datos realmente útiles para contexto legal en Panamá.
- [ ] No se agregaron campos ERP irrelevantes.
- [ ] Persona Jurídica maneja RUC y DV claramente.
- [ ] Información de contacto/dirección ampliada.
- [ ] Filtros de Clientes rediseñados por campo.
- [ ] Filtros de Casos rediseñados por campo.
- [ ] Filtros no se sobreponen en ningún viewport validado.
- [ ] Tabla Clientes sin scroll horizontal.
- [ ] Tabla Casos sin scroll horizontal.
- [ ] Collapse Clientes no repite datos visibles.
- [ ] Collapse Casos no repite datos visibles.
- [ ] Collapse muestra información secundaria útil.
- [ ] Sidebar colapsable en resoluciones intermedias.
- [ ] Sidebar móvil funciona como drawer.
- [ ] Configuración usa selector de tabla administrable.
- [ ] Alta/edición de catálogo ya no comparte permanentemente el mismo espacio con el listado.
- [ ] Crear catálogo funciona.
- [ ] Editar catálogo funciona.
- [ ] Activar/desactivar funciona.
- [ ] CaseStatus muestra flags con lenguaje entendible.
- [ ] Loading/empty/error states revisados.
- [ ] Responsive validado en 360, 390, 768, 1024, 1280 y 1440.
- [ ] Backend compila.
- [ ] Frontend compila.
- [ ] Todos los tests anteriores siguen pasando.
- [ ] Nuevos tests necesarios pasan.
- [ ] `git diff --check` correcto.
- [ ] Sprint 3 NO iniciado.

---

# 12. Forma de trabajo para Codex

Antes de cambiar código:

1. Revisar implementación actual de Clientes, Casos, layout/sidebar y Configuración.
2. Identificar exactamente por qué aparece el overflow actual.
3. Identificar qué datos se repiten en las filas expandidas.
4. Revisar modelo actual de `Client`.
5. Revisar endpoints actuales de filtros.
6. Revisar componentes reutilizables disponibles.
7. Presentar un resumen corto de plan técnico.
8. Luego implementar.

No hacer cambios a ciegas.

Después de cada bloque importante:

- compilar;
- ejecutar tests relevantes;
- revisar visualmente.

---

# 13. Validación final

Backend:

```bash
cd backend
dotnet restore
dotnet build --configuration Release
dotnet test
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

Además:

- aplicar cualquier migración nueva;
- verificar que EF no tenga cambios de modelo pendientes;
- validar visualmente los viewports obligatorios.

---

# 14. Entrega esperada

Al finalizar entregar:

```text
Estado Adenda 2

Resumen
- porcentaje
- estado
- pendientes

Clientes
- modelo
- filtros
- tabla
- collapse
- responsive

Casos
- filtros
- tabla
- collapse
- responsive

Sidebar
- desktop
- tablet
- móvil

Configuración
- selector de catálogo
- listado
- crear
- editar
- activar/desactivar

Migraciones
- nombre
- aplicada
- datos preservados

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

Pendientes reales
```

**No iniciar Sprint 3.**
