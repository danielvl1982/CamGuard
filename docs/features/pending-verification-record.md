# F4 - Registro de verificación pendiente

Ticket: sin ticket (es una capacidad asociada a F4; el catálogo no le asigna ticket propio)

## Propósito

Modela el registro terminal asociado a un resultado `HumanVerificationRequired`. Conserva referencia a la imagen archivada, datos de candidato opcionales, instante UTC y estado inicial pendiente; no representa un workflow suspendido. Capacidad asociada a [F4 - Supervisión de carpeta](folder-monitoring.md) (`src/Monitoring.Domain/PendingVerification.cs:5-18`).

## Disparador / Punto de entrada

- Creación del registro mediante `PendingVerification.Create` en el flujo de resultado de verificación humana (`src/Monitoring.Domain/PendingVerification.cs:48-64`)

## Entradas (payload/config)

- ID, referencia de imagen, distancia nullable, ID de miembro candidato nullable e instante UTC (`src/Monitoring.Domain/PendingVerification.cs:20-25,53-58`).

## Pasos

1. Crea el registro con los datos recibidos y estado inicial `Pending` (`src/Monitoring.Domain/PendingVerification.cs:20-46,53-64`).
2. Conserva una referencia a la imagen, no bytes de imagen; distancia y candidato son opcionales (`src/Monitoring.Domain/PendingVerification.cs:34-44`).
3. La resolución requiere una ruta de escritura separada que no forma parte de esta capacidad (`src/Monitoring.Domain/PendingVerification.cs:7-10`).

## Puntos de salida

| Salida | Detalle |
|---|---|
| Registro de dominio | `PendingVerification` con referencia, distancia, candidato, fecha y estado (`src/Monitoring.Domain/PendingVerification.cs:18-46`) |
| Repositorio | Puerto `IPendingVerificationRepository` acepta guardar el registro (`src/Monitoring.Application/IPendingVerificationRepository.cs:11-13`) |

## Reglas de negocio

- Cada nuevo registro comienza como `Pending` (`src/Monitoring.Domain/PendingVerification.cs:46-51`).
- La referencia de imagen debe ser no vacía y el ID no puede ser nulo (`src/Monitoring.Domain/PendingVerification.cs:53-63`).
- No hay transición automática a un estado resuelto en el agregado; una ruta de resolución está fuera de alcance (`src/Monitoring.Domain/PendingVerification.cs:7-10,18-18`).

## Errores y casos límite

- ID nulo o referencia en blanco: la fábrica rechaza el argumento (`src/Monitoring.Domain/PendingVerification.cs:59-62`).
- Distancia o candidato nulos se conservan como tales (`src/Monitoring.Domain/PendingVerification.cs:34-44`).

## Dónde actuar

| Si hay que cambiar... | Empezar en |
|---|---|
| Validación y estado inicial del registro | `src/Monitoring.Domain/PendingVerification.cs` → `Create` (`src/Monitoring.Domain/PendingVerification.cs:46-64`) |
| Campos del modelo | `src/Monitoring.Domain/PendingVerification.cs` → `PendingVerification` (`src/Monitoring.Domain/PendingVerification.cs:18-46`) |
| Persistencia abstracta | `src/Monitoring.Application/IPendingVerificationRepository.cs` → `IPendingVerificationRepository` (`src/Monitoring.Application/IPendingVerificationRepository.cs:11-13`) |

## Tests existentes

- No se encontró una prueba de `PendingVerification` ni de su repositorio en la evidencia disponible (`src/Monitoring.Domain/PendingVerification.cs:18-64`; `src/Monitoring.Infrastructure/InMemoryPendingVerificationRepository.cs:19`).

## Cobertura / Gaps

- La implementación disponible del repositorio se identifica como `InMemoryPendingVerificationRepository`; no hay persistencia durable demostrada por esta evidencia (`src/Monitoring.Infrastructure/InMemoryPendingVerificationRepository.cs:19`).
- No se identificaron pruebas dedicadas al registro ni a su almacenamiento (`src/Monitoring.Domain/PendingVerification.cs:18-64`; `src/Monitoring.Infrastructure/InMemoryPendingVerificationRepository.cs:19`).
