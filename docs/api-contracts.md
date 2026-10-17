# Contratos entre módulos

Los contratos de Application no exponen entidades de EF. La base compilable incluye la interfaz `IDeliveryOrderQueries` (`GetAvailableOrdersAsync`, `GetOrderAsync`) y `IDeliveryOrderOperations` (`AcceptAsync`, `MarkPickedUpAsync`, `MarkDeliveredAsync`) para que el módulo de reparto consulte trabajo y solicite cambios operativos. Son contratos iniciales sin implementación.

Los DTO de consulta exponen identificador y número del pedido, negocio, puntos de recogida y entrega, cliente, zona, tarifa, fecha de disponibilidad y estado. `bool` significa que la operación pudo aceptarse; `false` indica pedido ausente, no elegible o conflicto de asignación. Los adaptadores deben realizar validación de estado, autorización del repartidor y concurrencia. Las interfaces están declaradas, aún no tienen implementación ni endpoints asociados.

Estados actuales iniciales (`OrderStatus`): `PendingPayment`, `AwaitingPaymentReview`, `Confirmed`, `Preparing`, `ReadyForPickup`, `AssignedToDelivery`, `PickedUp`, `InTransit`, `Delivered`, `Cancelled` y `Rejected`. Este enum es una base provisional; se actualizará junto con la máquina de estados comprobada en las pruebas del flujo completo.

Las transferencias quedan sujetas a revisión manual. Una evidencia recibida no confirma el pedido automáticamente; la aplicación debe registrar la decisión y su actor. Las reglas de dominio y sus mapeos están en `feature/comercio-pedidos`; aún faltan casos de uso, autorización, endpoints, almacenamiento privado de comprobantes y migración. El contrato inicial de reparto todavía no está implementado y se reemplazará por interfaces de aplicación concretas cuando exista el recorrido funcional.
