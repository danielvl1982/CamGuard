# Configuración

## 1. Claves

Los valores predeterminados proceden de las clases Options; los archivos JSON pueden sobrescribirlos. Las rutas son relativas al repositorio.

| Clave | Tipo | Valor predeterminado | Lectura / efecto |
|---|---|---|---|
| VectorStore:ConnectionString | string | Data Source=camguard-enrollment.db | src/Common/VectorStoreOptions.cs:11-14; conexión SQLite de los repositorios de Enrollment. |
| VectorStore:MembersCollectionName | string | members | src/Common/VectorStoreOptions.cs:16-17; colección de embeddings de miembros. |
| VectorStore:MemberImagesRoot | string | photos/members | src/Common/VectorStoreOptions.cs:19-24; raíz de fotos de referencia. |
| VectorStore:MemberImagesCollectionName | string | member_images | src/Common/VectorStoreOptions.cs:26-27; colección de embeddings por imagen. |
| VectorStore:IncidentsTableName | string | incidents | src/Common/VectorStoreOptions.cs:29-34; tabla de incidentes. |
| VectorStore:RolesTableName | string | roles | src/Common/VectorStoreOptions.cs:36-37; tabla de roles. |
| VectorStore:RoleChannelsTableName | string | role_channels | src/Common/VectorStoreOptions.cs:39-40; tabla de canales por rol. |
| VectorStore:DefaultChannelsTableName | string | notification_default_channels | src/Common/VectorStoreOptions.cs:42-43; tabla de canales predeterminados. |
| VectorStore:NotificationSettingsTableName | string | notification_settings | src/Common/VectorStoreOptions.cs:45-46; tabla auxiliar para distinguir configuración nunca guardada de conjunto vacío. |
| VectorStore:DuplicateFaceDistanceThreshold | double | 0.4 | src/Common/VectorStoreOptions.cs:48-62; umbral para rechazar imágenes duplicadas de otra persona; calibración real pendiente. |
| EnrollmentWatch:Path | string | C:/Dev/CamGuard/photos/enroll | src/Enrollment.Console/EnrollmentWatchOptions.cs:11-19; carpeta vigilada por el modo watch de Enrollment Console. |
| EnrollmentWatch:StabilityRetryDelayMilliseconds | int | 200 | src/Enrollment.Console/EnrollmentWatchOptions.cs:15; demora base entre comprobaciones de estabilidad. |
| EnrollmentWatch:MaxStabilityRetryAttempts | int | 5 | src/Enrollment.Console/EnrollmentWatchOptions.cs:17; reintentos de estabilidad. |
| EnrollmentWatch:ChannelCapacity | int | 64 | src/Enrollment.Console/EnrollmentWatchOptions.cs:19; capacidad acotada del canal de archivos. |
| WatchFolder:Path | string | watch | src/Monitoring.Infrastructure/WatchFolderOptions.cs:11-15; carpeta de entrada de Monitoring. |
| WatchFolder:StabilityRetryDelayMilliseconds | int | 200 | src/Monitoring.Infrastructure/WatchFolderOptions.cs:17-18; demora base entre comprobaciones de estabilidad. |
| WatchFolder:MaxStabilityRetryAttempts | int | 5 | src/Monitoring.Infrastructure/WatchFolderOptions.cs:20-21; reintentos de estabilidad. |
| WatchFolder:ChannelCapacity | int | 256 | src/Monitoring.Infrastructure/WatchFolderOptions.cs:23-24; capacidad del canal de ingesta. |
| AccessDecision:AllowedMaxDistance | double | 0.35 | src/Monitoring.Infrastructure/AccessDecisionOptions.cs:12-19; distancia máxima inclusiva para permitir acceso. |
| AccessDecision:DeniedMinDistance | double | 0.55 | src/Monitoring.Infrastructure/AccessDecisionOptions.cs:21-25; distancia mínima inclusiva para denegar; el intervalo intermedio requiere verificación humana. |
| Ntfy:BaseUrl | string | https://ntfy.sh | src/Monitoring.Infrastructure/NtfyOptions.cs:9-13; servidor push. |
| Ntfy:Topic | string | vacío | src/Monitoring.Infrastructure/NtfyOptions.cs:15-21; tema ntfy, obligatorio fuera de Development. |
| EmailNotification:SmtpHost | string | vacío | src/Monitoring.Infrastructure/EmailNotificationOptions.cs:10-14; host SMTP, obligatorio fuera de Development. |
| EmailNotification:SmtpPort | int | 587 | src/Monitoring.Infrastructure/EmailNotificationOptions.cs:16-17; puerto SMTP con STARTTLS. |
| EmailNotification:FromAddress | string | vacío | src/Monitoring.Infrastructure/EmailNotificationOptions.cs:19-20; remitente, obligatorio fuera de Development. |
| EmailNotification:ToAddress | string | vacío | src/Monitoring.Infrastructure/EmailNotificationOptions.cs:22-23; destinatario, obligatorio fuera de Development. |
| EmailNotification:Username | string | vacío | src/Monitoring.Infrastructure/EmailNotificationOptions.cs:25-26; usuario SMTP; vacío omite autenticación. |
| EmailNotification:Password | string | vacío | src/Monitoring.Infrastructure/EmailNotificationOptions.cs:28-29; contraseña SMTP; nunca guardar valores reales en archivos versionados. |
| Logging:LogLevel:Default | string | Information | Configurado en JSON de Monitoring Host y Enrollment Web; nivel base de logging. |
| Logging:LogLevel:Microsoft o Microsoft.AspNetCore | string | Warning | Configurado en los JSON de host para reducir logs del framework. |
| AllowedHosts | string | * | src/Enrollment.Web/appsettings.json:8; límite de hosts de ASP.NET Core. |

Enrollment enlaza VectorStore mediante AddEnrollment (src/Enrollment.Infrastructure/EnrollmentServiceCollectionExtensions.cs:30-42). Monitoring enlaza AccessDecision, WatchFolder, Ntfy y EmailNotification (src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:30-53). La pareja de umbrales se valida: AllowedMaxDistance debe ser estrictamente menor que DeniedMinDistance (src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:40-44).

## 2. Recarga en caliente

No se encontró consumo de estas opciones mediante IOptionsMonitor<T>. Los servicios reciben IOptions<T> (por ejemplo src/Monitoring.Infrastructure/EmailNotificationService.cs:18-22 y src/Enrollment.Console/EnrollmentWatchArchiver.cs:17-20), por lo que cambios de configuración no actualizan las instancias en ejecución; reiniciá el proceso. Enrollment Console agrega su JSON sin habilitar explícitamente reloadOnChange (src/Enrollment.Console/Program.cs:16-18).

## 3. Ficheros de configuración adicionales

- src/Monitoring.Host/appsettings.json: Logging, WatchFolder, AccessDecision, VectorStore, Ntfy y EmailNotification.
- src/Enrollment.Web/appsettings.json: Logging, AllowedHosts y parte de VectorStore; las propiedades omitidas conservan los defaults de Options.
- src/Enrollment.Web/appsettings.Development.json: solamente niveles de Logging.
- src/Enrollment.Console/appsettings.json: sobrescribe conexión SQLite y ruta de EnrollmentWatch.
- src/Enrollment.Web/Properties/launchSettings.json: perfiles http y https establecen URLs locales y ASPNETCORE_ENVIRONMENT=Development; son ajustes de lanzamiento, no configuración de despliegue.

No se identificaron archivos .env.example ni YAML de configuración de la aplicación. openspec/config.yaml configura OpenSpec, no CamGuard.

## 4. Entornos

Enrollment Web crea un host web estándar y registra AddEnrollment (src/Enrollment.Web/Program.cs:5-12); Monitoring Host crea un host genérico y registra Enrollment y Monitoring (src/Monitoring.Host/Program.cs:8-13). El perfil local Web fija Development. En ese entorno Monitoring usa el servicio de notificación de logging; en otros entornos selecciona ntfy + SMTP y exige Ntfy:Topic, EmailNotification:SmtpHost, FromAddress y ToAddress (src/Monitoring.Infrastructure/MonitoringServiceCollectionExtensions.cs:73-103). No se encontraron archivos específicos de producción.

Enrollment Console construye su configuración con un único JSON opcional (src/Enrollment.Console/Program.cs:16-18); no agrega allí proveedores de variables de entorno. Las opciones Email documentan user-secrets en desarrollo o variables de entorno en producción para la contraseña, pero no hay configuración de esos proveedores específica en el módulo (src/Monitoring.Infrastructure/EmailNotificationOptions.cs:3-6).

## 5. Configuración de tests

Los tests configuran opciones directamente en Monitoring (tests/Monitoring.UnitTests/AccessDecisionPolicyTests.cs:17-18) y suministran configuración en memoria para el composition root de Enrollment (tests/Enrollment.UnitTests/EnrollmentServiceCollectionExtensionsTests.cs:25-30). No se encontró appsettings.Testing.json. tests/Monitoring.UnitTests/AppSettingsSecretsTests.cs:15-27 verifica que la contraseña de EmailNotification en configuración de prueba quede vacía.

## 6. Inconsistencias

- EnrollmentWatch:Path tiene una ruta absoluta de desarrollo como valor predeterminado y también en el JSON de Console; no hay evidencia de que esa ruta sea válida fuera de desarrollo.
- DuplicateFaceDistanceThreshold usa 0.4 sin calibración con fotos reales, según el comentario de la opción.
- MonitoringHostedService fija tres intentos de workflow y una espera incremental de 200 ms; no son claves de configuración (src/Monitoring.Infrastructure/MonitoringHostedService.cs:24,79-104).
- WatchFolder:ChannelCapacity tiene default 256, pero no aparece en appsettings de Monitoring Host; el valor efectivo proviene de Options.
