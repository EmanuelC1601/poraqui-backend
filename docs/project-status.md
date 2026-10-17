# Estado verificable del proyecto

Actualizado: 2026-10-17. `main` es la base de integración. PR #1–#8 están integrados hasta `6993610` (PR #23, SignalR). Hay un solo responsable; OpenCode ya no participa. Los siguientes incrementos se crean desde `main` en ramas `feature/*` y se integran por PR después de revisión y verificación.

## Ledger de funciones

| Estado | Función | Evidencia / pendiente |
|---|---|---|
| En `main` | Arquitectura, Identity y permisos por recurso, negocio, catálogo básico, zonas/tarifas, precio por servidor, transferencia/revisión manual, reparto propio/externo inicial, sync offline idempotente y SignalR con autorización por pedido | PR #1–#8; `6993610` |
| En rama activa | Opciones de producto, disponibilidad por grupo/opción, validación de selección y precios por servidor | `feature/catalog-options-main`; build 0 warnings/errores y 15/15 pruebas SQLite; migración aditiva revisable no aplicada |
| Pendiente | Horarios, OTP real, cancelaciones/incidencias/reembolsos, idempotencia restante, métricas, despliegue MonsterASP.NET y reconexión SignalR desde cliente real | Código previo en ramas apiladas, aún no integrado; revalidar antes de cada PR |
| Pendiente | Aceptación simultánea de oferta y asignación simultánea de courier comprobadas en SQL Server | Falta `ConnectionStrings:SqlServerTest` para `PoraquiIntegrationTests` |
| Pendiente | SMS real, reconexión SignalR desde cliente real, almacenamiento persistente y publicación MonsterASP.NET | Configuración externa no probada |
| Fuera de alcance | GPS, distancias calculadas, transferencias bancarias automáticas y frontend | Usar zonas/tarifas configuradas y revisión manual |

## Verificación del último incremento integrado

- Rama: `feature/offline-sync-main`; PR #22, merge `8ed5cb3`.
- `dotnet restore Poraqui.sln`: correcto.
- `dotnet build Poraqui.sln --no-restore`: correcto, 0 advertencias y 0 errores con salida net9.0.
- `dotnet test Poraqui.sln --no-build --no-restore`: 14/14 aprobadas, SQLite aislado. Se configuró `DOTNET_ROLL_FORWARD=Major` para ejecutar net9.0 con runtime .NET 10 disponible localmente.
- La migración de este incremento agrega una tabla e índice; script en `docs/sql/20261017192726_OfflineDeliveryCommandSync.sql`. No se aplicó durante este PR. En la rama acumulada se había observado su registro en el historial de EF.

## Último incremento activo: catálogo

Opciones de catálogo conservan los nombres/precios elegidos dentro del pedido y el servidor recalcula importes. Restore/build pasan (0 warnings/errores) y 15/15 pruebas SQLite pasan. La migración `CatalogProductOptions` y el script `docs/sql/CatalogProductOptions.sql` agregan tablas/índices, no se aplicaron.

SignalR de pedidos se integró en PR #23. En `main`, sus pruebas cubren evento WebSocket, autenticación y acceso cruzado. Pendiente reconexión desde cliente real.

## SQL Server

`ConnectionStrings:DefaultConnection` existe en User Secrets y EF se conectó desde la rama completa de integración. EF confirmó 12 migraciones aplicadas hasta `CourierAssignmentCommandIdempotency` y ningún cambio pendiente del modelo. No se aplicaron migraciones durante esta revisión. `ConnectionStrings:SqlServerTest` no existe; no dirigir pruebas que limpian datos hacia la BD compartida.

Configura `ConnectionStrings:DefaultConnection` en User Secrets (`src/Poraqui.Api`) o configuración privada del hosting; nunca guardes cadenas en Git. Las carreras SQL Server requieren otra base, `PoraquiIntegrationTests`. La API no migra automáticamente al iniciar.
