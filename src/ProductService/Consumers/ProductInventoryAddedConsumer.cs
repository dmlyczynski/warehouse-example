using IntegrationEvents;
using MassTransit;
using ProductService.Infrastructure;

namespace ProductService.Consumers;

public class ProductInventoryAddedConsumer : IConsumer<ProductInventoryAddedEvent>
{
    private readonly IProductRepository _repository;
    private readonly ILogger<ProductInventoryAddedConsumer> _logger;

    public ProductInventoryAddedConsumer(
        IProductRepository repository,
        ILogger<ProductInventoryAddedConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProductInventoryAddedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Received ProductInventoryAddedEvent: EventId={EventId}, ProductId={ProductId}, Quantity={Quantity}",
            message.EventId, message.ProductId, message.Quantity);

        try
        {
            if (await _repository.IsEventProcessedAsync(message.EventId))
            {
                _logger.LogInformation(
                    "Event {EventId} already processed, skipping",
                    message.EventId);
                return;
            }
            
            await _repository.UpdateProductAmountAsync(
                message.ProductId,
                message.Quantity,
                message.EventId);

            _logger.LogInformation(
                "Updated product {ProductId} amount by {Quantity} for event {EventId}",
                message.ProductId, message.Quantity, message.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing ProductInventoryAddedEvent: {EventId}",
                message.EventId);
        }
    }
}
