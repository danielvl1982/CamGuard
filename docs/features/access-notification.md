# Capacidad auxiliar - Notificación de acceso

Ticket: sin ticket (capacidad asociada a F4/F2; no se registró ticket asociado)

## Propósito

Emite alertas para decisiones de acceso y las enruta usando la configuración de canales de [F2](role-notification-configuration.md), dentro de [F4](folder-monitoring.md). No crea un nuevo ID de catálogo.

## Disparador / Punto de entrada

- Etapa terminal `NotifyExecutor` del flujo (`src/Monitoring.Application/NotifyExecutor.cs:26-42`).

## Entradas (payload/config)

- Decisión final, imagen y datos de miembro/contexto para construir `AccessAlert` (`src/Monitoring.Application/NotifyExecutor.cs:65-80`).
- Implementación seleccionada según entorno y canales configurados (`src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:73-103`).

## Pasos

1. `NotifyExecutor` construye una alerta con el resultado y los datos del contexto (`src/Monitoring.Application/NotifyExecutor.cs:65-80`).
2. Invoca `INotificationService`; los errores de notificación se capturan para no interrumpir el flujo (`src/Monitoring.Application/NotifyExecutor.cs:82-90`).
3. En Development se registra mediante el adaptador de logging; en otros entornos se configura el servicio compuesto con canales push y correo (`src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:78-103`).
4. El enrutamiento de canales se configura en [F2](role-notification-configuration.md).

## Puntos de salida

| Salida | Detalle |
|---|---|
| Canales de notificación | Servicio compuesto que registra canales push/correo fuera de Development (`src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:96-103`). |

## Reglas de negocio

- La notificación es terminal y se intenta para cada resultado final (`src/Monitoring.Application/NotifyExecutor.cs:10-19`).
- El fallo de notificación no debe propagar una excepción que detenga el flujo (`src/Monitoring.Application/NotifyExecutor.cs:82-90`).

## Errores y casos límite

- Las excepciones del servicio se gestionan en el ejecutor para permitir que continúe el procesamiento (`src/Monitoring.Application/NotifyExecutor.cs:82-90`).
- La implementación de logging se selecciona en Development; fuera de ese entorno se registran canales reales (`src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:78-103`).

## Dónde actuar

| Si hay que cambiar... | Empezar en |
|---|---|
| Construcción de alerta y llamada al puerto | `NotifyExecutor.HandleAsync` (`src/Monitoring.Application/NotifyExecutor.cs:65-90`) |
| Selección/configuración de servicios | Registro de notificaciones (`src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:73-103`) |
| Reglas de canal y destinatario | [F2 - Configuración de roles y notificaciones](role-notification-configuration.md) (`src/Monitoring.Infrastructure/CompositeNotificationService.cs:35-58`) |

## Tests existentes

- `NotifyExecutorTests` cubre el intento de notificación y su manejo de error (`tests/Monitoring.UnitTests/NotifyExecutorTests.cs:1-162`).
- `MonitoringWorkflowIntegrationTests` verifica el flujo integrado de notificación (`tests/Monitoring.UnitTests/MonitoringWorkflowIntegrationTests.cs:34-118`).

## Cobertura / Gaps

- Hay pruebas del ejecutor y del flujo; no se encontró en estas pruebas evidencia de entrega externa efectiva por push o correo (`tests/Monitoring.UnitTests/NotifyExecutorTests.cs:1-162`; `src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:96-103`).

