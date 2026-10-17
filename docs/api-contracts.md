# Contratos API y entre módulos

## Integrado en `main` (PR #1–#5)

### Identidad, permisos y negocio

- Identity API bajo `/api/auth` (registro/login bearer) cuando existe `ConnectionStrings:DefaultConnection`.
- `POST /api/auth/bootstrap-admin`: primer administrador, protegido por `Bootstrap:AdminKey` privada.
- `POST /api/businesses`: alta por usuario autenticado; se asigna `BusinessOwner`.
- `GET /api/businesses/active`, `GET/PUT /api/businesses/{id}` y `PUT /api/businesses/{id}/active`.
- El acceso a negocio se autoriza por rol y propietario; otros propietarios reciben 403.
- Roles: `PlatformAdmin`, `BusinessOwner`, `DeliveryPerson`, `ExternalCourier`; policies `PlatformAdministration`, `ManageBusiness`, `DeliveryOperations`.

### Catálogo, tarifa y pedido

- `GET/POST /api/businesses/{id}/products`; `PUT /api/products/{productId}`.
- `GET /api/delivery-zones/active`; admins administran zonas con `POST/PUT /api/delivery-zones`.
- Negocio/admin administra tarifas por zona con `GET/POST /api/businesses/{id}/delivery-rates` y `PUT /api/businesses/{id}/delivery-rates/{zoneId}`.
- `GET/PUT /api/businesses/{businessId}/transfer-details`: propietario/admin configura destinatario, institución, referencia e instrucciones.
- `POST /api/orders`: cliente envía negocio, zona, dirección, producto IDs y cantidades. El servidor obtiene precios y tarifa activos; no acepta importes del cliente. El pedido guarda snapshot de instrucciones de transferencia que solo ve el cliente del pedido.

### Pago manual y reparto propio

- `PUT /api/account/phone`: cliente guarda su teléfono; falta validación OTP.
- Cliente sube comprobante privado con `POST /api/orders/{id}/transfer-evidence` (`multipart/form-data`, `file`, PDF/JPEG/PNG, máximo 8 MB). Solo negocio propietario/admin puede descargarlo.
- Negocio/admin revisa pago con `POST /api/businesses/{businessId}/orders/{orderId}/payment-review`; aprobación deja `AwaitingBusinessDecision`.
- `POST .../decision`, `POST .../preparation` y `POST .../ready` gestionan decisión e inicio explícito de preparación.
- Admin asigna rol de courier propio con `PUT /api/admin/users/{id}/delivery-person`; negocio asigna con `POST .../assign-courier` y fija el importe que paga al recoger.
- Courier propio consulta asignaciones con `GET /api/courier/orders` y `GET /api/courier/orders/{orderId}`.
- `POST /api/orders/{id}/pickup` valida código y registra por separado que el courier recibió el pago del negocio.
- `POST /api/orders/{id}/delivery/start` permite `whatsapp` o `call`; solo el courier asignado recibe teléfono.
- `POST /api/orders/{id}/delivery/confirm` exige código de recepción del cliente.
- Cliente consulta `GET /api/orders/{id}`; negocio consulta `GET /api/businesses/{businessId}/orders` y `GET .../orders/{orderId}`. Incluyen estado e historial.

Cada transición de pedido incrementa `Order.Version`; la persistencia detecta escrituras obsoletas. Los códigos son de seis dígitos y se guardan hasheados; el código de recepción se entrega al cliente al crear el pedido y el código de recogida se entrega una vez al asignar.

## En desarrollo: reparto externo (`feature/external-courier-offers`)

- Admin habilita/deshabilita perfil externo con `PUT /api/admin/users/{id}/external-courier` (`isExternalCourier`).
- Courier establece disponibilidad y zona con `PUT /api/courier/external/availability` (`isAvailable`, `zoneId`).
- Negocio publica con `POST /api/businesses/{businessId}/orders/{orderId}/external-offers`, indicando `remuneration`, `conditions` y `expiresInMinutes`. Responde `NoCouriersAvailable` si nadie está disponible y `OffersAlreadyActive` al reintentar una publicación que ya existe.
- Courier consulta `GET /api/courier/external/offers`; ve origen, destino, zona, condiciones, remuneración y vencimiento antes de aceptar.
- Courier rechaza o acepta con `POST /api/courier/external/offers/{offerId}/reject` y `/accept`. Aceptar guarda oferta y asignación del pedido en una transacción y devuelve una vez el código de recogida. Al recoger se conserva el registro existente de pago del negocio.
- Negocio cancela ofertas abiertas con `POST /api/businesses/{businessId}/orders/{orderId}/external-offers/cancel`. Al listar o aceptar, las ofertas vencidas se marcan como expiradas.
- `ExternalDeliveryOffer.Version`, `Order.Version` y un índice único filtrado por pedido aceptado protegen frente a respuestas concurrentes. Falta ejecutar la carrera en SQL Server.
- Migración `ExternalCourierOffers` y script SQL revisables en `src/Poraqui.Infrastructure/Persistence/Migrations/` y `docs/sql/20261017191548_ExternalCourierOffers.sql`; no aplicados.

## En desarrollo: sincronización de entrega (`feature/offline-delivery-sync`, basada en PR #6)

- Courier asignado sube una confirmación capturada sin conexión con `POST /api/orders/{orderId}/sync/delivery-confirmation`, con `commandId`, `code` y `clientCapturedAtUtc`.
- El servidor no confía en el resultado local: valida autorización del courier, asignación vigente, estado y código antes de persistir la entrega. La hora local se conserva solo como dato de auditoría; el servidor registra la hora de aceptación.
- Repetir el mismo ID, orden y payload devuelve el resultado previamente persistido con `replayed: true` y no agrega otra transición al historial. Reutilizar ID para otro payload/orden responde 409. Código incorrecto no registra el comando y puede reintentarse con el mismo ID.
- El cambio de estado y el registro idempotente se guardan en la misma transacción EF. Migración `OfflineDeliveryCommandSync` crea tabla de claves de comando; script revisable `docs/sql/20261017192726_OfflineDeliveryCommandSync.sql`. No aplicado ni validado en SQL Server.

## En desarrollo: actualizaciones en tiempo real (`feature/realtime-order-updates`, basada en PR #7)

- Hub autenticado en `/hubs/orders`. Los clientes SignalR envían el bearer de Identity en `access_token` al negociar/conectar; el servidor acepta ese parámetro solo en esta ruta.
- Tras conectar, invocar `SubscribeOrder(orderId)`. El servidor valida la relación con el pedido: cliente propietario, negocio propio, courier asignado o administrador. Devuelve una instantánea actual `{ orderId, status, version, changedAtUtc }` y agrega la conexión al grupo de ese pedido autorizado.
- Los cambios se notifican como `OrderChanged` después de guardar la transición. Payload: `{ orderId, status, version, changedAtUtc }`; no incluye dirección, teléfono, comprobante ni códigos.
- Invocar `UnsubscribeOrder(orderId)` al salir de la pantalla. Después de una reconexión, volver a invocar `SubscribeOrder` para recuperar la versión/estado persistidos y recibir eventos futuros.
- Un fallo de SignalR no revierte la transición guardada; se registra y el cliente puede consultar `GET /api/orders/{id}`, `GET /api/businesses/{businessId}/orders/{orderId}` o `GET /api/courier/orders/{orderId}` con su permiso. Las notificaciones no son una cola durable.
- Pruebas: conexión WebSocket SignalR real en TestServer/SQLite temporal; el cliente recibe `OrderChanged` después del envío del comprobante y la consulta confirma el estado persistido. También se verifica negociación anónima rechazada y acceso cruzado de otro negocio denegado. SQL Server sigue sin comprobar.

## Validación, antecedentes y pendientes

`dotnet test Poraqui.sln` ejercita un recorrido aislado SQLite con importes, permisos, transferencia, comprobante, oferta externa, cancelación, ausencia de repartidores, rechazo, aceptación, códigos, pago al recoger y entrega. SQLite no verifica por sí solo los bloqueos, índices y concurrencia de SQL Server.

El contrato provisional `src/Poraqui.Application/Delivery/DeliveryOrderContracts.cs` se eliminó en PR #4 al implementar reparto propio. La asignación previa a OpenCode se conserva como antecedente; hay un solo responsable.

Pendiente: opciones de producto, horarios, OTP, cancelación de pedido, incidencias/reembolsos completos, idempotencia general para otros comandos, métricas, despliegue y verificación SQL Server incluyendo carreras simultáneas. No hay GPS, distancias inferidas ni transferencias bancarias automáticas.
