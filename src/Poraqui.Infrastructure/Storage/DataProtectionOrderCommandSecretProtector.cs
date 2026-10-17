using Microsoft.AspNetCore.DataProtection;
using Poraqui.Application.Commerce;

namespace Poraqui.Infrastructure.Storage;

public sealed class DataProtectionOrderCommandSecretProtector(IDataProtectionProvider provider) : IOrderCommandSecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Poraqui.OrderCreationCommand.DeliveryCode.v1");

    public string Protect(string secret) => _protector.Protect(secret);
    public string Unprotect(string protectedSecret) => _protector.Unprotect(protectedSecret);
}
