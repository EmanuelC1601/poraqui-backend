using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Poraqui.Api.Security;
using Poraqui.Application.Security;

namespace Poraqui.Domain.Tests;

public sealed class AuthorizationPolicyTests
{
    [Fact]
    public async Task Business_owner_and_courier_roles_are_limited_to_their_policies()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddPoraquiAuthorization()
            .BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var owner = UserWithRole(ApplicationRoles.BusinessOwner);
        var courier = UserWithRole(ApplicationRoles.DeliveryPerson);
        var admin = UserWithRole(ApplicationRoles.PlatformAdmin);

        Assert.True((await authorization.AuthorizeAsync(owner, null, ApplicationPolicies.ManageBusiness)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(owner, null, ApplicationPolicies.DeliveryOperations)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(owner, null, ApplicationPolicies.PlatformAdministration)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(courier, null, ApplicationPolicies.DeliveryOperations)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(courier, null, ApplicationPolicies.ManageBusiness)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(admin, null, ApplicationPolicies.PlatformAdministration)).Succeeded);
    }

    private static ClaimsPrincipal UserWithRole(string role) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)],
        authenticationType: "test"));
}
