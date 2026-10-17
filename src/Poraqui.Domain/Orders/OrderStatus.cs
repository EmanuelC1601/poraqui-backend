namespace Poraqui.Domain.Orders;

public enum OrderStatus
{
    PendingPayment,
    AwaitingPaymentReview,
    Confirmed,
    Preparing,
    ReadyForPickup,
    AssignedToDelivery,
    PickedUp,
    InTransit,
    Delivered,
    Cancelled,
    Rejected
}
