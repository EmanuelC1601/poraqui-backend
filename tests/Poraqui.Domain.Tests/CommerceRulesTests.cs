using Poraqui.Domain.Commerce;
using Poraqui.Domain.Orders;

namespace Poraqui.Domain.Tests;

public sealed class CommerceRulesTests
{
    [Fact]
    public void Business_requires_owner_and_nonempty_name_and_address()
    {
        Assert.Throws<ArgumentException>(() => new Business(Guid.Empty, "Tacos", "Centro"));
        Assert.Throws<ArgumentException>(() => new Business(Guid.NewGuid(), " ", "Centro"));
        Assert.Throws<ArgumentException>(() => new Business(Guid.NewGuid(), "Tacos", " "));
    }

    [Fact]
    public void Product_and_delivery_fee_reject_negative_prices()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CatalogProduct(Guid.NewGuid(), "Taco", -1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeliveryRate(Guid.NewGuid(), Guid.NewGuid(), -0.01m));

        var product = new CatalogProduct(Guid.NewGuid(), "Taco", 10.125m);
        Assert.Equal(10.13m, product.Price);
        product.SetAvailable(false);
        Assert.False(product.IsAvailable);
    }

    [Fact]
    public void Order_snapshots_line_price_and_calculates_total_with_delivery_fee()
    {
        var order = CreateOrder();

        Assert.Equal(2, order.Items.Single().Quantity);
        Assert.Equal(10m, order.Items.Single().UnitPrice);
        Assert.Equal(20m, order.Items.Single().LineTotal);
        Assert.Equal(25.5m, order.Total);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
    }

    [Fact]
    public void Order_requires_at_least_one_item_and_positive_quantities()
    {
        Assert.Throws<ArgumentException>(() => new Order(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Calle 1", 3m, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Order(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Calle 1", 3m,
            [(Guid.NewGuid(), "Taco", 10m, 0)]));
    }

    [Fact]
    public void Payment_evidence_requires_manual_review_by_identified_user()
    {
        var order = CreateOrder();
        order.SubmitTransferEvidence("test-transfer-001");

        Assert.Equal(OrderStatus.AwaitingPaymentReview, order.Status);
        Assert.Null(order.TransferReviews.Single().Decision);
        Assert.Throws<ArgumentException>(() => order.ReviewTransfer(Guid.Empty, approved: true));
        Assert.Equal(OrderStatus.AwaitingPaymentReview, order.Status);

        var reviewer = Guid.NewGuid();
        order.ReviewTransfer(reviewer, approved: true, note: "Identificado", reviewedAtUtc: DateTimeOffset.UnixEpoch);

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(reviewer, order.TransferReviews.Single().ReviewerUserId);
        Assert.Equal(TransferDecision.Approved, order.TransferReviews.Single().Decision);
        Assert.Equal(DateTimeOffset.UnixEpoch, order.TransferReviews.Single().ReviewedAtUtc);
    }

    [Fact]
    public void Rejected_transfer_does_not_confirm_order_and_cannot_be_reviewed_twice()
    {
        var order = CreateOrder();
        order.SubmitTransferEvidence("test-transfer-002");
        order.ReviewTransfer(Guid.NewGuid(), approved: false, note: "No se localizó el pago");

        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal(TransferDecision.Rejected, order.TransferReviews.Single().Decision);
        Assert.Throws<InvalidOperationException>(() => order.ReviewTransfer(Guid.NewGuid(), approved: true));
    }

    [Fact]
    public void Order_preparation_requires_confirmed_order()
    {
        var order = CreateOrder();
        Assert.Throws<InvalidOperationException>(() => order.BeginPreparation());

        order.SubmitTransferEvidence("test-transfer-003");
        order.ReviewTransfer(Guid.NewGuid(), approved: true);
        order.BeginPreparation();
        order.MarkReadyForPickup();

        Assert.Equal(OrderStatus.ReadyForPickup, order.Status);
        Assert.Throws<InvalidOperationException>(() => order.MarkReadyForPickup());
    }

    private static Order CreateOrder() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Calle de prueba 123", 5.5m,
        [(Guid.NewGuid(), "Taco de prueba", 10m, 2)]);
}
