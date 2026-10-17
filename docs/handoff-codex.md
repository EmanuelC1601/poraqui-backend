# Handoff de desarrollo

## Responsable y ramas

Una sola persona mantiene todos los módulos, contratos, `AppDbContext`, migraciones, pruebas y despliegue. `main` es la base de integración; usar ramas `feature/*` y PR por incremento. OpenCode ya no participa; su asignación anterior queda como antecedente.

`main` contiene PR #1–#6 (`d39d265`). Este PR se prepara en `feature/offline-sync-main`, con solo el incremento offline de PR #7 y basado directamente en `main`. PR #8–#21 siguen en ramas apiladas; `feature/backend-integration-verified` conserva la punta completa para integrar los siguientes incrementos en orden.

## PR de sincronización offline

`POST /api/orders/{orderId}/sync/delivery-confirmation` recibe `commandId`, código y `clientCapturedAtUtc`. El servidor vuelve a validar actor, courier asignado, estado y código antes de confirmar. La hora del dispositivo solo sirve de auditoría; los reintentos con mismo ID y payload recuperan el resultado guardado, mientras que una colisión devuelve 409.

Verificación en `feature/offline-sync-main`: restore correcto, build .NET 9 limpio (0 warnings/errores) y 14/14 pruebas aprobadas con SQLite aislado. La migración añade una tabla e índice; SQL en `docs/sql/20261017192726_OfflineDeliveryCommandSync.sql`. No fue aplicada en este turno.

## SQL y datos compartidos

La configuración privada `ConnectionStrings:DefaultConnection` funciona. EF comprobó desde la rama completa 12 migraciones aplicadas y sin cambios pendientes del modelo. No se aplicaron migraciones durante la revisión. Falta `ConnectionStrings:SqlServerTest`, necesaria para las pruebas concurrentes contra una base aislada `PoraquiIntegrationTests`. Nunca usar la BD compartida en pruebas que limpien datos; no borrar, recrear ni reiniciar datos de desarrollo. La API no migra al arrancar.

Antes de futuras migraciones: revisar historial y SQL generado, verificar que no haya operaciones destructivas, y aplicarlas explícitamente solo con destino validado. Mantener contraseñas fuera de Git, documentación y logs.

## Pendientes de la pila completa

Pruebas SQL Server de aceptación concurrente externa y asignación interna; SMS real con Twilio; reconexión SignalR desde cliente real; configuración de almacenamiento privado/Data Protection y publicación MonsterASP.NET. Ver [project-status.md](project-status.md), [api-contracts.md](api-contracts.md) y [order-flow.md](order-flow.md). Zonas/tarifas configuradas sustituyen distancias GPS; no hay transferencias automáticas ni frontend.
