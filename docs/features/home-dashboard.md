# F5 - Panel de inicio

Ticket: sin ticket (el catÃƒÂ¡logo no registra tickets para F5).

## Propósito
Presenta el punto de entrada de la aplicaciÃƒÂ³n, enlaces de acceso a miembros y roles, y una vista previa filtrable de incidentes recientes. La vista previa utiliza la capacidad de consulta de incidentes del proceso [F3](incident-log.md) (`src/Enrollment.Web/Components/Pages/Home.razor:110-116`).

## Dónde actuar
- HTTP / pÃƒÂ¡gina Blazor interactiva `/`; carga los incidentes al inicializarse (`src/Enrollment.Web/Components/Pages/Home.razor:1-2,110-116`).

## Disparador / Punto de entrada
## Entradas (payload/config)

- Filtro de tipo: selecciÃƒÂ³n local de Ã¢â‚¬Å“All kindsÃ¢â‚¬Â o de un valor `IncidentKind` (`src/Enrollment.Web/Components/Pages/Home.razor:35-44,100,105`).
- Filtro de nota: texto enlazado al evento `oninput`; no se envÃƒÂ­a como comando ni se persiste (`src/Enrollment.Web/Components/Pages/Home.razor:46-49,101,106-107`).

## Pasos

1. Al abrir `/`, el componente inicia en modo `InteractiveServer` y muestra el contenido del panel de inicio (`src/Enrollment.Web/Components/Pages/Home.razor:1-2,11-32`).
2. Presenta accesos a `/members` y `/roles`, ademÃƒÂ¡s de un enlace a `/incidents` para abrir la lista completa (`src/Enrollment.Web/Components/Pages/Home.razor:18-27,29-33`).
3. Durante la inicializaciÃƒÂ³n crea un ÃƒÂ¡mbito de servicios, resuelve `IIncidentRepository` y solicita los 20 incidentes recientes; el ÃƒÂ¡mbito se libera al salir del bloque `using` (`src/Enrollment.Web/Components/Pages/Home.razor:94-95,110-116`). Esta lectura conecta el panel con [F3](incident-log.md) (`src/Enrollment.Web/Components/Pages/Home.razor:115-116`).
4. Filtra en memoria por coincidencia exacta del tipo seleccionado y por subcadena de nota sin distinguir mayÃƒÂºsculas; un filtro vacÃƒÂ­o no restringe los resultados (`src/Enrollment.Web/Components/Pages/Home.razor:103-108`).
5. Muestra la tabla de resultados o el estado de carga, error o ausencia de coincidencias (`src/Enrollment.Web/Components/Pages/Home.razor:52-90`).

## Puntos de salida

| Salida | Detalle |
|---|---|
| Repositorio de incidentes | Lee hasta 20 registros recientes mediante `IIncidentRepository.ListRecentAsync`; el panel no escribe en el repositorio (`src/Enrollment.Web/Components/Pages/Home.razor:94-95,114-116`). |
| Interfaz `/` | Muestra fecha/hora UTC, tipo y nota (o `Ã¢â‚¬â€` si no hay nota), ademÃƒÂ¡s de accesos a `/members`, `/roles` y `/incidents` (`src/Enrollment.Web/Components/Pages/Home.razor:18-33,69-89`). |

## Reglas de negocio

- La vista previa limita su consulta a 20 incidentes (`src/Enrollment.Web/Components/Pages/Home.razor:94-95,114-116`). La razÃƒÂ³n del lÃƒÂ­mite no estÃƒÂ¡ documentada en cÃƒÂ³digo; vÃƒÂ©ase la pregunta abierta ya registrada para [F3](../preguntas-abiertas.md).
- El filtro de tipo compara la representaciÃƒÂ³n textual del tipo exactamente; el filtro de nota ignora mayÃƒÂºsculas y minÃƒÂºsculas (`src/Enrollment.Web/Components/Pages/Home.razor:103-108`).

## Errores y casos límite
- Mientras se cargan los incidentes muestra un estado de carga; al terminar, muestra resultados o el estado vacÃƒÂ­o si ningÃƒÂºn registro coincide con los filtros (`src/Enrollment.Web/Components/Pages/Home.razor:52-67,123-126`).
- Si falla la consulta, registra el error y muestra que la lista no pudo cargarse y que se refresque la pÃƒÂ¡gina (`src/Enrollment.Web/Components/Pages/Home.razor:118-125`).
- Una nota nula se filtra como no coincidente cuando hay texto de bÃƒÂºsqueda y se presenta como `Ã¢â‚¬â€` en la tabla (`src/Enrollment.Web/Components/Pages/Home.razor:106-107,79-85`).

## Dónde actuar
| Si hay que cambiar... | Empezar en |
|---|---|
| ValidaciÃƒÂ³n/entrada | No aplica: el panel no envÃƒÂ­a formularios ni comandos; sus filtros son locales (`src/Enrollment.Web/Components/Pages/Home.razor:35-49,94-108`). |
| SelecciÃƒÂ³n, filtros o consulta | `src/Enrollment.Web/Components/Pages/Home.razor` Ã¢â€ â€™ `FilteredIncidents` / `OnInitializedAsync` (`src/Enrollment.Web/Components/Pages/Home.razor:103-116`). |
| Regla de selecciÃƒÂ³n de registros | `src/Enrollment.Web/Components/Pages/Home.razor` Ã¢â€ â€™ `FilteredIncidents` (`src/Enrollment.Web/Components/Pages/Home.razor:103-108`). |
| Salida o persistencia | `src/Enrollment.Web/Components/Pages/Home.razor` Ã¢â€ â€™ markup de la tabla / `OnInitializedAsync` (`src/Enrollment.Web/Components/Pages/Home.razor:69-89,110-126`); persistencia: No aplica, el panel solo consulta (`src/Enrollment.Web/Components/Pages/Home.razor:114-116`). |
| ProgramaciÃƒÂ³n | No aplica: la lectura ocurre al inicializar la pÃƒÂ¡gina, sin trabajo periÃƒÂ³dico (`src/Enrollment.Web/Components/Pages/Home.razor:110-116`). |
| ConfiguraciÃƒÂ³n | No aplica: el panel no lee opciones propias (`src/Enrollment.Web/Components/Pages/Home.razor:94-127`). |

## Tests existentes

- No se localizaron pruebas de componente para `Home`; las pruebas de persistencia del repositorio de incidentes se documentan en [F3](incident-log.md) (`tests/Enrollment.UnitTests/IncidentRepositoryTests.cs:15-165`).

## Cobertura / Gaps

- No hay pruebas localizadas que fijen el comportamiento de filtros, estados de carga/error/vacÃƒÂ­o o los enlaces del panel.
- La justificaciÃƒÂ³n del lÃƒÂ­mite de 20 incidentes ya estÃƒÂ¡ planteada en la pregunta abierta asociada a [F3](../preguntas-abiertas.md) (`src/Enrollment.Web/Components/Pages/Home.razor:94-95`).

