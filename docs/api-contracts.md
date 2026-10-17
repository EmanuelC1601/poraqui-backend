# Contratos API y entre módulos

## Implementado en `feature/functional-order-flow`

- Identity API bajo `/api/auth` (registro/login y endpoints estándar Identity) cuando se configura `ConnectionStrings:DefaultConnection`. Usa tokens bearer para la API.
- `POST /api/auth/bootstrap-admin`: crea solo el primer administrador y requiere el secreto privado `Bootstrap:AdminKey`. Es una operación de instalación inicial.
- `GET /api/businesses/active`: lista negocios activos públicamente.
- `POST /api/businesses`: usuario autenticado registra un negocio y recibe el rol `BusinessOwner`.
- `GET /api/businesses/{businessId}` y `PUT /api/businesses/{businessId}`: administrador o propietario del recurso; otro propietario recibe 403.
- `PUT /api/businesses/{businessId}/active`: solo `PlatformAdmin`.

Roles: `PlatformAdmin`, `BusinessOwner`, `DeliveryPerson`, `ExternalCourier`. Policies actuales: `PlatformAdministration`, `ManageBusiness`, `DeliveryOperations`. La autorización de recurso se verifica además del rol.

La prueba integrada registra dos usuarios, crea sus negocios, verifica acceso al negocio propio y 403 sobre recurso ajeno. La suite completa pasa 10 pruebas con SQLite en memoria aislado. SQL Server todavía no fue probado desde la aplicación.

## Implementado localmente en `feature/catalog-pricing` (sin PR todavía)

- `GET /api/businesses/{businessId}/products`: productos disponibles de negocios activos.
- `POST /api/businesses/{businessId}/products`, `PUT /api/products/{productId}`: propietario del negocio o administrador administra nombre, precio y disponibilidad.
- `GET /api/delivery-zones/active`; `POST /api/delivery-zones` y `PUT /api/delivery-zones/{zoneId}` solo para administradores.
- `GET/POST /api/businesses/{businessId}/delivery-rates` y `PUT /api/businesses/{businessId}/delivery-rates/{zoneId}`: dueño del negocio o administrador administra tarifa en zona.
- `POST /api/orders`: usuario autenticado envía negocio, zona, dirección, IDs de producto y cantidades. Servidor carga productos disponibles y tarifa activa; no acepta precios del cliente como fuente de importe.

La prueba de recorrido de creación de pedido confirma subtotal, envío y total calculados con catálogo/tarifa del servidor incluso si el JSON incluye un campo de precio engañoso adicional. Este recorrido solo se probó sobre SQLite aislado; catálogo, tarifas y pedido aún no están verificados en SQL Server. Opciones de producto siguen pendientes.

## Implementado localmente en `feature/order-lifecycle` (sin PR todavía)

- `PUT /api/account/phone`: usuario autenticado registra su teléfono; requiere validación/confirmación del canal en una fase posterior.
- `POST /api/orders/{id}/transfer-evidence` recibe multipart `file`; solo cliente propietario. Admite PDF/JPEG/PNG con firma básica validada y máximo de 8 MB. Archivos fuera del web root; no hay acceso público directo.
- `GET /api/businesses/{businessId}/orders/{orderId}/transfer-evidence`: propietario del negocio/admin descarga como attachment.
- `POST /api/businesses/{businessId}/orders/{orderId}/payment-review`: propietario o admin registra la revisión manual. Aprobación deja el pedido en `AwaitingBusinessDecision`; no confirma aceptación del negocio.
- `POST .../decision`: negocio acepta o rechaza. Solo un pedido aceptado llega a `Confirmed`.
- `POST .../preparation` y `POST .../ready`: inicio explícito de preparación y disponibilidad de recogida.
- `PUT /api/admin/users/{id}/delivery-person`: administrador habilita/deshabilita rol de repartidor propio.
- `POST .../assign-courier`: negocio asigna repartidor propio y fija remuneración a pagar al recoger; genera código de recogida de seis dígitos, mostrado una vez en esa respuesta. El código de recepción se genera al crear el pedido, se devuelve solo al cliente en esa respuesta y se persiste como hash.
- `GET /api/courier/orders` y `GET /api/courier/orders/{orderId}`: repartidor propio consulta solo los pedidos asignados, con origen, destino, condiciones y remuneración.
- `POST /api/orders/{id}/pickup`: solo el repartidor asignado, exige código correcto y registra de forma separada que confirma haber recibido el pago del negocio.
- `POST /api/orders/{id}/delivery/start`: repartidor asignado inicia recorrido y selecciona `whatsapp` o `call`; devuelve teléfono de cliente y teléfono normalizado a ese repartidor.
- `POST /api/orders/{id}/delivery/confirm`: solo repartidor asignado, exige código del cliente.
- `GET /api/orders/{id}` (cliente propietario), `GET /api/businesses/{businessId}/orders` y `GET .../orders/{orderId}` (negocio autorizado) devuelven estado e historial persistidos.

Cada transición incrementa `Order.Version` como token de concurrencia; una decisión simultánea obsoleta produce conflicto en lugar de sobrescribir el resultado. Hay prueba de carrera secuencial con dos contextos aislados verificando que solo una decisión prevalece. El recorrido completo también está probado de extremo a extremo con SQLite en memoria aislado. `InitialCommerceAndIdentity` incluye el modelo actual. Aún falta verificar restricciones/concurrencia con SQL Server y aplicar esa migración.

El recorrido reproducible del API y sus límites están en `docs/order-flow.md`.

## Antecedentes y pendientes

El archivo provisional `src/Poraqui.Application/Delivery/DeliveryOrderContracts.cs` se eliminó en `feature/order-lifecycle` tras implementar endpoints y casos de uso persistidos. El estado inicial se amplió, pero faltan cancelaciones/reembolsos y los estados/ofertas del reparto externo. La revisión manual del comprobante no confirma por sí sola la aceptación del negocio: son pasos separados y ambos aparecen en historial.

## Contratos por definir al implementar

- Catálogo: opciones, disponibilidad, zona/tarifa y precios siempre cargados y calculados en servidor.
- Pedido: resumen persistido de importes, evidencia de transferencia privada, actor y decisión de revisión manual.
- Entrega: servicio/oferta con origen, destino, condiciones y remuneración visibles antes de aceptar; aceptación atómica con conflicto para solicitudes concurrentes.
- Idempotencia/sincronización: clave por operación y resultado previo; validar permisos y estado actual en servidor antes de aplicar acciones offline.
- SignalR: emitir solo después de persistir y a destinatarios autorizados; reconectar consultando estado actual.

Los cambios a estos contratos requieren pruebas y actualización de `docs/project-status.md`; mientras el proyecto sea de responsable único, actualizar también este documento dentro del mismo PR.
