# Contratos API y entre módulos

## Implementado en `main` (PR #1–#4)

### Identidad, permisos y negocio

- Identity API bajo `/api/auth` (registro/login bearer) cuando existe `ConnectionStrings:DefaultConnection`.
- `POST /api/auth/bootstrap-admin`: primer administrador, protegido por `Bootstrap:AdminKey` privada.
- `POST /api/businesses`: alta por usuario autenticado; se asigna `BusinessOwner`.
- `GET /api/businesses/active`, `GET/PUT /api/businesses/{id}` y `PUT /api/businesses/{id}/active`.
- Admin/propietario acceden a recurso de negocio; otros propietarios reciben 403.
- Roles: `PlatformAdmin`, `BusinessOwner`, `DeliveryPerson`, `ExternalCourier`; policies `PlatformAdministration`, `ManageBusiness`, `DeliveryOperations`.

### Catálogo y creación de pedido

- `GET /api/businesses/{id}/products`; `POST /api/businesses/{id}/products`; `PUT /api/products/{productId}`.
- `GET /api/delivery-zones/active`; admins administran zonas con `POST/PUT /api/delivery-zones`.
- Negocio/admin administra tarifas por zona con `GET/POST /api/businesses/{id}/delivery-rates` y `PUT /api/businesses/{id}/delivery-rates/{zoneId}`.
- `POST /api/orders`: cliente autenticado envía negocio, zona, dirección, producto IDs y cantidades. Servidor obtiene precios y tarifa activos; no acepta importes del cliente.

### Pago manual, pedido y courier propio

- `PUT /api/account/phone`: cliente guarda su teléfono. Aún no se valida por OTP.
- Cliente carga comprobante privado con `POST /api/orders/{id}/transfer-evidence` (`multipart/form-data`, campo `file`, PDF/JPEG/PNG, máximo 8 MB). Solo negocio propietario/admin puede descargarlo como archivo adjunto.
- Negocio/admin registra revisión de pago en `POST /api/businesses/{businessId}/orders/{orderId}/payment-review`; aprobación deja `AwaitingBusinessDecision`, separada de aceptación del negocio.
- `POST .../decision`, `POST .../preparation`, `POST .../ready` para decisión del negocio e inicio explícito de preparación.
- Admin gestiona rol courier propio con `PUT /api/admin/users/{id}/delivery-person`; negocio asigna en `POST .../assign-courier` y establece remuneración que pagará al recoger.
- Courier propio consulta pedidos asignados en `GET /api/courier/orders` y `GET /api/courier/orders/{orderId}`.
- `POST /api/orders/{id}/pickup` exige código y registra por separado que el repartidor confirma haber recibido el pago del negocio.
- `POST /api/orders/{id}/delivery/start` permite `whatsapp` o `call`; solo el courier asignado recibe el teléfono.
- `POST /api/orders/{id}/delivery/confirm` exige código del cliente.
- Cliente consulta `GET /api/orders/{id}`; negocio consulta `GET /api/businesses/{businessId}/orders` y `GET .../orders/{orderId}`. Devuelven estados e historial.

Cada transición incrementa `Order.Version` como token optimista de concurrencia. La persistencia responde conflicto si se intenta guardar una decisión desde un estado obsoleto. Los códigos son seis dígitos, el de recepción se devuelve al cliente al crear el pedido y se persisten como hashes; pickup se muestra una vez al negocio al asignar. Los detalles de transferencia del negocio y el snapshot del pedido se están implementando en `feature/business-payment-details`.

## En implementación local: `feature/business-payment-details`

- `GET/PUT /api/businesses/{businessId}/transfer-details`: dueño/admin configura y consulta destinatario, institución, referencia y nota de transferencia.
- `POST /api/orders` requiere estos datos; los copia al pedido y solo el cliente propietario ve las instrucciones persistidas. Actualizar los datos del negocio no cambia pedidos existentes.
- Migración aditiva revisable `BusinessTransferInstructionsSnapshot`; pendiente aplicar/validar en SQL Server.

La suite tiene un recorrido aislado SQLite que prueba importes, autorización entre negocios, archivo privado, revisión manual, courier y entrega. La prueba de snapshot comprueba que cambiar la cuenta del negocio no altera instrucciones de la orden creada.

## Antecedentes y pendientes

El archivo provisional `src/Poraqui.Application/Delivery/DeliveryOrderContracts.cs` se eliminó en PR #4 tras implementar casos de uso persistidos. El plan inicial incluyó a OpenCode para reparto; el proyecto usa un único responsable.

Pendiente: opciones de producto, disponibilidad por horario, verificación de teléfono, datos/cancelaciones/reembolsos más completos, reparto externo con ofertas y aceptación atómica, SignalR, sincronización offline e idempotencia, incidencias, métricas y validación SQL Server. El enum de estados actual no incluye todavía el flujo externo completo.
