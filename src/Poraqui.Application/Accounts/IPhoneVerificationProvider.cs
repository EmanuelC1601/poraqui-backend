namespace Poraqui.Application.Accounts;

public interface IPhoneVerificationProvider
{
    Task StartAsync(string phoneNumber, CancellationToken cancellationToken);
    Task<bool> IsApprovedAsync(string phoneNumber, string code, CancellationToken cancellationToken);
}

public sealed class PhoneVerificationProviderException(string message, Exception? innerException = null)
    : Exception(message, innerException);
