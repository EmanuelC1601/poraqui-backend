# Estado del proyecto

Actualizado: 2026-10-17. `main` es la base de integración, con un solo responsable. Los incrementos salen de `main` en ramas `feature/*` y se integran mediante PR. Contratos persistidos, `AppDbContext` y migraciones tienen un propietario único. La antigua asignación de OpenCode queda solo como antecedente; no hay dependencia operativa.

## Ledger verificable

| Estado | Capacidad | Evidencia / límite |
|---|---|---|
| Integrado | Alta/revocación de rol de courier y perfil en transacción; las operaciones API de reparto y SignalR consultan roles vigentes, de modo que tokens anteriores no bastan tras revocar | PR #90, merge `978638c`; pruebas API SQLite cubren ofertas externas, lista de pedidos y suscripciones a pedido/ofertas después de revocar. Sin cambios de esquema. |
| Rama publicada pendiente de PR | Al revocar un repartidor externo, sus ofertas `Offered` se cancelan en la misma transacción y la conexión vigente recibe el aviso después del commit | `feature/revoke-courier-offers`; prueba API SQLite confirma estado Cancelled persistido, aviso SignalR y denegación posterior. Build Release limpio; suite 47 aprobadas/8 SQL omitidas por configuración pendiente. Sin cambios de esquema. |
| Integrado; cobertura funcional parcial | Clean Architecture, Identity, roles y autorización por relación con negocio/pedido; comercios, catálogo y opciones, zonas, tarifas, horarios y precios calculados por servidor | PR #1, #22–#33; prueba API integral sobre SQLite; ver requisitos SQL y hosting pendientes |
| Integrado | Asignación de repartidor interno comprueba primero acceso al pedido/negocio antes de consultar el rol del usuario objetivo | PR #88, merge `914d291`; prueba API confirma `403` para propietario ajeno incluso con courier ID inexistente, evitando sondeo del rol de otros usuarios |
| Integrado; cobertura funcional parcial | Pedido, comprobante privado, revisión manual de transferencia, decisión del negocio, preparación, repartidor propio/externo, ofertas, códigos, contacto autorizado e historial | Prueba amplia SQLite y recorridos API SQL Server de reparto interno, externo y confirmación offline (PR #63–#65); suite SQL Server aislada 39/39 tras PR #67; límites en `docs/order-flow.md` |
| Integrado; cancelación del cliente previa a aceptación cubierta | El cliente cancela en `PendingPayment`, `AwaitingPaymentReview` o `AwaitingBusinessDecision`; con comprobante, crea seguimiento de reembolso manual. Reintento idéntico no duplica historial/caso y después de aceptar el pedido se rechaza | PR #73, merge `6e269c6`; prueba de dominio y flujo API; respuesta de cancelación devuelve `refundStatus` |
| Integrado; cobertura funcional parcial | Sincronización offline validada por servidor, SignalR autorizado con snapshot actual, cancelaciones, incidencias, reembolsos manuales e idempotencia | Prueba integral SQLite; las notificaciones de WhatsApp/llamada son datos de contacto para acción humana, no mensajes automáticos |
| Integrado | Idempotencia de pedido, comprobante, asignación interna, incidentes, entrega offline y aceptación de oferta externa; códigos protegidos persistidos | PR #29–#31, #47 y #67; replay retorna resultado estable y payload diferente produce conflicto |
| Integrado; concurrencia e invariantes verificados en SQL Server | Restricciones e índices y control optimista para aceptación/asignación, idempotencia, importes y cantidades válidos | PR #32, #58, #62 y #67; migraciones `EnforceSqlDomainInvariants` y `UseProviderSafeOrderAmountConstraint` agregan 10 restricciones `CHECK` para importes, cantidades, horarios, disponibilidad y ofertas aceptadas. `SqlServerDomainInvariantTests` comprobó rechazo de violaciones en SQL Server; cinco pruebas de concurrencia cubren asignación, aceptación y replays simultáneos. |
| Integrado | Ofertas externas por zona, rechazo, expiración, cancelación, courier ausente y SignalR privado tras persistir | PR #43–#46; pruebas API con SQLite |
| Integrado | `GET /api/admin/metrics/pilot`, restringido a PlatformAdmin | PR #33; permisos, límites de rango y valores agregados probados; query traducido por provider SQL Server sin conexión real |
| Integrado | Endpoints anónimos de liveness y readiness SQL | PR #84, merge `a38ee0a`; `/health/live` informa proceso activo y `/health/ready` responde 200 solo cuando puede conectar a la base configurada, o 503 sin exponer detalles; prueba 503 cuando no hay cadena SQL y contrato OpenAPI probado |
| Integrado; SMS real pendiente | OTP Twilio Verify, E.164, confirmación Identity y límite de 5 intentos/10 min/IP | PR #36; pruebas HTTP simuladas. Requiere secretos Twilio privados para comprobar entrega real |
| Integrado | Snapshots SignalR al reconectar y ofertas actuales al suscribirse | PR #38 y #45–#46; reconexión y destinatarios cubiertos por pruebas |
| Artefacto IIS verificado; hosting pendiente | Guía de despliegue MonsterASP.NET y recorrido reproducible | PR #86, merge `90b177d`; run `39092852859` publicó un paquete de 5,679,063 bytes con expiración a 14 días; aún no se ha publicado ni verificado el sitio |
| Integrado; migraciones aplicadas a desarrollo | 15 migraciones EF integradas, incluida `ExternalOfferAcceptancePickupCode` e invariantes SQL | `dotnet-ef migrations list` confirmó las 15 aplicadas en `db74183`; la revisión read-only previa verificó cero filas que incumplieran las nuevas restricciones. |
| Integrado | Actualización del target a .NET 10 LTS y EF Core 10 | PR #51, merge `5078005`; no cambia el modelo ni genera migraciones |
| Integrado; guard de startup verificado | Configuración privada en Production | PR #69 exige `PrivateFiles:Root` fuera de `wwwroot` y comprueba creación/acceso en startup. PR actual exige además `DataProtection:KeysPath` privada y fuera de `wwwroot`, necesaria para descifrar códigos protegidos tras reinicios; el roundtrip entre proveedores prueba recuperación tras reinicio. Suite Release 48/48 (siete SQL Server aisladas). No cambia esquema. |
| Pendiente; guard de configuración implementado | Publicar y comprobar API, SignalR y almacenamiento privado en MonsterASP.NET | PR #69 valida `PrivateFiles:Root`; PR actual valida `DataProtection:KeysPath`, ambos fuera de `wwwroot` y comprobados en startup de Production; falta elegir sitio y proporcionar las rutas persistentes privadas admitidas por el plan |
| Pendiente | Verificar OTP Twilio extremo a extremo | Se necesitan `TwilioVerify:ServiceSid`, `TwilioVerify:ApiKeySid`, `TwilioVerify:ApiKeySecret` en configuración privada y un teléfono de prueba autorizado |
| Verificado; esquema actualizado | Acceso y esquema de la BD compartida de desarrollo | User Secrets contiene `ConnectionStrings:DefaultConnection`; hay 15 migraciones aplicadas. En el incremento de invariantes se consultaron únicamente conteos agregados antes de migrar y se verificaron las diez restricciones después. |
| Pendiente por configuración de hosting | Despliegue y replays de códigos tras reinicios | Esta copia no tiene `PrivateFiles:Root` ni `DataProtection:KeysPath`; el sitio MonsterASP aún requiere rutas persistentes apropiadas para archivos y claves. |
| Fuera de alcance | GPS, distancias inferidas, transferencias bancarias automáticas y frontend | Se usan zonas/tarifas y revisión/pago manual |

## Verificación vigente tras PR #70

PR #70 (`11f1717`) quedó integrado en `main` como merge `461965c`. La base de integración exige un directorio privado persistente para archivos y claves de Data Protection en Production; no cambia el esquema. La comprobación de modelo no requiere nueva migración.

SDK .NET 10.0.402 instalado localmente en `work/dotnet10` (el SDK global de esta estación sigue en .NET 9). `dotnet test Poraqui.sln --configuration Release --no-build`, exponiendo `ConnectionStrings:SqlServerTest` de User Secrets solo como variable de entorno del proceso, terminó en **48 aprobadas, 0 fallidas y 0 omitidas** (1 min 25 s). Incluye las siete pruebas SQL Server en la base aislada `db74230`; el test de recorrido interno llamó a `MigrateAsync` y confirmó que el esquema ya estaba al día, y limpió sus registros identificables. El panel MonsterASP confirmó Remote habilitado y su test de conectividad devolvió OK. La BD compartida no fue destino de esas pruebas ni recibió migraciones.

En esta copia, User Secrets tiene `ConnectionStrings:DefaultConnection` y `ConnectionStrings:SqlServerTest`; solo se consultaron nombres de clave. La lectura de `dotnet-ef migrations list` con la primera confirmó las 13 migraciones aplicadas. Siguen ausentes `PrivateFiles:Root`, `DataProtection:KeysPath` y `TwilioVerify:ServiceSid`, `TwilioVerify:ApiKeySid`, `TwilioVerify:ApiKeySecret`; el destino de sitio MonsterASP y sus rutas persistentes tampoco se han elegido. El envío SMS real y hosting continúan pendientes.

## Invariantes SQL de pedidos y catálogo

Rama `feature/sql-domain-invariants`: `20261018020605_EnforceSqlDomainInvariants` agrega diez restricciones `CHECK` aditivas. La migración `20261018021039_UseProviderSafeOrderAmountConstraint` reemplaza la comprobación entre columnas decimales por no negatividad individual, compatible con SQLite y SQL Server. Los SQL generados se conservan en `docs/sql/`; el modelo EF y ambos destinos contienen las 15 migraciones.

Antes de aplicar en `db74183`, una consulta read-only encontró cero filas inválidas en productos, opciones, tarifas, pedidos, líneas, ofertas, disponibilidad, reembolsos y horarios. Se revisaron ambos scripts: solo agregan/reemplazan restricciones y registran la migración. Tras aplicarlas, una consulta de `sys.check_constraints` confirmó las diez y los conteos agregados siguieron en cero. No se modificaron importes ni otros datos funcionales.

Build Release: 0 advertencias y 0 errores. Suite completa Release con `ConnectionStrings__SqlServerTest` privada en proceso: **49 aprobadas, 0 fallidas, 0 omitidas**, SQLite más SQL Server aislado `db74230`; `SqlServerDomainInvariantTests` provoca violaciones y confirma errores 547 del servidor. `dotnet-ef migrations has-pending-model-changes` sin cambios pendientes. Hosting y SMS real siguen pendientes.

## Cancelación por cliente antes de aceptación

PR #73 (`cdb061b`) quedó integrado en `main` como commit `6e269c6`. El cliente ahora puede cancelar hasta que el negocio acepte. Si había comprobante pendiente de revisión o transferencia aprobada, la cancelación abre el caso de revisión manual del reembolso. `OrderWorkflowView` devuelve `refundStatus` también en la respuesta inmediata. La regla y el flujo API validan reintentos, el estado pagado, el caso de reembolso y la prohibición después de aceptar. No cambia el esquema ni ejecuta migraciones. Suite Release completa: 49 aprobadas, 0 fallidas, 0 omitidas (SQLite y SQL Server aislado `db74230`); CI del PR pasó.

## Verificaciones de .NET 10 integradas por PR #51

Commit `05d5410`, integrado en `main` como `5078005`:

- `dotnet build Poraqui.sln --no-restore` con SDK local 10.0.402: 0 advertencias, 0 errores.
- `dotnet test Poraqui.sln --no-restore`: 31 pasaron; 2 pruebas SQL Server omitidas por falta de configuración aislada.
- `dotnet list Poraqui.sln package --vulnerable --include-transitive`: ningún paquete vulnerable en los cinco proyectos.
- `dotnet-ef migrations has-pending-model-changes`: no hay cambios de modelo.
- `dotnet-ef migrations list`: conexión privada de desarrollo comprobada en lectura; 13 migraciones y ninguna pendiente.

Los tests API usan SQLite en memoria y almacenamiento temporal. No prueban SQL Server real, Twilio ni el entorno MonsterASP.NET. La API no aplica migraciones al iniciar. Antes de aplicar cambios de esquema, revisar SQL generado y el historial de destino; nunca ejecutar limpiezas ni recreaciones sobre la base compartida.

## Configuración privada de la prueba SQL

La base de pruebas aislada identificada en MonsterASP es `db74230`; el panel muestra Remote habilitado. En esta copia de trabajo, User Secrets contiene `ConnectionStrings:DefaultConnection`, pero no `ConnectionStrings:SqlServerTest`. Para volver a ejecutar los cinco tests SQL Server, guarda la cadena de conexión Remote de `db74230` solo en User Secrets (`ConnectionStrings:SqlServerTest`) y el runner la expondrá temporalmente como `ConnectionStrings__SqlServerTest`. El guard de pruebas compara ambos catálogos y bloquea la conexión si son iguales. No añadas valores a archivos versionados ni uses la BD compartida como destino de pruebas.

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

PR #62, integrado como merge `7ac25f8`, añade `SqlServerOrderCreationIdempotencyTests`: fuerza dos solicitudes con igual `commandId` a llegar juntas a `SaveChanges`, y verifica una sola orden/comando persistidos y respuestas iguales, incluido el código de entrega.

PR #63 se integró como merge `cb86962`. Añade prueba API en SQL Server para el recorrido interno de negocio, comprobante, revisión de pago, preparación, repartidor propio, pago al recoger, contacto y entrega. GitHub Actions pasó; la validación local previa pasó 36/36 con cuatro pruebas SQL Server. No hay migración nueva ni cambios a la BD compartida.

PR #64 se integró como merge `fe37ce7`. Añade un recorrido API de reparto externo contra SQL Server aislado, con disponibilidad, oferta visible antes de aceptar, aceptación repetible, códigos, pago del negocio al recoger, contacto autorizado y entrega. GitHub Actions pasó; la suite Release local pasó 37/37 con cinco pruebas SQL Server. No cambia modelo ni migraciones, y la BD compartida no se tocó.

PR #65 se integró en `main` como merge `be7da78`. Cambia el final de la prueba externa SQL Server a sincronización offline y verifica rechazo de código incorrecto, aceptación del código real, replay idempotente y conflicto ante reutilización del comando con payload distinto. Los dos recorridos API serializan su uso de la base aislada y crean admins de prueba identificados. GitHub Actions pasó. Antes de integrar, el build Release local pasó con 0 advertencias/errores y la suite configurada con SQL Server aislado pasó 37/37; la BD compartida no se modificó.

Verificación del PR #67, integrado en `main` como merge `2c50be9`: con `ConnectionStrings:SqlServerTest` desde User Secrets y comprobación local de catálogo `db74230`, build Release pasó con 0 advertencias y 0 errores; suite Release pasó 39/39, incluidas las siete pruebas SQL Server. GitHub Actions también pasó. Las pruebas usaron la base aislada autorizada; la BD compartida no recibió cambios. Remote está habilitado solo para la BD de prueba `db74230`.

## Replay idempotente en carreras SQL

PR #67, integrado en `main` como merge `2c50be9`, cubre reporte de incidencia y confirmación de entrega offline. Si dos solicitudes concurrentes con el mismo actor y `commandId` compiten por la restricción única, la solicitud perdedora recupera el resultado persistido; el replay idéntico no vuelve a publicar evento. Un payload distinto devuelve conflicto. Las dos pruebas SQL Server fuerzan la carrera en `SaveChanges` y verifican un solo registro e historial. No hay cambios al modelo EF ni migraciones en este incremento.

## Guard del almacenamiento de comprobantes en Production

Rama `feature/require-private-evidence-storage`: la API rechaza el arranque en `Production` (con SQL configurado) si falta `PrivateFiles:Root` o la ruta cae dentro de `wwwroot`; después de construir el host resuelve el servicio para crear/verificar el directorio. Esto evita arrancar con el fallback a `LocalApplicationData`, cuya persistencia no está garantizada por MonsterASP. No prueba persistencia tras reinicios del hosting.

Verificación local en Release: build con 0 advertencias y 0 errores; 4 pruebas unitarias del guard pasaron y suite completa pasó 43/43, incluidas siete pruebas SQL Server sobre la base aislada `db74230`. No se generó migración ni se modificó la BD compartida. En esta copia siguen ausentes `ConnectionStrings:DefaultConnection`, `PrivateFiles:Root` y los tres valores privados `TwilioVerify:*`; el sitio MonsterASP aún requiere una ruta persistente autorizada y elegir el destino de publicación.

## Contrato y autorización de cancelación (PR #76–#77)

PR #76 se integró en `main` como `e91298d`: OpenAPI describe la respuesta tipada de cancelación (`OrderWorkflowView`, incluido `refundStatus`), los errores y la regla previa a la aceptación; `docs/openapi.json` refleja el documento generado. PR #77 se integró como `ade461f` y comprueba que otro cliente recibe `403` y no modifica el pedido ni crea reembolso. Ambos pasaron CI. Pruebas locales pertinentes: contrato OpenAPI 2/2; recorrido API SQLite 1/1. Suite local sin `ConnectionStrings__SqlServerTest`: 42 aprobadas, 8 pruebas SQL Server omitidas; no se ejecutaron migraciones ni hubo escrituras SQL.

PR #79, merge `96f31a0`, refuerza el replay de cancelación en el recorrido API: la segunda solicitud mantiene el estado, `refundStatus`, cantidad de historial y una sola transición `Cancelled`. Prueba enfocada SQLite 1/1 y CI aprobado. No hay migraciones ni cambios SQL.

## Verificación local más reciente (2026-10-17)

Después de PR #81, `dotnet test Poraqui.sln --configuration Release --no-restore` compiló la solución y pasó **50/50 pruebas**, cero fallidas y cero omitidas (42 con SQLite y 8 con SQL Server aislado `db74230`). La cadena `ConnectionStrings:SqlServerTest` se leyó de User Secrets y se expuso solo al proceso; no se imprimió ni versionó. El arnés compara el catálogo de test con `DefaultConnection`. Estas pruebas ejecutaron migraciones únicamente en la base aislada; la BD compartida `db74183` no fue modificada.

La suite incluye concurrencia de aceptación y comandos. Esta ejecución local no sustituye CI, una verificación en MonsterASP, SMS real o pruebas con WhatsApp. La publicación sigue pendiente hasta configurar sitio, rutas persistentes y servicios privados.

PR #84 se integró en `main` como `a38ee0a`. La suite Release completa pasó **53/53**, incluidas las ocho pruebas SQL Server de `db74230`; tres pruebas nuevas usan SQLite para readiness con conexión, readiness sin cadena y contrato OpenAPI. La BD compartida no fue modificada.
