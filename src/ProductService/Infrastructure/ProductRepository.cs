using System.Data;
using ProductService.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ProductService.Infrastructure;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default);
    Task InsertAsync(Product product, CancellationToken cancellationToken = default);
    Task<bool> UpdateProductAmountAsync(Guid productId, int quantityToAdd, Guid eventId, CancellationToken cancellationToken = default);
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
        return await _context.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task InsertAsync(Product product, CancellationToken cancellationToken = default)
    {
        await _context.Products.AddAsync(product, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdateProductAmountAsync(Guid productId, int quantityToAdd, Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var isProcessed = await _context.ProcessedEvents.AsNoTracking()
                .AnyAsync(e => e.EventId == eventId, cancellationToken);

            if (isProcessed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            var updated = await _context.Products
                .Where(x => x.Id == productId)
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(p => p.Amount, p => p.Amount + quantityToAdd)
                        .SetProperty(p => p.UpdatedAt, _ => DateTime.UtcNow),
                    cancellationToken);

            if (updated == 0)
            {
                return false;
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
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new Exception("Already exists");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return true;
    }

    public async Task<bool> IsEventProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _context.ProcessedEvents.AsNoTracking()
            .AnyAsync(e => e.EventId == eventId, cancellationToken);
    }

    public static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pgEx
               && pgEx.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}
