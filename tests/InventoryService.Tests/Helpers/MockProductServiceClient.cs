using InventoryService.Infrastructure;

namespace InventoryService.Tests.Helpers;

public class MockProductServiceClient : IProductServiceClient
{
    public MockProductServiceClient()
    {
    }

    public async Task<bool> ProductExistsAsync(Guid productId, string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(true);
    }
}