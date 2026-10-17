# Guía de integración y ejecución

## Responsable y ramas

Un solo responsable mantiene todos los módulos, contratos, `AppDbContext`, migraciones, pruebas y despliegue. `main` es la base de integración y contiene PR #1–#8 hasta PR #23 (`6993610`), incluido sync offline y SignalR. Crear incrementos desde el `main` actualizado, usar ramas `feature/*` y abrir PRs pequeños. OpenCode ya no participa; su asignación previa queda como antecedente.

Los cambios posteriores permanecen en ramas apiladas. No asumir que están integrados ni que sus pruebas corresponden al `main`; revisar cada cambio y revalidarlo contra la rama actual antes de presentar PR.

## Incremento integrado: SignalR

Rama `feature/comercio-pedidos-next`, commit inicial `b2d015e` sobre el `main` integrado. El hub autenticado `/hubs/orders` autoriza suscripciones por relación con el pedido. Devuelve estado/versión actuales y publica `OrderChanged` después de persistir cada cambio. El snapshot se vuelve a leer tras unirse al grupo para evitar perder transiciones concurrentes. Payload no contiene datos personales.

Verificado al integrar PR #23: `dotnet restore Poraqui.sln`, build (0 warnings/errores) y 14/14 pruebas SQLite. Las pruebas ejercitan WebSocket SignalR en TestServer, autenticación y acceso cruzado. `dotnet ef migrations has-pending-model-changes` no reportó cambios de modelo. La reconexión de un cliente real sigue pendiente.

## Incremento activo: opciones de catálogo

Rama `feature/catalog-options-main`, commit `819bd20` sobre `main`. Negocio crea grupos de opciones y cambia su disponibilidad; el servidor valida límites y disponibilidad al crear pedido y guarda snapshots de nombre/precio. Restore/build pasan (0 warnings/errores), 15/15 pruebas en SQLite. `CatalogProductOptions` agrega tablas e índices; script en `docs/sql/CatalogProductOptions.sql`, aún no aplicado.

## SQL Server y datos compartidos

`ConnectionStrings:DefaultConnection` está en User Secrets y se conectó desde la rama integrada acumulada. EF confirmó 12 migraciones aplicadas y ningún cambio de modelo pendiente en esa revisión; no se aplicaron migraciones durante este incremento. Falta `ConnectionStrings:SqlServerTest`, que debe apuntar a la base aislada `PoraquiIntegrationTests` para pruebas concurrentes. Nunca configurar las pruebas que limpian datos con la BD compartida.

Para desarrollo local, guardar la conexión privada con `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api`. No guardar ni imprimir el valor. Revisar todo SQL de migración por operaciones destructivas y aplicar migraciones explícitamente cuando el destino esté validado. La API no migra al arrancar.

## Ejecución y OpenAPI

Desde la raíz: `dotnet restore Poraqui.sln`, `dotnet build Poraqui.sln` y `dotnet test Poraqui.sln`. Si solo está instalado runtime .NET 10 para proyectos net9.0, en PowerShell puede usarse `$env:DOTNET_ROLL_FORWARD='Major'` para las pruebas y luego eliminar esa variable. OpenAPI está disponible en Development en `/openapi/v1.json`.

El recorrido automatizado de API está en `tests/Poraqui.Domain.Tests/BusinessApiTests.cs`; cubre precios del servidor, permisos, revisión manual, reparto y códigos. SQLite aislado sirve para el recorrido, pero no valida concurrencia ni migraciones SQL Server.

## Pendientes verificables

- Confirmar reconexión SignalR desde cliente real y el comportamiento en el hosting de producción.
- Opciones/horarios, OTP real, cancelación de pedido, seguimiento de reembolsos manuales, idempotencia restante, métricas y despliegue MonsterASP.NET.
- Completar pruebas de concurrencia con `ConnectionStrings:SqlServerTest` aislada y prueba de reconexión SignalR desde cliente real.
- Revisar el ledger de `docs/project-status.md` y los contratos de `docs/api-contracts.md` antes de cada PR.

GPS, distancias calculadas, transferencias bancarias automáticas y frontend están fuera de alcance.
