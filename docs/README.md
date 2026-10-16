# Documentación de CamGuard

Documentación derivada del código real (commit `d21147a`). Las afirmaciones relevantes citan `archivo:l línea` y lo no verificable queda en [preguntas-abiertas.md](preguntas-abiertas.md).

## Cómo usar esta documentación

1. Consulta [features/README.md](features/README.md) para localizar una entrada.
2. Abre la feature y revisa **Dónde actuar**.
3. Consulta su spec en `openspec/specs/<slug>/spec.md`.
4. Revisa [preguntas-abiertas.md](preguntas-abiertas.md) antes de cambiar comportamiento.

## Índice

| Documento | Contenido |
|---|---|
| [arquitectura.md](arquitectura.md) | Contexto, módulos, DI, flujos y observabilidad |
| [datos.md](datos.md) | Almacenes, persistencia y matriz de acceso |
| [configuracion.md](configuracion.md) | Claves, valores por defecto y entornos |
| [api.md](api.md) | Endpoint de imágenes de miembros |
| [operaciones.md](operaciones.md) | Compilación, ejecución, tests y riesgos |
| [preguntas-abiertas.md](preguntas-abiertas.md) | Inconsistencias y decisiones pendientes |
| [features/README.md](features/README.md) | Catálogo y matrices de entrada/salida |

## Features

| ID | Feature | Ticket | Documento | Spec |
|---|---|---|---|---|
| F1 | Member management | — | [feature](features/member-management.md) | [spec](../openspec/specs/member-management/spec.md) |
| F2 | Role and notification-channel configuration | — | [feature](features/role-notification-configuration.md) | [spec](../openspec/specs/role-notification-configuration/spec.md) |
| F3 | Incident log and manual reporting | — | [feature](features/incident-log.md) | [spec](../openspec/specs/incident-log/spec.md) |
| F4 | Watched-image monitoring workflow | — | [feature](features/folder-monitoring.md) | [spec](../openspec/specs/folder-monitoring/spec.md) |
| F5 | Home dashboard | — | [feature](features/home-dashboard.md) | [spec](../openspec/specs/home-dashboard/spec.md) |

### Capacidades asociadas

Las siguientes capacidades están implementadas y asociadas a F1, F2 o F4; se documentan para mantener trazabilidad con sus specs OpenSpec:

- [Access decision policy](features/access-decision-policy.md)
- [Access notification](features/access-notification.md)
- [Enrollment watch ingestion](features/enrollment-watch-ingestion.md)
- [Identity verification](features/identity-verification.md)
- [Member directory query](features/member-directory-query.md)
- [Member enrollment](features/member-enrollment.md)
- [Motion image ingestion](features/motion-image-ingestion.md)
- [Pending verification record](features/pending-verification-record.md)
- [Person detection](features/person-detection.md)
