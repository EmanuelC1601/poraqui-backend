# Contratos API y entre módulos

## Integrado en `main` (PR #1–#6)

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

## Integrado en `main`: reparto externo (PR #6)

- Admin habilita/deshabilita perfil externo con `PUT /api/admin/users/{id}/external-courier` (`isExternalCourier`).
- Courier establece disponibilidad y zona con `PUT /api/courier/external/availability` (`isAvailable`, `zoneId`).
- Negocio publica con `POST /api/businesses/{businessId}/orders/{orderId}/external-offers`, indicando `remuneration`, `conditions` y `expiresInMinutes`. Responde `NoCouriersAvailable` si nadie está disponible y `OffersAlreadyActive` al reintentar una publicación que ya existe.
- Courier consulta `GET /api/courier/external/offers`; ve origen, destino, zona, condiciones, remuneración y vencimiento antes de aceptar.
- Courier rechaza o acepta con `POST /api/courier/external/offers/{offerId}/reject` y `/accept`. Aceptar guarda oferta y asignación del pedido en una transacción y devuelve una vez el código de recogida. Al recoger se conserva el registro existente de pago del negocio.
- Negocio cancela ofertas abiertas con `POST /api/businesses/{businessId}/orders/{orderId}/external-offers/cancel`. Al listar o aceptar, las ofertas vencidas se marcan como expiradas.
- `ExternalDeliveryOffer.Version`, `Order.Version` y un índice único filtrado por pedido aceptado protegen frente a respuestas concurrentes. Falta ejecutar la carrera en SQL Server.
- Migración `ExternalCourierOffers` y script SQL están en `src/Poraqui.Infrastructure/Persistence/Migrations/` y `docs/sql/20261017191548_ExternalCourierOffers.sql`; EF reporta la migración aplicada en desarrollo. La prueba de aceptación simultánea sobre SQL Server aislado sigue pendiente.

## Implementado en rama apilada: sincronización de entrega (PR #7)

- Courier asignado sube una confirmación capturada sin conexión con `POST /api/orders/{orderId}/sync/delivery-confirmation`, con `commandId`, `code` y `clientCapturedAtUtc`.
- El servidor no confía en el resultado local: valida autorización del courier, asignación vigente, estado y código antes de persistir la entrega. La hora local se conserva solo como dato de auditoría; el servidor registra la hora de aceptación.
- Repetir el mismo ID, orden y payload devuelve el resultado previamente persistido con `replayed: true` y no agrega otra transición al historial. Reutilizar ID para otro payload/orden responde 409. Código incorrecto no registra el comando y puede reintentarse con el mismo ID.
- El cambio de estado y el registro idempotente se guardan en la misma transacción EF. Migración `OfflineDeliveryCommandSync` crea tabla de claves de comando; script revisable `docs/sql/20261017192726_OfflineDeliveryCommandSync.sql`. PR #7 está integrado en `feature/external-courier-offers`, aún no en `main`; EF reporta la migración aplicada en desarrollo.

## Implementado en rama apilada: actualizaciones en tiempo real (PR #8)

- Hub autenticado en `/hubs/orders`. Los clientes SignalR envían el bearer de Identity en `access_token` al negociar/conectar; el servidor acepta ese parámetro solo en esta ruta.
- Tras conectar, invocar `SubscribeOrder(orderId)`. El servidor valida la relación con el pedido: cliente propietario, negocio propio, courier asignado o administrador. Devuelve una instantánea actual `{ orderId, status, version, changedAtUtc }` y agrega la conexión al grupo de ese pedido autorizado.
- Los cambios se notifican como `OrderChanged` después de guardar la transición. Payload: `{ orderId, status, version, changedAtUtc }`; no incluye dirección, teléfono, comprobante ni códigos.
- Invocar `UnsubscribeOrder(orderId)` al salir de la pantalla. Después de una reconexión, volver a invocar `SubscribeOrder` para recuperar la versión/estado persistidos y recibir eventos futuros.
- Un fallo de SignalR no revierte la transición guardada; se registra y el cliente puede consultar `GET /api/orders/{id}`, `GET /api/businesses/{businessId}/orders/{orderId}` o `GET /api/courier/orders/{orderId}` con su permiso. Las notificaciones no son una cola durable.
- Pruebas: conexión WebSocket SignalR real en TestServer/SQLite temporal; el cliente recibe `OrderChanged` después del envío del comprobante y la consulta confirma el estado persistido. También se verifica negociación anónima rechazada y acceso cruzado de otro negocio denegado. PR #8 está integrado en `feature/offline-delivery-sync`, aún no en `main`; el cliente real debe volver a invocar `SubscribeOrder` tras reconectar.

## En PR abierto: opciones de catálogo (PR #9 sobre PR #8)

- Grupos con selección mínima/máxima y variantes con diferencia de precio no negativa. Grupo/variante se puede poner no disponible; la lectura pública oculta los no disponibles y el servidor rechaza su selección.
- El precio unitario de línea refleja las opciones y el pedido guarda una copia de nombre/importe para conservar el recibo si el negocio cambia el catálogo después.
- Migración `CatalogProductOptions` y `docs/sql/CatalogProductOptions.sql` crean tres tablas e índices aditivos. EF reporta aplicada esta migración en desarrollo. El cambio de API y reglas sigue en PR #9; no está integrado en `main`.

## Validación, antecedentes y pendientes

`dotnet test Poraqui.sln` ejercita un recorrido aislado SQLite con importes, permisos, transferencia, comprobante, oferta externa, cancelación, ausencia de repartidores, rechazo, aceptación, códigos, pago al recoger, sincronización offline y SignalR. SQLite no verifica por sí solo los bloqueos, índices y concurrencia de SQL Server. `ConnectionStrings:DefaultConnection` está presente en User Secrets y una consulta EF de solo lectura confirmó las migraciones base hasta `CatalogProductOptions`; la conexión aislada `ConnectionStrings:SqlServerTest` sigue ausente.

El contrato provisional `src/Poraqui.Application/Delivery/DeliveryOrderContracts.cs` se eliminó en PR #4 al implementar reparto propio. La asignación previa a OpenCode se conserva como antecedente; hay un solo responsable.

PR #18–#21 implementan idempotencia durable para incidencias, creación de pedidos, comprobantes y asignación de courier propio; están integrados en la pila `feature/phone-verification`, aún fuera de `main`. Pendiente: integrar PR #7–#21 en orden, horarios, OTP configurado con proveedor real, cancelaciones/incidencias/reembolsos completos, métricas, despliegue y pruebas de concurrencia SQL Server. No hay GPS, distancias inferidas ni transferencias bancarias automáticas.
