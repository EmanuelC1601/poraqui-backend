using Microsoft.EntityFrameworkCore;
using Poraqui.Application.Orders;
using Poraqui.Application.Security;

namespace Poraqui.Infrastructure.Persistence;

public sealed class PlatformAdminDirectory(AppDbContext db) : IPlatformAdminDirectory
{
    public async Task<IReadOnlyList<Guid>> GetCurrentPlatformAdminUserIdsAsync(CancellationToken cancellationToken) =>
        await (from userRole in db.UserRoles
               join role in db.Roles on userRole.RoleId equals role.Id
               where role.Name == ApplicationRoles.PlatformAdmin
               select userRole.UserId).Distinct().ToArrayAsync(cancellationToken);
}
