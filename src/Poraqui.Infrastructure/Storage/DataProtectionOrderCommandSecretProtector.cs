using Microsoft.AspNetCore.DataProtection;
using Poraqui.Application.Commerce;

namespace Poraqui.Infrastructure.Storage;

public sealed class DataProtectionOrderCommandSecretProtector(IDataProtectionProvider provider) : IOrderCommandSecretProtector,
    Poraqui.Application.Orders.IExternalOfferSecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Poraqui.OrderCreationCommand.DeliveryCode.v1");
    private readonly IDataProtector _externalOfferProtector = provider.CreateProtector("Poraqui.ExternalOffer.AcceptancePickupCode.v1");

    public string Protect(string secret) => _protector.Protect(secret);
    public string Unprotect(string protectedSecret) => _protector.Unprotect(protectedSecret);

    string Poraqui.Application.Orders.IExternalOfferSecretProtector.Protect(string secret) => _externalOfferProtector.Protect(secret);
    string Poraqui.Application.Orders.IExternalOfferSecretProtector.Unprotect(string protectedSecret) => _externalOfferProtector.Unprotect(protectedSecret);
}
