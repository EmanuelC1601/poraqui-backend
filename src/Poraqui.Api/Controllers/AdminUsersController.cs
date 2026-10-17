using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Poraqui.Application.Security;
using Poraqui.Infrastructure.Identity;

namespace Poraqui.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = ApplicationPolicies.PlatformAdministration)]
public sealed class AdminUsersController(UserManager<ApplicationUser> users, RoleManager<IdentityRole<Guid>> roles) : ControllerBase
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
}

public sealed record SetRoleRequest(bool IsDeliveryPerson);
