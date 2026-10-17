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

## Contratos preliminares pendientes de implementación

`IDeliveryOrderQueries` y `IDeliveryOrderOperations` son interfaces provisionales. No hay endpoints o servicios operativos de reparto que las implementen. Sus operaciones propuestas (`GetAvailableOrdersAsync`, `GetOrderAsync`, `AcceptAsync`, `MarkPickedUpAsync`, `MarkDeliveredAsync`) deberán sustituirse/ajustarse junto con el flujo comprobado, incluyendo pago al repartidor al recoger, códigos e historial.

El enum de estado inicial no representa todavía una máquina de estados validada. No usarlo como promesa de API estable hasta cubrir aceptación/rechazo, revisión del pago, preparación, asignación, recogida, trayecto, entrega y cancelación.

## Contratos por definir al implementar

- Catálogo: opciones, disponibilidad, zona/tarifa y precios siempre cargados y calculados en servidor.
- Pedido: resumen persistido de importes, evidencia de transferencia privada, actor y decisión de revisión manual.
- Entrega: servicio/oferta con origen, destino, condiciones y remuneración visibles antes de aceptar; aceptación atómica con conflicto para solicitudes concurrentes.
- Idempotencia/sincronización: clave por operación y resultado previo; validar permisos y estado actual en servidor antes de aplicar acciones offline.
- SignalR: emitir solo después de persistir y a destinatarios autorizados; reconectar consultando estado actual.

Los cambios a estos contratos requieren pruebas y actualización de `docs/project-status.md`; mientras el proyecto sea de responsable único, actualizar también este documento dentro del mismo PR.
