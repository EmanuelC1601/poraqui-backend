# Contratos API y entre módulos

Este documento describe contratos implementados. `main` integra PR #1–#6; PR #7–#12 ya están integrados entre ramas apiladas y esperan que la pila llegue a `main`. PR #10 se integró en `feature/catalog-options`; PR #11, con PR #12 de métricas, se integró en `feature/business-hours`.

## Identidad y comercio — `main`

- Identity API: registro/login bearer en `/api/auth`; primer administrador mediante `POST /api/auth/bootstrap-admin`, protegido por `Bootstrap:AdminKey` privada.
- `POST /api/businesses` crea negocio para usuario autenticado. `GET /api/businesses/active`, `GET/PUT /api/businesses/{id}` y `PUT /api/businesses/{id}/active` consultan/configuran negocio. Acceso por rol y propietario; otro negocio recibe 403.
- Roles: `PlatformAdmin`, `BusinessOwner`, `DeliveryPerson`, `ExternalCourier`; policies `PlatformAdministration`, `ManageBusiness`, `DeliveryOperations`.
- Catálogo base: `GET/POST /api/businesses/{id}/products`, `PUT /api/products/{productId}`. Admin configura zonas (`GET /api/delivery-zones/active`, `POST/PUT /api/delivery-zones`); negocio/admin administra tarifa por zona en `/api/businesses/{id}/delivery-rates`.
- Transferencia: `GET/PUT /api/businesses/{businessId}/transfer-details`; el pedido conserva snapshot visible al cliente propietario.
- `POST /api/orders` recibe negocio, zona, dirección, producto IDs y cantidades. El servidor calcula importes desde precio/tarifa activos, sin aceptar precios del cliente.
- Cliente sube comprobante privado por `POST /api/orders/{id}/transfer-evidence` (PDF/JPEG/PNG; máximo 8 MB); propietario/admin revisa pago en `POST /api/businesses/{businessId}/orders/{orderId}/payment-review`.
- El negocio decide, inicia preparación explícitamente y marca listo mediante `.../decision`, `.../preparation`, `.../ready`. Admin concede courier propio con `PUT /api/admin/users/{id}/delivery-person`; negocio asigna en `.../assign-courier` e indica el monto que pagará al recoger.
- Courier consulta `/api/courier/orders`, confirma recogida en `POST /api/orders/{id}/pickup` y registra así el pago recibido del negocio; inicia recorrido en `.../delivery/start` (`whatsapp` o `call`) y confirma entrega con código de cliente en `.../delivery/confirm`.
- Cliente, negocio y courier autorizado consultan estado e historial por sus rutas GET. Códigos de recogida/entrega se guardan hasheados.
- Reparto externo (PR #6): admin habilita perfil; courier fija disponibilidad y zona; negocio publica oferta con origen, destino, condiciones, remuneración y vencimiento. Courier consulta, rechaza o acepta. La aceptación persiste oferta/asignación con control de concurrencia e índice único filtrado. Ofertas se cancelan o vencen; sin candidatos se responde `NoCouriersAvailable`.

## Contratos integrados en ramas apiladas

### PR #7 — Sync offline

`POST /api/orders/{orderId}/sync/delivery-confirmation` acepta `{ commandId, code, clientCapturedAtUtc }`. El servidor valida actor/courier asignado, estado y código vigentes. Idéntico reintento reproduce el resultado guardado; reutilizar clave con orden/payload distinto devuelve 409. Captura y transición persisten en la misma transacción.

### PR #8 — SignalR

Hub autenticado `/hubs/orders`. `SubscribeOrder(orderId)` comprueba que el usuario sea cliente, propietario del negocio, courier asignado o admin y devuelve snapshot `{ orderId, status, version, changedAtUtc }`. `OrderChanged` se publica después de persistir y solo a grupos autorizados, sin datos personales. Tras reconectar se vuelve a suscribir para obtener el estado actual. SignalR no es una cola durable.

### PR #9 — Opciones de catálogo

`POST /api/products/{productId}/option-groups` configura límites de selección y variantes con diferencias de precio; disponibilidad con `PUT /api/option-groups/{id}/availability` y `/api/product-options/{id}/availability`. Lectura pública oculta opciones no disponibles. Pedido acepta `optionIds`; servidor valida selección y congela nombre/diferencia en snapshot.

### PR #10 — Horarios

Propietario/admin configura `PUT /api/businesses/{id}/opening-hours` con zona horaria y siete días. `GET` devuelve horario y `isAcceptingOrders`. El servidor rechaza pedido fuera de horario local; sin configuración conserva el comportamiento previo.

### PR #11 y PR #12 — Cancelación, incidencias, reembolso y métricas

- Cliente cancela antes del comprobante con `POST /api/orders/{id}/cancel`; negocio/admin cancela hasta antes de asignar courier en `/api/businesses/{businessId}/orders/{orderId}/cancel`. Un reintento con mismo actor/motivo no duplica historial. Cancelar después de evidencia o pago aprobado crea seguimiento `ReviewRequired` para revisión humana; no hay reembolso automático.
- Participantes autorizados informan y consultan incidencias en `/api/orders/{id}/incidents`; propietario/admin resuelve mediante `/api/businesses/{businessId}/orders/{orderId}/incidents/{incidentId}/resolve`.
- Negocio/admin consulta el caso de reembolso en `GET .../refund` y registra resultado manual mediante `POST .../refund-review`. Cliente ve solo `refundStatus` en su pedido.
- `GET /api/admin/metrics/pilot?fromUtc={ISO-8601}&toUtc={ISO-8601}` solo para PlatformAdmin. Devuelve totales agregados de pedidos/estados, valor, entregas, cancelaciones, incidencias abiertas, reembolsos pendientes y minutos promedio a entrega. Rango UTC semiabierto máximo 366 días; sin parámetros, 30 días. No expone datos personales.

## Migraciones y estado

Las migraciones `ExternalCourierOffers`, `OfflineDeliveryCommandSync`, `CatalogProductOptions`, `BusinessOpeningHours`, `OrderCancellationRefundsIncidents` y `OrderCancellationAuditConstraints` están registradas en el historial SQL Server privado consultado. EF no detecta cambios pendientes del modelo. No se aplicaron migraciones durante esta revisión. Scripts revisables bajo `docs/sql/`; la API nunca migra automáticamente al arrancar.

Las pruebas actuales usan SQLite aislada, salvo una prueba de traducción de consulta con proveedor SQL Server que no conecta a la BD. Restricciones vivas y carreras concurrentes necesitan `ConnectionStrings:SqlServerTest` apuntando a una base separada; no ejecutar pruebas de limpieza contra la BD compartida.

## Pendientes y límites

PR #13–#14 y #18–#21 siguen en sus ramas apiladas; comprobar idempotencia/OTP e integrarlas por orden. Falta cliente real para reconexión SignalR, validación SQL aislada de restricciones y concurrencia, almacenamiento privado persistente y despliegue MonsterASP.NET. WhatsApp/llamada se completa manualmente fuera de la API. Tarifas usan zonas configuradas: no hay GPS ni cálculo de distancias. No hay transferencias bancarias automáticas ni frontend.

El plan original asignó módulos de reparto a OpenCode; la decisión vigente es responsable único. Se conserva el antecedente sin dependencia operativa.
