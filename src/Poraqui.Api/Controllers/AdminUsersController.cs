using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Poraqui.Application.Orders;
using Poraqui.Application.Security;
using Poraqui.Domain.Orders;
using Poraqui.Infrastructure.Identity;
using Poraqui.Infrastructure.Persistence;

namespace Poraqui.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = ApplicationPolicies.PlatformAdministration)]
public sealed class AdminUsersController(UserManager<ApplicationUser> users, RoleManager<IdentityRole<Guid>> roles, AppDbContext db,
    IExternalOfferPublisher offerPublisher) : ControllerBase
{
    [HttpPut("{userId:guid}/delivery-person")]
    public async Task<IActionResult> SetDeliveryPerson(Guid userId, SetRoleRequest request)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound();
        if (!await roles.RoleExistsAsync(ApplicationRoles.DeliveryPerson))
        {
            var createRole = await roles.CreateAsync(new IdentityRole<Guid>(ApplicationRoles.DeliveryPerson));
            if (!createRole.Succeeded) return Problem("Delivery person role could not be initialized.");
        }
        var isAssigned = await users.IsInRoleAsync(user, ApplicationRoles.DeliveryPerson);
        if (isAssigned == request.IsDeliveryPerson) return NoContent();
        var result = request.IsDeliveryPerson
            ? await users.AddToRoleAsync(user, ApplicationRoles.DeliveryPerson)
            : await users.RemoveFromRoleAsync(user, ApplicationRoles.DeliveryPerson);
        return result.Succeeded ? NoContent() : Problem("Delivery person access could not be updated.");
    }

    [HttpPut("{userId:guid}/external-courier")]
    public async Task<IActionResult> SetExternalCourier(Guid userId, SetExternalCourierRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound();
        if (!await roles.RoleExistsAsync(ApplicationRoles.ExternalCourier))
        {
            var created = await roles.CreateAsync(new IdentityRole<Guid>(ApplicationRoles.ExternalCourier));
            if (!created.Succeeded) return Problem("External courier role could not be initialized.");
        }
        var assigned = await users.IsInRoleAsync(user, ApplicationRoles.ExternalCourier);
        if (assigned != request.IsExternalCourier)
        {
            var result = request.IsExternalCourier
                ? await users.AddToRoleAsync(user, ApplicationRoles.ExternalCourier)
                : await users.RemoveFromRoleAsync(user, ApplicationRoles.ExternalCourier);
            if (!result.Succeeded) return Problem("External courier access could not be updated.");
        }
        var profile = await db.CourierAvailabilities.SingleOrDefaultAsync(x => x.CourierUserId == userId, cancellationToken);
        if (request.IsExternalCourier && profile is null) db.CourierAvailabilities.Add(new CourierAvailability(userId));
        ExternalDeliveryOffer[] cancelledOffers = [];
        if (!request.IsExternalCourier)
        {
            profile?.SetAvailable(false);
            cancelledOffers = await db.ExternalDeliveryOffers.Where(x => x.CourierUserId == userId &&
                x.Status == ExternalDeliveryOfferStatus.Offered).ToArrayAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            foreach (var offer in cancelledOffers) offer.Cancel(now);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        foreach (var offer in cancelledOffers)
            await offerPublisher.PublishChangedAsync(userId,
                new ExternalOfferChangedNotice(offer.Id, offer.OrderId, offer.Status.ToString()), cancellationToken);
        return NoContent();
    }
}

public sealed record SetRoleRequest(bool IsDeliveryPerson);
public sealed record SetExternalCourierRequest(bool IsExternalCourier);
