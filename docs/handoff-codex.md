# Estado y entrega de Codex
## Configuración de pruebas SQL pendiente en la sesión actual

El usuario dejó aplazada la conexión de la base aislada `db74230`; no se debe volver a solicitar en cada incremento. La última ejecución documentada con esa conexión pasó las pruebas SQL en fechas anteriores, pero no acredita disponibilidad actual. El nombre de clave `ConnectionStrings:SqlServerTest` aparece en User Secrets; no se leyó el valor ni se usó. La suite Release ejecutada después del PR #129 aprobó 62 pruebas, no tuvo fallos y omitió nueve verificaciones SQL Server porque el usuario aplazó la configuración de la base de pruebas. Al reanudar, validar la configuración privada y correr las pruebas dirigidas; nunca usar la conexión compartida para pruebas mutables. No se ejecutaron migraciones en esta actualización. La consulta `dotnet ef migrations list` usó `ConnectionStrings:DefaultConnection` y devolvió 16 migraciones sin pendientes, solo en lectura. La revisión del sync offline confirmó que la hora del dispositivo es solo auditoría: el servidor vuelve a validar asignación, estado y código, y la idempotencia compara el payload. Ver [project-status.md](project-status.md).

PR #129 corrigió el orden de persistencia y notificación de expiración de ofertas externas; la prueba API SQLite verificó que el evento `Expired` ya observa el estado persistido. PR #127 integró rate limit al bootstrap de PlatformAdmin: cinco solicitudes cada 10 minutos por IP, con 429 al exceder; OpenAPI y prueba API están actualizados. Pruebas enfocadas: 3/3; suite Release completa: 62 aprobadas, 0 fallidas y 9 SQL Server omitidas por configuración aplazada. No se ejecutaron migraciones ni hubo escrituras SQL.

PR #123 se integró en `main` como `b22a545`; PR #124 y #125 también pasaron sus checks e integraron pruebas de almacenamiento y estado de despliegue. `main` integra PR #129 en `754a8ab` y PR #128 documental en `de5169e`. El CI post-merge de PR #129 pasó en el run `39723167300` y publicó `poraqui-api-iis-754a8ab0005b24b4889fa0f326059dbc6eb0ec5a` (5,690,338 bytes; vence 2026-11-05). PR #127 quedó integrado en `2ccd9b5`; su CI pasó en el run `39722353246` y publicó el artefacto IIS de ese commit. En `feature/transfer-evidence-storage-tests`, las pruebas enfocadas pasan 9/9 y la suite Release pasa 60 pruebas con 9 pruebas SQL omitidas. Las pruebas usan SQLite y filesystem temporal; la consulta de migraciones fue de solo lectura y no se aplicaron migraciones.

## Flujo de trabajo

El backend tiene un único responsable. `main` es la base de integración; cada incremento parte de `main` actualizado en una rama `feature/*`, se verifica y se integra mediante PR. Contratos, `AppDbContext` y migraciones tienen un solo propietario. La asignación anterior a OpenCode queda como antecedente en `docs/team-work.md`.

## Cambios integrados

PR #1 y los incrementos posteriores están integrados en `main`. El código contiene identidad y permisos por rol/recurso, comercio y catálogo, pedidos con precios del servidor, comprobantes privados y revisión manual, estados de preparación, reparto interno y externo, ofertas, expiración/cancelación, códigos, contacto autorizado, historial, idempotencia, sincronización offline validada por el servidor, SignalR, incidencias, reembolsos manuales, OTP y métricas piloto. Tras PR #102, la suite Release local más reciente aprobó **59/59 pruebas**, incluidas nueve pruebas SQL Server en la base aislada configurada en User Secrets; no hubo fallos ni omitidas. CI del PR y post-merge pasaron; el runner de GitHub no recibe cadena de BD. Esto no acredita servicios externos ni hosting reales. Ver [ledger](project-status.md) y [recorrido reproducible](order-flow.md).

El proyecto contiene 16 migraciones EF. `dotnet-ef migrations list` confirmó las 16 aplicadas en la base de desarrollo, incluida `UniqueActiveExternalOfferPerCourier`. La consulta fue de solo lectura; la suite SQL Release migra solo la base aislada. Los registros históricos de integración y despliegue conservan los conteos vigentes cuando se ejecutaron. No se aplica migración al iniciar la API.

PR #49 documenta el test integral reproducible y sus límites. PR #50 distingue su configuración aislada de la configuración privada requerida para ejecutar la API manualmente. PR #52 (`edac6bf`) registra la integración de .NET 10 y mantiene explícitos los pendientes.

## Incrementos posteriores

PR #51 se integró en `main` como merge `5078005` (commit de trabajo `05d5410`). Actualiza proyectos y paquetes de .NET/EF Core a .NET 10 LTS, SQL Client 7.1.1 y PKCS 10.0.13. No cambia contratos API ni el modelo EF y no genera migraciones.

Validación local con SDK 10.0.402:

- `dotnet build Poraqui.sln --no-restore`: 0 advertencias, 0 errores.
- `dotnet test Poraqui.sln --no-restore`: 31 pasaron; 2 pruebas de concurrencia SQL omitidas por falta de `ConnectionStrings__SqlServerTest`.
- `dotnet list Poraqui.sln package --vulnerable --include-transitive`: ningún paquete vulnerable.
- `dotnet-ef migrations has-pending-model-changes`: no hay cambios de modelo.
- `dotnet-ef migrations list`: 13 migraciones aplicadas, ninguna pendiente (consulta de solo lectura).

Revalidación tras PR #52 con el SDK local 10.0.402: build con 0 advertencias/errores; 31 tests pasaron y 2 carreras SQL se omitieron; listado de migraciones de desarrollo sin pendientes. La prueba SQL requiere que `ConnectionStrings__SqlServerTest` exista en el entorno del proceso de test; `ConnectionStrings:DefaultConnection` se lee de User Secrets. Una consulta directa de solo lectura al esquema SQL Server confirmó 27 tablas, 43 claves foráneas, 40 índices únicos, clave primaria para cada tabla, índice filtrado de una oferta aceptada por pedido y las dos columnas de versión; no hay restricciones `CHECK`. Esto confirma el esquema persistido, no el resultado de carreras simultáneas.

## Pendientes y bloqueos

1. Publicar en un sitio MonsterASP.NET con sus variables privadas y almacenamiento persistente; validar health, SignalR y comprobante privado.
2. Configurar secretos Twilio en privado y probar el envío OTP con un teléfono autorizado.

## Incremento en revisión: unicidad de ofertas activas

La rama `feature/unique-active-external-offer` añade un índice único filtrado para impedir dos ofertas `Offered` para el mismo pedido/courier y convierte colisiones al despachar concurrentemente en HTTP 409. El despacho persiste primero las expiraciones del pedido para que ofertas vencidas fuera de la ventana del barrido no bloqueen el redispatch. Incluye cobertura API SQLite para expiración/redispatch y prueba SQL Server de despacho simultáneo. La migración revisable `20261021061647_UniqueActiveExternalOfferPerCourier` y su script están incluidos en el cambio. En `db74183`, una consulta previa encontró cero pares duplicados activos; la migración fue aplicada explícitamente y una consulta posterior confirmó índice, registro de migración y cero duplicados. No eliminó ni actualizó datos funcionales.

PR #102 se integró en `main` como merge `012c64e`. Tras incorporar `main` hasta `b47ff54`, se ejecutó `dotnet test Poraqui.sln --configuration Release --no-restore` con `ConnectionStrings:SqlServerTest` cargada desde User Secrets solo al proceso: **59 aprobadas, 0 fallidas, 0 omitidas**. La prueba SQL de dispatch simultáneo y la de aceptación concurrente pasaron; el loader comparó el catálogo de test con `DefaultConnection` antes de conectar. La CI pre-merge del head `dff51dd` (run `39646313838`) y el CI post-merge (run `39647404852`) pasaron. La migración `20261021061647_UniqueActiveExternalOfferPerCourier` es aditiva y no borró ni reescribió datos funcionales.

No se publican secretos ni se toman credenciales del chat. El estado verificable vigente está en `docs/project-status.md`.

## Integrado: PR #103, teléfono confirmado para contacto de entrega

PR #103 quedó integrado en `main` como merge `c3270da`. `OrderWorkflowRepository.GetCustomerPhoneAsync` ahora devuelve solo teléfonos con `PhoneNumberConfirmed=true`. `BeginDeliveryAsync` ya restringe el acceso al courier asignado; este filtro evita revelar un teléfono no confirmado. Se actualizó el contrato; no requiere migración ni cambia el esquema.

Prueba SQLite enfocada: **1/1**; suite Release completa: **49 aprobadas, 0 fallidas, 8 omitidas** (integraciones SQL Server, configuración aislada aplazada); CI `build-and-test` del PR pasó antes de integrar. No hubo escrituras en SQL Server ni migraciones.

## Integrado: PR #106, teléfono no confirmado sin transición parcial

PR #106 se integró en `main` como merge `3c32c68`. `BusinessApiTests` verifica inicio de entrega con teléfono desconfirmado: el endpoint responde `409`, el mensaje no filtra el teléfono y `GET /api/orders/{id}` conserva `PickedUp`; al restablecer `PhoneNumberConfirmed=true`, iniciar contacto devuelve el número esperado. Prueba API focalizada **1/1**; suite Release **49 aprobadas, 0 fallidas, 8 SQL Server omitidas**. CI pasó. No hay cambios de producción, esquema ni bases de datos.

## Artefacto de despliegue vigente en `main`

El run `39557674487` pasó para `main` en `91a5def695be4ccf542941b757ac66bf9a4df0ac` y publicó `poraqui-api-iis-91a5def695be4ccf542941b757ac66bf9a4df0ac` (5,683,276 bytes; vence 2026-11-04). [Abrir ejecución de CI](https://github.com/EmanuelC1601/poraqui-backend/actions/runs/39557674487). Esto verifica la compilación y el paquete IIS, no el despliegue, la conectividad del sitio ni almacenamiento persistente en MonsterASP.

## Actualización posterior: PR #56

La corrección de precisión del contrato de carga de comprobante se integró en `main` como merge `8f637d6` (commit `fbe5803`). El snapshot OpenAPI ahora coincide con la salida de ejecución para las 61 rutas y declara la carga multipart y sus respuestas reales. Una prueba de contrato valida ese endpoint. Antes de integrar, SDK .NET 10.0.402 compiló con 0 advertencias/errores y ejecutó 32 pruebas aprobadas; las 2 pruebas de concurrencia SQL permanecieron omitidas al faltar la cadena de una base aislada. La consulta de esquema más reciente usó `ConnectionStrings:DefaultConnection` privada en modo solo lectura y confirmó 13 migraciones aplicadas, sin aplicar migraciones nuevas. No hay otro agente del que dependa el trabajo.

## Actualización posterior: PR #58 y verificación SQL aislada

PR #58 (`d38493f`) quedó integrado en `main` como merge `f9fa0d2`. Corrige la limpieza de la prueba de concurrencia externa: descarta snapshots rastreados anteriores a los cambios con rowversion y relee los registros antes de borrarlos.

Verificado localmente con SDK .NET 10.0.402: `dotnet test Poraqui.sln --no-restore` aprobó 32 pruebas y omitió las dos SQL por configuración ausente en el proceso predeterminado. Con `ConnectionStrings:SqlServerTest` privada y aislada, ambas pruebas SQL Server —asignación interna simultánea y aceptación externa simultánea— aprobaron (2/2). La base aislada estaba vacía antes de las pruebas y recibió solo migraciones revisables mediante `MigrateAsync` dentro de los tests; la base compartida no recibió migraciones en esta actualización. `dotnet-ef migrations list` leyó de nuevo la configuración privada de desarrollo y mostró las 13 migraciones integradas. PR #58 no tuvo checks automáticos configurados en GitHub; las verificaciones descritas son locales.

## Preparación de publicación y CI

Se agregó `.github/workflows/ci.yml` para restaurar, compilar Release y ejecutar pruebas en cambios a `main`. En un runner sin configuración SQL privada, los dos tests SQL se omiten intencionalmente; no se conecta la BD compartida a CI. Build y tests Release pasaron localmente (0 advertencias/errores, 32 aprobadas y 2 omitidas). `dotnet publish` generó el output IIS esperado (`web.config`, ensamblado, `.deps.json` y `.runtimeconfig.json`). El paquete no se publicó: hace falta elegir el sitio web destino y configurar sus variables privadas y ruta persistente desde el panel. La documentación oficial de MonsterASP.NET admite .NET moderno y su opción Git Deploy **Build .NET app** para `.csproj`.

PR #60 se integró como merge `cb11b72`. GitHub Actions `39084197135` pasó en `ubuntu-24.04` con checkout/setup-dotnet v5. El panel muestra cuatro sitios activos, pero ninguno identificado como Poraqui; no se publicó sobre sitios no identificados. La prueba de concurrencia SQL corre contra una base aislada con `ConnectionStrings:SqlServerTest` guardada en User Secrets; Remote está habilitado únicamente para esa base. La base compartida permanece fuera de las pruebas mutables.

Revalidación local con SDK .NET 10.0.402 y la conexión aislada desde User Secrets: `dotnet test Poraqui.sln --configuration Release --no-restore` pasó 34/34, incluidas las dos pruebas SQL Server de concurrencia (0 omitidas). La base compartida no recibió cambios.

PR #62 se integró en `main` como merge `7ac25f8`: agrega una carrera SQL Server para dos reintentos simultáneos de creación con el mismo `commandId`. La suite Release pasó 35/35 (incluidas tres pruebas SQL Server), el build Release pasó sin advertencias y el esquema no cambió. Ese proceso solo utilizó la BD aislada.

PR #64 se integró en `main` como merge `fe37ce7`, con un recorrido API de reparto externo probado contra SQL Server aislado.

PR #65 se integró en `main` como merge `be7da78`; verifica entrega offline, validación del código en servidor, replay idempotente y conflicto por payload diferente. Build Release pasó con 0 advertencias/errores; antes del merge, suite Release completa pasó 37/37 con cinco pruebas SQL Server. No cambia esquema ni accede a la BD compartida.

## Incremento de replay concurrente de comandos

PR #67 (`feature/sql-idempotent-command-replays`, commit `48da602`) se fusionó en `main` como merge `2c50be9`. Implementa recuperación del resultado al chocar solicitudes idénticas de reporte de incidencia y confirmación de entrega offline; payload distinto conserva respuesta de conflicto. Las pruebas SQL sincronizan ambas solicitudes antes de guardar y verifican resultado repetible y un solo historial/registro. No hay cambios de modelo ni migraciones.

La cadena `ConnectionStrings:SqlServerTest` está disponible en User Secrets de esta copia y apunta a `db74230`; no se imprimió ni versionó. `dotnet build Poraqui.sln --configuration Release --no-restore` pasó con 0 advertencias/errores. La suite Release completa pasó 39/39 (32 pruebas SQLite y siete pruebas SQL Server); la base aislada recibió las operaciones de los tests, sin cambios en la BD compartida. El check `backend-ci / build-and-test` pasó antes de integrar.

Siguiente trabajo desde `main`: resolver los pendientes desplegables descritos en `docs/project-status.md` (destino del sitio MonsterASP, ruta persistente para archivos y credenciales Twilio privadas para verificar SMS). Los módulos comerciales siguen con cobertura parcial documentada; no declarar completitud basándose solo en entidades o mapeos.

## Guard de storage privado en Production

Rama `feature/require-private-evidence-storage` añade validación de `PrivateFiles:Root` durante el arranque en Production: debe configurarse y quedar fuera de `wwwroot`; el registro del servicio en startup comprueba que IIS pueda crear/abrir el directorio. Evita arrancar con un fallback de almacenamiento no persistente, pero la persistencia después de reciclos debe probarse en el sitio MonsterASP elegido.

Build Release: 0 advertencias, 0 errores. Pruebas del guard: 4/4. Suite Release completa: 43/43, con siete pruebas SQL Server en la BD aislada `db74230`; no hubo cambios de modelo, migraciones o datos en la BD compartida. El `git diff --check` debe ejecutarse antes del commit.

Configuración local confirmada por nombre de clave, sin mostrar valores: existe `ConnectionStrings:SqlServerTest`; faltan `ConnectionStrings:DefaultConnection`, `PrivateFiles:Root`, `DataProtection:KeysPath` y `TwilioVerify:ServiceSid`, `TwilioVerify:ApiKeySid`, `TwilioVerify:ApiKeySecret`. Para la cadena compartida de desarrollo, agrega `ConnectionStrings:DefaultConnection` a User Secrets del proyecto API con la cadena Local adecuada; `docs/deployment-monsterasp.md` incluye el comando y las variables de sitio. No hay credenciales de base compartida disponibles en esta copia y no se usaron valores de mensajes.

## Persistencia de claves de Data Protection

Rama `feature/require-persistent-data-protection-keys`: exige `DataProtection:KeysPath` en Production cuando la conexión SQL activa Identity y rechaza una ruta dentro de `wwwroot`. El directorio se crea al inicio; persiste las claves que cifran códigos sensibles, para que los replays de asignación puedan recuperar el código tras un reinicio. En varias instancias, la ruta debe ser almacenamiento compartido. Cinco pruebas cubren clave ausente, path público/privado y descifrado con un segundo proveedor después del reinicio. No hay cambios de modelo ni migraciones.

Build Release: 0 advertencias, 0 errores. La suite Release pasó 48/48, incluidas siete pruebas SQL Server en `db74230`; no se modificó la BD compartida.

## Estado vigente después de PR #70

PR #70 se integró en `main` como merge `461965c`. La rama local se sincronizó con `origin/main`. El control de CI del PR pasó; localmente, con SDK .NET 10.0.402 instalado bajo `work/dotnet10`, la suite Release pasó 48/48 (siete sobre SQL Server aislado `db74230`). Se cargó la cadena de la BD de test desde User Secrets al entorno de un solo proceso; no se imprimió ni versionó. El panel del hosting confirmó Remote habilitado y conexión OK. `dotnet-ef migrations list` hizo lectura de la BD compartida por `DefaultConnection` y encontró las 13 migraciones aplicadas; esta actualización no ejecutó migración alguna sobre ella.

La suite SQL debe invocarse con `ConnectionStrings__SqlServerTest` cargada en el proceso y apuntando a la base aislada, nunca a la compartida. Las pruebas SQL limpian solo sus filas identificadas en `db74230`. Funcionalmente están implementados los recorridos internos y externos descritos en [order-flow.md](order-flow.md), pero la entrega aún depende de servicios reales y publicación: faltan escoger el sitio MonsterASP y sus rutas privadas persistentes, y validar SMS con credenciales Twilio y un teléfono autorizado. No hay migraciones nuevas, cambios de contrato API ni trabajo delegado a OpenCode en este incremento.

## Invariantes del esquema en SQL Server

Rama activa `feature/sql-domain-invariants` agrega diez `CHECK` revisables para precios, tarifas, cantidades, montos, horarios, disponibilidad y estado de las ofertas. Migraciones: `20261018020605_EnforceSqlDomainInvariants` y `20261018021039_UseProviderSafeOrderAmountConstraint`. Los scripts generados están en `docs/sql/`; el segundo migra la condición de importes a una expresión portable entre SQLite y SQL Server.

Antes de modificar `db74183`, consultas read-only de conteos agregados verificaron cero filas incompatibles. Los scripts inspeccionados no borran ni actualizan datos; el primero agrega checks y el segundo reemplaza solo una expresión de check. Ambas migraciones se aplicaron explícitamente; después se confirmaron 15 filas de historial y las diez restricciones en `sys.check_constraints`. No se aplicaron al iniciar la API.

Validación local actual con SDK .NET 10.0.402: build Release 0 advertencias/errores; suite Release **49/49** con las siete pruebas SQL Server contra `db74230`; `SqlServerDomainInvariantTests` también prueba que SQL Server rechaza violaciones. La base compartida no fue destino de tests.

## Cancelación de cliente antes de aceptación (PR #73)

Integrado en `main` como `6e269c6`. Cancelar después de enviar comprobante o de aprobar el pago, antes de que el negocio acepte, crea seguimiento manual de reembolso. La API devuelve `refundStatus` en la respuesta de cancelación; los reintentos idénticos no agregan cambios y la cancelación después de aceptar se rechaza. No requiere migración. Pruebas locales: 49/49 Release con SQLite y SQL Server aislado; CI del PR pasó.

La conexión SQL aislada se consume desde User Secrets solo en el proceso de test. No se imprimió ni versionó. El usuario pidió dejar para más adelante cualquier configuración privada adicional desde el panel del hosting.

## Revisión del PR #1

GitHub confirma PR #1 cerrado y fusionado a `main` (`e346995`, ancestro de `main`); añadió las entidades iniciales de negocio, catálogo, zona, tarifa y pedido, sus mapeos y pruebas de dominio/round-trip SQLite. El merge no tiene check runs registrados ni reviews; las pruebas originales cubrían reglas básicas y persistencia SQLite, no permisos de API ni operación real en SQL Server. Por eso el ledger lo conserva como fundación parcial. Los endpoints y recorridos completos se añadieron en incrementos posteriores. La suite SQL-configurada 49/49 fue una ejecución anterior a PR #76; la última suite completa local sin `ConnectionStrings__SqlServerTest` pasó 42 pruebas y omitió ocho SQL Server sobre 50. No se reescribió el PR ya integrado.

PR #76 (`e91298d`) documenta en OpenAPI la respuesta y los errores de cancelación; PR #77 (`ade461f`) prueba autorización por propietario del pedido y ausencia de cambios al rechazar a otro cliente. Ambos checks CI pasaron. En esta revisión local, las pruebas específicas pasaron (OpenAPI 2/2, flujo API SQLite 1/1). La suite sin exponer una cadena SQL Server al proceso pasó 42 y omitió 8 pruebas SQL; no hubo acceso de escritura a SQL Server.

PR #79, integrado en `main` como `96f31a0`, asegura en el flujo API que reintentar la cancelación del mismo cliente conserva el número de entradas de historial y una sola transición `Cancelled`. Prueba SQLite enfocada 1/1; CI pasó. No hay migración.

En la ejecución anterior sin `ConnectionStrings:SqlServerTest`, 42 pruebas pasaron y ocho SQL Server se omitieron. La ejecución local completa más reciente cargó la conexión de la base aislada desde User Secrets solo en el proceso y pasó **50/50**, incluidas esas ocho pruebas. El detalle y la fecha de ambas comprobaciones están en [project-status.md](project-status.md).

## Verificación vigente 2026-10-17

Desde `main` en `1952640`, con SDK .NET 10.0.402, `dotnet test Poraqui.sln --configuration Release --no-restore` terminó **50/50 aprobadas, 0 fallidas, 0 omitidas**. Incluye las ocho pruebas SQL Server en `db74230`, mediante `ConnectionStrings:SqlServerTest` leída de User Secrets y cargada solo en el proceso. El arnés compara el catálogo con `DefaultConnection`; las migraciones de prueba se ejecutaron solo en la base aislada. La base compartida no fue modificada. No se imprimieron ni versionaron secretos.

El paquete IIS fue publicado localmente en `work/publish`: contiene la API y `web.config` con hosting InProcess. El directorio está ignorado. No hay publicación en MonsterASP ni se han verificado runtime IIS o almacenamiento persistente del sitio. `ConnectionStrings:DefaultConnection` está presente en User Secrets pero no se inspeccionó su valor. Continúan pendientes la configuración privada del sitio, SMS real y las verificaciones de hosting descritas en `docs/project-status.md`.

## Readiness para despliegues

PR #84 quedó integrado en `main` como `a38ee0a`. `GET /health/ready` comprueba conexión SQL sin autenticación, devuelve HTTP 200/503 sin exponer detalles y no aplica migraciones. `GET /health/live` conserva su función de comprobar el proceso. Pruebas focalizadas Release: 3/3; suite Release completa: 53/53, incluidas ocho pruebas SQL Server en `db74230`. CI pasó antes de integrar. MonsterASP no se ha configurado ni verificado.

PR #86 integró el paquete IIS en CI (`90b177d`). La ejecución de `main` #39092852859 pasó y generó `poraqui-api-iis-90b177d54c2eb632a9c0a41fe8942063fcfd3fdf` (5,679,063 bytes, retención de 14 días). La publicación local también generó `web.config` InProcess; suite completa: 53/53. El sitio MonsterASP aún no se ha configurado ni verificado.

Después de PR #92, el run vigente de `main` #39550690307 pasó CI para `18f66946d905c55fef313e8c884560169bf351d2` y generó `poraqui-api-iis-18f66946d905c55fef313e8c884560169bf351d2` (5,683,250 bytes; vence 2026-11-04). Confirma el artefacto del commit actual, pero no el despliegue/arranque en IIS; siguen pendientes elegir sitio y configurar sus variables/rutas privadas.

PR #88 integró en `main` (`914d291`) la autorización del negocio/pedido antes de consultar la existencia o rol del courier en `POST /api/businesses/{businessId}/orders/{orderId}/assign-courier`. La prueba API enfocada aprobó; suite Release completa 54/54 con ocho pruebas SQL Server en la BD aislada. Sin migraciones.

## PR #90 integrado: atomicidad y revocación efectiva de rol courier

PR #90 quedó integrado en `main` como `978638c`. `PUT /api/admin/users/{userId}/external-courier` agrupa cambios de Identity y `CourierAvailabilities` en una transacción. Se agregó verificación de rol actual en rutas de oferta y operaciones/listados de reparto; SignalR la consulta al suscribirse a ofertas o pedidos y antes de publicar avisos. La prueba API SQLite cubre tokens anteriores después de revocar roles. No modifica contratos ni esquema.

Para el head de PR, build Release: 0 advertencias/errores; suite Release: 47 aprobadas, 0 fallidas, 8 SQL Server omitidas por la configuración diferida de `SqlServerTest`. También pasaron el test de revocación (1/1) y el recorrido interno (1/1) sobre SQLite. CI `build-and-test` pasó antes de integrar. No hubo migraciones ni accesos a bases compartidas o SQL de pruebas.

## PR #91 integrado: cancelar ofertas al revocar courier

PR #91 quedó integrado como `5701981` en `main`. `PUT /api/admin/users/{userId}/external-courier` cancela las ofertas `Offered` de ese courier en la transacción que revoca rol/perfil; después del commit publica el evento SignalR `Cancelled`. Las ofertas no reaparecen al reactivar el rol y la conexión activa recibe el cierre. Test API enfocado pasó 1/1 en SQLite. Build Release: 0 advertencias/errores; suite: 47 aprobadas, 0 fallidas y 8 SQL Server omitidas. CI `build-and-test` pasó antes de integrar. Sin migraciones ni acceso a SQL Server.

La verificación SQL Server aislada queda pendiente hasta que el usuario configure `ConnectionStrings:SqlServerTest` en User Secrets. Instrucción: `dotnet user-secrets set "ConnectionStrings:SqlServerTest" "<cadena privada de la BD aislada>" --project src/Poraqui.Api`; no enviar el valor por chat ni usar `DefaultConnection` compartida para pruebas que escriban.

## Cobertura SQL pendiente: revocación de courier

Rama `feature/sql-revocation-offer-test` extiende `SqlServerExternalDeliveryFlowApiTests`: oferta inicial, revocación y cancelación persistida, acceso denegado mientras se revocó, reactivación sin reaparición de la oferta vieja y nueva publicación antes de aceptar. Build Release pasó sin advertencias/errores; la prueba enfocada fue omitida porque falta la cadena privada aislada. Suite local Release: 47 aprobadas, 0 fallidas, 8 SQL Server omitidas. Sin migraciones.

## PR #94 integrado y corrección de la ejecución paralela de pruebas

PR #94 quedó integrado en `main` como `93fc30a`; añade al flujo SQL Server la comprobación de revocación y reactivación de un repartidor externo. No cambia el esquema. La prueba SQL específica no se ejecutó localmente: falta `ConnectionStrings:SqlServerTest` en User Secrets por decisión del usuario.

El push de `main` tras ese merge falló en CI porque `Readiness_checks_database_connectivity_without_requiring_authentication` respondió 503. La suite local reprodujo el fallo mientras los API test hosts cambiaban variables de entorno globales durante ejecución paralela; además, algunas pruebas de API fallaban al arrancar sin los valores globales esperados. PR #95 serializó el assembly xUnit para evitar que un host lea la configuración temporal de otro; se integró en `main` como `6965432`. El check del PR y el CI de `main` después del merge pasaron. Suite local Release: 47 aprobadas, 0 fallidas y 8 pruebas SQL omitidas. `git diff --check` pasó. Sin migraciones ni acceso a bases de datos.

La documentación de seguimiento se integró mediante PR #96 (`5955ff1`). El CI vigente de `main`, run `39552373161`, pasó y produjo `poraqui-api-iis-5955ff12499c8d3cab870fb12b043088a357d78f` (5,683,271 bytes, vence 2026-11-04). Aún no se ha desplegado a MonsterASP. El usuario aplazó configurar la cadena SQL aislada; las ocho pruebas SQL continúan omitidas en la ejecución local y no se usó ninguna base.

PR #99 (`3a510cf`, `b894df3`) se integró como `982a973`: el test OpenAPI compara `openapi`, `info`, `paths` y `components` completos contra `docs/openapi.json`; solo ignora el arreglo dinámico `servers`. Prueba focalizada 1/1; suite Release 48 aprobadas, 0 fallidas y 8 SQL omitidas. CI posterior al merge `39553449615` pasó y produjo `poraqui-api-iis-982a9731156cabeb11d27ddd02f050af75b35564` (5,683,240 bytes, vence 2026-11-04). Sin cambios de esquema o acceso a bases. El despliegue MonsterASP y pruebas SQL siguen pendientes.

PR #100 quedó integrado como `886c7c1`; el run de `main` `39553789750` pasó y generó `poraqui-api-iis-886c7c150998ea428409107825af34108e144118` (5,683,261 bytes, vence 2026-11-04). El paquete continúa sin publicarse porque falta seleccionar y configurar el sitio y sus rutas privadas.

## PR #108 integrado: límite de comprobante

PR #108 quedó integrado en `main` como merge `801d6a8`. Un archivo de comprobante que excede 8 MiB responde HTTP 413, conforme al contrato existente. La prueba API verifica el caso de 8 MiB + 1 byte y confirma que el pedido permanece en `PendingPayment`. Prueba enfocada SQLite 1/1; suite Release completa 49 aprobadas, 0 fallidas y 8 pruebas SQL Server omitidas por la conexión aislada aplazada. CI `build-and-test` pasó antes del merge y el CI post-merge run `39645985977` también pasó. No hay cambios de esquema ni migraciones.

PR #102 ya está integrado y su estado queda registrado en el ledger; el usuario configuró posteriormente la cadena privada de pruebas, por lo que la suite SQL Server pudo ejecutarse sin compartir ni imprimir secretos.
