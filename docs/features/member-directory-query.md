# F1 - Consulta del directorio de miembros

Ticket: sin ticket (es una capacidad asociada a F1; el catálogo no le asigna ticket propio)

## Propósito

Expone una consulta de coincidencia facial sobre el directorio de miembros enrolados. La frontera pública devuelve la identidad y puntuación de la mejor coincidencia, o `null` cuando no hay coincidencia; Monitoring la consume sin acceder a persistencia interna (`src/Enrollment.PublicApi/IMemberDirectoryReader.cs:3-15`). Capacidad asociada a [F1 - Gestión de miembros](member-management.md).

## Disparador / Punto de entrada

- Llamada a `IMemberDirectoryReader.FindBestMatchAsync` por un consumidor (`src/Enrollment.PublicApi/IMemberDirectoryReader.cs:9-12`)

## Entradas (payload/config)

- Vector de embedding (`float[]`) y token de cancelación (`src/Enrollment.PublicApi/IMemberDirectoryReader.cs:9-12`)

## Pasos

1. El adaptador rechaza un vector nulo y construye la consulta de aplicación (`src/Enrollment.PublicApi/MemberDirectoryReader.cs:13-18`).
2. El handler valida el embedding y consulta el miembro más cercano mediante el repositorio (`src/Enrollment.Application/QueryByEmbeddingQuery.cs:13-29`).
3. Un error de consulta o ausencia de coincidencia se traduce a `null`; en caso de coincidencia se devuelve `MemberMatch` con GUID y score (`src/Enrollment.PublicApi/MemberDirectoryReader.cs:20-26`).
4. La capacidad se integra en [F4 - Supervisión de carpeta](folder-monitoring.md), que la usa para consultar miembros sin depender de Enrollment internamente (`src/Enrollment.PublicApi/IMemberDirectoryReader.cs:3-8`).

## Puntos de salida

| Salida | Detalle |
|---|---|
| Resultado de consulta | `MemberMatch(MemberId, Score)` o `null` (`src/Enrollment.PublicApi/MemberDirectoryReader.cs:20-26`) |

## Reglas de negocio

- La frontera pública es `IMemberDirectoryReader`; no requiere tipos internos de persistencia ni del dominio (`src/Enrollment.PublicApi/IMemberDirectoryReader.cs:3-12`).
- El handler valida el embedding antes de consultar el repositorio (`src/Enrollment.Application/QueryByEmbeddingQuery.cs:19-28`).

## Errores y casos límite

- Vector nulo: el adaptador lanza por argumento nulo (`src/Enrollment.PublicApi/MemberDirectoryReader.cs:13-16`).
- Embedding inválido: el handler devuelve `InvalidEmbedding`, que el adaptador presenta como ausencia (`src/Enrollment.Application/QueryByEmbeddingQuery.cs:21-25`; `src/Enrollment.PublicApi/MemberDirectoryReader.cs:20-23`).
- Sin coincidencia o fallo de consulta: el límite público devuelve `null` (`src/Enrollment.PublicApi/MemberDirectoryReader.cs:20-23`).

## Dónde actuar

| Si hay que cambiar... | Empezar en |
|---|---|
| Validación/consulta del embedding | `src/Enrollment.Application/QueryByEmbeddingQuery.cs` → `QueryByEmbeddingQueryHandler.HandleAsync` (`src/Enrollment.Application/QueryByEmbeddingQuery.cs:13-29`) |
| Contrato y forma de salida pública | `src/Enrollment.PublicApi/IMemberDirectoryReader.cs` → `IMemberDirectoryReader`, `MemberMatch` (`src/Enrollment.PublicApi/IMemberDirectoryReader.cs:9-15`) |
| Traducción de resultado de aplicación | `src/Enrollment.PublicApi/MemberDirectoryReader.cs` → `FindBestMatchAsync` (`src/Enrollment.PublicApi/MemberDirectoryReader.cs:11-27`) |

## Tests existentes

- `MemberDirectoryReaderTests`: pruebas del lector de directorio (`tests/Enrollment.UnitTests/MemberDirectoryReaderTests.cs`).

## Cobertura / Gaps

- El lector tiene pruebas identificadas; no se documenta aquí cobertura de todos los escenarios del contrato.
