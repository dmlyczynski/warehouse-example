using ProductService.Domain;
using Microsoft.EntityFrameworkCore;

namespace ProductService.Infrastructure;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default);
    Task InsertAsync(Product product, CancellationToken cancellationToken = default);
    Task UpdateProductAmountAsync(Guid productId, int quantityToAdd, Guid eventId, CancellationToken cancellationToken = default);
    Task<bool> IsEventProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
}

public class ProductRepository : IProductRepository
{
    private readonly ProductDbContext _context;

    public ProductRepository(ProductDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Products.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products.ToListAsync(cancellationToken);
    }

    public async Task InsertAsync(Product product, CancellationToken cancellationToken = default)
    {
        await _context.Products.AddAsync(product, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateProductAmountAsync(Guid productId, int quantityToAdd, Guid eventId, CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            var isProcessed = await _context.ProcessedEvents
                .AnyAsync(e => e.EventId == eventId, cancellationToken);

            if (isProcessed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            var product = await _context.Products.FirstOrDefaultAsync(x=>x.Id == productId, cancellationToken);
            if (product != null)
            {
                product.Amount += quantityToAdd;
                product.UpdatedAt = DateTime.UtcNow;
            }
            
            await _context.ProcessedEvents.AddAsync(new ProcessedEvent
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                ProcessedAt = DateTime.UtcNow
            }, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> IsEventProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _context.ProcessedEvents
            .AnyAsync(e => e.EventId == eventId, cancellationToken);
    }
}
