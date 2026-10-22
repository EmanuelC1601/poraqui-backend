using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Poraqui.Infrastructure.Identity;

namespace Poraqui.Api.Security;

public sealed class CurrentRoleMembershipRequirement(params string[] roleNames) : IAuthorizationRequirement
{
    public IReadOnlyList<string> RoleNames { get; } = roleNames;
}

public sealed class CurrentRoleMembershipHandler(UserManager<ApplicationUser> users)
    : AuthorizationHandler<CurrentRoleMembershipRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        CurrentRoleMembershipRequirement requirement)
    {
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            context.Fail();
            return;
        }

        var claimedRoles = requirement.RoleNames.Where(context.User.IsInRole).ToArray();
        if (claimedRoles.Length == 0)
        {
            context.Fail();
            return;
        }

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            context.Fail();
            return;
        }

        // Some controller actions derive their admin/owner behavior from JWT claims.
        // Reject the whole policy when any allowed claim is stale so a remaining
        // lower-privilege role cannot accidentally preserve a revoked admin claim.
        foreach (var role in claimedRoles)
        {
            if (!await users.IsInRoleAsync(user, role))
            {
                context.Fail();
                return;
            }
        }

        context.Succeed(requirement);
    }
}
