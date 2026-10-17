using Microsoft.EntityFrameworkCore;
using Poraqui.Application.Commerce;
using Poraqui.Domain.Commerce;

namespace Poraqui.Infrastructure.Persistence;

public sealed class BusinessRepository(AppDbContext db) : IBusinessRepository
{
    public Task<Business?> GetByIdAsync(Guid businessId, CancellationToken cancellationToken) =>
        db.Businesses.SingleOrDefaultAsync(item => item.Id == businessId, cancellationToken);

    public async Task<IReadOnlyList<Business>> GetActiveAsync(CancellationToken cancellationToken) =>
        await db.Businesses.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Name).ToListAsync(cancellationToken);

    public async Task AddAsync(Business business, CancellationToken cancellationToken) =>
        await db.Businesses.AddAsync(business, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await db.SaveChangesAsync(cancellationToken);
}
