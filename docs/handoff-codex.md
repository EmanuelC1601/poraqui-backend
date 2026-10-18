# Estado y entrega de Codex

## Flujo de trabajo

El backend tiene un único responsable. `main` es la base de integración; cada incremento parte de `main` actualizado en una rama `feature/*`, se verifica y se integra mediante PR. Contratos, `AppDbContext` y migraciones tienen un solo propietario. La asignación anterior a OpenCode queda como antecedente en `docs/team-work.md`.

## Cambios integrados

PR #1 y PR #22–#60 están integrados. El código incluye identidad y permisos por rol/recurso, comercio y catálogo, pedidos con precios del servidor, comprobantes privados y revisión manual, estados de preparación, reparto interno y externo, ofertas, expiración/cancelación, códigos, contacto autorizado, historial, idempotencia, sync offline, SignalR, incidencias, reembolsos manuales, OTP y métricas piloto. La prueba amplia usa SQLite aislado; las carreras SQL Server ya se verificaron por separado contra una base aislada. Esto no acredita servicios externos reales. Ver [ledger](project-status.md) y [recorrido reproducible](order-flow.md).

Las 13 migraciones integradas están aplicadas en la BD compartida de desarrollo. `dotnet-ef migrations list` se ejecutó el 2026-10-17 con `ConnectionStrings:DefaultConnection` de User Secrets y mostró todas las migraciones sin pendientes. No se aplica migración desde el arranque de la API.

PR #49 documenta el test integral reproducible y sus límites. PR #50 distingue su configuración aislada de la configuración privada requerida para ejecutar la API manualmente. PR #52 (`edac6bf`) registra la integración de .NET 10 y mantiene explícitos los pendientes.

## Último incremento de código: PR #51 (los PR #52 y #53 actualizaron documentación y estado)

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

PR #63 se integró en `main` como merge `cb86962`, con un recorrido API de reparto interno probado contra SQL Server aislado. El siguiente incremento en `feature/sql-server-external-delivery-flow` prueba API de reparto externo con SQL Server. La suite Release pasó 37/37 con cinco pruebas SQL Server y 0 omitidas; build Release pasó sin advertencias ni errores. No cambia el esquema ni la BD compartida. `docs/order-flow.md` contiene los dos recorridos y sus límites.
