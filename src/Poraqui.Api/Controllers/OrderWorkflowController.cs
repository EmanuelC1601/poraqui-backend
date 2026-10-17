using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Poraqui.Application.Orders;
using Poraqui.Application.Security;
using Poraqui.Infrastructure.Identity;

namespace Poraqui.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class OrderWorkflowController(OrderWorkflowService workflow, UserManager<ApplicationUser> users,
    ITransferEvidenceStorage evidenceStorage) : ControllerBase
{
    [HttpGet("orders/{orderId:guid}")]
    public async Task<IActionResult> GetCustomerOrder(Guid orderId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out _)) return Unauthorized();
        try
        {
            var order = await workflow.GetForCustomerAsync(orderId, actorId, cancellationToken);
            return order is null ? NotFound() : Ok(order);
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
    }

    [HttpPost("orders/{orderId:guid}/cancel")]
    public async Task<IActionResult> CancelCustomerOrder(Guid orderId, CancelOrderRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out _)) return Unauthorized();
        try
        {
            var order = await workflow.CancelForCustomerAsync(orderId, actorId, request.Reason, cancellationToken);
            return order is null ? NotFound() : Ok(order);
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Order changed; reload its current state." }); }
    }

    [HttpGet("orders/{orderId:guid}/incidents")]
    public async Task<IActionResult> GetIncidents(Guid orderId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try { return Ok(await workflow.GetIncidentsAsync(orderId, actorId, isAdmin, cancellationToken)); }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("orders/{orderId:guid}/incidents")]
    public async Task<IActionResult> ReportIncident(Guid orderId, ReportIncidentRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try { return Ok(await workflow.ReportIncidentAsync(orderId, actorId, isAdmin, request.Category, request.Description, cancellationToken)); }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Order changed; reload its current state." }); }
    }

    [Authorize(Policy = ApplicationPolicies.DeliveryOperations)]
    [HttpGet("courier/orders")]
    public async Task<IActionResult> GetCourierOrders(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var courierId, out _)) return Unauthorized();
        return Ok(await workflow.GetOrdersForCourierAsync(courierId, cancellationToken));
    }

    [Authorize(Policy = ApplicationPolicies.DeliveryOperations)]
    [HttpGet("courier/orders/{orderId:guid}")]
    public async Task<IActionResult> GetCourierOrder(Guid orderId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var courierId, out _)) return Unauthorized();
        try
        {
            var order = await workflow.GetForCourierAsync(orderId, courierId, cancellationToken);
            return order is null ? NotFound() : Ok(order);
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpGet("businesses/{businessId:guid}/orders")]
    public async Task<IActionResult> GetBusinessOrders(Guid businessId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try { return Ok(await workflow.GetBusinessOrdersAsync(businessId, actorId, isAdmin, cancellationToken)); }
        catch (OrderResourceForbiddenException) { return Forbid(); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpGet("businesses/{businessId:guid}/orders/{orderId:guid}")]
    public async Task<IActionResult> GetBusinessOrder(Guid businessId, Guid orderId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var order = await workflow.GetForBusinessAsync(orderId, actorId, isAdmin, cancellationToken);
            if (order is null) return NotFound();
            return order.BusinessId == businessId ? Ok(order) : NotFound();
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPost("businesses/{businessId:guid}/orders/{orderId:guid}/cancel")]
    public async Task<IActionResult> CancelBusinessOrder(Guid businessId, Guid orderId, CancelOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var order = await workflow.CancelForBusinessAsync(businessId, orderId, actorId, isAdmin, request.Reason, cancellationToken);
            return order is null ? NotFound() : Ok(order);
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Order changed; reload its current state." }); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpGet("businesses/{businessId:guid}/orders/{orderId:guid}/refund")]
    public async Task<IActionResult> GetRefundCase(Guid businessId, Guid orderId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var refund = await workflow.GetRefundCaseAsync(businessId, orderId, actorId, isAdmin, cancellationToken);
            return refund is null ? NotFound() : Ok(refund);
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPost("businesses/{businessId:guid}/orders/{orderId:guid}/refund-review")]
    public async Task<IActionResult> ReviewRefundCase(Guid businessId, Guid orderId, ReviewRefundRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var refund = await workflow.ReviewRefundCaseAsync(businessId, orderId, actorId, isAdmin,
                request.Refunded, request.Amount, request.Reference, request.Note, cancellationToken);
            return refund is null ? NotFound() : Ok(refund);
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Refund case changed; reload its current state." }); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPost("businesses/{businessId:guid}/orders/{orderId:guid}/incidents/{incidentId:guid}/resolve")]
    public async Task<IActionResult> ResolveIncident(Guid businessId, Guid orderId, Guid incidentId,
        ResolveIncidentRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var incident = await workflow.ResolveIncidentAsync(businessId, orderId, incidentId, actorId, isAdmin,
                request.Resolution, cancellationToken);
            return incident is null ? NotFound() : Ok(incident);
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Order changed; reload its current state." }); }
    }

    [HttpPost("orders/{orderId:guid}/transfer-evidence")]
    [RequestSizeLimit(9 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitTransferEvidence(Guid orderId, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out _)) return Unauthorized();
        if (file is null) return BadRequest(new { error = "Evidence file is required." });
        try
        {
            var order = await workflow.GetForCustomerAsync(orderId, actorId, cancellationToken);
            if (order is null) return NotFound();
            string reference;
            using (var upload = file.OpenReadStream())
                reference = await evidenceStorage.SaveAsync(upload, file.FileName, file.ContentType, cancellationToken);
            try
            {
                await workflow.SubmitTransferEvidenceAsync(orderId, actorId, reference, cancellationToken);
            }
            catch
            {
                await evidenceStorage.DeleteAsync(reference, CancellationToken.None);
                throw;
            }
            return Accepted();
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Order changed; reload its current state." }); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpGet("businesses/{businessId:guid}/orders/{orderId:guid}/transfer-evidence")]
    public async Task<IActionResult> GetTransferEvidence(Guid businessId, Guid orderId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var order = await workflow.GetForBusinessAsync(orderId, actorId, isAdmin, cancellationToken);
            if (order is null || order.BusinessId != businessId) return NotFound();
            var reference = await workflow.GetTransferEvidenceForBusinessAsync(orderId, actorId, isAdmin, cancellationToken);
            if (reference is null) return NotFound();
            var stored = await evidenceStorage.OpenAsync(reference, cancellationToken);
            return stored is null ? NotFound() : File(stored.Value.Content, stored.Value.ContentType, "transfer-evidence", enableRangeProcessing: true);
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPost("businesses/{businessId:guid}/orders/{orderId:guid}/payment-review")]
    public async Task<IActionResult> ReviewPayment(Guid businessId, Guid orderId, ReviewPaymentRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var isAdmin)) return Unauthorized();
        try
        {
            var order = await workflow.GetForBusinessAsync(orderId, actorId, isAdmin, cancellationToken);
            if (order is null || order.BusinessId != businessId) return NotFound();
            await workflow.ReviewPaymentAsync(orderId, actorId, request.Approved, request.Note, isAdmin, cancellationToken);
            return NoContent();
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Order changed during review; reload its current state." }); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPost("businesses/{businessId:guid}/orders/{orderId:guid}/decision")]
    public async Task<IActionResult> DecideBusiness(Guid businessId, Guid orderId, BusinessDecisionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out _)) return Unauthorized();
        try
        {
            var order = await workflow.GetForBusinessAsync(orderId, actorId, false, cancellationToken);
            if (order is null || order.BusinessId != businessId) return NotFound();
            await workflow.DecideBusinessAsync(orderId, actorId, request.Accept, request.Reason, cancellationToken);
            return NoContent();
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Order changed; reload its current state." }); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPost("businesses/{businessId:guid}/orders/{orderId:guid}/preparation")]
    public Task<IActionResult> BeginPreparation(Guid businessId, Guid orderId, CancellationToken cancellationToken) =>
        ChangePreparation(businessId, orderId, ready: false, cancellationToken);

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPost("businesses/{businessId:guid}/orders/{orderId:guid}/ready")]
    public Task<IActionResult> MarkReady(Guid businessId, Guid orderId, CancellationToken cancellationToken) =>
        ChangePreparation(businessId, orderId, ready: true, cancellationToken);

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPost("businesses/{businessId:guid}/orders/{orderId:guid}/assign-courier")]
    public async Task<IActionResult> AssignCourier(Guid businessId, Guid orderId, AssignCourierRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out _)) return Unauthorized();
        var courier = await users.FindByIdAsync(request.CourierUserId.ToString());
        if (courier is null || !await users.IsInRoleAsync(courier, ApplicationRoles.DeliveryPerson))
            return BadRequest(new { error = "The selected user is not an internal delivery person." });
        try
        {
            var order = await workflow.GetForBusinessAsync(orderId, actorId, false, cancellationToken);
            if (order is null || order.BusinessId != businessId) return NotFound();
            return Ok(await workflow.AssignCourierAsync(orderId, actorId, request.CourierUserId, request.CourierPaymentAmount, cancellationToken));
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Order changed; reload its current state." }); }
    }

    [Authorize(Policy = ApplicationPolicies.DeliveryOperations)]
    [HttpPost("orders/{orderId:guid}/pickup")]
    public async Task<IActionResult> MarkPickedUp(Guid orderId, PickupCodeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var courierId, out _)) return Unauthorized();
        try { await workflow.MarkPickedUpAsync(orderId, courierId, request.Code, cancellationToken); return NoContent(); }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Order changed; reload its current state." }); }
    }

    [Authorize(Policy = ApplicationPolicies.DeliveryOperations)]
    [HttpPost("orders/{orderId:guid}/delivery/start")]
    public async Task<IActionResult> BeginDelivery(Guid orderId, BeginDeliveryRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var courierId, out _)) return Unauthorized();
        try { return Ok(await workflow.BeginDeliveryAsync(orderId, courierId, request.ContactChannel, cancellationToken)); }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Order changed; reload its current state." }); }
    }

    [Authorize(Policy = ApplicationPolicies.DeliveryOperations)]
    [HttpPost("orders/{orderId:guid}/delivery/confirm")]
    public async Task<IActionResult> ConfirmDelivery(Guid orderId, PickupCodeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var courierId, out _)) return Unauthorized();
        try { await workflow.MarkDeliveredAsync(orderId, courierId, request.Code, cancellationToken); return NoContent(); }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Order changed; reload its current state." }); }
    }

    [Authorize(Policy = ApplicationPolicies.DeliveryOperations)]
    [HttpPost("orders/{orderId:guid}/sync/delivery-confirmation")]
    public async Task<IActionResult> SyncDeliveryConfirmation(Guid orderId, SyncDeliveryConfirmationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var courierId, out _)) return Unauthorized();
        try
        {
            return Ok(await workflow.SyncDeliveryConfirmationAsync(orderId, courierId, request.CommandId, request.Code,
                request.ClientCapturedAtUtc, cancellationToken));
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (OfflineDeliveryCommandConflictException) { return Conflict(new { error = "Command id was already used for a different order or payload." }); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Order changed; retry the same command id to read its accepted result." }); }
    }

    private async Task<IActionResult> ChangePreparation(Guid businessId, Guid orderId, bool ready, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out _)) return Unauthorized();
        try
        {
            var order = await workflow.GetForBusinessAsync(orderId, actorId, false, cancellationToken);
            if (order is null || order.BusinessId != businessId) return NotFound();
            await workflow.ChangePreparationAsync(orderId, actorId, ready, cancellationToken);
            return NoContent();
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Order changed; reload its current state." }); }
    }

    private bool TryGetActor(out Guid actorId, out bool isAdmin)
    {
        isAdmin = User.IsInRole(ApplicationRoles.PlatformAdmin);
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? users.GetUserId(User), out actorId);
    }
}

public sealed record ReviewPaymentRequest(bool Approved, string? Note);
public sealed record CancelOrderRequest(string Reason);
public sealed record ReportIncidentRequest(string Category, string Description);
public sealed record ResolveIncidentRequest(string Resolution);
public sealed record ReviewRefundRequest(bool Refunded, decimal? Amount, string? Reference, string? Note);
public sealed record BusinessDecisionRequest(bool Accept, string? Reason);
public sealed record AssignCourierRequest(Guid CourierUserId, decimal CourierPaymentAmount);
public sealed record PickupCodeRequest(string Code);
public sealed record BeginDeliveryRequest(string ContactChannel);
public sealed record SyncDeliveryConfirmationRequest(Guid CommandId, string Code, DateTimeOffset ClientCapturedAtUtc);
