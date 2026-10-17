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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Business>(entity =>
        {
            entity.ToTable("Businesses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(500).IsRequired();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.OwnerUserId);
        });

        builder.Entity<CatalogProduct>(entity =>
        {
            entity.ToTable("CatalogProducts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.HasOne<Business>().WithMany().HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.BusinessId, x.IsAvailable });
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
            entity.ToTable("DeliveryRates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Fee).HasPrecision(18, 2);
            entity.HasOne<Business>().WithMany().HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<DeliveryZone>().WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.BusinessId, x.ZoneId }).IsUnique();
        });

        builder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.OrderNumber).IsUnique();
            entity.Property(x => x.DeliveryAddress).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DeliveryFee).HasPrecision(18, 2);
            entity.Property(x => x.Total).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Business>().WithMany().HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<DeliveryZone>().WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Items).WithOne().HasForeignKey("OrderId").OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.TransferReviews).WithOne().HasForeignKey("OrderId").OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(x => x.TransferReviews).HasField("_transferReviews").UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasIndex(x => new { x.CustomerUserId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.BusinessId, x.Status, x.CreatedAtUtc });
        });

        builder.Entity<OrderLine>(entity =>
        {
            entity.ToTable("OrderLines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
        });

        builder.Entity<TransferReview>(entity =>
        {
            entity.ToTable("TransferReviews");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EvidenceReference).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
