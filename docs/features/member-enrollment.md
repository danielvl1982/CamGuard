# F1 - Enrolamiento de miembros

Ticket: sin ticket (es una capacidad asociada a F1; el catálogo no le asigna ticket propio)

## Propósito

Registra un miembro desde su nombre y fotos de referencia, extrayendo embeddings y persistiendo el miembro en el directorio. La capacidad de ingesta de carpeta que invoca el servicio es distinta y está descrita en [ingesta de carpeta de enrolamiento](enrollment-watch-ingestion.md); esta capacidad pertenece a [F1 - Gestión de miembros](member-management.md) (`src/Enrollment.Application/RegisterMemberCommand.cs:6-17`).

## Disparador / Punto de entrada

- Comando de aplicación `RegisterMemberCommand` con nombre y fotos (`src/Enrollment.Application/RegisterMemberCommand.cs:10-27`)
- Servicio de foto única `EnrollmentService.EnrollAsync`, usado por la ingesta de carpeta (`src/Enrollment.Application/EnrollmentService.cs:13-21`)

## Entradas (payload/config)

- `FullName` y una o más fotos (`IReadOnlyList<Stream>`); la lista vacía se rechaza al construir el comando (`src/Enrollment.Application/RegisterMemberCommand.cs:10-27`).
- En el servicio de foto única: nombre, stream y token de cancelación (`src/Enrollment.Application/EnrollmentService.cs:17-22`).

## Pasos

1. El comando conserva el nombre y fotos; requiere una lista no nula y no vacía (`src/Enrollment.Application/RegisterMemberCommand.cs:12-27`).
2. El servicio de foto única rechaza nombre vacío, normaliza quitando espacios exteriores y busca un miembro por nombre normalizado (`src/Enrollment.Application/EnrollmentService.cs:24-32`).
3. Extrae el embedding; ante error mapea la extracción a un error de enrolamiento (`src/Enrollment.Application/EnrollmentService.cs:33-37,56-61`).
4. Reutiliza el identificador encontrado o crea uno nuevo, construye el miembro y guarda el resultado (`src/Enrollment.Application/EnrollmentService.cs:39-53`).
5. La llamada desde la carpeta de enrolamiento está asociada a [ingesta de carpeta de enrolamiento](enrollment-watch-ingestion.md) (`src/Enrollment.Application/EnrollmentService.cs:6-11`).

## Puntos de salida

| Salida | Detalle |
|---|---|
| Directorio de miembros | Guarda el miembro y devuelve su `MemberId` (`src/Enrollment.Application/EnrollmentService.cs:39-53`) |
| Resultado de aplicación | Error de nombre o extracción cuando no se completa el alta (`src/Enrollment.Application/EnrollmentService.cs:24-37,56-61`) |

## Reglas de negocio

- Un nombre vacío o en blanco se rechaza antes de extraer la foto (`src/Enrollment.Application/EnrollmentService.cs:24-27`).
- Un nombre existente reutiliza su `MemberId`; un nombre nuevo obtiene uno nuevo (`src/Enrollment.Application/EnrollmentService.cs:31-41`).
- El enrolamiento exitoso guarda el miembro (`src/Enrollment.Application/EnrollmentService.cs:41-53`).

## Errores y casos límite

- Lista de fotos nula: constructor lanza `ArgumentNullException`; lista vacía: lanza `ArgumentException` (`src/Enrollment.Application/RegisterMemberCommand.cs:12-18`).
- Nombre nulo, vacío o en blanco en el servicio: devuelve `InvalidName` (`src/Enrollment.Application/EnrollmentService.cs:24-27`).
- Fallo de extracción se mapea a `NoFaceDetected`, `MultipleFacesDetected` o `ExtractionFailed` (`src/Enrollment.Application/EnrollmentService.cs:56-61`).

## Dónde actuar

| Si hay que cambiar... | Empezar en |
|---|---|
| Validación de entradas por lote | `src/Enrollment.Application/RegisterMemberCommand.cs` → `RegisterMemberCommand` (`src/Enrollment.Application/RegisterMemberCommand.cs:10-27`) |
| Selección de miembro por nombre y persistencia de alta individual | `src/Enrollment.Application/EnrollmentService.cs` → `EnrollAsync` (`src/Enrollment.Application/EnrollmentService.cs:17-53`) |
| Traducción de errores de detección | `src/Enrollment.Application/EnrollmentService.cs` → `MapExtractionError` (`src/Enrollment.Application/EnrollmentService.cs:56-61`) |

## Tests existentes

- `RegisterMemberCommandHandlerTests`: comportamiento del handler (`tests/Enrollment.UnitTests/RegisterMemberCommandHandlerTests.cs`).
- `EnrollmentServiceTests`: comportamiento del servicio (`tests/Enrollment.UnitTests/EnrollmentServiceTests.cs`).

## Cobertura / Gaps

- Existen pruebas del handler y servicio; la cobertura específica de cada escenario de la especificación no se infiere de los nombres de clase.
