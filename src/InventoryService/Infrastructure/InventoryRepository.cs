using InventoryService.Domain;

namespace InventoryService.Infrastructure;

public interface IInventoryRepository
{
    Task InsertAsync(Inventory inventory, CancellationToken cancellationToken = default);
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
}
