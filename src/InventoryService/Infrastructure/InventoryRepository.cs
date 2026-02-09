using InventoryService.Domain;

namespace InventoryService.Infrastructure;

public interface IInventoryRepository
{
    Task InsertAsync(Inventory inventory, CancellationToken cancellationToken = default);
    Task<bool> ProductExistsAsync(Guid productId, CancellationToken cancellationToken = default);
}

public class InventoryRepository : IInventoryRepository
{
    private readonly InventoryDbContext _context;

    public InventoryRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task InsertAsync(Inventory inventory, CancellationToken cancellationToken = default)
    {
        await _context.Inventories.AddAsync(inventory, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
    
    public async Task<bool> ProductExistsAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        // For this example, we assume all products exist (cross-service validation could be done via HTTP or events)
        // In real-world scenario, you might call ProductService API or have a local cache
        return await Task.FromResult(true);
    }
}
