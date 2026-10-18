using Microsoft.EntityFrameworkCore;
using Poraqui.Application.Orders;
using Poraqui.Domain.Orders;

namespace Poraqui.Infrastructure.Persistence;

public sealed class OrderWorkflowRepository(AppDbContext db) : IOrderWorkflowRepository
{
    private IQueryable<Order> OrderGraph() => db.Orders
        .Include(order => order.Items).ThenInclude(item => item.Options)
        .Include(order => order.TransferReviews)
        .Include(order => order.StatusHistory)
        .Include(order => order.Incidents)
        .Include(order => order.RefundCase);

    public Task<Order?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        OrderGraph().SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken);

    public async Task<IReadOnlyList<Order>> GetOrdersForBusinessAsync(Guid businessId, CancellationToken cancellationToken) =>
        await OrderGraph().AsNoTracking().Where(order => order.BusinessId == businessId)
            .OrderByDescending(order => order.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Order>> GetOrdersForCourierAsync(Guid courierId, CancellationToken cancellationToken) =>
        await OrderGraph().AsNoTracking().Where(order => order.CourierUserId == courierId &&
            order.Status != OrderStatus.Delivered && order.Status != OrderStatus.Cancelled)
            .OrderByDescending(order => order.CreatedAtUtc).ToListAsync(cancellationToken);

    public Task<string?> GetBusinessAddressAsync(Guid businessId, CancellationToken cancellationToken) =>
        db.Businesses.Where(business => business.Id == businessId).Select(business => business.Address).SingleOrDefaultAsync(cancellationToken);

    public Task<bool> BusinessBelongsToAsync(Guid businessId, Guid ownerUserId, CancellationToken cancellationToken) =>
        db.Businesses.AnyAsync(business => business.Id == businessId && business.OwnerUserId == ownerUserId, cancellationToken);

    public Task<string?> GetCustomerPhoneAsync(Guid customerUserId, CancellationToken cancellationToken) =>
        db.Users.Where(user => user.Id == customerUserId).Select(user => user.PhoneNumber).SingleOrDefaultAsync(cancellationToken);

    public Task<OfflineDeliverySyncRecord?> GetOfflineDeliveryCommandAsync(Guid actorUserId, Guid commandId, CancellationToken cancellationToken) =>
        db.OfflineDeliverySyncRecords.SingleOrDefaultAsync(command => command.ActorUserId == actorUserId && command.Id == commandId, cancellationToken);

    public Task<OrderIncident?> GetIncidentCommandAsync(Guid actorUserId, Guid commandId, CancellationToken cancellationToken) =>
        db.Set<OrderIncident>().SingleOrDefaultAsync(incident => incident.ReporterUserId == actorUserId &&
            incident.CommandId == commandId, cancellationToken);

    public Task<TransferReview?> GetTransferEvidenceCommandAsync(Guid actorUserId, Guid commandId, CancellationToken cancellationToken) =>
        db.Set<TransferReview>().SingleOrDefaultAsync(review => review.SubmittedByUserId == actorUserId &&
            review.CommandId == commandId, cancellationToken);

    public Task<CourierAssignmentCommand?> GetCourierAssignmentCommandAsync(Guid actorUserId, Guid commandId, CancellationToken cancellationToken) =>
        db.Set<CourierAssignmentCommand>().SingleOrDefaultAsync(command => command.BusinessUserId == actorUserId &&
            command.CommandId == commandId, cancellationToken);

    public async Task<IReadOnlyList<ExternalDeliveryOffer>> CancelOpenExternalOffersForOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var offers = await db.ExternalDeliveryOffers.Where(offer => offer.OrderId == orderId &&
            offer.Status == ExternalDeliveryOfferStatus.Offered).ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var offer in offers)
        {
            offer.Expire(now);
            if (offer.Status == ExternalDeliveryOfferStatus.Offered) offer.Cancel(now);
        }
        return offers;
    }

    public async Task<bool> SaveOfflineDeliveryCommandAsync(OfflineDeliverySyncRecord command, CancellationToken cancellationToken)
    {
        db.OfflineDeliverySyncRecords.Add(command);
        try
        {
            await SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (await db.OfflineDeliverySyncRecords.AnyAsync(existing =>
                    existing.ActorUserId == command.ActorUserId && existing.Id == command.Id, cancellationToken))
                return false;
            throw;
        }
    }

    public async Task<bool> SaveIncidentCommandAsync(Guid actorUserId, Guid commandId, CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (await db.Set<OrderIncident>().AnyAsync(existing => existing.ReporterUserId == actorUserId &&
                    existing.CommandId == commandId, cancellationToken))
                return false;
            throw;
        }
    }

    public async Task<bool> SaveTransferEvidenceCommandAsync(Order order, CancellationToken cancellationToken)
    {
        var commandIds = order.TransferReviews.Where(review => review.CommandId.HasValue)
            .Select(review => review.CommandId!.Value).ToArray();
        try
        {
            await SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (commandIds.Length > 0 && await db.Set<TransferReview>().AnyAsync(review => review.SubmittedByUserId == order.CustomerUserId &&
                    review.CommandId.HasValue && commandIds.Contains(review.CommandId.Value), cancellationToken))
                return false;
            throw;
        }
    }

    public async Task<bool> SaveCourierAssignmentCommandAsync(Order order, CourierAssignmentCommand command, CancellationToken cancellationToken)
    {
        db.Set<CourierAssignmentCommand>().Add(command);
        try
        {
            await SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (await db.Set<CourierAssignmentCommand>().AnyAsync(existing => existing.BusinessUserId == command.BusinessUserId &&
                    existing.CommandId == command.CommandId, cancellationToken))
                return false;
            if (await db.Set<CourierAssignmentCommand>().AnyAsync(existing => existing.OrderId == command.OrderId, cancellationToken))
                throw new CourierAssignmentCommandConflictException();
            throw;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        // Status history is append-only. EF's field-backed read-only collection can
        // detect the new shadow-FK dependent as Modified; persist it as an insert.
        foreach (var entry in db.ChangeTracker.Entries<OrderStatusChange>().Where(entry => entry.State == EntityState.Modified))
            entry.State = EntityState.Added;
        foreach (var entry in db.ChangeTracker.Entries<OrderIncident>()
                     .Where(entry => entry.State == EntityState.Modified && entry.Entity.ResolverUserId is null))
            entry.State = EntityState.Added;
        foreach (var entry in db.ChangeTracker.Entries<ManualRefundCase>()
                     .Where(entry => entry.State == EntityState.Modified && entry.Entity.ReviewedByUserId is null))
            entry.State = EntityState.Added;
        foreach (var entry in db.ChangeTracker.Entries<TransferReview>()
                     .Where(entry => entry.State == EntityState.Modified && entry.Entity.Decision is null && entry.Entity.ReviewerUserId is null))
            entry.State = EntityState.Added;
        await db.SaveChangesAsync(cancellationToken);
    }
}
