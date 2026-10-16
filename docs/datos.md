# Datos

## 1. Almacenes

### SQLite local

La sección VectorStore configura una conexión SQLite, por defecto Data Source=camguard-enrollment.db. El mismo almacén mantiene la colección de miembros, metadatos de fotos, incidentes, roles y notificaciones; la ubicación absoluta depende del contexto de ejecución. Es almacenamiento local propiedad de CamGuard. (src/Common/VectorStoreOptions.cs:8-17,26-47; src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs:45-56)

### Archivos de fotos

Los bytes de referencia van bajo photos/members por defecto, separados del registro vectorial member_images. La ruta configurada sigue la forma raíz/memberId/imageId.ext. Los archivos quedan fuera de wwwroot y se sirven por endpoint dedicado. Al combinar filesystem y SQLite, las operaciones no forman una transacción común. (src/Common/VectorStoreOptions.cs:19-27; src/Enrollment.Infrastructure/SqliteVecMemberImageRepository.cs:12-17,36-48; src/Enrollment.Web/MemberImageEndpoints.cs:37-68)

## 2. Tablas / colecciones

| Almacén | Columnas y claves conocidas | Evidencia de persistencia |
|---|---|---|
| members (SqliteVec) | MemberVectorRecord almacena miembro, embedding y columna IsAnnulled. Los tipos físicos completos los determina el proveedor. | Repositorio y esquema: src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs:20,45-60; mapper: src/Enrollment.Infrastructure/MemberVectorRecordMapper.cs:1-5. |
| member_images (SqliteVec) | Id, MemberId, FilePath, StoredAtUtc, Embedding. Ids como texto; el esquema físico completo depende del proveedor. | src/Enrollment.Infrastructure/MemberImageRecordMapper.cs:8-15; src/Enrollment.Infrastructure/SqliteVecMemberImageRepository.cs:19-21,36-48. |
| Archivos de foto | Contenido binario y ruta por miembro/foto; no son columnas SQLite. | src/Common/VectorStoreOptions.cs:19-27; src/Enrollment.Infrastructure/SqliteVecMemberImageRepository.cs:12-17. |
| incidents | Id TEXT PK; OccurredAtUtc y Kind TEXT NOT NULL; MemberId, Note, SnapshotImagePath nullable. DDL inicial define defaults vacíos para fecha y tipo; columnas ausentes se agregan al abrir. | src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:40-63,113-127. |
| roles | Id TEXT PK; Name TEXT NOT NULL UNIQUE COLLATE NOCASE; CreatedAtUtc TEXT NOT NULL DEFAULT ''. | src/Enrollment.Infrastructure/SqliteRoleRepository.cs:62-84. |
| role_channels | RoleId TEXT FK a roles(Id) ON DELETE CASCADE; Channel TEXT NOT NULL; PK compuesta (RoleId, Channel). | src/Enrollment.Infrastructure/SqliteRoleRepository.cs:77-80. |
| notification_default_channels | Una fila por canal predeterminado; el repositorio define sus columnas concretas. | Configuración: src/Common/VectorStoreOptions.cs:42-47; repositorio: src/Enrollment.Infrastructure/SqliteNotificationSettingsRepository.cs:12-18,20-39. |
| notification_settings | Tabla clave/valor con marcador default_channels_configured, que distingue conjunto vacío guardado de configuración nunca guardada. | src/Enrollment.Infrastructure/SqliteNotificationSettingsRepository.cs:12-18; src/Common/VectorStoreOptions.cs:45-47. |

## 3. Matriz lectura/escritura por operación

R indica lectura; INSERT/UPDATE/DELETE son escrituras SQL; FS significa filesystem.

| Feature / operación | members | member_images | archivos | incidents | roles | role_channels | notificaciones |
|---|---|---|---|---|---|---|---|
| F1 miembros: alta, edición, consulta, baja lógica | R, INSERT, UPDATE — src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs:20,45-60 | — | — | — | R rol asociado — src/Enrollment.Infrastructure/SqliteRoleRepository.cs:252-261 | — | — |
| F1 fotos | R | R, INSERT, DELETE — src/Enrollment.Infrastructure/SqliteVecMemberImageRepository.cs:19-21,36-48 | FS lectura, creación y eliminación — src/Enrollment.Infrastructure/SqliteVecMemberImageRepository.cs:12-17 | — | — | — | — |
| F2 roles/canales | R para validar uso/asignaciones — src/Enrollment.Infrastructure/SqliteRoleRepository.cs:184-249 | — | — | — | R, INSERT, UPDATE, DELETE — src/Enrollment.Infrastructure/SqliteRoleRepository.cs:86-182 | R, DELETE, INSERT — src/Enrollment.Infrastructure/SqliteRoleRepository.cs:94-136 | R y escritura de defaults — src/Enrollment.Infrastructure/SqliteNotificationSettingsRepository.cs:12-18,20-39 |
| F3 registrar/listar incidentes | R indirecta al asociar miembro | — | — | INSERT OR REPLACE, R — src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:113-127,132-164 | — | — | — |
| F4 monitoreo de imágenes | R de candidatos/embeddings — src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs:20-28,45-60 | R evidencia de referencia — src/Enrollment.Infrastructure/SqliteVecMemberImageRepository.cs:19-21,36-48 | R y mover/procesar/cuarentena — src/Monitoring.Infrastructure/MonitoringHostedService.cs:45-62,86-91 | INSERT si el flujo reporta incidente — src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:104-127 | R rol — src/Enrollment.Infrastructure/SqliteRoleRepository.cs:252-286 | R canales del rol — src/Enrollment.Infrastructure/SqliteRoleRepository.cs:252-286 | R configuración — src/Enrollment.Infrastructure/SqliteNotificationSettingsRepository.cs:12-18 |
| F5 panel | — | — | — | R incidentes recientes — src/Enrollment.Web/Components/Pages/Home.razor:115-116 | — | — | — |

La escritura de incidente en F4 es condicional al resultado; el monitor también procesa archivos y los pone en cuarentena cuando falla el flujo. (src/Monitoring.Infrastructure/MonitoringHostedService.cs:45-62,86-91)

## 4. Semántica de estados

- IsAnnulled es baja lógica del miembro: no implica borrado físico. Reconocimiento inspecciona hasta 10 vecinos; si los candidatos cercanos son anulados, devuelve null y falla de forma segura. (src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs:22-28,59-60)
- default_channels_configured permite diferenciar nunca configurado, configurado con canales y conjunto vacío configurado explícitamente. (src/Enrollment.Infrastructure/SqliteNotificationSettingsRepository.cs:12-18)
- incidents carece de columna de estado: Kind clasifica el evento y OccurredAtUtc ordena el historial. (src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:47-54,113-127,147-164)
- roles no tiene estado booleano. DeleteIfUnused produce Deleted, InUse o NotFound; los canales dependientes se eliminan por cascada. (src/Enrollment.Infrastructure/SqliteRoleRepository.cs:77-80,184-216)
- Archivos vigilados se procesan o se aíslan en cuarentena al fallar; este ciclo no es un estado en tabla SQL. (src/Monitoring.Infrastructure/MonitoringHostedService.cs:45-62,86-91)

## 5. Fórmulas y cálculos

- IDs se serializan desde GUID a texto. Ejemplo: MemberId se guarda mediante Value.ToString() y se convierte de vuelta a tipo de dominio al rehidratar. (src/Enrollment.Infrastructure/MemberImageRecordMapper.cs:8-13; src/Enrollment.Infrastructure/SqliteRoleRepository.cs:104-106,300-305)
- Fechas de roles e imágenes usan UTC round-trip ISO 8601; incidentes usan formato o con cultura invariante. La lista ordena OccurredAtUtc descendente y rowid descendente para desempatar. Ejemplo: entre dos incidentes con misma fecha, el insertado al final aparece primero. (src/Enrollment.Infrastructure/SqliteRoleRepository.cs:102-106; src/Enrollment.Infrastructure/MemberImageRecordMapper.cs:8-14; src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:121-123,147-153)
- Umbral de duplicado es distancia coseno estrictamente menor que DuplicateFaceDistanceThreshold, default 0.4: 0.39 rechaza y 0.40 no satisface la condición. El propio código dice que falta calibrarlo con fotos reales. (src/Common/VectorStoreOptions.cs:48-62)
- Reconocimiento limita a diez candidatos más próximos para descartar anulados; diez registros anulados más cercanos pueden ocultar uno activo posterior y causar denegación segura. (src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs:22-28)

## 6. Contratos de datos generados

- MemberVectorRecord persiste datos del miembro y su embedding; no se identifica exportación de archivo en este flujo. (src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs:14-20; src/Enrollment.Infrastructure/MemberVectorRecordMapper.cs:1-5)
- MemberImageRecord contiene Id, MemberId, FilePath, StoredAtUtc y Embedding; los bytes permanecen en el archivo señalado. (src/Enrollment.Infrastructure/MemberImageRecordMapper.cs:8-15)
- Incidente contiene Id, OccurredAtUtc, Kind, MemberId opcional, Note opcional y SnapshotImagePath opcional. (src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:47-54,116-127)
- Endpoint de foto devuelve stream o 404; es una respuesta, no un contrato adicional persistido. (src/Enrollment.Web/MemberImageEndpoints.cs:37-68)

## 7. Diferencias de esquema

- No hay DDL independiente de producción en las fuentes revisadas; repositorios crean o completan esquema en ejecución. La equivalencia con bases desplegadas no puede afirmarse sin inspección del almacén real. Fragmento en docs/.work/questions/datos.md. (src/Enrollment.Infrastructure/SqliteIncidentRepository.cs:40-63; src/Enrollment.Infrastructure/SqliteRoleRepository.cs:62-84)
- DDL completo de las colecciones vectoriales depende del proveedor SqliteVec y no está expresado íntegramente como SQL del repositorio. Fragmento en docs/.work/questions/datos.md. (src/Enrollment.Infrastructure/SqliteVecMemberRepository.cs:14-20,59-60; src/Enrollment.Infrastructure/SqliteVecMemberImageRepository.cs:19-21,50-51)
- Archivos y registros SQLite se escriben en recursos distintos, de modo que una falla intermedia puede dejar archivo sin fila o fila sin archivo. Fragmento en docs/.work/questions/datos.md. (src/Enrollment.Infrastructure/SqliteVecMemberImageRepository.cs:12-17,36-48)
- Los tests tienen esquemas de prueba, pero no se verificó su paridad con un DDL de producción. Fragmento en docs/.work/questions/datos.md.
