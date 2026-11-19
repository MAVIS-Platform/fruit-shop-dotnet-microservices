using FruitOrdering.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace FruitOrdering.API.Data;

public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<IntegrationEventLogEntry> IntegrationEventLogEntries => Set<IntegrationEventLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ordering");

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => x.CustomerId);

            entity.Property(x => x.CustomerId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.FailureReason).HasMaxLength(500);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.OwnsOne(x => x.ShippingAddress, address =>
            {
                address.Property(a => a.Street).HasColumnName("shipping_street").HasMaxLength(200).IsRequired();
                address.Property(a => a.Ward).HasColumnName("shipping_ward").HasMaxLength(100).IsRequired();
                address.Property(a => a.City).HasColumnName("shipping_city").HasMaxLength(100).IsRequired();
            });

            entity.HasMany(x => x.Items)
                .WithOne()
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProductId).IsRequired();
            entity.Property(x => x.ProductNameSnapshot).HasMaxLength(200).IsRequired();
            entity.Property(x => x.UnitPriceSnapshot).HasPrecision(18, 2).IsRequired();
            entity.Property(x => x.Quantity).IsRequired();
            entity.Ignore(x => x.LineTotal);
        });

        modelBuilder.Entity<IntegrationEventLogEntry>(entity =>
        {
            entity.ToTable("integration_event_outbox");
            entity.HasKey(x => x.EventId);

            entity.HasIndex(x => new { x.State, x.CreatedAtUtc });

            entity.Property(x => x.EventType).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PayloadJson).IsRequired();
            entity.Property(x => x.State).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
        });
    }
}
