namespace Poraqui.Application.Orders;

public interface IPlatformAdminDirectory
{
    Task<IReadOnlyList<Guid>> GetCurrentPlatformAdminUserIdsAsync(CancellationToken cancellationToken);
}
