# F1 - Gestión de miembros

Ticket: sin ticket (el catálogo indica que no hay ticket asociado)

## Propósito

Permite consultar y administrar miembros enrolados: alta con imágenes de referencia, consulta de detalle, cambio de nombre y rol, anulación lógica, gestión de imágenes y servicio HTTP de imágenes. El catálogo no asocia un ticket con este proceso.

## Disparador / Punto de entrada

- Interfaz Blazor de listado y enrolamiento (`/members`) (`src/Enrollment.Web/Components/Pages/Members.razor:1`)
- Interfaz Blazor de detalle (`/members/{id}`) (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:1`)
- `GET /api/members/{id:guid}/images/{imageId:guid}` (`src/Enrollment.Web/MemberImageEndpoints.cs:22`)

## Entradas (payload/config)

- Nombre y archivos de imagen seleccionados para enrolar; el componente crea `RegisterMemberCommand(name, streams)` (`src/Enrollment.Web/Components/Pages/Members.razor:372-377`)
- Identificador del miembro y nombre ingresado para renombrar (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:413-417`)
- Rol seleccionado como GUID o vacío para quitarlo; valida GUID y `RoleId` antes de asignarlo (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:462-468`)
- Imágenes seleccionadas para el miembro en detalle (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:694-700`)
- GUID del miembro e imagen de la ruta HTTP (`src/Enrollment.Web/MemberImageEndpoints.cs:22`)

## Pasos

1. La página de miembros consulta miembros no anulados y carga los roles en una sola consulta para mostrar sus nombres (`src/Enrollment.Web/Components/Pages/Members.razor:223-232`).
2. Para enrolar, resuelve un handler en un scope nuevo, construye el comando con nombre e imágenes y procesa el resultado (`src/Enrollment.Web/Components/Pages/Members.razor:372-379`).
3. La acción de anulación establece la marca lógica y recarga el listado (`src/Enrollment.Web/Components/Pages/Members.razor:480-495`).
4. El detalle carga las imágenes del miembro y la lista de roles (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:349-353`).
5. Para cambiar el nombre, envía `RenameMemberCommand` y vuelve a leer el miembro para mostrar el nombre persistido (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:413-429`).
6. Para asignar o quitar un rol, valida el valor seleccionado y realiza la operación de asignación (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:462-468`).
7. Para agregar imágenes, crea un scope nuevo, ejecuta `AddMemberImagesCommand` y procesa su resultado (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:694-702`).
8. Para eliminar una imagen, vuelve a listar las imágenes y comprueba que la imagen pertenezca al miembro mostrado antes de borrarla (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:528-543`).
9. El endpoint obtiene el registro de miembro e imagen y responde con el flujo de imagen o 404 (`src/Enrollment.Web/MemberImageEndpoints.cs:37-68`).

## Puntos de salida

| Salida | Detalle |
|---|---|
| Directorio de miembros | Alta de miembro, cambios de nombre/rol y anulación lógica (`src/Enrollment.Web/Components/Pages/Members.razor:375-377`; `src/Enrollment.Web/Components/Pages/MemberDetail.razor:417,477`) |
| Repositorio de imágenes | Agrega y elimina imágenes; las lista para el detalle (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:349,555,700`) |
| Respuesta HTTP | Devuelve la imagen solicitada o 404 (`src/Enrollment.Web/MemberImageEndpoints.cs:37-68`) |

## Reglas de negocio

- El listado presenta miembros no anulados; la anulación usa `SetAnnulledAsync(id, true, ...)` (`src/Enrollment.Web/Components/Pages/Members.razor:227,489-495`).
- Las anulaciones son lógicas: el repositorio conserva el registro y actualiza su estado, manteniendo la identidad, nombre, embedding y rol (`src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs:149-160`).
- La asignación de rol puede quitarse con `null`; si el rol ya no existe, la operación no lo asigna (`src/Enrollment.Domain/IMemberVectorRepository.cs:27-35`).
- El endpoint de imagen obtiene el miembro y el registro de imagen y sirve la imagen o responde 404 (`src/Enrollment.Web/MemberImageEndpoints.cs:37-68`).

## Errores y casos límite

- Un error al cargar el listado queda gestionado por la página (`src/Enrollment.Web/Components/Pages/Members.razor:223-234`).
- Un valor de rol que no sea GUID válido o no genere un `RoleId` aceptado muestra “Choose a valid role.” (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:462-468`).
- La asignación puede devolver que el rol no existe; la interfaz procesa el resultado de la operación (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:469-504`).
- Si se intenta anular un identificador inexistente, el repositorio lanza `KeyNotFoundException` (`src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs:140-156`).
- Un cambio de rol concurrente puede revertirse si coincide con la lectura-escritura completa de `RenameAsync` o `SetAnnulledAsync` (`src/Enrollment.Domain/IMemberVectorRepository.cs:32-33`).

## Dónde actuar

| Si hay que cambiar... | Empezar en |
|---|---|
| Validación y alta de miembro | `src/Enrollment.Web/Components/Pages/Members.razor` → `Members` (`src/Enrollment.Web/Components/Pages/Members.razor:372-377`) |
| Selección/consulta de miembros y roles | `src/Enrollment.Web/Components/Pages/Members.razor` → `LoadAsync` (`src/Enrollment.Web/Components/Pages/Members.razor:223-232`) |
| Reglas de renombrado y asignación de rol | `src/Enrollment.Web/Components/Pages/MemberDetail.razor` → `RenameAsync`, `SaveRoleAsync` (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:413-417,460-468`) |
| Persistencia del miembro y anulación | `src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs` → `SetAnnulledAsync` (`src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs:140-160`) |
| Salida/persistencia de imágenes del miembro | `src/Enrollment.Web/Components/Pages/MemberDetail.razor` → `ConfirmDeleteAsync`, `AddImagesAsync` (`src/Enrollment.Web/Components/Pages/MemberDetail.razor:528-543,694-702`) |
| Respuesta HTTP de imagen | `src/Enrollment.Web/MemberImageEndpoints.cs` → endpoint de imagen (`src/Enrollment.Web/MemberImageEndpoints.cs:22,37-68`) |

## Tests existentes

- `RegisterMemberCommandHandlerTests`: pruebas del handler de enrolamiento (archivo: `tests/Enrollment.UnitTests/RegisterMemberCommandHandlerTests.cs`).
- `RenameMemberCommandHandlerTests`: pruebas del handler de renombrado (archivo: `tests/Enrollment.UnitTests/RenameMemberCommandHandlerTests.cs`).
- `MemberAnnulTests`: pruebas relacionadas con la anulación lógica (archivo: `tests/Enrollment.UnitTests/MemberAnnulTests.cs`).
- `MemberRoleTests` y `AssignMemberRoleCommandHandlerTests`: pruebas de asignación de rol (archivos: `tests/Enrollment.UnitTests/MemberRoleTests.cs`, `tests/Enrollment.UnitTests/AssignMemberRoleCommandHandlerTests.cs`).
- `MemberImageTests`: pruebas relacionadas con imágenes de miembros (archivo: `tests/Enrollment.UnitTests/MemberImageTests.cs`).

## Cobertura / Gaps

- No se identificó en esta inspección una prueba específica del mapeo HTTP de imagen (`src/Enrollment.Web/MemberImageEndpoints.cs:22,37-68`).
- La interfaz del repositorio documenta una posible reversión de cambio de rol por escrituras de registro completo en operaciones concurrentes; no se encontró prueba de concurrencia que cubra este caso (`src/Enrollment.Domain/IMemberVectorRepository.cs:32-33`).
