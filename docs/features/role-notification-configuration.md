# F2 - Configuración de roles y canales de notificación

Ticket: sin ticket (el catálogo no registra un ticket)

## Propósito

La página `/roles` permite consultar, crear, editar y eliminar roles, y seleccionar los canales Push y Email asociados a cada rol. También configura los canales predeterminados para alertas de acceso denegado o desconocido. (`src/Enrollment.Web/Components/Pages/Roles.razor:14-18,193-197`)

## Disparador / Punto de entrada

- HTTP: página Blazor interactiva `/roles`; la inicialización carga roles y canales predeterminados. (`src/Enrollment.Web/Components/Pages/Roles.razor:1-2,260-274`)

## Entradas (payload/config)

- Nombre del rol, escrito en texto y limitado en la UI a 50 caracteres; el dominio recorta espacios y rechaza nombres vacíos o de más de 50 caracteres. (`src/Enrollment.Web/Components/Pages/Roles.razor:36-46,106-115`; `src/Enrollment.Domain/Role.cs:11,83-94`)
- Selección de canales Push y Email para cada rol; se puede dejar ambos sin marcar, lo que representa ningún aviso. (`src/Enrollment.Web/Components/Pages/Roles.razor:47-58,121-130`; `src/Enrollment.Domain/NotificationChannel.cs:4-8`)
- Selección de canales Push y Email predeterminados para accesos denegados o desconocidos; la selección vacía significa que no se envían notificaciones. (`src/Enrollment.Web/Components/Pages/Roles.razor:193-215,519-540`)

## Pasos

1. Al abrir la página se consultan todos los roles y los canales predeterminados; la UI refleja Push y Email como casillas independientes. (`src/Enrollment.Web/Components/Pages/Roles.razor:260-274`)
2. Para crear un rol, la página envía nombre y canales al handler; si el resultado falla conserva lo escrito y muestra el mensaje correspondiente, y si tiene éxito limpia el formulario y vuelve a cargar la lista. (`src/Enrollment.Web/Components/Pages/Roles.razor:287-301,314-364`)
3. Para editar, el formulario parte del nombre y canales actuales; guardar reemplaza ambos en un comando, y ante éxito se recarga la lista. (`src/Enrollment.Web/Components/Pages/Roles.razor:366-380,393-445`)
4. Para eliminar, se pide confirmación; el handler rechaza la eliminación si algún miembro (incluidos los anulados) conserva el rol, y la página comunica el error o recarga tras éxito. (`src/Enrollment.Web/Components/Pages/Roles.razor:151-165,447-517`; `src/Enrollment.Application/DeleteRoleCommand.cs:6-23`)
5. Guardar los canales predeterminados reemplaza la selección persistida; la página informa si queda vacía, indicando que accesos denegados y desconocidos no generarán notificaciones. (`src/Enrollment.Web/Components/Pages/Roles.razor:193-215,519-552`; `src/Enrollment.Application/SetDefaultChannelsCommand.cs:6-26`)

## Puntos de salida

| Salida | Detalle |
|---|---|
| Repositorio de roles | Lectura de roles; escritura de alta, actualización y baja condicional. (`src/Enrollment.Web/Components/Pages/Roles.razor:266-268,326-329,405-409,480-483`; `src/Enrollment.Application/ListRolesQuery.cs:8-15`; `src/Enrollment.Application/DeleteRoleCommand.cs:18-23`) |
| Repositorio de configuración de notificaciones | Lectura de canales predeterminados y reemplazo de la selección guardada. (`src/Enrollment.Web/Components/Pages/Roles.razor:270-274,531-534`; `src/Enrollment.Application/GetDefaultChannelsQuery.cs:8-15`; `src/Enrollment.Application/SetDefaultChannelsCommand.cs:10-26`) |
| UI de `/roles` | Presenta lista, formularios, confirmación de baja y mensajes de estado/error según el resultado de cada acción. (`src/Enrollment.Web/Components/Pages/Roles.razor:20-32,73-87,193-228,330-364,409-445,484-517,535-546`) |

## Reglas de negocio

- El nombre se recorta; no puede estar vacío ni superar 50 caracteres. (`src/Enrollment.Domain/Role.cs:11,83-94`)
- Los canales del rol se reemplazan como conjunto; duplicados se colapsan, canales vacíos son válidos y valores de enum no definidos se rechazan. (`src/Enrollment.Domain/Role.cs:37-52,64-79`)
- No se permiten nombres duplicados entre roles; la comparación se aplica en la búsqueda del repositorio y una colisión concurrente se traduce a error tipado. (`src/Enrollment.Application/CreateRoleCommand.cs:10-48`; `src/Enrollment.Application/UpdateRoleCommand.cs:27-50`)
- Una baja se rechaza si el rol está asignado a algún miembro, incluso si está anulado. (`src/Enrollment.Application/DeleteRoleCommand.cs:6-23`)
- Los canales predeterminados se reemplazan de forma completa; el conjunto vacío significa no notificar y solo se aceptan canales definidos. (`src/Enrollment.Domain/INotificationSettingsRepository.cs:6-13`; `src/Enrollment.Application/SetDefaultChannelsCommand.cs:6-26`)
- Cuando nunca se guardaron preferencias predeterminadas, la lectura devuelve todos los canales; guardar un conjunto vacío es distinto y expresa explícitamente que no se notifique. (`src/Enrollment.Domain/INotificationSettingsRepository.cs:6-10`; `tests/Enrollment.UnitTests/SqliteNotificationSettingsRepositoryTests.cs:23-60`)

## Errores y casos límite

- Nombre vacío o demasiado largo → creación/edición rechazada y mensaje de validación; la UI conserva el texto ingresado al fallar la creación. (`src/Enrollment.Web/Components/Pages/Roles.razor:330-334,356-364,409-445`; `src/Enrollment.Domain/Role.cs:83-94`)
- Nombre ya utilizado (incluida colisión durante guardado) → operación rechazada como nombre duplicado. (`src/Enrollment.Application/CreateRoleCommand.cs:26-48`; `src/Enrollment.Application/UpdateRoleCommand.cs:32-50`)
- Baja de rol asignado → no se borra y se informa que debe quitarse de los miembros primero. (`src/Enrollment.Application/DeleteRoleCommand.cs:9-23`; `src/Enrollment.Web/Components/Pages/Roles.razor:512-517`)
- Fallo al cargar los roles o preferencias → se registra el error y la página muestra un mensaje de carga; preferencias permanecen deshabilitadas hasta completar la carga. (`src/Enrollment.Web/Components/Pages/Roles.razor:202-213,262-284`)
- Selección predeterminada vacía → guardado válido y mensaje explícito de que accesos denegados/desconocidos no notificarán. (`src/Enrollment.Web/Components/Pages/Roles.razor:535-540`)

## Dónde actuar

| Si hay que cambiar... | Empezar en |
|---|---|
| Validación de nombre o canales de entrada | `src/Enrollment.Domain/Role.cs` → `ValidateName`, `Create`, `SetChannels` (`src/Enrollment.Domain/Role.cs:41-52,64-94`) |
| Consulta y orden de roles | `src/Enrollment.Application/ListRolesQuery.cs` → `ListRolesQueryHandler` (`src/Enrollment.Application/ListRolesQuery.cs:8-15`) |
| Reglas de alta, edición o baja | `src/Enrollment.Application/CreateRoleCommand.cs` → `CreateRoleCommandHandler`; `src/Enrollment.Application/UpdateRoleCommand.cs` → `UpdateRoleCommandHandler`; `src/Enrollment.Application/DeleteRoleCommand.cs` → `DeleteRoleCommandHandler` (`src/Enrollment.Application/CreateRoleCommand.cs:10-48`; `src/Enrollment.Application/UpdateRoleCommand.cs:13-53`; `src/Enrollment.Application/DeleteRoleCommand.cs:10-23`) |
| Persistencia y salida de roles | `src/Enrollment.Domain/IRoleRepository.cs` → `IRoleRepository`; `src/Enrollment.Infrastructure/SqliteRoleRepository.cs` → `SqliteRoleRepository` (`src/Enrollment.Web/Components/Pages/Roles.razor:326-329,405-409,480-483`) |
| Lectura/guardado de configuración predeterminada | `src/Enrollment.Application/SetDefaultChannelsCommand.cs` → `SetDefaultChannelsCommandHandler`; `src/Enrollment.Infrastructure/SqliteNotificationSettingsRepository.cs` → `GetDefaultChannelsAsync`, `SetDefaultChannelsAsync` (`src/Enrollment.Application/SetDefaultChannelsCommand.cs:10-26`; `src/Enrollment.Infrastructure/SqliteNotificationSettingsRepository.cs:68-120`) |
| Presentación e interacción de configuración | `src/Enrollment.Web/Components/Pages/Roles.razor` → `LoadAsync`, `CreateAsync`, `SaveEditAsync`, `ConfirmDeleteAsync`, `SaveDefaultsAsync` (`src/Enrollment.Web/Components/Pages/Roles.razor:262-285,314-364,393-445,469-517,519-553`) |

## Tests existentes

- `CreateRoleCommandHandlerTests`: alta con nombre recortado/canales; rechazo de nombre vacío y nombre duplicado, incluida colisión al guardar. (`tests/Enrollment.UnitTests/CreateRoleCommandHandlerTests.cs:11-68`)
- `UpdateRoleCommandHandlerTests`: actualización, colisión, conservar nombre propio con distinta capitalización, nombre vacío y rol inexistente. (`tests/Enrollment.UnitTests/UpdateRoleCommandHandlerTests.cs:22-106`)
- `DeleteRoleCommandHandlerTests`: mapeo de baja rechazada y éxito. (`tests/Enrollment.UnitTests/DeleteRoleCommandHandlerTests.cs:11-34`)
- `DefaultChannelsHandlerTests`: lectura, guardado válido y rechazo de canal no definido. (`tests/Enrollment.UnitTests/DefaultChannelsHandlerTests.cs:11-48`)
- `SqliteNotificationSettingsRepositoryTests`: valores iniciales predeterminados, persistencia/lectura, reemplazo, conjunto vacío y rechazo sin guardar de canal no definido. (`tests/Enrollment.UnitTests/SqliteNotificationSettingsRepositoryTests.cs:23-78`)
- `RoleTests`: nombre, duplicados de canal, conjunto vacío y rechazo de valores no definidos. (`tests/Enrollment.UnitTests/RoleTests.cs:8-82`)

## Cobertura / Gaps

- No se identificaron pruebas específicas de interacción de la página Blazor (formulario, confirmación y mensajes); los tests citados cubren dominio, handlers y persistencia. (`tests/Enrollment.UnitTests/CreateRoleCommandHandlerTests.cs:9`; `tests/Enrollment.UnitTests/UpdateRoleCommandHandlerTests.cs:9`; `tests/Enrollment.UnitTests/DefaultChannelsHandlerTests.cs:9`; `src/Enrollment.Web/Components/Pages/Roles.razor:1-2`)
- Ninguna decisión pendiente conocida.
