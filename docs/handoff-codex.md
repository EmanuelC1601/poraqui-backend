# Estado y entrega de Codex

## Flujo de trabajo

El backend tiene un único responsable. `main` es la base de integración; cada incremento parte de `main` actualizado en una rama `feature/*`, se verifica y se integra mediante PR. Contratos, `AppDbContext` y migraciones tienen un solo propietario. La asignación anterior a OpenCode queda como antecedente en `docs/team-work.md`.

## Cambios integrados

PR #1 y PR #22–#67 están integrados. El código incluye identidad y permisos por rol/recurso, comercio y catálogo, pedidos con precios del servidor, comprobantes privados y revisión manual, estados de preparación, reparto interno y externo, ofertas, expiración/cancelación, códigos, contacto autorizado, historial, idempotencia, sincronización offline validada por el servidor, SignalR, incidencias, reembolsos manuales, OTP y métricas piloto. La suite Release completa pasó 39/39 con SQL Server aislado `db74230` y SQLite. Esto no acredita servicios externos reales. Ver [ledger](project-status.md) y [recorrido reproducible](order-flow.md).

Las 13 migraciones integradas están aplicadas en la BD compartida de desarrollo. `dotnet-ef migrations list` se ejecutó el 2026-10-17 con `ConnectionStrings:DefaultConnection` de User Secrets y mostró todas las migraciones sin pendientes. No se aplica migración desde el arranque de la API.

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

No se publican secretos ni se toman credenciales del chat. El estado verificable vigente está en `docs/project-status.md`.

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

GitHub confirma PR #1 cerrado y fusionado a `main` (`e346995`, ancestro de `main`); añadió las entidades iniciales de negocio, catálogo, zona, tarifa y pedido, sus mapeos y pruebas de dominio/round-trip SQLite. El merge no tiene check runs registrados ni reviews; las pruebas originales cubrían reglas básicas y persistencia SQLite, no permisos de API ni operación real en SQL Server. Por eso el ledger lo conserva como fundación parcial. Los endpoints y recorridos completos se añadieron en incrementos posteriores. En la copia actual, la suite Release 49/49 valida el estado acumulado en SQLite y SQL Server aislado; no se reescribió el PR ya integrado.
