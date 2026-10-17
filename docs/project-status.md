# Estado verificable del proyecto

Actualizado: 2026-10-17. `main` es la base de integración; PR #25 (horarios comerciales) quedó integrado en `c372018`. Hay un solo responsable; OpenCode ya no participa. Cada incremento se crea desde `main` en `feature/*` y se integra por PR después de revisión y verificación.

## Ledger de funciones

| Estado | Función | Evidencia / pendiente |
|---|---|---|
| En `main` | Arquitectura, Identity y permisos por recurso, negocio, catálogo con opciones, zonas/tarifas, precio por servidor, transferencia/revisión manual, reparto propio/externo inicial, sync offline idempotente, SignalR con autorización por pedido y horarios semanales | Base inicial y PR #22–#25; `c372018` |
| En rama activa | Cancelación de pedidos, registro/resolución de incidencias y seguimiento de reembolsos manuales | `feature/order-incidents-refunds`, commit `912c0eb`; build 0 advertencias/errores y 18/18 pruebas SQLite; PR pendiente |
| Pendiente | OTP real, idempotencia adicional, métricas, despliegue MonsterASP.NET y reconexión SignalR desde cliente real | Hay commits previos en ramas locales/remotas; deben auditarse y verificarse antes de integrar |
| Pendiente | Aceptación simultánea de oferta y asignación simultánea de courier comprobadas en SQL Server | Falta `ConnectionStrings:SqlServerTest` para `PoraquiIntegrationTests` |
| Pendiente | SMS real, reconexión SignalR desde cliente real, almacenamiento persistente y publicación MonsterASP.NET | Configuración externa no probada |
| Fuera de alcance | GPS, distancias calculadas, transferencias bancarias automáticas y frontend | Usar zonas/tarifas configuradas y revisión manual |

## Verificación de incrementos integrados

- Rama: `feature/offline-sync-main`; PR #22, merge `8ed5cb3`.
- `dotnet restore Poraqui.sln`: correcto.
- `dotnet build Poraqui.sln --no-restore`: correcto, 0 advertencias y 0 errores con salida net9.0.
- `dotnet test Poraqui.sln --no-build --no-restore`: 14/14 aprobadas, SQLite aislado. Se configuró `DOTNET_ROLL_FORWARD=Major` para ejecutar net9.0 con runtime .NET 10 disponible localmente.
- La migración de este incremento agrega una tabla e índice; script en `docs/sql/20261017192726_OfflineDeliveryCommandSync.sql`. No se aplicó durante este PR. En la rama acumulada se había observado su registro en el historial de EF.

## Incrementos recientes

Opciones de catálogo conservan los nombres/precios elegidos dentro del pedido y el servidor recalcula importes. Restore/build pasaron (0 warnings/errores) y 15/15 pruebas SQLite pasaron en PR #24. `CatalogProductOptions` y su script agregan tablas/índices; todavía no se aplicaron.

SignalR de pedidos se integró en PR #23. En `main`, sus pruebas cubren evento WebSocket, autenticación y acceso cruzado. Pendiente reconexión desde cliente real.

Horarios comerciales pasan restore/build (0 warnings/errores) y 15/15 pruebas SQLite en la rama activa. `BusinessOpeningHours` agrega columnas con defaults y una tabla semanal; script `docs/sql/BusinessOpeningHours.sql`. No se aplicó en SQL Server.

Horarios comerciales quedaron integrados en PR #25 (`c372018`): configuración semanal y zona horaria, lectura pública, permisos de propietario/admin y bloqueo de nuevos pedidos fuera del horario configurado. La migración es aditiva y está en `docs/sql/BusinessOpeningHours.sql`.

Cancelaciones, incidencias y reembolsos manuales están en la rama activa. El cliente cancela solo antes de enviar comprobante; negocio/admin puede cancelar antes de recogida. Se crea seguimiento de reembolso cuando hay transferencia aprobada o pendiente. Los participantes autorizados consultan/reportan incidencias; negocio propietario/admin las resuelve y revisa el caso de reembolso.

## SQL Server

`ConnectionStrings:DefaultConnection` existe en User Secrets y la conexión de lectura a SQL Server se verificó desde esa configuración. El historial `dbo.__EFMigrationsHistory` contiene 12 registros hasta `CourierAssignmentCommandIdempotency`. El checkout `main` contiene solo la cadena hasta `BusinessOpeningHours`; las migraciones registradas posteriores aún deben reconciliarse con sus entidades/código antes de generar o aplicar otras migraciones. No se aplicaron migraciones durante esta revisión. `ConnectionStrings:SqlServerTest` no existe; no dirigir pruebas que limpian datos hacia la BD compartida.

Configura `ConnectionStrings:DefaultConnection` en User Secrets (`src/Poraqui.Api`) o configuración privada del hosting; nunca guardes cadenas en Git. Las carreras SQL Server requieren otra base, `PoraquiIntegrationTests`. La API no migra automáticamente al iniciar.
