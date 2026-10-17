# Contratos entre módulos

Los contratos de Application no exponen entidades de EF. La base incluye la interfaz `IDeliveryOrderQueries` (`GetAvailableOrdersAsync`, `GetOrderAsync`) y `IDeliveryOrderOperations` (`AcceptAsync`, `MarkPickedUpAsync`, `MarkDeliveredAsync`) para que el módulo de reparto consulte trabajo y solicite cambios operativos.

Los DTO de consulta exponen identificador y número del pedido, negocio, puntos de recogida y entrega, cliente, zona, tarifa, fecha de disponibilidad y estado. `bool` significa que la operación pudo aceptarse; `false` indica pedido ausente, no elegible o conflicto de asignación. Los adaptadores deben realizar validación de estado, autorización del repartidor y concurrencia. Las interfaces están declaradas, aún no tienen implementación ni endpoints asociados.

Estados iniciales compartidos (`OrderStatus`): `PendingPayment`, `AwaitingPaymentReview`, `Confirmed`, `Preparing`, `ReadyForPickup`, `AssignedToDelivery`, `PickedUp`, `InTransit`, `Delivered`, `Cancelled` y `Rejected`. Coordinar cualquier cambio de nombre o transición entre ambos agentes antes de integrarlo.

Las transferencias quedan sujetas a revisión manual. Una evidencia recibida no confirma el pedido automáticamente; la aplicación de administración debe registrar la decisión y su actor. Ese flujo aún no está implementado en esta base.
