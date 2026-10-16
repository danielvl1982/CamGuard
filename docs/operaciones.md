# Operaciones

## 1. Requisitos

- SDK de .NET 10; `global.json` fija `10.0.100` y permite avanzar dentro de la banda `feature`, y todos los proyectos compilan para `net10.0` (`global.json:1-7`, `Directory.Build.props:3-9`). Restaurar por primera vez requiere acceso a las fuentes NuGet configuradas en la máquina; este repositorio no aporta un `NuGet.Config` propio. No hace falta un servidor de base de datos: los hosts usan SQLite en archivo (`src/Monitoring.Host/appsettings.json:17-20`, `src/Enrollment.Web/appsettings.json:9-12`).
- No se configura Docker como requisito. Para la ingesta F4, Monitoring necesita acceso de lectura/escritura al directorio `WatchFolder:Path` y permisos para crear sus subdirectorios `processed/` y `quarantine/` (`src/Monitoring.Host/appsettings.json:8-12`, `src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:57-67`, `src/Monitoring.Infrastructure/FileSystemImageArchiver.cs:27-43`).
- F1, F2, F3 y F5 se sirven desde Enrollment Web; F4 corre en Monitoring Host. Enrollment Console ofrece `spike`, `register` y `watch` para tareas de inscripción (`src/Enrollment.Web/Program.cs:5-14`, `src/Monitoring.Host/Program.cs:8-13`, `src/Enrollment.Console/Program.cs:27-35`).

## 2. Compilar

Desde la raíz del repositorio:

```powershell
dotnet build CamGuard.slnx
```

La solución incluye los proyectos `src/` y ambos proyectos de pruebas (`CamGuard.slnx:1-18`).

## 3. Ejecutar

Enrollment Web (F1, F2, F3, F5), usando el perfil local HTTP:

```powershell
dotnet run --project src/Enrollment.Web --launch-profile http
```

El perfil `http` publica `http://localhost:5203` y establece `ASPNETCORE_ENVIRONMENT=Development`; el perfil `https` agrega `https://localhost:7147` (`src/Enrollment.Web/Properties/launchSettings.json:4-20`).

Monitoring Host (F4):

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet run --project src/Monitoring.Host
```

En Development se usa el canal de notificación de logging y no se envían notificaciones reales (`src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:73-82`). En otros entornos, configure `Ntfy:Topic`, `EmailNotification:SmtpHost`, `EmailNotification:FromAddress` y `EmailNotification:ToAddress` antes de habilitar el envío real (`src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:83-103`). El host vigila `WatchFolder:Path` (predeterminado `watch`) y crea esa carpeta al iniciar el watcher (`src/Monitoring.Host/appsettings.json:8-12`, `src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:57-67`).

Enrollment Console (F1: inscripción manual, watcher o prueba del detector):

```powershell
dotnet run --project src/Enrollment.Console -- register "Nombre Apellido" "ruta\foto.jpg"
dotnet run --project src/Enrollment.Console -- watch
dotnet run --project src/Enrollment.Console -- spike "ruta\foto.jpg"
```

La CLI documenta esos modos y sus argumentos (`src/Enrollment.Console/Program.cs:1-5`, `src/Enrollment.Console/Program.cs:27-35`, `src/Enrollment.Console/Program.cs:123-140`). `EnrollmentWatch:Path` predeterminado apunta a `C:/Dev/CamGuard/photos/enroll`; configure la ruta para el entorno local si esa ubicación no existe (`src/Enrollment.Console/EnrollmentWatchOptions.cs:9-19`, `src/Enrollment.Console/appsettings.json:6-8`).

## 4. Probar

No se documentan requisitos de servicios externos para los proyectos de pruebas; ambos son proyectos xUnit y se ejecutan desde la solución o individualmente (`tests/Enrollment.UnitTests/Enrollment.UnitTests.csproj:7-15`, `tests/Monitoring.UnitTests/Monitoring.UnitTests.csproj:7-17`).

```powershell
dotnet test CamGuard.slnx
dotnet test tests/Enrollment.UnitTests/Enrollment.UnitTests.csproj
dotnet test tests/Monitoring.UnitTests/Monitoring.UnitTests.csproj
```

OpenSpec todavía declara `projects: []` y comandos de prueba/verificación vacíos, aunque la solución ya incluye los dos proyectos xUnit (`openspec/config.yaml:15-23`, `openspec/config.yaml:39-47`, `CamGuard.slnx:15-18`). Véase [O3](preguntas-abiertas.md).

## 5. Cobertura

Comando para recolectar cobertura con Coverlet Collector:

```powershell
dotnet test CamGuard.slnx --collect:"XPlat Code Coverage"
```

Los dos proyectos de prueba referencian `coverlet.collector` (`tests/Enrollment.UnitTests/Enrollment.UnitTests.csproj:7-14`, `tests/Monitoring.UnitTests/Monitoring.UnitTests.csproj:7-16`). No hay una medición válida registrada. El intento del 2026-10-16 no llegó a ejecutar los tests: la restauración falló con `NU1301` porque este entorno no pudo acceder a `https://api.nuget.org/v3/index.json`. Véase [O4](preguntas-abiertas.md).

## 6. Logs

- Enrollment Web y Monitoring Host usan los constructores estándar de ASP.NET Core y Generic Host; sus configuraciones fijan `Default=Information` y reducen logs del framework a `Warning` (`src/Enrollment.Web/Program.cs:5`, `src/Enrollment.Web/appsettings.json:1-7`, `src/Monitoring.Host/Program.cs:8-13`, `src/Monitoring.Host/appsettings.json:1-7`). Leer la salida estándar del proceso o del supervisor que lo ejecuta es **inferido del código**: no se configura un destino persistente de logs en el repositorio.
- Monitoring registra errores del watcher, eventos descartados por canal lleno, archivos que no se estabilizan y fallas/reintentos del workflow (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:96-106`, `src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:109-139`, `src/Monitoring.Infrastructure/MonitoringHostedService.cs:51-68`, `src/Monitoring.Infrastructure/MonitoringHostedService.cs:96-104`).
- Enrollment Console imprime resultados y errores de CLI directamente en consola (`src/Enrollment.Console/Program.cs:63-93`, `src/Enrollment.Console/Program.cs:123-140`). La persistencia/retención centralizada de logs no consta. El destino de los eventos `ILogger` tampoco se declara explícitamente; véase [O7](preguntas-abiertas.md).

## 7. Interruptores y parámetros

| Interruptor / parámetro | Efecto operativo | Evidencia |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT=Development` (Web) / `DOTNET_ENVIRONMENT=Development` (Monitoring Host) | Activa configuración de desarrollo; Monitoring sustituye ntfy/SMTP por notificación de logging. | `src/Enrollment.Web/Properties/launchSettings.json:8-20`; `src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:73-103` |
| `WatchFolder:Path` | Directorio de entrada de F4; crea `processed/` y `quarantine/` para archivar. | `src/Monitoring.Host/appsettings.json:8-12`; `src/Monitoring.Infrastructure/FileSystemImageArchiver.cs:27-43` |
| `WatchFolder:StabilityRetryDelayMilliseconds` / `MaxStabilityRetryAttempts` / `ChannelCapacity` | Controlan el intervalo base de estabilidad, cantidad de intentos y tamaño de cola. Defaults: 200 ms, 5 intentos y 256 elementos. | `src/Monitoring.Infrastructure/WatchFolderOptions.cs:9-24`; `src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:45-54`, `src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:109-139` |
| `EnrollmentWatch:Path` / `StabilityRetryDelayMilliseconds` / `MaxStabilityRetryAttempts` / `ChannelCapacity` | Configuran el modo `watch` de Console; defaults: ruta absoluta `C:/Dev/CamGuard/photos/enroll`, 200 ms, 5 intentos y 64 elementos. | `src/Enrollment.Console/EnrollmentWatchOptions.cs:9-19` |
| Reintentos del workflow F4 | Tres intentos máximos con esperas incrementales de 200 ms; son constantes de código, no opciones. | `src/Monitoring.Infrastructure/MonitoringHostedService.cs:24`, `src/Monitoring.Infrastructure/MonitoringHostedService.cs:79-104` |
| `AccessDecision:AllowedMaxDistance` / `DeniedMinDistance` | Límites para decidir acceso; la aplicación valida que el primer umbral sea estrictamente menor que el segundo. | `src/Monitoring.Host/appsettings.json:13-16`; `src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:40-44` |

El watcher usa eventos de `FileSystemWatcher` sin frecuencia de polling configurada (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:57-67`). Cuando la cola se llena, descarta el evento; los desbordamientos del watcher se registran, pero no se ve una recuperación automática en este adaptador (`src/Monitoring.Infrastructure/FileSystemWatchIngestionSource.cs:96-106`). Véase [O1](preguntas-abiertas.md).

## 8. Despliegue

No consta en el repositorio un pipeline de despliegue ni instrucciones de publicación; el directorio `.github/workflows` no contiene workflows. Véase [O5](preguntas-abiertas.md).

## 9. Riesgos conocidos

| Riesgo operativo | Seguimiento |
|---|---|
| El watcher puede perder notificaciones ante desbordamiento o descartar eventos cuando su canal está lleno. | [O1](preguntas-abiertas.md) |
| La ruta predeterminada de Enrollment Console es específica de una máquina; la frecuencia y los intentos del watcher/reintentos del workflow tampoco son todos configurables. | [O2](preguntas-abiertas.md) |
| La cobertura no tiene una medición válida disponible. | [O4](preguntas-abiertas.md) |
| No está definido en el repositorio cómo desplegar ni retener logs fuera del proceso. | [O5](preguntas-abiertas.md), [O6](preguntas-abiertas.md) |
