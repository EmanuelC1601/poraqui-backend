using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Poraqui.Application.Orders;
using Poraqui.Application.Security;
using Poraqui.Infrastructure.Identity;

namespace Poraqui.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class ExternalCourierController(ExternalCourierService couriers, Infrastructure.Persistence.AppDbContext db,
    UserManager<ApplicationUser> users) : ControllerBase
{
    [Authorize(Roles = ApplicationRoles.ExternalCourier)]
    [HttpPut("courier/external/availability")]
    public async Task<IActionResult> SetAvailability(SetAvailabilityRequest request, CancellationToken ct)
    {
        if (!Actor(out var courierId)) return Unauthorized();
        if (!await HasCurrentExternalCourierRoleAsync(courierId)) return Forbid();
        try
        {
            await couriers.SetAvailabilityAsync(courierId, request.IsAvailable, request.ZoneId, ct);
            return NoContent();
        }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [Authorize(Roles = ApplicationRoles.ExternalCourier)]
    [HttpGet("courier/external/offers")]
    public async Task<IActionResult> GetOffers(CancellationToken ct)
    {
        if (!Actor(out var courierId)) return Unauthorized();
        if (!await HasCurrentExternalCourierRoleAsync(courierId)) return Forbid();
        return Ok(await couriers.GetOffersAsync(courierId, ct));
    }

    [Authorize(Roles = ApplicationRoles.ExternalCourier)]
    [HttpPost("courier/external/offers/{offerId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid offerId, CancellationToken ct)
    {
        if (!Actor(out var courierId)) return Unauthorized();
        if (!await HasCurrentExternalCourierRoleAsync(courierId)) return Forbid();
        try { await couriers.RejectAsync(offerId, courierId, ct); return NoContent(); }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Offer changed; reload its current state." }); }
    }

    [Authorize(Roles = ApplicationRoles.ExternalCourier)]
    [HttpPost("courier/external/offers/{offerId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid offerId, CancellationToken ct)
    {
        if (!Actor(out var courierId)) return Unauthorized();
        if (!await HasCurrentExternalCourierRoleAsync(courierId)) return Forbid();
        try { return Ok(await couriers.AcceptAsync(offerId, courierId, ct)); }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Order offer was accepted or changed concurrently; reload offers." }); }
        catch (DbUpdateException) { return Conflict(new { error = "Order offer could not be accepted because its assignment changed concurrently; reload offers." }); }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPost("businesses/{businessId:guid}/orders/{orderId:guid}/external-offers")]
    public async Task<IActionResult> CreateOffers(Guid businessId, Guid orderId, CreateExternalOffersRequest request, CancellationToken ct)
    {
        if (!Actor(out var ownerId)) return Unauthorized();
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null || order.BusinessId != businessId) return NotFound();
        if (!User.IsInRole(ApplicationRoles.PlatformAdmin) && !await db.Businesses.AnyAsync(x => x.Id == businessId && x.OwnerUserId == ownerId, ct)) return Forbid();
        try
        {
            var count = await couriers.CreateOffersAsync(orderId, ownerId, User.IsInRole(ApplicationRoles.PlatformAdmin), request.Remuneration, request.Conditions,
                TimeSpan.FromMinutes(request.ExpiresInMinutes), ct);
            var outcome = count.AvailableCourierCount == 0 ? "NoCouriersAvailable"
                : count.NewOfferCount == 0 ? "OffersAlreadyActive" : "OffersCreated";
            return Accepted(new { offerCount = count.NewOfferCount, availableCourierCount = count.AvailableCourierCount, outcome });
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(new { error = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { error = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Order changed while offers were being created." }); }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Conflict(new { error = "Offers changed while dispatch was being processed; reload active offers." });
        }
    }

    [Authorize(Policy = ApplicationPolicies.ManageBusiness)]
    [HttpPost("businesses/{businessId:guid}/orders/{orderId:guid}/external-offers/cancel")]
    public async Task<IActionResult> CancelOffers(Guid businessId, Guid orderId, CancellationToken ct)
    {
        if (!Actor(out var ownerId)) return Unauthorized();
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == orderId, ct);
        if (order is null || order.BusinessId != businessId) return NotFound();
        if (!User.IsInRole(ApplicationRoles.PlatformAdmin) && !await db.Businesses.AnyAsync(x => x.Id == businessId && x.OwnerUserId == ownerId, ct)) return Forbid();
        try
        {
            var cancelled = await couriers.CancelOffersAsync(orderId, ownerId, User.IsInRole(ApplicationRoles.PlatformAdmin), ct);
            return Ok(new { cancelledOffers = cancelled });
        }
        catch (OrderResourceForbiddenException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "Offers changed while cancellation was being processed." }); }
    }

    private bool Actor(out Guid actorId) => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };

    private async Task<bool> HasCurrentExternalCourierRoleAsync(Guid courierId)
    {
        var user = await users.FindByIdAsync(courierId.ToString());
        return user is not null && await users.IsInRoleAsync(user, ApplicationRoles.ExternalCourier);
    }
}

public sealed record SetAvailabilityRequest(bool IsAvailable, Guid? ZoneId);
public sealed record CreateExternalOffersRequest(decimal Remuneration, string? Conditions, int ExpiresInMinutes = 15);
