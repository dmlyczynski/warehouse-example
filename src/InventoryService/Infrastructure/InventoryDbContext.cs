using InventoryService.Domain;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Infrastructure;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Inventory> Inventories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProductId).IsRequired();
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.AddedAt).IsRequired();
            entity.Property(e => e.AddedBy).IsRequired();
        });
    }
}
