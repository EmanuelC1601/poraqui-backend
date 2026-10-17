namespace Poraqui.Application.Security;

public static class ApplicationRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string BusinessOwner = "BusinessOwner";
    public const string DeliveryPerson = "DeliveryPerson";
    public const string ExternalCourier = "ExternalCourier";
}

public static class ApplicationPolicies
{
    public const string PlatformAdministration = "PlatformAdministration";
    public const string ManageBusiness = "ManageBusiness";
    public const string DeliveryOperations = "DeliveryOperations";
}
