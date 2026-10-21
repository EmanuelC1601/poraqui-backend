using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Poraqui.Domain.Commerce;
using Poraqui.Domain.Orders;
using Poraqui.Infrastructure.Identity;

namespace Poraqui.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<CatalogProduct> CatalogProducts => Set<CatalogProduct>();
    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();
    public DbSet<DeliveryRate> DeliveryRates => Set<DeliveryRate>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<CourierAvailability> CourierAvailabilities => Set<CourierAvailability>();
    public DbSet<ExternalDeliveryOffer> ExternalDeliveryOffers => Set<ExternalDeliveryOffer>();
    public DbSet<OfflineDeliverySyncRecord> OfflineDeliverySyncRecords => Set<OfflineDeliverySyncRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Business>(entity =>
        {
            entity.ToTable("Businesses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(500).IsRequired();
            entity.Property(x => x.TransferRecipientName).HasMaxLength(160);
            entity.Property(x => x.TransferInstitution).HasMaxLength(120);
            entity.Property(x => x.TransferAccountReference).HasMaxLength(120);
            entity.Property(x => x.TransferInstructions).HasMaxLength(500);
            entity.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.OwnerUserId);
            entity.HasMany(x => x.OpeningHours).WithOne().HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(x => x.OpeningHours).HasField("_openingHours").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<BusinessOpeningHours>(entity =>
        {
            entity.ToTable("BusinessOpeningHours", table => table.HasCheckConstraint("CK_BusinessOpeningHours_ClosedOrValidHours",
                "([IsClosed] = 1 AND [OpensAt] IS NULL AND [ClosesAt] IS NULL) OR ([IsClosed] = 0 AND [OpensAt] IS NOT NULL AND [ClosesAt] IS NOT NULL AND [OpensAt] < [ClosesAt])"));
            entity.HasKey(x => new { x.BusinessId, x.Day });
            entity.Property(x => x.Day).HasConversion<int>();
            entity.Property(x => x.OpensAt).HasColumnType("time");
            entity.Property(x => x.ClosesAt).HasColumnType("time");
        });

        builder.Entity<CatalogProduct>(entity =>
        {
            entity.ToTable("CatalogProducts", table => table.HasCheckConstraint("CK_CatalogProducts_NonNegativePrice", "[Price] >= 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.HasOne<Business>().WithMany().HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.BusinessId, x.IsAvailable });
            entity.HasMany(x => x.OptionGroups).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(x => x.OptionGroups).HasField("_optionGroups").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<ProductOptionGroup>(entity =>
        {
            entity.ToTable("ProductOptionGroups", table => table.HasCheckConstraint("CK_ProductOptionGroups_SelectionBounds",
                "[MinSelections] >= 0 AND [MaxSelections] >= 1 AND [MinSelections] <= [MaxSelections]"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasMany(x => x.Options).WithOne().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(x => x.Options).HasField("_options").UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasIndex(x => new { x.ProductId, x.Name }).IsUnique();
        });

        builder.Entity<ProductOption>(entity =>
        {
            entity.ToTable("ProductOptions", table => table.HasCheckConstraint("CK_ProductOptions_NonNegativePriceDelta", "[PriceDelta] >= 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.PriceDelta).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.GroupId, x.Name }).IsUnique();
        });

        builder.Entity<DeliveryZone>(entity =>
        {
            entity.ToTable("DeliveryZones");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<DeliveryRate>(entity =>
        {
            entity.ToTable("DeliveryRates", table => table.HasCheckConstraint("CK_DeliveryRates_NonNegativeFee", "[Fee] >= 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Fee).HasPrecision(18, 2);
            entity.HasOne<Business>().WithMany().HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<DeliveryZone>().WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.BusinessId, x.ZoneId }).IsUnique();
        });

        builder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders", table => table.HasCheckConstraint("CK_Orders_NonNegativeAmountsAndValidVersion",
                "[DeliveryFee] >= 0 AND [Total] >= 0 AND ([CourierPaymentAmount] IS NULL OR [CourierPaymentAmount] >= 0) AND [Version] > 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.OrderNumber).IsUnique();
            entity.Property(x => x.DeliveryAddress).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CancellationReason).HasMaxLength(500);
            entity.Property(x => x.DeliveryFee).HasPrecision(18, 2);
            entity.Property(x => x.Total).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.Property(x => x.PickupCodeHash).HasMaxLength(128);
            entity.Property(x => x.DeliveryCodeHash).HasMaxLength(128);
            entity.Property(x => x.ContactChannel).HasMaxLength(20);
            entity.Property(x => x.CourierPaymentAmount).HasPrecision(18, 2);
            entity.Property(x => x.TransferRecipientName).HasMaxLength(160);
            entity.Property(x => x.TransferInstitution).HasMaxLength(120);
            entity.Property(x => x.TransferAccountReference).HasMaxLength(120);
            entity.Property(x => x.TransferInstructions).HasMaxLength(500);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CourierUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CourierPaymentReceivedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Business>().WithMany().HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<DeliveryZone>().WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Items).WithOne().HasForeignKey("OrderId").OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.TransferReviews).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(x => x.TransferReviews).HasField("_transferReviews").UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(x => x.StatusHistory).WithOne().HasForeignKey("OrderId").OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(x => x.StatusHistory).HasField("_statusHistory").UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(x => x.Incidents).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(x => x.Incidents).HasField("_incidents").UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasOne(x => x.RefundCase).WithOne().HasForeignKey<ManualRefundCase>(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.CustomerUserId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.BusinessId, x.Status, x.CreatedAtUtc });
        });

        builder.Entity<OrderCreationCommand>(entity =>
        {
            entity.ToTable("OrderCreationCommands");
            entity.HasKey(x => new { x.CustomerUserId, x.CommandId });
            entity.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ProtectedDeliveryCode).HasMaxLength(2000).IsRequired();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.OrderId).IsUnique();
        });

        builder.Entity<CourierAssignmentCommand>(entity =>
        {
            entity.ToTable("CourierAssignmentCommands");
            entity.HasKey(x => new { x.BusinessUserId, x.CommandId });
            entity.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ProtectedPickupCode).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.CourierPaymentAmount).HasPrecision(18, 2);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.BusinessUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.OrderId).IsUnique();
        });

        builder.Entity<OrderLine>(entity =>
        {
            entity.ToTable("OrderLines", table => table.HasCheckConstraint("CK_OrderLines_NonNegativeAmountsAndPositiveQuantity",
                "[UnitPrice] >= 0 AND [LineTotal] >= 0 AND [Quantity] > 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
            entity.HasMany(x => x.Options).WithOne().HasForeignKey("OrderLineId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(x => x.Options).HasField("_options").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<OrderLineOption>(entity =>
        {
            entity.ToTable("OrderLineOptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.PriceDelta).HasPrecision(18, 2);
        });

        builder.Entity<OrderIncident>(entity =>
        {
            entity.ToTable("OrderIncidents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => new { x.ReporterUserId, x.CommandId }).IsUnique().HasFilter("[CommandId] IS NOT NULL");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Resolution).HasMaxLength(2000);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReporterUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ResolverUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrderId, x.ReportedAtUtc });
        });

        builder.Entity<ManualRefundCase>(entity =>
        {
            entity.ToTable("ManualRefundCases", table => table.HasCheckConstraint("CK_ManualRefundCases_ValidAmounts",
                "[RequestedAmount] >= 0 AND ([RefundedAmount] IS NULL OR ([RefundedAmount] >= 0 AND [RefundedAmount] <= [RequestedAmount]))"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequestedAmount).HasPrecision(18, 2);
            entity.Property(x => x.RefundedAmount).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.Reference).HasMaxLength(160);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.OrderId).IsUnique();
            entity.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        });

        builder.Entity<TransferReview>(entity =>
        {
            entity.ToTable("TransferReviews");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EvidenceReference).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ContentHash).HasMaxLength(64);
            entity.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.SubmittedByUserId, x.CommandId }).IsUnique().HasFilter("[CommandId] IS NOT NULL");
        });

        builder.Entity<OrderStatusChange>(entity =>
        {
            entity.ToTable("OrderStatusHistory");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex("OrderId", nameof(OrderStatusChange.ChangedAtUtc));
        });

        builder.Entity<CourierAvailability>(entity =>
        {
            entity.ToTable("CourierAvailabilities", table => table.HasCheckConstraint("CK_CourierAvailabilities_ZoneWhenAvailable",
                "[IsAvailable] = 0 OR [ZoneId] IS NOT NULL"));
            entity.HasKey(x => x.CourierUserId);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CourierUserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<DeliveryZone>().WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ExternalDeliveryOffer>(entity =>
        {
            entity.ToTable("ExternalDeliveryOffers", table => table.HasCheckConstraint("CK_ExternalDeliveryOffers_ValidRemunerationVersionAndAcceptance",
                "[Remuneration] >= 0 AND [Version] > 0 AND ([Status] <> 'Accepted' OR [ProtectedPickupCode] IS NOT NULL)"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PickupAddress).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DestinationAddress).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Remuneration).HasPrecision(18, 2);
            entity.Property(x => x.Conditions).HasMaxLength(1000);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.Property(x => x.ProtectedPickupCode).HasMaxLength(2000);
            entity.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CourierUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<DeliveryZone>().WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CourierUserId, x.Status, x.ExpiresAtUtc });
            entity.HasIndex(x => new { x.OrderId, x.CourierUserId }).IsUnique().HasFilter("[Status] = 'Offered'");
            entity.HasIndex(x => x.OrderId).IsUnique().HasFilter("[Status] = 'Accepted'");
        });

        builder.Entity<OfflineDeliverySyncRecord>(entity =>
        {
            entity.ToTable("OfflineDeliverySyncRecords");
            entity.HasKey(x => new { x.ActorUserId, x.Id });
            entity.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
            entity.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.OrderId);
        });
    }
}
