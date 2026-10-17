using Poraqui.Domain.Commerce;
using Poraqui.Domain.Orders;
using System.Security.Cryptography;
using System.Text;

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
    public void Business_transfer_instructions_are_validated_and_normalized()
    {
        var business = new Business(Guid.NewGuid(), "Tacos", "Centro");
        Assert.False(business.HasTransferDetails);
        business.ConfigureTransferDetails("  Taquería  ", " Banco Uno ", " CLABE-TEST-001 ", "  Usa el folio  ");

        Assert.True(business.HasTransferDetails);
        Assert.Equal("Taquería", business.TransferRecipientName);
        Assert.Equal("CLABE-TEST-001", business.TransferAccountReference);
        Assert.Equal("Usa el folio", business.TransferInstructions);
        Assert.Throws<ArgumentException>(() => business.ConfigureTransferDetails("", "Banco Dos", "Otra cuenta", null));
        Assert.Equal("CLABE-TEST-001", business.TransferAccountReference);
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
        Assert.StartsWith("PQ-", order.OrderNumber);
        Assert.InRange(order.OrderNumber.Length, 35, 40);
        Assert.NotEqual(order.OrderNumber, CreateOrder().OrderNumber);
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

        Assert.Equal(OrderStatus.AwaitingBusinessDecision, order.Status);
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
        var businessUserId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => order.BeginPreparation(businessUserId));

        order.SubmitTransferEvidence("test-transfer-003");
        order.ReviewTransfer(Guid.NewGuid(), approved: true);
        Assert.Throws<InvalidOperationException>(() => order.BeginPreparation(businessUserId));
        order.AcceptByBusiness(businessUserId);
        order.BeginPreparation(businessUserId);
        order.MarkReadyForPickup(businessUserId);

        Assert.Equal(OrderStatus.ReadyForPickup, order.Status);
        Assert.Throws<InvalidOperationException>(() => order.MarkReadyForPickup(businessUserId));
    }

    [Fact]
    public void Pickup_and_delivery_codes_gate_courier_transitions_and_record_payment_separately()
    {
        var order = CreateOrder();
        var merchant = Guid.NewGuid();
        var courier = Guid.NewGuid();
        order.SetDeliveryCodeHash(Hash("654321"));
        order.SubmitTransferEvidence("private/test-proof");
        order.ReviewTransfer(Guid.NewGuid(), approved: true);
        order.AcceptByBusiness(merchant);
        order.BeginPreparation(merchant);
        order.MarkReadyForPickup(merchant);
        order.AssignCourier(merchant, courier, Hash("123456"), 42.75m);

        Assert.Throws<InvalidOperationException>(() => order.MarkPickedUp(Guid.NewGuid(), Hash("123456")));
        Assert.Throws<InvalidOperationException>(() => order.MarkPickedUp(courier, Hash("000000")));
        Assert.Null(order.CourierPaymentReceivedAtUtc);

        order.MarkPickedUp(courier, Hash("123456"));
        Assert.Equal(OrderStatus.PickedUp, order.Status);
        Assert.Equal(42.75m, order.CourierPaymentAmount);
        Assert.Equal(courier, order.CourierPaymentReceivedByUserId);
        Assert.NotNull(order.CourierPaymentReceivedAtUtc);

        order.BeginDelivery(courier, "call");
        Assert.Equal("call", order.ContactChannel);
        Assert.Throws<InvalidOperationException>(() => order.MarkDelivered(courier, Hash("111111")));
        order.MarkDelivered(courier, Hash("654321"));
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.NotNull(order.DeliveredAtUtc);
        Assert.Equal(OrderStatus.Delivered, order.StatusHistory.Last().Status);
    }

    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    private static Order CreateOrder() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Calle de prueba 123", 5.5m,
        [(Guid.NewGuid(), "Taco de prueba", 10m, 2)]);
}
