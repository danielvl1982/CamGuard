# Capacidad auxiliar - Detección de persona

Ticket: sin ticket (capacidad derivada de F4; no se registró ticket asociado)

## Propósito

Usa detección de rostros como indicador de presencia en una imagen ingerida. Es una capacidad interna de [F4](folder-monitoring.md), no un nuevo ID de catálogo.

## Disparador / Punto de entrada

- Etapa inicial `DetectPersonExecutor` para una imagen ingerida (`src/Monitoring.Application/DetectPersonExecutor.cs:8-20`).

## Entradas (payload/config)

- Contexto de monitoreo con la imagen a analizar (`src/Monitoring.Application/DetectPersonExecutor.cs:32-40`).
- Puerto de detección `IPersonDetector` (`src/Monitoring.Application/DetectPersonExecutor.cs:17-20`).

## Pasos

1. Ejecuta detección de personas/rostros sobre la imagen de entrada (`src/Monitoring.Application/DetectPersonExecutor.cs:32-40`).
2. Si hay uno o más rostros, continúa hacia verificación de identidad (`src/Monitoring.Application/DetectPersonExecutor.cs:41-49`).
3. Si no hay rostros, sintetiza una decisión `HumanVerificationRequired`; el contexto regresa sin embedding (`src/Monitoring.Application/DetectPersonExecutor.cs:48-57`).

## Puntos de salida

| Salida | Detalle |
|---|---|
| Continuación normal | Contexto con detecciones para verificar identidad (`src/Monitoring.Application/DetectPersonExecutor.cs:41-49`). |
| Revisión humana | Resultado terminal para cero rostros (`src/Monitoring.Application/DetectPersonExecutor.cs:48-57`). |

## Reglas de negocio

- Cero rostros no se descarta: se requiere verificación humana sin intentar verificar identidad (`src/Monitoring.Application/DetectPersonExecutor.cs:48-57`).
- Uno o más rostros continúan al siguiente paso del flujo (`src/Monitoring.Application/DetectPersonExecutor.cs:41-60`).

## Errores y casos límite

- El caso cero rostros usa la razón constante `No face detected` para identificar el resultado (`src/Monitoring.Application/DetectPersonExecutor.cs:27,48-57`).

## Dónde actuar

| Si hay que cambiar... | Empezar en |
|---|---|
| Detección de rostro | `DetectPersonExecutor.HandleAsync` (`src/Monitoring.Application/DetectPersonExecutor.cs:32-40`) |
| Enrutamiento por cantidad de rostros | `DetectPersonExecutor.HandleAsync` (`src/Monitoring.Application/DetectPersonExecutor.cs:41-60`) |
| Implementación del detector | Registro de `IPersonDetector` (`src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:62-65`) |

## Tests existentes

- `DetectPersonExecutorTests` cubre la rama con detección y la rama de cero rostros (`tests/Monitoring.UnitTests/DetectPersonExecutorTests.cs:1-98`).
- `MonitoringWorkflowIntegrationTests` cubre la integración del workflow (`tests/Monitoring.UnitTests/MonitoringWorkflowIntegrationTests.cs:34-118`).

## Cobertura / Gaps

- Las ramas principales tienen pruebas; no se identificó aquí cobertura con el detector de producción sobre imágenes reales (`tests/Monitoring.UnitTests/DetectPersonExecutorTests.cs:1-98`).

