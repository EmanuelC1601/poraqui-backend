# Contratos API

Estado: `main` es la base única de integración. Este documento describe los contratos implementados; revisar `docs/project-status.md` para evidencia y pendientes. OpenAPI en Development: `/openapi/v1.json`.

## Identidad, permisos y comercio

- Identity API bajo `/api/auth` con bearer; bootstrap inicial `/api/auth/bootstrap-admin` requiere `Bootstrap:AdminKey` privada.
- Roles: `PlatformAdmin`, `BusinessOwner`, `DeliveryPerson`, `ExternalCourier`; policies `PlatformAdministration`, `ManageBusiness`, `DeliveryOperations`.
- Negocios en `/api/businesses`; catálogo en `/api/businesses/{id}/products`, opciones en `/api/products/{id}/option-groups` y disponibilidad en endpoints `/availability`.
- Zonas `/api/delivery-zones/active`; administración por plataforma. Negocio configura tarifas por zona y horarios semanales.
- El pedido recibe IDs, cantidades y opciones; servidor calcula precios y tarifa usando catálogo vigente. Nunca acepta precios del cliente. Persiste snapshots de artículos, opciones, pago y tarifa.
- El propietario del recurso o admin puede administrarlo; los permisos se validan sobre el negocio/pedido asociado, no solo por rol.

## Pedido, pago y entrega

- Cliente crea con `POST /api/orders`. Requiere `commandId` GUID estable por intento lógico. Repetir ID con mismo payload devuelve mismo pedido y código; payload distinto con el mismo ID da 409. La migración aditiva `OrderCreationCommandIdempotency` está integrada en PR #29.
- Cliente sube comprobante privado a `POST /api/orders/{id}/transfer-evidence` como multipart `commandId` + `file`. PR #30 requiere `commandId` en multipart, devuelve replay con el mismo ID/hash y 409 si contenido/orden difiere; migración aditiva integrada. Negocio revisa pago manualmente, acepta/rechaza, inicia preparación y marca listo.
- Reparto propio: admin habilita rol y negocio asigna con monto pagadero al recoger. `POST /api/orders/{id}/pickup` valida el código y registra pago negocio→repartidor por separado del pago cliente→negocio.
- Reparto externo: admin habilita perfil; courier marca disponibilidad/zona; negocio publica oferta con origen, destino, condiciones y remuneración; courier acepta/rechaza. La asignación acepta solo un ganador y cancela/vence ofertas según flujo persistido.
- Repartidor inicia recorrido eligiendo `whatsapp` o `call`; teléfono solo se revela al asignado. Confirma entrega con código del cliente. Estados e historial consultables por participantes autorizados.
- Acciones de transición reintentables no duplican historial ni pagos. Reportes de incidentes aceptan `commandId`; reembolso es seguimiento manual, sin movimiento bancario de la API.
- Sync offline de entrega requiere `commandId`, código y hora de captura; el servidor vuelve a comprobar autorización, estado y código antes de aceptar.
- `GET /api/admin/metrics/pilot?fromUtc={utc}&toUtc={utc}`, restringido a `PlatformAdmin`. El rango predeterminado son los últimos 30 días y el máximo es 366 días; devuelve cantidad, valor bruto de pedidos, entregados/cancelados, tiempo medio de entrega, incidencias abiertas, reembolsos por revisar y conteos por estado. Importes agregan pedidos creados dentro del intervalo.

## SignalR

Hub autenticado `/hubs/orders`; `SubscribeOrder(orderId)` valida cliente, negocio, courier asignado o admin y devuelve snapshot de estado/versión. Publica `OrderChanged` después de persistir, con datos mínimos. Al reconectar, el cliente vuelve a invocar `SubscribeOrder` para obtener el snapshot actual.

## Operación y límites

No se ejecuta migración al iniciar la API. Migraciones y scripts revisables están bajo `src/Poraqui.Infrastructure/Persistence/Migrations` y `docs/sql`. Una consulta de solo lectura al historial SQL Server del 2026-10-17 confirmó que todas las migraciones actuales están aplicadas. `ConnectionStrings:SqlServerTest` debe apuntar a una base aislada para verificar concurrencia. Sin GPS ni distancia inferida; no hay llamadas automáticas de WhatsApp, OTP productivo ni pagos bancarios automáticos.
