# F3 - Registro de incidentes y reporte manual

Ticket: sin ticket (el catÃƒÂ¡logo no registra tickets para F3).

## Propósito
Permite que un operador registre manualmente una nota de incidente y consulte el historial reciente. TambiÃƒÂ©n ofrece una vista previa filtrable en el inicio. Los incidentes son evidencia de eventos, no modifican decisiones de acceso (`src/Enrollment.Domain/Incident.cs:6-9`).

## Dónde actuar
- HTTP / pÃƒÂ¡gina Blazor interactiva `/incidents`; carga la lista al inicializar y permite reportar manualmente (`src/Enrollment.Web/Components/Pages/Incidents.razor:1-2,120-128,142-156`).
- HTTP / pÃƒÂ¡gina Blazor interactiva `/`; carga una vista previa reciente con filtros de tipo y texto (`src/Enrollment.Web/Components/Pages/Home.razor:1-2,35-49,110-116`).

## Disparador / Punto de entrada
## Entradas (payload/config)

- Tipo: selecciÃƒÂ³n de uno de los valores de `IncidentKind`; el formulario inicia con `ManualNote` (`src/Enrollment.Web/Components/Pages/Incidents.razor:21-26,114`).
- Nota: texto opcional con lÃƒÂ­mite visual de 1000 caracteres (`src/Enrollment.Web/Components/Pages/Incidents.razor:28-30`).
- Filtro de tipo y texto de nota en inicio; el texto se actualiza al escribir (`src/Enrollment.Web/Components/Pages/Home.razor:35-49,100-107`).
- ID nuevo y hora UTC se generan al enviar; la pÃƒÂ¡gina no permite asociar miembro ni ruta de imagen desde el formulario manual (`src/Enrollment.Web/Components/Pages/Incidents.razor:153-156`).

## Pasos

1. Al abrir `/incidents`, obtiene los 100 incidentes mÃƒÂ¡s recientes desde `IIncidentRepository`; un fallo se registra y se muestra como error de carga (`src/Enrollment.Web/Components/Pages/Incidents.razor:109,120-139`).
2. El operador elige un tipo y opcionalmente ingresa una nota (`src/Enrollment.Web/Components/Pages/Incidents.razor:16-39`).
3. Al enviar, evita una segunda ejecuciÃƒÂ³n concurrente, construye un incidente con ID nuevo, hora UTC, miembro nulo, nota y ruta de imagen nulas, y lo guarda (`src/Enrollment.Web/Components/Pages/Incidents.razor:142-156`).
4. Tras guardar, limpia la nota, restablece el tipo a `ManualNote`, recarga la lista y muestra confirmaciÃƒÂ³n; si falla el guardado, muestra un error y conserva los datos ingresados (`src/Enrollment.Web/Components/Pages/Incidents.razor:157-167`).
5. En inicio, carga hasta 20 incidentes y filtra en memoria por tipo exacto y por texto de nota sin distinguir mayÃƒÂºsculas/minÃƒÂºsculas (`src/Enrollment.Web/Components/Pages/Home.razor:95,103-116`).

## Puntos de salida

| Salida | Detalle |
|---|---|
| Repositorio de incidentes | Guarda/upsert por ID los campos `Id`, `OccurredAtUtc`, `Kind`, `MemberId`, `Note` y `SnapshotImagePath` (`src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:104-127`). |
| Lista de incidentes | Devuelve hasta el lÃƒÂ­mite solicitado, ordenada por hora UTC descendente y por `rowid` descendente para desempatar (`src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:132-163`). |
| Interfaz `/incidents` | Presenta fecha/hora UTC, tipo, nota, miembro y estados de carga, error o lista vacÃƒÂ­a (`src/Enrollment.Web/Components/Pages/Incidents.razor:57-103`). |
| Inicio `/` | Presenta hasta 20 resultados filtrados; muestra carga, error o estado sin coincidencias (`src/Enrollment.Web/Components/Pages/Home.razor:52-90,95,103-108`). |

## Reglas de negocio

- El dominio normaliza la fecha a UTC, recorta espacios en valores opcionales y convierte cadenas vacÃƒÂ­as o solo espacios en `null` (`src/Enrollment.Domain/Incident.cs:48-63,85-91`).
- La lista usa el orden mÃƒÂ¡s reciente primero; un lÃƒÂ­mite menor o igual a cero devuelve una lista vacÃƒÂ­a (`src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:132-152`).
- Guardar de nuevo el mismo ID reemplaza la fila existente (`src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:116-127`).
- Un incidente no tiene semÃƒÂ¡ntica de anulaciÃƒÂ³n ni concede o deniega acceso (`src/Enrollment.Domain/Incident.cs:6-9`).

## Errores y casos límite
- Fallo al cargar `/incidents` o la vista previa del inicio Ã¢â€ â€™ registra el error y muestra un mensaje para refrescar la pÃƒÂ¡gina (`src/Enrollment.Web/Components/Pages/Incidents.razor:130-135`; `src/Enrollment.Web/Components/Pages/Home.razor:118-122`).
- Fallo al guardar una nota Ã¢â€ â€™ registra el error y muestra Ã¢â‚¬Å“Could not record the incident. Please try again.Ã¢â‚¬Â (`src/Enrollment.Web/Components/Pages/Incidents.razor:163-167`).
- Fila persistida con ID, tipo o ID de miembro invÃƒÂ¡lidos Ã¢â€ â€™ la lectura lanza `InvalidOperationException` en vez de devolver un incidente corrupto (`src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:192-239`).
- LÃƒÂ­mite no positivo Ã¢â€ â€™ devuelve lista vacÃƒÂ­a sin consultar la base (`src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:132-139`).

## Dónde actuar
| Si hay que cambiar... | Empezar en |
|---|---|
| ValidaciÃƒÂ³n/entrada del reporte manual | `src/Enrollment.Web/Components/Pages/Incidents.razor` Ã¢â€ â€™ `SubmitAsync` (`src/Enrollment.Web/Components/Pages/Incidents.razor:142-167`) |
| SelecciÃƒÂ³n, filtros o consulta de listas | `src/Enrollment.Web/Components/Pages/Home.razor` Ã¢â€ â€™ `FilteredIncidents` (`src/Enrollment.Web/Components/Pages/Home.razor:95-108`) y `src/Enrollment.Infrastructure/SqliteIncidentRepository.cs` Ã¢â€ â€™ `ListRecentAsync` (`src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:132-164`) |
| Regla de normalizaciÃƒÂ³n del incidente | `src/Enrollment.Domain/Incident.cs` Ã¢â€ â€™ `Create` / `NormalizeTimestamp` / `NormalizeOptional` (`src/Enrollment.Domain/Incident.cs:48-63,85-91`) |
| Salida y persistencia | `src/Enrollment.Infrastructure/SqliteIncidentRepository.cs` Ã¢â€ â€™ `SaveAsync` / `ReadIncident` (`src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:104-127,192-239`) |
| ProgramaciÃƒÂ³n | No aplica: el reporte de este proceso se inicia manualmente desde la pÃƒÂ¡gina (`src/Enrollment.Web/Components/Pages/Incidents.razor:16-39,142-156`). |
| ConfiguraciÃƒÂ³n | `src/Enrollment.Infrastructure/SqliteIncidentRepository.cs` Ã¢â€ â€™ `SqliteIncidentRepository` (lee la cadena y el nombre de tabla de `VectorStoreOptions`) (`src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:28-37`). |

## Tests existentes

- `IncidentRepositoryTests`: prueba round-trip de campos, bÃƒÂºsqueda por ID, orden/lÃƒÂ­mite, opcionales nulos, reemplazo del mismo ID y normalizaciÃƒÂ³n de fecha/notas (`tests/Enrollment.UnitTests/IncidentRepositoryTests.cs:15,35-165`).

## Cobertura / Gaps

- No se encontraron pruebas de componente para el envÃƒÂ­o manual, estados de error ni los filtros del inicio; las pruebas localizadas cubren el dominio y repositorio (`tests/Enrollment.UnitTests/IncidentRepositoryTests.cs:15-165`).
- Los lÃƒÂ­mites de 100 elementos en `/incidents` y 20 en inicio estÃƒÂ¡n codificados como constantes; su justificaciÃƒÂ³n no se desprende del cÃƒÂ³digo (`src/Enrollment.Web/Components/Pages/Incidents.razor:109`; `src/Enrollment.Web/Components/Pages/Home.razor:95`). Ver pregunta abierta en [`../preguntas-abiertas.md`](../preguntas-abiertas.md).

