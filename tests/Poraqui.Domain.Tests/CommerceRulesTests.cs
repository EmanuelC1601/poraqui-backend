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
    public void Business_opening_hours_validate_week_and_evaluate_local_time_zone()
    {
        var business = new Business(Guid.NewGuid(), "Tacos", "Centro");
        var schedule = Enum.GetValues<DayOfWeek>().Select(day =>
            (day, day != DayOfWeek.Monday,
                day == DayOfWeek.Monday ? TimeOnly.Parse("09:00") : (TimeOnly?)null,
                day == DayOfWeek.Monday ? TimeOnly.Parse("17:00") : (TimeOnly?)null));

        business.ConfigureOpeningHours("UTC", schedule);

        Assert.True(business.HasConfiguredOpeningHours);
        Assert.True(business.IsAcceptingOrdersAt(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero)));
        Assert.False(business.IsAcceptingOrdersAt(new DateTimeOffset(2026, 10, 19, 17, 0, 0, TimeSpan.Zero)));
        Assert.False(business.IsAcceptingOrdersAt(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero)));
        Assert.Throws<ArgumentException>(() => business.ConfigureOpeningHours("UTC", []));
        Assert.Throws<ArgumentException>(() => business.ConfigureOpeningHours("not-a-time-zone", schedule));
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
    public void Repeated_payment_decision_acceptance_and_preparation_commands_do_not_duplicate_history()
    {
        var order = CreateOrder();
        var reviewer = Guid.NewGuid();
        var merchant = Guid.NewGuid();
        order.SubmitTransferEvidence("test-transfer-idempotent");

        Assert.True(order.ReviewTransfer(reviewer, approved: true, note: "Pago localizado"));
        Assert.False(order.ReviewTransfer(reviewer, approved: true, note: "Pago localizado"));
        Assert.True(order.AcceptByBusiness(merchant));
        Assert.False(order.AcceptByBusiness(merchant));
        Assert.True(order.BeginPreparation(merchant));
        Assert.False(order.BeginPreparation(merchant));
        Assert.True(order.MarkReadyForPickup(merchant));
        Assert.False(order.MarkReadyForPickup(merchant));

        Assert.Equal(OrderStatus.ReadyForPickup, order.Status);
        Assert.Equal(6, order.StatusHistory.Count);
        Assert.Equal(6, order.Version);
        Assert.Throws<InvalidOperationException>(() => order.AcceptByBusiness(Guid.NewGuid()));
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
    public void Customer_cancellation_is_limited_to_unpaid_orders_and_same_request_is_idempotent()
    {
        var order = CreateOrder();
        Assert.True(order.CancelByCustomer(order.CustomerUserId, "El cliente cambió de opinión"));
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(2, order.StatusHistory.Count);
        Assert.False(order.CancelByCustomer(order.CustomerUserId, "El cliente cambió de opinión"));
        Assert.Equal(2, order.StatusHistory.Count);
        Assert.Null(order.RefundCase);

        var other = CreateOrder();
        other.SubmitTransferEvidence("private/test-proof");
        Assert.Throws<InvalidOperationException>(() => other.CancelByCustomer(other.CustomerUserId, "Cambio de plan"));
    }

    [Fact]
    public void Rejection_after_confirmed_transfer_creates_manual_refund_followup()
    {
        var order = CreateOrder();
        var businessUserId = Guid.NewGuid();
        order.SubmitTransferEvidence("private/test-proof");
        order.ReviewTransfer(Guid.NewGuid(), approved: true);
        order.RejectByBusiness(businessUserId, "No se puede preparar");

        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal(ManualRefundStatus.ReviewRequired, order.RefundCase!.Status);
        Assert.Equal(order.Total, order.RefundCase.RequestedAmount);
        Assert.True(order.MarkRefunded(businessUserId, 15m, "REF-TEST-001", "Transferencia manual confirmada"));
        Assert.False(order.MarkRefunded(businessUserId, 15m, "REF-TEST-001", "Transferencia manual confirmada"));
        Assert.Equal(15m, order.RefundCase.RefundedAmount);
        Assert.Equal(ManualRefundStatus.Refunded, order.RefundCase.Status);
    }

    [Fact]
    public void Incidents_keep_resolution_history_and_support_safe_retry()
    {
        var order = CreateOrder();
        var reporter = Guid.NewGuid();
        var resolver = Guid.NewGuid();
        var incident = order.AddIncident(reporter, "LateDelivery", "El pedido tardó más de lo esperado.");

        Assert.Equal(OrderIncidentStatus.Open, incident.Status);
        Assert.True(order.ResolveIncident(incident.Id, resolver, "Se contactó al cliente y se atendió el caso."));
        Assert.False(order.ResolveIncident(incident.Id, resolver, "Se contactó al cliente y se atendió el caso."));
        Assert.Equal(resolver, incident.ResolverUserId);
        Assert.Equal(OrderIncidentStatus.Resolved, incident.Status);
        Assert.Throws<ArgumentException>(() => order.AddIncident(reporter, " ", "Descripción"));
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
        Assert.False(order.MarkReadyForPickup(businessUserId));
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

    [Fact]
    public void Repeated_delivery_start_is_safe_only_for_same_courier_and_contact_channel()
    {
        var order = CreateOrder();
        var merchant = Guid.NewGuid();
        var courier = Guid.NewGuid();
        order.SetDeliveryCodeHash(Hash("654321"));
        order.SubmitTransferEvidence("private/idempotent-proof");
        order.ReviewTransfer(Guid.NewGuid(), approved: true);
        order.AcceptByBusiness(merchant);
        order.BeginPreparation(merchant);
        order.MarkReadyForPickup(merchant);
        order.AssignCourier(merchant, courier, Hash("123456"), 42.75m);
        order.MarkPickedUp(courier, Hash("123456"));
        var version = order.Version;

        Assert.True(order.BeginDelivery(courier, "whatsapp"));
        var transitionedVersion = order.Version;
        Assert.False(order.BeginDelivery(courier, "WHATSAPP"));
        Assert.Equal(OrderStatus.InTransit, order.Status);
        Assert.Equal("whatsapp", order.ContactChannel);
        Assert.Equal(transitionedVersion, order.Version);
        Assert.Equal(version + 1, order.Version);
        Assert.Throws<InvalidOperationException>(() => order.BeginDelivery(courier, "call"));
        Assert.Throws<InvalidOperationException>(() => order.BeginDelivery(Guid.NewGuid(), "whatsapp"));
    }

    [Fact]
    public void Repeated_external_offer_rejection_does_not_change_version()
    {
        var offer = new ExternalDeliveryOffer(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Origen", "Destino",
            30m, "Frágil", DateTimeOffset.UtcNow.AddMinutes(5));

        Assert.True(offer.Reject(DateTimeOffset.UtcNow));
        var version = offer.Version;
        Assert.False(offer.Reject(DateTimeOffset.UtcNow));
        Assert.Equal(ExternalDeliveryOfferStatus.Rejected, offer.Status);
        Assert.Equal(version, offer.Version);
    }

    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    private static Order CreateOrder() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Calle de prueba 123", 5.5m,
        [(Guid.NewGuid(), "Taco de prueba", 10m, 2)]);
}
