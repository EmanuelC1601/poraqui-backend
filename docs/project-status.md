# Estado del proyecto

Actualizado: 2026-10-17. `main` es la base de integración, con un solo responsable. Los incrementos salen de `main` en ramas `feature/*` y se integran mediante PR. Contratos persistidos, `AppDbContext` y migraciones tienen un propietario único. La antigua asignación de OpenCode queda solo como antecedente; no hay dependencia operativa.

## Ledger verificable

| Estado | Capacidad | Evidencia / límite |
|---|---|---|
| Integrado; cobertura funcional parcial | Clean Architecture, Identity, roles y autorización por relación con negocio/pedido; comercios, catálogo y opciones, zonas, tarifas, horarios y precios calculados por servidor | PR #1, #22–#33; prueba API integral sobre SQLite; ver requisitos SQL y hosting pendientes |
| Integrado; cobertura funcional parcial | Pedido, comprobante privado, revisión manual de transferencia, decisión del negocio, preparación, repartidor propio/externo, ofertas, códigos, contacto autorizado e historial | Prueba `BusinessApiTests.Manual_payment_and_internal_delivery_flow_preserves_server_pricing_and_history`; `docs/order-flow.md` describe su ejecución y límites |
| Integrado; cobertura funcional parcial | Sincronización offline validada por servidor, SignalR autorizado con snapshot actual, cancelaciones, incidencias, reembolsos manuales e idempotencia | Prueba integral SQLite; las notificaciones de WhatsApp/llamada son datos de contacto para acción humana, no mensajes automáticos |
| Integrado | Idempotencia de pedido, comprobante, asignación interna, incidentes y aceptación de oferta externa; códigos protegidos persistidos | PR #29–#31 y #47; replay retorna resultado estable y payload diferente produce conflicto |
| Integrado; concurrencia verificada en SQL Server aislado | Restricciones e índices y control optimista para limitar la aceptación/asignación concurrente a un courier; reintentos simultáneos de creación idempotente de pedidos | PR #32, PR #58 y PR #62; consulta SQL Server confirmó 27 tablas, 43 claves foráneas, 40 índices únicos, PK en las 27 tablas, índice único filtrado para una oferta aceptada por pedido y dos columnas `Version`. Tres pruebas de concurrencia en la BD aislada cubren asignación interna, aceptación externa y creación de pedidos con el mismo `commandId`. No hay restricciones `CHECK` |
| Integrado | Ofertas externas por zona, rechazo, expiración, cancelación, courier ausente y SignalR privado tras persistir | PR #43–#46; pruebas API con SQLite |
| Integrado | `GET /api/admin/metrics/pilot`, restringido a PlatformAdmin | PR #33; permisos, límites de rango y valores agregados probados; query traducido por provider SQL Server sin conexión real |
| Integrado; SMS real pendiente | OTP Twilio Verify, E.164, confirmación Identity y límite de 5 intentos/10 min/IP | PR #36; pruebas HTTP simuladas. Requiere secretos Twilio privados para comprobar entrega real |
| Integrado | Snapshots SignalR al reconectar y ofertas actuales al suscribirse | PR #38 y #45–#46; reconexión y destinatarios cubiertos por pruebas |
| Integrado; paquete Release verificado, hosting pendiente | Guía de despliegue MonsterASP.NET y recorrido reproducible | PR #39 y #49–#50; `dotnet publish` genera output IIS con `web.config`, aún no hay publicación ni verificación de hosting |
| Integrado; migraciones aplicadas a desarrollo | 13 migraciones EF integradas, incluida `ExternalOfferAcceptancePickupCode` | `dotnet-ef migrations list` conectado en lectura el 2026-10-17 mostró 13 migraciones, ninguna pendiente. La más reciente solo añadió `ProtectedPickupCode nvarchar(2000) NULL` |
| Integrado | Actualización del target a .NET 10 LTS y EF Core 10 | PR #51, merge `5078005`; no cambia el modelo ni genera migraciones |
| Pendiente | Publicar y comprobar API, SignalR y almacenamiento privado en MonsterASP.NET | Se necesita sitio destino, variables privadas de producción y ruta persistente autorizada |
| Pendiente | Verificar OTP Twilio extremo a extremo | Se necesitan `TwilioVerify:ServiceSid`, `TwilioVerify:ApiKeySid`, `TwilioVerify:ApiKeySecret` en configuración privada y un teléfono de prueba autorizado |
| Fuera de alcance | GPS, distancias inferidas, transferencias bancarias automáticas y frontend | Se usan zonas/tarifas y revisión/pago manual |

## Verificaciones de .NET 10 integradas por PR #51

Commit `05d5410`, integrado en `main` como `5078005`:

- `dotnet build Poraqui.sln --no-restore` con SDK local 10.0.402: 0 advertencias, 0 errores.
- `dotnet test Poraqui.sln --no-restore`: 31 pasaron; 2 pruebas SQL Server omitidas por falta de configuración aislada.
- `dotnet list Poraqui.sln package --vulnerable --include-transitive`: ningún paquete vulnerable en los cinco proyectos.
- `dotnet-ef migrations has-pending-model-changes`: no hay cambios de modelo.
- `dotnet-ef migrations list`: conexión privada de desarrollo comprobada en lectura; 13 migraciones y ninguna pendiente.

Los tests API usan SQLite en memoria y almacenamiento temporal. No prueban SQL Server real, Twilio ni el entorno MonsterASP.NET. La API no aplica migraciones al iniciar. Antes de aplicar cambios de esquema, revisar SQL generado y el historial de destino; nunca ejecutar limpiezas ni recreaciones sobre la base compartida.

## Configuración privada de la prueba SQL

`ConnectionStrings:SqlServerTest` está configurada solo en User Secrets locales con la cadena Remote de la base aislada; el proceso de test la expone temporalmente como `ConnectionStrings__SqlServerTest`. Remote está habilitado solo para esa base. Los tests leen además `ConnectionStrings:DefaultConnection` de User Secrets para rechazar el catálogo compartido. No añadas valores a archivos versionados ni uses la base compartida como destino de pruebas.

Verificación repetida tras integrar PR #52 (`edac6bf`): SDK local 10.0.402, compilación con 0 advertencias/errores, 31 tests aprobados y las dos carreras SQL omitidas porque falta `ConnectionStrings__SqlServerTest`. `dotnet-ef migrations list` conectó en lectura a la BD de desarrollo privada y mostró las 13 migraciones integradas sin pendientes. No se ejecutaron pruebas mutables ni migraciones en esta comprobación.

Validación de esquema del 2026-10-17: una consulta directa de solo lectura mediante `ConnectionStrings:DefaultConnection` privada confirmó 13 filas de historial de migraciones y el esquema anterior. No leyó datos funcionales ni modificó tablas. La presencia del índice filtrado y columnas de versión no sustituye ejecutar aceptación simultánea en SQL Server aislado; esa verificación continúa pendiente.

## Corrección del contrato OpenAPI por PR #56

PR #56 se integró en `main` como merge `8f637d6` (commit de trabajo `fbe5803`). La especificación generada ahora describe el comprobante como `multipart/form-data`, con `commandId` UUID y archivo binario obligatorios, respuesta `202` con `replayed` y los estados de error observados. `OpenApiContractTests` comprueba esos campos y respuestas; `docs/openapi.json` coincide con el documento emitido en ejecución y contiene 61 rutas.

Validación ejecutada antes de integrar PR #56 con SDK .NET 10.0.402: build correcto con 0 advertencias y 0 errores; 32 pruebas pasaron y las 2 pruebas de concurrencia SQL Server quedaron omitidas porque falta `ConnectionStrings__SqlServerTest`; `git diff --check` pasó. La consulta SQL Server de solo lectura más reciente confirmó conexión privada y las 13 migraciones aplicadas, hasta `20261017234424_ExternalOfferAcceptancePickupCode`. No se ejecutaron migraciones en esta actualización.

## Estado vigente tras PR #58

PR #58 (`d38493f`) se integró en `main` como merge `f9fa0d2`; corrige la limpieza de entidades con versiones de fila desactualizadas en el test SQL de aceptación concurrente.

Con SDK .NET 10.0.402, `dotnet test Poraqui.sln --no-restore` pasó con 32 aprobadas y 2 omitidas (concurrencia SQL no está configurada para el proceso predeterminado). Al configurar temporalmente `ConnectionStrings__SqlServerTest` desde User Secrets, las dos pruebas SQL Server de asignación interna y aceptación externa simultáneas pasaron en una base aislada; la base estaba vacía antes, y solo ella recibió migraciones desde el test. La cadena de test es distinta a `DefaultConnection` y nunca se añadió al repositorio. `dotnet-ef migrations list` volvió a conectarse en modo de consulta a la BD compartida privada y mostró las 13 migraciones integradas. No se aplicó migración a la BD compartida en esta actualización. No hay checks de CI definidos para PR #58.

La concurrencia con SQL Server deja de estar pendiente. Siguen pendientes la publicación/validación en MonsterASP.NET, el comprobante en almacenamiento persistente del hosting y la prueba de entrega SMS real con credenciales Twilio privadas.

## Preparación de build y publicación

Se agregó `.github/workflows/ci.yml` para restaurar, compilar Release y ejecutar pruebas en push/PR contra `main`. Sin secretos SQL en CI, las dos pruebas de concurrencia se omiten por diseño; se verificaron por separado en la BD aislada. El build/test Release local pasó con 0 advertencias, 0 errores, 32 aprobadas y 2 omitidas. `dotnet publish src/Poraqui.Api/Poraqui.Api.csproj --configuration Release --no-restore --output work/publish` produjo el paquete web con `Poraqui.Api.dll`, `web.config`, `.deps.json` y `.runtimeconfig.json`; `work/publish` está ignorado por Git. Esto no prueba ejecución bajo IIS ni publicación real.

## Estado de CI y hosting tras PR #60

PR #60 quedó integrado en `main` con merge `cb11b72` (commits `1480c05` y `47a31f9`). El workflow usa `ubuntu-24.04`, `actions/checkout@v5` y `actions/setup-dotnet@v5`. La ejecución de GitHub Actions `39084197135` finalizó correctamente; el job restauró, compiló y probó la solución. Las pruebas SQL siguen omitidas en CI porque no se exponen secretos de bases de datos al runner.

Reejecución local en Release con SDK .NET 10.0.402 y `ConnectionStrings:SqlServerTest` privada desde User Secrets: `dotnet test Poraqui.sln --configuration Release --no-restore` aprobó las 34 pruebas (0 omitidas), incluidas las dos pruebas SQL Server. Ese proceso usó exclusivamente la base aislada de test; no aplicó cambios a la base compartida.

La prueba SQL aislada continúa verificada con Remote habilitado solo para la BD `db74230`; dos pruebas de concurrencia aprobaron allí. No se modificó la base compartida en esta actualización. El panel MonsterASP lista cuatro sitios existentes, ninguno identificado de forma inequívoca como Poraqui; por eso el backend no se ha publicado. Para continuar el despliegue se necesita elegir el sitio destino y sus variables privadas/ruta persistente. El envío SMS real también requiere la configuración privada Twilio y un teléfono de prueba autorizado.

La rama `feature/sql-order-idempotency-concurrency` añade `SqlServerOrderCreationIdempotencyTests`: fuerza dos solicitudes con igual `commandId` a llegar juntas a `SaveChanges`, y verifica una sola orden/comando persistidos y respuestas iguales, incluido el código de entrega. Suite Release local: 35 aprobadas, 0 omitidas con la base aislada configurada; no hubo cambios al modelo EF ni migraciones.
