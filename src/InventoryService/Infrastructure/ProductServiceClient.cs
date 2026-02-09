using System.Net.Http.Headers;

namespace InventoryService.Infrastructure;

public interface IProductServiceClient
{
    Task<bool> ProductExistsAsync(Guid productId, string? accessToken = null,
        CancellationToken cancellationToken = default);
}

public class ProductServiceClient : IProductServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductServiceClient> _logger;

    public ProductServiceClient(HttpClient httpClient, ILogger<ProductServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> ProductExistsAsync(Guid productId, string? accessToken = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrEmpty(accessToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            var response = await _httpClient.GetAsync($"Products/{productId}", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Product {ProductId} exists in ProductService", productId);
                return true;
            }
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Product {ProductId} not found in ProductService", productId);
                return false;
            }

            _logger.LogError("Error checking product {ProductId}: {StatusCode}", productId, response.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while checking product {ProductId}", productId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while checking product {ProductId}", productId);
            return false;
        }
    }
}
