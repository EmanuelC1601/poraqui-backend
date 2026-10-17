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
- El negocio agrega grupos de opciones con `POST /api/products/{productId}/option-groups` (`name`, `minSelections`, `maxSelections`, `options[{name,priceDelta}]`); cambia disponibilidad con `PUT /api/option-groups/{id}/availability` y `PUT /api/product-options/{id}/availability`. Requiere propietario del producto o administrador.
- `GET /api/businesses/{id}/products` incluye solo grupos y opciones disponibles con límites y diferencias de precio. Al crear pedido, `items[]` puede incluir `optionIds[]`. El servidor comprueba opciones disponibles y límites de cada grupo, calcula `precio base + diferencias` y guarda snapshots de nombres/precios seleccionados en cada línea. El cliente no envía importes.
- `GET /api/delivery-zones/active`; admins administran zonas con `POST/PUT /api/delivery-zones`.
- Negocio/admin administra tarifas por zona con `GET/POST /api/businesses/{id}/delivery-rates` y `PUT /api/businesses/{id}/delivery-rates/{zoneId}`.
- Negocio/admin configura la semana y zona horaria con `PUT /api/businesses/{id}/opening-hours` (`timeZoneId`, siete `days` con `day`, `isClosed`, `opensAt`, `closesAt`). Consulta pública `GET /api/businesses/{id}/opening-hours` devuelve horario y `isAcceptingOrders`. Los pedidos se rechazan fuera del horario local configurado. Hasta configurar el horario, el negocio conserva la disponibilidad previa.
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

## En desarrollo: opciones de catálogo (`feature/catalog-options`, basada en PR #8)

- Grupos con selección mínima/máxima y variantes con diferencia de precio no negativa. Grupo/variante se puede poner no disponible; la lectura pública oculta los no disponibles y el servidor rechaza su selección.
- El precio unitario de línea refleja las opciones y el pedido guarda una copia de nombre/importe para conservar el recibo si el negocio cambia el catálogo después.
- Migración `CatalogProductOptions` y `docs/sql/CatalogProductOptions.sql` crean tres tablas e índices aditivos. No elimina datos ni está aplicada. La suite pasa en SQLite; SQL Server aún no se ha validado.

## En desarrollo: horarios del negocio (`feature/business-hours`, basada en PR #9)

- El propietario o admin configura los siete días y zona horaria; la API rechaza días duplicados/incompletos, zona inválida, horas ausentes y rangos invertidos. `GET /api/businesses/{id}/opening-hours` expone horario y disponibilidad actual.
- `POST /api/orders` rechaza nuevos pedidos cuando el negocio está cerrado según su zona horaria. Los negocios sin horario configurado mantienen temporalmente su disponibilidad actual.
- Migración `BusinessOpeningHours` añade `Businesses.TimeZoneId`, `Businesses.HasConfiguredOpeningHours` y una tabla con clave primaria `(BusinessId, Day)`. `docs/sql/BusinessOpeningHours.sql` es aditivo y no aplicado. Pruebas actuales con SQLite; falta verificar migración y tipo `time` en SQL Server.

## PR #11 abierto: cancelaciones, incidencias y reembolsos manuales (`feature/order-cancellations-refunds-incidents`, basada en PR #10)

- Cliente cancela con `POST /api/orders/{id}/cancel` solo antes de enviar comprobante; propietario/admin usa `POST /api/businesses/{businessId}/orders/{orderId}/cancel` antes de asignar courier. Si hay ofertas externas abiertas, se cancelan en el mismo guardado. Reintentar con el mismo actor y motivo no duplica historial.
- Cancelar un pedido con comprobante enviado o pago confirmado crea un caso `ReviewRequired`; rechazo del negocio después del pago aprobado también lo crea. El cliente ve solo `refundStatus` en su consulta de pedido. Negocio/admin revisa el caso en `GET .../refund` y registra el resultado manualmente con `POST .../refund-review` (`refunded`, `amount`, `reference`, `note`); el importe se limita al total. No se inicia una transferencia bancaria.
- Cliente, negocio propietario o courier asignado registra/consulta incidencias con `POST/GET /api/orders/{id}/incidents`; negocio/admin resuelve con `POST /api/businesses/{businessId}/orders/{orderId}/incidents/{incidentId}/resolve`. Cambios de pedido, incidente y reembolso incrementan versión y notifican después de persistir.
- Migraciones `OrderCancellationRefundsIncidents` y `OrderCancellationAuditConstraints`; script `docs/sql/OrderCancellationRefundsIncidents.sql`. Añaden campos, tablas e índices; no aplicadas. Pruebas locales con SQLite; SQL Server sigue pendiente.

## PR #12 abierto: métricas del piloto (`feature/pilot-metrics`, basada en PR #11)

- `GET /api/admin/metrics/pilot?fromUtc={ISO-8601}&toUtc={ISO-8601}` devuelve conteo de pedidos por estado, suma de totales de todos los pedidos creados en el periodo (incluye cancelados/rechazados), entregas, cancelaciones, incidencias abiertas, reembolsos que esperan revisión y tiempo medio hasta entrega. Solo admin de plataforma puede consultarlo.
- El periodo es UTC, inclusivo al inicio y exclusivo al final, máximo 366 días. Sin parámetros usa los últimos 30 días. La respuesta es agregada y no incluye identificadores, direcciones, teléfonos ni datos de pago.
- Consulta de solo lectura; no requiere migración ni cambia el esquema. Prueba de permisos y agregados en el recorrido API aislado con SQLite.

## Validación, antecedentes y pendientes

`dotnet test Poraqui.sln` ejercita un recorrido aislado SQLite con importes, permisos, transferencia, comprobante, oferta externa, cancelación, ausencia de repartidores, rechazo, aceptación, códigos, pago al recoger y entrega. SQLite no verifica por sí solo los bloqueos, índices y concurrencia de SQL Server.

El contrato provisional `src/Poraqui.Application/Delivery/DeliveryOrderContracts.cs` se eliminó en PR #4 al implementar reparto propio. La asignación previa a OpenCode se conserva como antecedente; hay un solo responsable.

Pendiente: OTP, idempotencia general para otros comandos, métricas, despliegue y verificación SQL Server incluyendo carreras simultáneas. No hay GPS, distancias inferidas ni transferencias bancarias automáticas.
