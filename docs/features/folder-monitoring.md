# F4 - Monitoreo de imágenes en carpeta

Ticket: sin ticket (el catálogo indica que no hay ticket asociado)

## Propósito

El proceso alojado observa una carpeta de entrada, valida que cada archivo creado esté estable y ejecuta el flujo de detección, verificación de identidad, decisión de acceso y notificación. Al terminar, archiva el archivo como procesado o lo mueve a cuarentena si el flujo falla. (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:26-30,34-68,79-104`)

## Disparador / Punto de entrada

- Job: el host de Monitoring compone Enrollment y Monitoring e inicia el proceso alojado hasta su cierre (`src/Monitoring.Host/Program.cs:8-13`).
- Job: `MonitoringHostedService.ExecuteAsync` consume rutas de `IIngestionSource.WatchAsync` durante la vida del proceso (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:26-30`).

## Entradas (payload/config)

- Sección `WatchFolder`: `Path` (por defecto `watch`), demora y máximo de intentos para comprobar estabilidad, y capacidad del canal acotado (`src/Monitoring.Infrastructure/WatchFolderOptions.cs:9-24`).
- Ruta completa de un archivo notificado por `FileSystemWatcher.Created`; las carpetas se descartan y no se aplica un filtro de extensión en el manejador del evento (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:89-99`).
- El flujo abre la ruta como archivo de imagen y crea `MonitoringWorkflowContext` con esa ruta y su stream (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:79-91`).

## Pasos

1. El host registra Enrollment y Monitoring y ejecuta el proceso alojado (`src/Monitoring.Host/Program.cs:8-13`).
2. `FileSystemWatchIngestionSource` crea la carpeta configurada, activa `FileSystemWatcher` para nombre, última escritura y tamaño, y convierte eventos `Created` en rutas dentro del canal acotado (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:57-75,89-99`).
3. Antes de emitir una ruta, intenta abrir el archivo con acceso exclusivo; reintenta errores de E/S con demora creciente. Si el archivo desapareció o no se estabiliza tras el máximo configurado, no lo emite (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:109-139`).
4. Por cada ruta emitida, el servicio crea un scope de DI y ejecuta el workflow con una lectura nueva del archivo, hasta tres intentos con pausas crecientes ante excepciones transitorias (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:24,34-44,72-104`).
5. El workflow detecta personas y deriva directamente a notificación si no detecta ninguna; en el camino normal verifica identidad, decide acceso y notifica (`src/Monitoring.Application/MonitoringWorkflowHost.cs:32-38`). La selección de canales de notificación depende de la configuración de roles y canales predeterminados de [F2](role-notification-configuration.md) (`src/Monitoring.Infrastructure/CompositeNotificationService.cs:12-17,56-79`; `src/Enrollment.PublicApi/NotificationRoutingReader.cs:15-32`).
6. Si el workflow termina, mueve el archivo a `processed/`; si no completa o falla tras los reintentos, registra el fallo y lo mueve a `quarantine/` (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:45-62`).

## Puntos de salida

| Salida | Detalle |
|---|---|
| Archivos observados | Lee cada archivo emitido y ejecuta el workflow de monitoreo (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:86-91`). |
| Carpeta `processed/` | Mueve allí el archivo cuando el workflow completa (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:45-48`; `src/Monitoring.Infrastructure/FileSystemImageArchiver.cs:25-26,31-42`). |
| Carpeta `quarantine/` | Mueve allí el archivo si el workflow no completa o lanza una excepción no cancelatoria (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:51-68`; `src/Monitoring.Infrastructure/FileSystemImageArchiver.cs:28-29,31-42`). |
| Canales de notificación | Emite alertas según canales de rol o predeterminados; ante fallo de resolución de canales predeterminados intenta todos los canales configurados (`src/Monitoring.Infrastructure/CompositeNotificationService.cs:36-52,56-79`). |

## Reglas de negocio

- El monitor solo atiende eventos de creación; ignora rutas que correspondan a directorios (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:61-67,89-94`).
- Los archivos que aún están bloqueados se reintentan antes de emitirse; una ruta inexistente o que no se estabiliza se omite (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:114-139`).
- Cada archivo se procesa en un scope independiente; la secuencia de intentos de workflow reabre el stream en cada intento (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:34-44,79-103`).
- Una alerta permitida con miembro reconocido usa los canales del rol; los demás resultados usan los canales predeterminados. Si la consulta de miembro falla, se usan los predeterminados; si falla esa consulta, se intenta cada canal disponible (`src/Monitoring.Infrastructure/CompositeNotificationService.cs:12-17,56-79`; `src/Enrollment.PublicApi/NotificationRoutingReader.cs:15-32`).
- El fallo de un canal no impide intentar los demás (`src/Monitoring.Infrastructure/CompositeNotificationService.cs:42-52`).

## Errores y casos límite

- El canal acotado lleno hace que se descarte el evento de creación y se registre una advertencia (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:45-54,96-99`).
- El evento `Error` del watcher se registra como posible desbordamiento del búfer interno; el código no muestra una recuperación automática de eventos perdidos (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:102-107`).
- Un archivo que no se estabiliza tras los intentos configurados se omite con advertencia (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:128-139`).
- Si los intentos de workflow no completan, o una excepción no cancelatoria persiste, el archivo se envía a cuarentena; un error al moverlo también se registra y no detiene el bucle (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:45-68`).
- La cancelación no se convierte en fallo de archivo: los bloques de captura excluyen `OperationCanceledException` (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:53-58,64-68`).

## Dónde actuar

| Si hay que cambiar... | Empezar en |
|---|---|
| Validación de estabilidad de la entrada y tratamiento de archivos bloqueados | `src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs` → `IsStableAsync` (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:109-139`) |
| Selección de rutas/eventos recibidos | `src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs` → `OnCreated`, `WatchAsync` (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:57-75,89-99`) |
| Decisión del workflow por detección de persona | `src/Monitoring.Application/MonitoringWorkflowHost.cs` → construcción de `Workflow` (`src/Monitoring.Application/MonitoringWorkflowHost.cs:32-38`) |
| Resolución de canales por rol o valores predeterminados | `src/Monitoring.Infrastructure/CompositeNotificationService.cs` → `ResolveTargetsAsync`; `src/Enrollment.PublicApi/NotificationRoutingReader.cs` → `GetChannelsForMemberAsync` (`src/Monitoring.Infrastructure/CompositeNotificationService.cs:56-79`; `src/Enrollment.PublicApi/NotificationRoutingReader.cs:15-32`) |
| Salida/persistencia y archivo procesado o en cuarentena | `src/Monitoring.Infrastructure/MonitoringHostedService.cs` → `ProcessFileAsync`; `src/Monitoring.Infrastructure/FileSystemImageArchiver.cs` → `MoveToProcessedAsync`, `MoveToQuarantineAsync` (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:34-68`; `src/Monitoring.Infrastructure/FileSystemImageArchiver.cs:25-42`) |
| Programación del proceso y configuración de carpeta/canal | `src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs` → `AddMonitoring`; `src/Monitoring.Infrastructure/WatchFolderOptions.cs` → `WatchFolderOptions` (`src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:30,46-47,105-132`; `src/Monitoring.Infrastructure/WatchFolderOptions.cs:9-24`) |

## Tests existentes

- `FileSystemWatchIngestionSourceTests`: creación, estabilidad de archivo bloqueado, emisión única y descarte observable al llenarse el canal (`tests/Monitoring.UnitTests/FileSystemWatchIngestionSourceTests.cs:31-145`).
- `FileSystemImageArchiverTests`: movimiento a `processed/` y `quarantine/`, y creación de la subcarpeta de procesados (`tests/Monitoring.UnitTests/FileSystemImageArchiverTests.cs:16-60`).
- `MonitoringWorkflowIntegrationTests.Dropped_file_is_archived_and_notified_exactly_once`: integración de carpeta, workflow, archivo y notificación (`tests/Monitoring.UnitTests/MonitoringWorkflowIntegrationTests.cs:34-118`).
- `MonitoringHostedServiceTests.Dropped_file_is_processed_archived_and_notified_by_the_hosted_service`: recorrido del servicio alojado hasta archivo y notificación (`tests/Monitoring.UnitTests/MonitoringHostedServiceTests.cs:28-114`).
- `MonitoringHostedServiceTests.Transient_workflow_failures_are_retried_and_the_file_is_still_processed`: reintentos ante fallos transitorios y archivo final (`tests/Monitoring.UnitTests/MonitoringHostedServiceTests.cs:117-222`).

## Cobertura / Gaps

- Hay fragmento abierto sobre recuperación ante desbordamiento del búfer de `FileSystemWatcher`: el código registra el error, pero no evidencia reexploración de carpeta ni otra recuperación (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:102-107`; `docs/.work/questions/folder-monitoring.md`).
- No se encontró una prueba del camino de fallo que termina en cuarentena del servicio alojado (`tests/Monitoring.UnitTests/MonitoringHostedServiceTests.cs:28-222`).
