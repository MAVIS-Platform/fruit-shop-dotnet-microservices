using FruitCatalog.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace FruitCatalog.API.Data;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<FruitProduct> FruitProducts => Set<FruitProduct>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");

        modelBuilder.Entity<FruitProduct>(entity =>
        {
            entity.ToTable("fruit_products");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Sku).IsUnique();

            entity.Property(x => x.Sku).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Origin).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(30).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2).IsRequired();
            entity.Property(x => x.AvailableStock).IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken().IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
        });
    }
}
