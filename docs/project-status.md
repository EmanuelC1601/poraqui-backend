# Estado verificable del proyecto

Actualizado: 2026-10-17. `main` es la base de integración y contiene PR #1–#6 (`d39d265`). PR #7 ya se integró en la rama de PR #6 y está preparado en `feature/offline-sync-main` como incremento acotado hacia `main`. PR #8–#21 están en ramas apiladas y fuera de `main`; `feature/backend-integration-verified` contiene esa pila completa. Hay un solo responsable; OpenCode ya no participa.

## Ledger de funciones

| Estado | Función | Evidencia / pendiente |
|---|---|---|
| En `main` | Arquitectura, Identity y permisos por recurso, negocio, catálogo básico, zonas/tarifas, pedido con precio del servidor, transferencia/revisión manual, reparto propio y externo inicial | PR #1–#6, `d39d265` |
| En PR #7 hacia `main` | Sincronización offline validada por el servidor, con commandId idempotente | `feature/offline-sync-main`; build limpio, 14/14 pruebas SQLite |
| En la pila, fuera de `main` | SignalR autorizado, opciones/horarios, cancelaciones, incidencias/reembolsos, métricas, OTP, idempotencia de comandos y asignación | PR #8–#21, en `feature/backend-integration-verified` |
| Pendiente | Aceptación simultánea de oferta y asignación simultánea de courier comprobadas en SQL Server | Falta `ConnectionStrings:SqlServerTest` para `PoraquiIntegrationTests` |
| Pendiente | SMS real, reconexión SignalR desde cliente real, almacenamiento persistente y publicación MonsterASP.NET | Configuración externa no probada |
| Fuera de alcance | GPS, distancias calculadas, transferencias bancarias automáticas y frontend | Usar zonas/tarifas configuradas y revisión manual |

## Verificación del incremento actual

- Rama: `feature/offline-sync-main`, basada en `origin/main`; commits `cc83f10` y `231315c`.
- `dotnet restore Poraqui.sln`: correcto.
- `dotnet build Poraqui.sln --no-restore`: correcto, 0 advertencias y 0 errores con salida net9.0.
- `dotnet test Poraqui.sln --no-build --no-restore`: 14/14 aprobadas, SQLite aislado. Se configuró `DOTNET_ROLL_FORWARD=Major` para ejecutar net9.0 con runtime .NET 10 disponible localmente.
- La migración de este incremento crea una tabla e índice; el script está en `docs/sql/20261017192726_OfflineDeliveryCommandSync.sql`. No se aplicó durante esta verificación.

## SQL Server

`ConnectionStrings:DefaultConnection` existe en User Secrets y EF se conectó desde la rama completa de integración. EF confirmó 12 migraciones aplicadas hasta `CourierAssignmentCommandIdempotency` y ningún cambio pendiente del modelo. No se aplicaron migraciones durante esta revisión. `ConnectionStrings:SqlServerTest` no existe; no dirigir pruebas que limpian datos hacia la BD compartida.

Configura `ConnectionStrings:DefaultConnection` en User Secrets (`src/Poraqui.Api`) o configuración privada del hosting; nunca guardes cadenas en Git. Las carreras SQL Server requieren otra base, `PoraquiIntegrationTests`. La API no migra automáticamente al iniciar.
