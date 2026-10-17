# Contratos HTTP y entre módulos

## Estado y origen

`main` (PR #1–#6, `d39d265`) es la base de integración. La pila posterior PR #7–#21 está consolidada en `feature/backend-integration-verified`, aún pendiente de PR a `main`. PR #14, #19, #20 y #21 están confirmados como merged en sus ramas apiladas. Development OpenAPI se publica en `/openapi/v1.json`; [snapshot OpenAPI generado](openapi.json) contiene 61 rutas y 49 esquemas, verificados con la API compilada en Development. No se aplican migraciones al iniciar la API.

La API usa bearer de Identity, salvo endpoints de autenticación y bootstrap protegidos por clave privada. El acceso a recursos valida rol y relación con el recurso; por ejemplo, un propietario de negocio no puede operar pedidos de otro negocio.

## Identidad y comercio

- `/api/auth`: registro, login y bootstrap del primer administrador (`Bootstrap:AdminKey`). Roles principales: `PlatformAdmin`, `BusinessOwner`, `DeliveryPerson`, `ExternalCourier`.
- `POST /api/businesses`; `GET /api/businesses/active`; `GET/PUT /api/businesses/{id}`; activar negocio mediante la ruta de administración. La administración del comercio se limita al dueño o admin.
- `/api/businesses/{id}/products`, `/api/products/{productId}`, `/api/products/{productId}/option-groups` y rutas de disponibilidad para productos/grupos/opciones. El servidor valida pertenencia, disponibilidad y límites de selección.
- `/api/delivery-zones` y `/api/businesses/{id}/delivery-rates`: zonas/tarifas configuradas; no hay geolocalización ni distancia calculada.
- `/api/businesses/{id}/opening-hours`: configuración semanal y zona horaria; pedidos nuevos se rechazan cuando el horario configurado indica cerrado.
- `/api/businesses/{id}/transfer-details`: datos e instrucciones de transferencia para cliente/pedido. No se procesan transferencias automáticamente.

## Pedido, precio y pago

- `POST /api/orders` exige `commandId` GUID estable. El cliente envía IDs de producto/opciones y cantidades; el servidor valida disponibilidad y calcula precio de productos más tarifa de zona. Repetir actor, clave y payload devuelve el mismo pedido; reutilizar clave con otro payload da 409. La respuesta incluye snapshots de catálogo/transferencia y código de recepción protegido.
- `POST /api/orders/{id}/transfer-evidence` recibe multipart `file` y `commandId`, acepta PDF/JPEG/PNG hasta 8 MB y almacena el archivo en ruta privada. Reintento igual no crea otra revisión/evento; colisión de payload devuelve 409. Descarga limitada al dueño del negocio/admin.
- `POST /api/businesses/{businessId}/orders/{orderId}/payment-review` registra revisión manual. El negocio luego acepta/rechaza con `.../decision`; preparación inicia explícitamente con `.../preparation` y el negocio marca listo con `.../ready`.
- El rechazo/cancelación con comprobante o pago confirmado crea seguimiento manual de reembolso. Cliente ve el estado; negocio/admin registra revisión/resultado manual. No hay transferencia bancaria automática.

## Reparto propio y externo

- Reparto propio: admin asigna rol con `PUT /api/admin/users/{id}/delivery-person`; el negocio asigna courier mediante `POST /api/businesses/{businessId}/orders/{orderId}/assign-courier`, con `commandId`. Courier y código de recogida se registran atómicamente y el reintento devuelve el código original protegido; colisión o segunda asignación da 409.
- Courier consulta `GET /api/courier/orders` y `GET /api/courier/orders/{orderId}`. `POST /api/orders/{id}/pickup` verifica el código y registra, separado del precio del pedido, que el negocio pagó al courier al recoger. No regresa a cobrar.
- Reparto externo: admin configura perfil; courier publica disponibilidad y zona; negocio publica oferta con origen/destino, condiciones, expiración y remuneración. `GET /api/courier/external/offers` muestra esos datos antes de aceptar. Rutas `/accept`, `/reject` y cancelación de ofertas permiten responder/cancelar; expiradas se vencen al consultar/aceptar y no disponibles responde `NoCouriersAvailable`.
- Aceptación externa es transaccional, usa versión e índice único filtrado para asignar un pedido a una sola oferta. Hay pruebas de concurrencia escritas para SQL Server; quedan sin ejecutar hasta configurar BD aislada `PoraquiIntegrationTests`.
- `POST /api/orders/{id}/delivery/start` inicia recorrido y acepta `contactChannel=whatsapp|call`; el contacto del cliente solo se devuelve al courier asignado. El contacto se realiza manualmente fuera de la API.
- `POST /api/orders/{id}/delivery/confirm` exige código de recepción; `POST /api/orders/{id}/sync/delivery-confirmation` permite sincronizar una acción offline, pero el servidor vuelve a validar actor, asignación, estado y código antes de guardar.
- Cliente, negocio y courier autorizado consultan pedido/estado/historial en sus rutas GET. Todos los comandos idempotentes repiten la respuesta persistida sin incrementar versión ni duplicar historial/notificación; mismo ID con contenido distinto devuelve 409.

## Incidencias, señalización y métricas

- `/api/orders/{id}/incidents` requiere `commandId`; un replay equivalente devuelve el registro original. Negocio/admin resuelve la incidencia. Cancelaciones crean auditoría y, cuando corresponde, caso de reembolso manual.
- Hub autenticado `/hubs/orders`: `SubscribeOrder(orderId)` comprueba acceso y devuelve snapshot persistido; `OrderChanged` se envía después de persistir y omite información personal. Al reconectar, cliente vuelve a suscribirse para obtener la versión actual.
- `GET /api/admin/metrics/pilot?fromUtc=&toUtc=` requiere admin y devuelve agregados acotados a 366 días, sin datos personales.
- OTP: `POST /api/account/phone/verification` y `/confirm` usan Twilio Verify; solo `approved` confirma Identity. Límite de cinco inicios por IP cada diez minutos. No se persisten códigos.

## Esquema y verificación

Migraciones EF, confirmadas aplicadas por EF en SQL Server de desarrollo el 2026-10-17: `InitialCommerceAndIdentity`, `BusinessTransferInstructionsSnapshot`, `ExternalCourierOffers`, `OfflineDeliveryCommandSync`, `CatalogProductOptions`, `BusinessOpeningHours`, `OrderCancellationRefundsIncidents`, `OrderCancellationAuditConstraints`, `IncidentCommandIdempotency`, `OrderCreationCommandIdempotency`, `TransferEvidenceCommandIdempotency`, `CourierAssignmentCommandIdempotency`. El snapshot está actualizado y EF no detecta cambios pendientes.

Build .NET 10: 0 advertencias/errores. Suite actual: 24 aprobadas, 2 omitidas de 26 por falta de conexión SQL Server de pruebas aislada. Los recorridos API usan SQLite aislado; no sustituye pruebas de restricciones y concurrencia del proveedor. Configura `ConnectionStrings__SqlServerTest` con una BD separada llamada `PoraquiIntegrationTests`, nunca con la BD compartida.

Los contratos anteriores documentan el estado de la rama de integración; el OpenAPI generado en ejecución es la fuente de los esquemas concretos de petición/respuesta.
