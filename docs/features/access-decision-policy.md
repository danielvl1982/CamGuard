# Capacidad auxiliar - Política de decisión de acceso

Ticket: sin ticket (capacidad derivada de F4; no se registró ticket asociado)

## Propósito

Resuelve una distancia coseno en uno de tres resultados de acceso. Pertenece a [F4 - Monitoreo de imágenes](folder-monitoring.md); no crea un nuevo ID de catálogo.

## Disparador / Punto de entrada

- Etapa de decisión del flujo de monitoreo, `DecideAccessExecutor` (`src/Monitoring.Application/DecideAccessExecutor.cs:35-40`).

## Entradas (payload/config)

- Distancia coseno obtenida por verificación; si es nula por ausencia de coincidencia, el ejecutor produce `Denied` (`src/Monitoring.Application/DecideAccessExecutor.cs:35-40`).
- `AllowedMaxDistance` y `DeniedMinDistance`; opciones con valores predeterminados 0.35 y 0.55 (`src/Monitoring.Infrastructure/AccessDecisionOptions.cs:10-25`).

## Pasos

1. El flujo delega la distancia a `IAccessDecisionPolicy`; un valor nulo se convierte directamente en denegación (`src/Monitoring.Application/DecideAccessExecutor.cs:35-40`).
2. La política resuelve `Allowed` si la distancia es menor o igual al máximo permitido; `Denied` si alcanza el mínimo de denegación; de lo contrario requiere verificación humana (`src/Monitoring.Infrastructure/AccessDecisionPolicy.cs:24-36`).
3. Esta capacidad es utilizada por [F4](folder-monitoring.md); no se duplica aquí el flujo de monitoreo.

## Puntos de salida

| Salida | Detalle |
|---|---|
| Decisión | `AccessDecisionResult` con resultado, distancia y motivo (`src/Monitoring.Infrastructure/AccessDecisionPolicy.cs:26-36`). |

## Reglas de negocio

- Los umbrales deben cumplir `AllowedMaxDistance < DeniedMinDistance` y se validan en el registro de opciones (`src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:40-44`).
- Solo se emiten los tres resultados y los límites se comparan inclusivamente para permitir/denegar (`src/Monitoring.Infrastructure/AccessDecisionPolicy.cs:26-36`).

## Errores y casos límite

- Una configuración con máximo permitido mayor o igual al mínimo de denegación no satisface la validación (`src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:42-44`).
- Una distancia estrictamente entre umbrales requiere revisión humana (`src/Monitoring.Infrastructure/AccessDecisionPolicy.cs:31-36`).

## Dónde actuar

| Si hay que cambiar... | Empezar en |
|---|---|
| Selección de la decisión según distancia | `AccessDecisionPolicy.Decide` (`src/Monitoring.Infrastructure/AccessDecisionPolicy.cs:24-36`) |
| Validación/configuración de umbrales | Registro de opciones (`src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:40-44`) |
| Integración con el flujo | `DecideAccessExecutor.HandleAsync` (`src/Monitoring.Application/DecideAccessExecutor.cs:35-40`) |

## Tests existentes

- `AccessDecisionPolicyTests` cubre los resultados según distancia y los umbrales (`tests/Monitoring.UnitTests/AccessDecisionPolicyTests.cs:1-99`).
- `DecideAccessExecutorTests` cubre la integración con el flujo y el caso sin coincidencia (`tests/Monitoring.UnitTests/DecideAccessExecutorTests.cs:1-54`).

## Cobertura / Gaps

- El comportamiento de decisión tiene pruebas unitarias; no se identificó aquí una cobertura específica de que una configuración inválida impida iniciar el host (`src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:40-44`).

