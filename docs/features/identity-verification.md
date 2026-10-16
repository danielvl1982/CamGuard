# Capacidad auxiliar - Verificación de identidad

Ticket: sin ticket (capacidad derivada de F4/F1; no se registró ticket asociado)

## Propósito

Busca una coincidencia para el embedding de una cara detectada y transmite su distancia sin alterarla. Se integra con [F4](folder-monitoring.md) y consulta el directorio de [F1](member-management.md); no crea un nuevo ID de catálogo.

## Disparador / Punto de entrada

- Etapa `VerifyIdentityExecutor` tras la detección de persona (`src/Monitoring.Application/VerifyIdentityExecutor.cs:16-19`).

## Entradas (payload/config)

- `EmbeddingVector` del contexto de monitoreo (`src/Monitoring.Application/VerifyIdentityExecutor.cs:35-37`).
- Puerto `IMemberDirectoryReader` expuesto por Enrollment (`src/Monitoring.Application/VerifyIdentityExecutor.cs:16-22`).

## Pasos

1. Consulta la mejor coincidencia del directorio usando el embedding (`src/Monitoring.Application/VerifyIdentityExecutor.cs:35-37`).
2. Copia `MemberId` y `Score` al contexto; el score se conserva como distancia, sin redondeo ni conversión (`src/Monitoring.Application/VerifyIdentityExecutor.cs:41-45`).
3. La política que interpreta los umbrales se documenta en [access-decision-policy](access-decision-policy.md).

## Puntos de salida

| Salida | Detalle |
|---|---|
| Contexto actualizado | Miembro candidato y distancia, o ambos nulos sin coincidencia (`src/Monitoring.Application/VerifyIdentityExecutor.cs:41-45`). |

## Reglas de negocio

- `MemberMatch.Score` representa distancia coseno: menor significa una coincidencia más cercana (`src/Monitoring.Application/VerifyIdentityExecutor.cs:8-14`).
- Se reenvía el valor exacto de score para conservar las fronteras de decisión (`src/Monitoring.Application/VerifyIdentityExecutor.cs:10-13,41-45`).

## Errores y casos límite

- Una ausencia de coincidencia deja `CandidateMemberId` y `Distance` nulos (`src/Monitoring.Application/VerifyIdentityExecutor.cs:41-45`).

## Dónde actuar

| Si hay que cambiar... | Empezar en |
|---|---|
| Consulta del directorio | `VerifyIdentityExecutor.HandleAsync` (`src/Monitoring.Application/VerifyIdentityExecutor.cs:35-37`) |
| Mapeo de coincidencia al contexto | `VerifyIdentityExecutor.HandleAsync` (`src/Monitoring.Application/VerifyIdentityExecutor.cs:41-45`) |
| Búsqueda de miembros y contrato de lectura | [F1 - Gestión de miembros](member-management.md) (`src/Enrollment.PublicApi/IMemberDirectoryReader.cs:9-15`) |

## Tests existentes

- `VerifyIdentityExecutorTests` cubre la consulta al directorio y el mapeo de resultados al contexto (`tests/Monitoring.UnitTests/VerifyIdentityExecutorTests.cs:1-100`).

## Cobertura / Gaps

- Hay pruebas del ejecutor; no se identificó una prueba de integración contra el almacenamiento real del directorio en esta suite unitaria (`tests/Monitoring.UnitTests/VerifyIdentityExecutorTests.cs:1-100`).

