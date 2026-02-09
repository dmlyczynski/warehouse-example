using ProductService.Domain;
using Microsoft.EntityFrameworkCore;

namespace ProductService.Infrastructure;

public class ProductDbContext : DbContext
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }
    public DbSet<ProcessedEvent> ProcessedEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Price).IsRequired().HasPrecision(18, 2);
            entity.Property(e => e.Amount).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventId).IsUnique();
            entity.Property(e => e.EventId).IsRequired();
            entity.Property(e => e.ProcessedAt).IsRequired();
        });
    }
}
