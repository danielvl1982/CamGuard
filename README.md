# CamGuard

CamGuard es una aplicación .NET para gestionar miembros, roles, incidentes y monitorizar imágenes.

## Documentación

- [Índice de documentación](docs/README.md)
- [Arquitectura](docs/arquitectura.md)
- [Datos y persistencia](docs/datos.md)
- [Configuración](docs/configuracion.md)
- [Operaciones](docs/operaciones.md)
- [API](docs/api.md)
- [Preguntas abiertas](docs/preguntas-abiertas.md)
- [Catálogo de funcionalidades](docs/features/README.md)

### Funcionalidades

- [F1 — Gestión de miembros](docs/features/member-management.md) · [Spec](openspec/specs/member-management/spec.md)
- [F2 — Roles y canales de notificación](docs/features/role-notification-configuration.md) · [Spec](openspec/specs/role-notification-configuration/spec.md)
- [F3 — Registro de incidentes](docs/features/incident-log.md) · [Spec](openspec/specs/incident-log/spec.md)
- [F4 — Monitorización de carpetas](docs/features/folder-monitoring.md) · [Spec](openspec/specs/folder-monitoring/spec.md)
- [F5 — Panel principal](docs/features/home-dashboard.md) · [Spec](openspec/specs/home-dashboard/spec.md)

## Revisión de información sensible

Se revisaron nombres de archivos, contenido textual y archivos de datos del repositorio. No se encontraron secretos, credenciales, claves privadas ni bases SQLite de aplicación versionadas. Existen dos bases `.db` bajo `.vs/CamGuard/CopilotIndices/`, generadas por Visual Studio y no versionadas; no son datos de runtime de CamGuard. El código sí contiene repositorios SQLite/SqliteVec como parte de la persistencia prevista; sus detalles están documentados en [datos.md](docs/datos.md).

## Proyecto

- Solución: `CamGuard.slnx`
- Código: `src/`
- Tests: `tests/`
- Contexto OpenSpec: `openspec/config.yaml`
