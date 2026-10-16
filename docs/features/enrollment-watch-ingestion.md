# F1 - Ingesta de carpeta de enrolamiento (capacidad asociada)

Ticket: sin ticket (capacidad asociada; el catálogo no asigna ticket propio; podría organizarse como F6)

## Propósito

Observa por separado `photos/enroll` para enrolar miembros desde archivos, solicitar un nombre por archivo estable y archivar el resultado. No procesa la carpeta de supervisión `photos/watch`; se documenta como capacidad distinta asociada a [F1 - Gestión de miembros](member-management.md) y conectada con [F4 - Supervisión de carpeta](folder-monitoring.md) solo como proceso de ingesta de archivos distinto. Podría organizarse como F6 en el catálogo (`src/Enrollment.Console/EnrollmentWatchServiceCollectionExtensions.cs:6-12`).

## Disparador / Punto de entrada

- Registro de `EnrollmentWatchIngestionSource` mediante `AddEnrollmentWatch` (`src/Enrollment.Console/EnrollmentWatchServiceCollectionExtensions.cs:13-25`)
- Creación de archivo en la carpeta de enrolamiento configurada (`src/Enrollment.Console/EnrollmentWatchIngestionSource.cs:43-61,125-136`)

## Entradas (payload/config)

- Opciones de la carpeta, capacidad del canal y parámetros de estabilidad (`src/Enrollment.Console/EnrollmentWatchIngestionSource.cs:25-40,43-53,143-169`).
- Archivos encontrados en carpeta superior y nombre provisto por prompt en el consumidor del flujo (`src/Enrollment.Console/EnrollmentWatchIngestionSource.cs:97-117`; `src/Enrollment.Application/EnrollmentService.cs:17-30`).

## Pasos

1. La extensión enlaza `EnrollmentWatchOptions` y registra watcher y archivador como singleton (`src/Enrollment.Console/EnrollmentWatchServiceCollectionExtensions.cs:15-24`).
2. El watcher crea la carpeta configurada, encola eventos de archivo y verifica estabilidad con apertura exclusiva (`src/Enrollment.Console/EnrollmentWatchIngestionSource.cs:43-67,125-169`).
3. Al reanudarse tras una pausa, vuelve a explorar solo archivos de primer nivel para recuperar llegadas durante el prompt (`src/Enrollment.Console/EnrollmentWatchIngestionSource.cs:80-117`).
4. Los archivos estables se entregan a la ingesta; los inestables se envían a cuarentena (`src/Enrollment.Console/EnrollmentWatchIngestionSource.cs:57-67,172-216`).
5. El servicio de aplicación valida el nombre, busca coincidencia normalizada y guarda el miembro (`src/Enrollment.Application/EnrollmentService.cs:24-53`).
6. El archivador mueve archivos terminados a `processed/` o `quarantine/` (`src/Enrollment.Console/EnrollmentWatchArchiver.cs:23-40`).

## Puntos de salida

| Salida | Detalle |
|---|---|
| Directorio de miembros | Miembro guardado por `EnrollmentService` (`src/Enrollment.Application/EnrollmentService.cs:39-53`) |
| Carpetas de archivo | Archivos completados pasan a `processed/` o `quarantine/` (`src/Enrollment.Console/EnrollmentWatchArchiver.cs:23-40`) |

## Reglas de negocio

- La composición declara una carpeta `photos/enroll` aislada de la ruta de Monitoring (`src/Enrollment.Console/EnrollmentWatchServiceCollectionExtensions.cs:6-12`).
- La reexploración mira solo la carpeta superior y reencola archivos que llegaron durante pausa (`src/Enrollment.Console/EnrollmentWatchIngestionSource.cs:97-117`).
- Nombre vacío o en blanco se rechaza antes de extraer o guardar (`src/Enrollment.Application/EnrollmentService.cs:24-27`).
- Archivo estable con éxito se archiva como procesado; las rutas de fallo se archivan en cuarentena (`src/Enrollment.Console/EnrollmentWatchArchiver.cs:23-40`).

## Errores y casos límite

- Canal acotado configurado con `DropOldest`: puede descartar el elemento más antiguo en saturación (`src/Enrollment.Console/EnrollmentWatchIngestionSource.cs:35-40`).
- Archivo inestable: se intenta cuarentena después de agotar comprobaciones (`src/Enrollment.Console/EnrollmentWatchIngestionSource.cs:143-169,172-216`).
- Error del watcher se registra como posible desbordamiento; no se ve recuperación automática de ese error (`src/Enrollment.Console/EnrollmentWatchIngestionSource.cs:138-141`).

## Dónde actuar

| Si hay que cambiar... | Empezar en |
|---|---|
| Configuración y registros de servicios | `src/Enrollment.Console/EnrollmentWatchServiceCollectionExtensions.cs` → `AddEnrollmentWatch` (`src/Enrollment.Console/EnrollmentWatchServiceCollectionExtensions.cs:13-25`) |
| Eventos, canal, estabilidad y pausa/reanudación | `src/Enrollment.Console/EnrollmentWatchIngestionSource.cs` → `WatchAsync`, `Pause`, `Resume`, `IsStableAsync` (`src/Enrollment.Console/EnrollmentWatchIngestionSource.cs:43-95,143-169`) |
| Archival de salida | `src/Enrollment.Console/EnrollmentWatchArchiver.cs` → `MoveToProcessedAsync`, `MoveToQuarantineAsync` (`src/Enrollment.Console/EnrollmentWatchArchiver.cs:23-40`) |
| Resolución de nombre y escritura de miembro | `src/Enrollment.Application/EnrollmentService.cs` → `EnrollAsync` (`src/Enrollment.Application/EnrollmentService.cs:17-53`) |

## Tests existentes

- `EnrollmentWatchIngestionTests`: ingestión de carpeta (`tests/Enrollment.UnitTests/EnrollmentWatchIngestionTests.cs`).
- `EnrollmentWatchArchiverTests`: archivo de salida (`tests/Enrollment.UnitTests/EnrollmentWatchArchiverTests.cs`).
- `EnrollmentServiceTests`: enrolamiento individual (`tests/Enrollment.UnitTests/EnrollmentServiceTests.cs`).

## Cobertura / Gaps

- El watcher usa `DropOldest`; en contraste con el comentario contractual del prompt, esa configuración sí permite expulsión de elementos bajo saturación (`src/Enrollment.Console/EnrollmentWatchIngestionSource.cs:35-40`).
