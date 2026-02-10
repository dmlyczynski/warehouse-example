using InventoryService.Domain;

namespace InventoryService.Infrastructure;

public interface IInventoryRepository
{
    Task InsertAsync(Inventory inventory, CancellationToken cancellationToken = default);
    Task<bool> ProductExistsAsync(Guid productId, string authHeader, CancellationToken cancellationToken = default);
}

public class InventoryRepository : IInventoryRepository
{
    private readonly IProductServiceClient _productServiceClient;
    private readonly InventoryDbContext _context;

    public InventoryRepository(InventoryDbContext context, IProductServiceClient productServiceClient)
    {
        _context = context;
        _productServiceClient = productServiceClient;
    }

    public async Task InsertAsync(Inventory inventory, CancellationToken cancellationToken = default)
    {
        await _context.Inventories.AddAsync(inventory, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ProductExistsAsync(Guid productId, string authHeader, CancellationToken cancellationToken = default)
    {
        return await _productServiceClient.ProductExistsAsync(productId, authHeader, cancellationToken);
    }
}
