# Guía de integración y ejecución

## Responsable y ramas

Un solo responsable mantiene todos los módulos, contratos, `AppDbContext`, migraciones, pruebas y despliegue. `main` es la base de integración; contiene la base inicial y PR #22–#27 hasta `ce0cc7b`, incluidos sync offline, SignalR, catálogo, horarios, cancelaciones, incidencias, reembolsos manuales e idempotencia de transiciones. Crear incrementos desde el `main` actualizado, usar ramas `feature/*` y abrir PRs pequeños. OpenCode ya no participa; su asignación previa queda como antecedente.

Los cambios posteriores permanecen en ramas apiladas. No asumir que están integrados ni que sus pruebas corresponden al `main`; revisar cada cambio y revalidarlo contra la rama actual antes de presentar PR.

## Incremento integrado: SignalR

Rama `feature/comercio-pedidos-next`, commit inicial `b2d015e` sobre el `main` integrado. El hub autenticado `/hubs/orders` autoriza suscripciones por relación con el pedido. Devuelve estado/versión actuales y publica `OrderChanged` después de persistir cada cambio. El snapshot se vuelve a leer tras unirse al grupo para evitar perder transiciones concurrentes. Payload no contiene datos personales.

Verificado al integrar PR #23: `dotnet restore Poraqui.sln`, build (0 warnings/errores) y 14/14 pruebas SQLite. Las pruebas ejercitan WebSocket SignalR en TestServer, autenticación y acceso cruzado. `dotnet ef migrations has-pending-model-changes` no reportó cambios de modelo. La reconexión de un cliente real sigue pendiente.

## Incremento integrado: opciones de catálogo

PR #24 integra `CatalogProductOptions`: negocio crea grupos de opciones y cambia su disponibilidad; el servidor valida límites y disponibilidad al crear pedido y guarda snapshots de nombre/precio. Restore/build pasaron (0 warnings/errores), 15/15 pruebas SQLite. La migración agrega tablas e índices; script en `docs/sql/CatalogProductOptions.sql`, aún no aplicada.

## Incremento integrado: horarios comerciales

PR #25, merge `c372018` a `main`. Configura siete días y zona horaria, publica consulta del horario y rechaza pedidos cuando el negocio está cerrado. Restore/build pasaron (0 warnings/errores), 15/15 pruebas SQLite. `BusinessOpeningHours` agrega dos columnas con defaults y tabla semanal; script en `docs/sql/BusinessOpeningHours.sql`.

## Incremento integrado: cancelaciones, incidencias y reembolsos manuales

PR #26, merge `1d75577`. Implementa cancelación por cliente/negocio, auditoría de cancelación, cierre de ofertas externas abiertas, incidencias y revisión de reembolso manual. Restore/build pasaron (0 warnings/errores) y 18/18 pruebas SQLite. Migraciones aditivas `OrderCancellationRefundsIncidents` y `OrderCancellationAuditConstraints`.

## Incremento integrado: reintentos de transiciones

Rama `feature/workflow-idempotency-main`, commits `0c39f51` y `69131f7`, basada en `main` `1d75577`. Reintentar decisión de negocio, preparación, recogida, inicio y confirmación de recorrido no duplica los efectos persistidos. Restore/build pasan (0 warnings/errores), 21/21 pruebas SQLite. PR #27 integrado; no requirió migración.

## Incremento activo: idempotencia de reportes de incidentes

Rama `feature/incident-idempotency`, commit `8181753`, basada en `main` `ce0cc7b`. Reportar una incidencia acepta `commandId`; el mismo ID y payload repiten el resultado sin registrar una segunda incidencia. Usa la migración `IncidentCommandIdempotency`, ya presente en el historial de SQL Server. Restore/build y 21/21 pruebas SQLite pasan. PR pendiente.

## SQL Server y datos compartidos

`ConnectionStrings:DefaultConnection` está en User Secrets; la conexión SQL Server y lectura de tablas/historial se verificó sin exponer la cadena. `dbo.__EFMigrationsHistory` contiene 12 IDs hasta `CourierAssignmentCommandIdempotency`, mientras `main` versiona ocho migraciones en total después de PR #26. Varios IDs aplicados aún no tienen sus migraciones/implementaciones en `main`; reconciliar el historial completo desde las ramas existentes antes de crear o aplicar otras migraciones. No se ejecutó ninguna migración en esta revisión. `ConnectionStrings:SqlServerTest` falta; configurar una base aislada `PoraquiIntegrationTests` para pruebas concurrentes. Nunca usar la BD compartida en pruebas que limpian datos.

Para desarrollo local, guardar la conexión privada con `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api`. No guardar ni imprimir el valor. Revisar todo SQL de migración por operaciones destructivas y aplicar migraciones explícitamente cuando el destino esté validado. La API no migra al arrancar.

## Ejecución y OpenAPI

Desde la raíz: `dotnet restore Poraqui.sln`, `dotnet build Poraqui.sln` y `dotnet test Poraqui.sln`. Si solo está instalado runtime .NET 10 para proyectos net9.0, en PowerShell puede usarse `$env:DOTNET_ROLL_FORWARD='Major'` para las pruebas y luego eliminar esa variable. OpenAPI está disponible en Development en `/openapi/v1.json`; el snapshot actualizado se guarda en `docs/openapi.json` (59 paths).

El recorrido automatizado de API está en `tests/Poraqui.Domain.Tests/BusinessApiTests.cs`; cubre precios del servidor, permisos, revisión manual, reparto y códigos. SQLite aislado sirve para el recorrido, pero no valida concurrencia ni migraciones SQL Server.

## Pendientes verificables

- Confirmar reconexión SignalR desde cliente real y el comportamiento en el hosting de producción.
- OTP real, idempotencia de reportes, pedidos, comprobantes y asignaciones, métricas y despliegue MonsterASP.NET.
- Completar pruebas de concurrencia con `ConnectionStrings:SqlServerTest` aislada y prueba de reconexión SignalR desde cliente real.
- Revisar el ledger de `docs/project-status.md` y los contratos de `docs/api-contracts.md` antes de cada PR.

GPS, distancias calculadas, transferencias bancarias automáticas y frontend están fuera de alcance.
