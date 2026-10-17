# Handoff de desarrollo

## Base, ramas y responsabilidad

`main` contiene PR #1–#5 hasta `d677167` y es la base de integración. PR #6 agrega reparto externo; PR #7 sincronización offline está apilado sobre #6; PR #8 SignalR está apilado sobre #7; PR #9 opciones de catálogo se apila sobre #8; PR #10 horarios se apila sobre #9; la rama actual de cancelaciones/incidencias/reembolsos continúa desde #10. Un solo responsable mantiene todos los módulos, contratos, `AppDbContext` y migraciones. OpenCode ya no participa; su anterior asignación de reparto se conserva como antecedente.

## Incremento previo: sincronización offline (PR #7)

Agrega `POST /api/orders/{orderId}/sync/delivery-confirmation` para confirmaciones capturadas offline. El servidor valida courier y código con el estado actual del pedido; el dato de hora del dispositivo se guarda como auditoría y no determina una transición. Guarda transición e ID/payload idempotente en una transacción. Un reintento idéntico devuelve la aceptación guardada; misma clave para datos distintos responde 409.

Migración nueva `OfflineDeliveryCommandSync` crea tabla e índice por pedido. Script desde la migración externa anterior: `docs/sql/20261017192726_OfflineDeliveryCommandSync.sql`. No altera ni elimina tablas existentes. Aún no aplicada.

## Verificación

Verificado antes de publicar PR #7: `dotnet build Poraqui.sln` (0 advertencias, 0 errores), `dotnet test Poraqui.sln` (14/14; SQLite en memoria y archivos temporales), `git diff --check` y `dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/Poraqui.Infrastructure --startup-project src/Poraqui.Api` (sin cambios pendientes en el modelo). La suite cubre código inválido, acceso del courier asignado, sync aceptado, repetición sin duplicado y colisión de clave.

PR #7 está publicado y apilado sobre PR #6; PR #8 agrega SignalR encima de #7. La base compartida se inspeccionó desde el portal de solo lectura: `dbo` sin tablas. Falta `ConnectionStrings:DefaultConnection` en la configuración privada de la API, así que no se verificó EF contra SQL Server, migraciones vivas ni aceptación simultánea real. Mantener PR #6/#7 abiertos hasta hacer esas verificaciones. No ejecutar migraciones hasta configurar la clave y revisar estado actual. La API no aplica migraciones automáticamente al iniciar.

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api
```

## Incremento SignalR

`/hubs/orders` acepta solo conexiones autenticadas. `SubscribeOrder(orderId)` valida cliente, propietario, courier asignado o administrador y devuelve `{ orderId, status, version, changedAtUtc }`. Los eventos `OrderChanged` se publican tras persistir y contienen solo estado/version. Al reconectar, volver a invocar `SubscribeOrder` para recuperar la instantánea actual. La suite completa 14/14 pasa en SQLite; una conexión WebSocket SignalR en TestServer recibe el evento y luego lee la transición persistida. Falta verificar reconexión de un cliente real.

## Incremento actual: opciones de catálogo

Agrega grupos de opciones obligatorios/opcionales con límites de selección, opciones con diferencia de precio y cambios de disponibilidad. El alta requiere propietario o administrador. La lectura pública oculta las opciones no disponibles. `POST /api/orders` recibe `optionIds`; el servidor valida disponibilidad y cardinalidad antes de calcular subtotal/total. La línea guarda un snapshot de nombre y precio elegido para mantener el recibo histórico.

Migración `CatalogProductOptions` crea `ProductOptionGroups`, `ProductOptions` y `OrderLineOptions`; script aditivo `docs/sql/CatalogProductOptions.sql`, generado desde `OfflineDeliveryCommandSync`. No aplicado. Verificado con `dotnet build Poraqui.sln` (0 advertencias/errores), `dotnet test Poraqui.sln` (14/14; SQLite temporal), `git diff --check` y `migrations has-pending-model-changes` (sin cambios). SQL Server no está configurado.

## Incremento actual: horarios del negocio

Rama `feature/business-hours`, commit `d25e64f`, PR #10: https://github.com/EmanuelC1601/poraqui-backend/pull/10. Configura siete días y una zona horaria mediante `PUT /api/businesses/{businessId}/opening-hours`; consulta horario y disponibilidad con `GET /api/businesses/{businessId}/opening-hours`. Los pedidos se rechazan fuera del horario local. Los negocios sin horario conservan temporalmente el comportamiento abierto previo. Migración `BusinessOpeningHours` y script `docs/sql/BusinessOpeningHours.sql`; es aditiva y no aplicada. `dotnet build Poraqui.sln` y `dotnet test Poraqui.sln` pasan (15/15, SQLite temporal); la migración no tiene cambios pendientes del modelo. Falta validación SQL Server.

## Incremento actual: cancelaciones, incidencias y reembolsos manuales

La rama `feature/order-cancellations-refunds-incidents` implementa cancelación del cliente antes del comprobante y cancelación del negocio antes de asignar courier. Las ofertas externas abiertas se cancelan en el mismo guardado. Evidencia enviada o pago aprobado crea seguimiento manual; rechazo del negocio tras aprobación también lo crea. El negocio registra resolución/refund de forma manual, el cliente consulta estado. Los participantes autorizados reportan incidencias y el negocio/admin resuelve. Cambios se guardan en historial/versionado y notifican después de persistir.

Migraciones `OrderCancellationRefundsIncidents` y `OrderCancellationAuditConstraints`; script `docs/sql/OrderCancellationRefundsIncidents.sql`, no aplicados. `dotnet test Poraqui.sln` pasa 18/18 con SQLite temporal; se debe verificar migración SQL Server y concurrencia antes de integrar. PR todavía pendiente.

## Siguiente trabajo

Completar OTP, idempotencia de los comandos restantes, métricas y despliegue. Reconexión SignalR de cliente real y pruebas SQL Server siguen pendientes. Los PR #6–#10 están publicados pero no integrados porque sus cambios incluyen migraciones sin validar en la BD configurada. Ver contratos y ledger en `docs/api-contracts.md` y `docs/project-status.md`.
