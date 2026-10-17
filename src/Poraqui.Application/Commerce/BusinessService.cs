using Poraqui.Domain.Commerce;

namespace Poraqui.Application.Commerce;

public interface IBusinessRepository
{
    Task<Business?> GetByIdAsync(Guid businessId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Business>> GetActiveAsync(CancellationToken cancellationToken);
    Task AddAsync(Business business, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record BusinessView(Guid Id, Guid OwnerUserId, string Name, string Address, bool IsActive);
public sealed record BusinessTransferDetailsView(string? RecipientName, string? Institution, string? AccountReference,
    string? Instructions, bool IsConfigured);

public sealed class BusinessAuthorizationException : Exception { }

public sealed class BusinessService(IBusinessRepository repository)
{
    public async Task<BusinessView> RegisterAsync(Guid ownerUserId, string name, string address, CancellationToken cancellationToken)
    {
        var business = new Business(ownerUserId, name, address);
        await repository.AddAsync(business, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(business);
    }

    public async Task<BusinessView?> GetAsync(Guid businessId, Guid actorUserId, bool isPlatformAdmin, CancellationToken cancellationToken)
    {
        var business = await repository.GetByIdAsync(businessId, cancellationToken);
        if (business is null) return null;
        EnsureCanManage(business, actorUserId, isPlatformAdmin);
        return ToView(business);
    }

    public async Task<BusinessView?> ConfigureAsync(Guid businessId, Guid actorUserId, bool isPlatformAdmin,
        string name, string address, CancellationToken cancellationToken)
    {
        var business = await repository.GetByIdAsync(businessId, cancellationToken);
        if (business is null) return null;
        EnsureCanManage(business, actorUserId, isPlatformAdmin);
        business.Rename(name);
        business.ChangeAddress(address);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(business);
    }

    public async Task<BusinessView?> SetActiveAsync(Guid businessId, bool active, bool isPlatformAdmin, CancellationToken cancellationToken)
    {
        if (!isPlatformAdmin) throw new BusinessAuthorizationException();
        var business = await repository.GetByIdAsync(businessId, cancellationToken);
        if (business is null) return null;
        business.SetActive(active);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(business);
    }

    public async Task<IReadOnlyList<BusinessView>> GetActiveAsync(CancellationToken cancellationToken) =>
        (await repository.GetActiveAsync(cancellationToken)).Select(ToView).ToArray();

    public async Task<BusinessTransferDetailsView?> GetTransferDetailsAsync(Guid businessId, Guid actorUserId,
        bool isPlatformAdmin, CancellationToken cancellationToken)
    {
        var business = await repository.GetByIdAsync(businessId, cancellationToken);
        if (business is null) return null;
        EnsureCanManage(business, actorUserId, isPlatformAdmin);
        return new BusinessTransferDetailsView(business.TransferRecipientName, business.TransferInstitution,
            business.TransferAccountReference, business.TransferInstructions, business.HasTransferDetails);
    }

    public async Task<BusinessTransferDetailsView?> ConfigureTransferDetailsAsync(Guid businessId, Guid actorUserId,
        bool isPlatformAdmin, string recipientName, string institution, string accountReference, string? instructions,
        CancellationToken cancellationToken)
    {
        var business = await repository.GetByIdAsync(businessId, cancellationToken);
        if (business is null) return null;
        EnsureCanManage(business, actorUserId, isPlatformAdmin);
        business.ConfigureTransferDetails(recipientName, institution, accountReference, instructions);
        await repository.SaveChangesAsync(cancellationToken);
        return new BusinessTransferDetailsView(business.TransferRecipientName, business.TransferInstitution,
            business.TransferAccountReference, business.TransferInstructions, business.HasTransferDetails);
    }

    private static void EnsureCanManage(Business business, Guid actorUserId, bool isPlatformAdmin)
    {
        if (business.OwnerUserId != actorUserId && !isPlatformAdmin)
            throw new BusinessAuthorizationException();
    }

    private static BusinessView ToView(Business business) =>
        new(business.Id, business.OwnerUserId, business.Name, business.Address, business.IsActive);
}
