# Contratos API y entre módulos

Estado: `main` integra la base inicial y PR #22–#26 hasta `1d75577`: sincronización offline, SignalR, opciones de catálogo, horarios comerciales, cancelaciones/incidencias y reembolsos manuales. Un responsable mantiene contratos y migraciones. OpenAPI en Development: `/openapi/v1.json`.

## Integrado en `main` (base inicial y PR #22–#26)

### Identidad, permisos y negocio

- Identity API bajo `/api/auth` (registro/login bearer) cuando existe `ConnectionStrings:DefaultConnection`.
- `POST /api/auth/bootstrap-admin`: primer administrador, protegido por `Bootstrap:AdminKey` privada.
- `POST /api/businesses`: alta por usuario autenticado; se asigna `BusinessOwner`.
- `GET /api/businesses/active`, `GET/PUT /api/businesses/{id}` y `PUT /api/businesses/{id}/active`.
- El acceso a negocio se autoriza por rol y propietario; otros propietarios reciben 403.
- Roles: `PlatformAdmin`, `BusinessOwner`, `DeliveryPerson`, `ExternalCourier`; policies `PlatformAdministration`, `ManageBusiness`, `DeliveryOperations`.

### Catálogo, tarifa y pedido

- `GET/POST /api/businesses/{id}/products`; `PUT /api/products/{productId}`.
- Negocio administra grupos de opciones con `POST /api/products/{productId}/option-groups`; cambia disponibilidad del grupo/opción con `PUT /api/option-groups/{optionGroupId}/availability` y `PUT /api/product-options/{optionId}/availability`.
- Los productos muestran solo grupos/opciones disponibles. Al crear pedido, `items[].optionIds` debe satisfacer los límites configurados; el servidor valida disponibilidad y calcula el precio de cada línea con los incrementos de opción. El pedido conserva snapshot de nombres e importes.
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
- Migración `ExternalCourierOffers` y script SQL revisables en `src/Poraqui.Infrastructure/Persistence/Migrations/` y `docs/sql/20261017191548_ExternalCourierOffers.sql`. EF confirmó aplicada en la BD compartida dentro de la cadena actual.

## Integrado: sincronización de entrega (PR #22)

- Courier asignado sube una confirmación capturada sin conexión con `POST /api/orders/{orderId}/sync/delivery-confirmation`, con `commandId`, `code` y `clientCapturedAtUtc`.
- El servidor no confía en el resultado local: valida autorización del courier, asignación vigente, estado y código antes de persistir la entrega. La hora local se conserva solo como dato de auditoría; el servidor registra la hora de aceptación.
- Repetir el mismo ID, orden y payload devuelve el resultado previamente persistido con `replayed: true` y no agrega otra transición al historial. Reutilizar ID para otro payload/orden responde 409. Código incorrecto no registra el comando y puede reintentarse con el mismo ID.
- El cambio de estado y el registro idempotente se guardan en la misma transacción EF. Migración `OfflineDeliveryCommandSync` crea tabla de claves de comando; script revisable `docs/sql/20261017192726_OfflineDeliveryCommandSync.sql`. EF reportó la migración aplicada en la BD compartida; la rama específica del PR no ejecutó migraciones.

## Validación, antecedentes y pendientes

La rama de PR #7 pasa 14/14 pruebas con SQLite aislado; la pila completa pasa 24 y omite dos carreras que requieren SQL Server aislado. SQLite no verifica por sí solo los bloqueos, índices y concurrencia de SQL Server.

El contrato provisional `src/Poraqui.Application/Delivery/DeliveryOrderContracts.cs` se eliminó en PR #4 al implementar reparto propio. La asignación previa a OpenCode se conserva como antecedente; hay un solo responsable.

La cadena privada `ConnectionStrings:DefaultConnection` existe y EF verificó SQL Server. `ConnectionStrings:SqlServerTest` falta; configura esa clave solo con una base aislada `PoraquiIntegrationTests` para ejecutar las carreras.

## Integrado en `main`: SignalR de pedidos (PR #23)

- Hub autenticado en `/hubs/orders`. La conexión usa el bearer de Identity; `access_token` de query string se procesa solo para esta ruta.
- `SubscribeOrder(orderId)` autoriza cliente propietario, negocio propietario, courier asignado o administrador. Devuelve `{ orderId, status, version, changedAtUtc }` y suscribe al grupo asociado al pedido.
- Tras guardar una transición, se publica `OrderChanged` con estado, versión y fecha. No incluye dirección, teléfono, comprobante ni códigos. Un fallo de publicación queda registrado y no revierte una transición persistida.
- La suscripción vuelve a consultar el snapshot después de entrar al grupo para cubrir cambios que ocurran mientras se autoriza/conecta. Al reconectar, invocar `SubscribeOrder` otra vez para recuperar el estado actual.
- Pruebas de acceso cruzado, negociación anónima y entrega WebSocket en TestServer existen en la rama; esta rama requiere volver a compilar y ejecutarlas tras el ajuste de snapshot.

## Integrado en `main`: opciones del catálogo (PR #24)

- `POST /api/products/{productId}/option-groups` recibe `name`, `minSelections`, `maxSelections` y `options[]` (`name`, `priceDelta`). Cada opción parte disponible.
- Grupo y opción pueden habilitarse/deshabilitarse solo por propietario del negocio o admin.
- El pedido envía `optionIds` por renglón; selecciones repetidas/no disponibles o límites inválidos se rechazan. Se conservan opciones, nombres e importes en el pedido.
- Migración aditiva `CatalogProductOptions`; el historial y las tablas se observaron en SQL Server de desarrollo. Script revisable en `docs/sql/CatalogProductOptions.sql`.

## Integrado en `main`: horarios del negocio (PR #25)

- `PUT /api/businesses/{businessId}/opening-hours` configura zona horaria y exactamente un registro por cada día. Días cerrados no aceptan horas; los días abiertos requieren hora de apertura anterior al cierre.
- `GET /api/businesses/{businessId}/opening-hours` es público y devuelve zona horaria, los siete días configurados y `isAcceptingOrders` calculado con el reloj UTC del servidor convertido a la zona del negocio.
- Solo el dueño del negocio o un administrador puede modificar el horario. El servidor rechaza pedidos cuando la empresa está cerrada. Un negocio sin horario configurado conserva el comportamiento anterior y acepta pedidos cuando está activo.
- Migración aditiva `BusinessOpeningHours`: agrega dos columnas a `Businesses` con defaults y una tabla semanal con FK. Su tabla figura en la BD de desarrollo. Script revisable: `docs/sql/BusinessOpeningHours.sql`.

## Integrado en `main`: cancelaciones, incidencias y reembolsos manuales (PR #26)

- Cliente cancela antes de enviar comprobante; negocio propietario/admin puede cancelar hasta antes de que el repartidor recoja. Se guarda motivo, actor, fecha e historial; las ofertas externas abiertas se cancelan con el pedido.
- Si existe transferencia pendiente o aprobada, se registra un caso de reembolso manual. `GET .../refund` consulta el caso y `POST .../refund-review` permite al negocio propietario/admin marcarlo reembolsado con importe y referencia, o marcar que no se requiere. La API no transfiere dinero.
- Los participantes autorizados consultan y reportan incidencias con `GET/POST /api/orders/{orderId}/incidents`; el negocio propietario/admin resuelve por `POST .../incidents/{incidentId}/resolve`.
- Migraciones aditivas `OrderCancellationRefundsIncidents` y `OrderCancellationAuditConstraints`, con SQL revisable en `docs/sql/OrderCancellationRefundsIncidents.sql`.

## En desarrollo: idempotencia de transiciones

Decisiones de negocio, inicio/listo de preparación, recogida, inicio del recorrido y confirmación de entrega toleran reintentos con el mismo actor y datos, sin duplicar eventos o pago de recogida. Acciones sobre ofertas externas también comparan su estado ya persistido. Parámetros distintos continúan sujetos a validación/conflicto. No se añade migración en este incremento.

Pendiente: OTP real, idempotencia de reportes, creación de pedidos, comprobantes y asignación, métricas, reconexión SignalR en cliente real, pruebas SQL Server aisladas y despliegue. No hay GPS, distancias inferidas ni transferencias bancarias automáticas.
