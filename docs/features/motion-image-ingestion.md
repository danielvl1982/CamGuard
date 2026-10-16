# F4 - Ingesta de imágenes de supervisión

Ticket: sin ticket (es una capacidad asociada a F4; el catálogo no le asigna ticket propio)

## Propósito

Observa la carpeta configurada de supervisión, espera a que cada archivo quede desbloqueado y estable, y entrega cada candidato al servicio alojado para ejecutar el workflow. Es una capacidad de [F4 - Supervisión de carpeta](folder-monitoring.md), separada de la carpeta de enrolamiento (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:57-76`).

## Disparador / Punto de entrada

- Creación de archivo detectada por `FileSystemWatcher` en la carpeta configurada (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:57-75,89-100`)

## Entradas (payload/config)

- Ruta configurada y límites de canal/reintentos/retardo de estabilidad (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:35-54,59-67,114-139`).
- Ruta de archivo detectada (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:89-99`).

## Pasos

1. Crea la carpeta configurada y activa el watcher para creación, cambio de escritura y tamaño (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:57-67`).
2. Encola rutas nuevas; omite directorios y registra advertencia si el canal acotado está lleno (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:89-100`).
3. Comprueba estabilidad abriendo el archivo con `FileShare.None`; ante bloqueo reintenta con retardo multiplicado por intento (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:109-139`).
4. Emite cada ruta estable al servicio alojado; este ejecuta el workflow y archiva éxito en `processed` o fallo en `quarantine` (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:26-31,34-68`).

## Puntos de salida

| Salida | Detalle |
|---|---|
| Servicio de supervisión | Emite la ruta estable como elemento de ingesta (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:71-76`) |
| Archivo de imágenes | El servicio mueve el archivo terminado a procesados o cuarentena (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:43-68`) |

## Reglas de negocio

- La ruta solo se emite si pasa la apertura exclusiva de estabilidad (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:71-76,109-121`).
- Una carpeta no se encola como archivo (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:89-95`).
- Cada archivo se procesa en un scope; se intenta workflow con reintentos antes del archivo final (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:34-47,79-104`).

## Errores y casos límite

- Archivo desaparecido durante validación: se descarta sin emitirlo (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:122-127`).
- Archivo que no se estabiliza: se omite tras los intentos configurados (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:128-139`).
- Canal lleno: el evento se descarta y queda advertencia (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:96-99`).
- Error de `FileSystemWatcher`: se registra como posible desbordamiento, sin recuperación visible en este código (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:102-107`).

## Dónde actuar

| Si hay que cambiar... | Empezar en |
|---|---|
| Selección de eventos/rutas | `src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs` → `OnCreated`, `WatchAsync` (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:57-76,89-100`) |
| Validación de estabilidad y reintentos | `src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs` → `IsStableAsync` (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:109-140`) |
| Archivo tras ejecución del workflow | `src/Monitoring.Infrastructure/MonitoringHostedService.cs` → `ProcessFileAsync` (`src/Monitoring.Infrastructure/MonitoringHostedService.cs:34-68`) |

## Tests existentes

- `FileSystemWatchIngestionSourceTests`: eventos, estabilidad y canal (`tests/Monitoring.UnitTests/FileSystemWatchIngestionSourceTests.cs`).
- `FileSystemImageArchiverTests`: movimiento a carpetas de salida (`tests/Monitoring.UnitTests/FileSystemImageArchiverTests.cs`).

## Cobertura / Gaps

- El desbordamiento del buffer del watcher solo se registra; recuperación de eventos perdidos no está evidenciada (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:102-107`).
