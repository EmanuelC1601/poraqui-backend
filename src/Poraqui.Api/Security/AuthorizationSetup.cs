using Microsoft.AspNetCore.Authorization;
using Poraqui.Application.Security;

namespace Poraqui.Api.Security;

public static class AuthorizationSetup
{
    public static IServiceCollection AddPoraquiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(ApplicationPolicies.PlatformAdministration,
                policy => policy.RequireRole(ApplicationRoles.PlatformAdmin)
                    .AddRequirements(new CurrentRoleMembershipRequirement(ApplicationRoles.PlatformAdmin)));
            options.AddPolicy(ApplicationPolicies.ManageBusiness,
                policy => policy.RequireRole(ApplicationRoles.PlatformAdmin, ApplicationRoles.BusinessOwner)
                    .AddRequirements(new CurrentRoleMembershipRequirement(
                        ApplicationRoles.PlatformAdmin, ApplicationRoles.BusinessOwner)));
            options.AddPolicy(ApplicationPolicies.DeliveryOperations,
                policy => policy.RequireRole(ApplicationRoles.PlatformAdmin, ApplicationRoles.DeliveryPerson, ApplicationRoles.ExternalCourier)
                    .AddRequirements(new CurrentRoleMembershipRequirement(
                        ApplicationRoles.PlatformAdmin, ApplicationRoles.DeliveryPerson, ApplicationRoles.ExternalCourier)));
        });
        return services;
    }
}
