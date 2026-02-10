using System.Diagnostics;
using FluentAssertions;
using FluentAssertions.Execution;
using IntegrationEvents;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute.Extensions;
using ProductService.Consumers;
using ProductService.Domain;
using ProductService.Infrastructure;

namespace ProductService.Tests.Integration;

public class ProductInventoryConsumerIdempotencyTests : IClassFixture<ProductServiceFactory>
{
    private readonly ProductServiceFactory _factory;

    public ProductInventoryConsumerIdempotencyTests(ProductServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConsumeEvent_WhenReceivedTwice_ShouldUpdateProductOnlyOnce()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var harness = scope.ServiceProvider.GetRequiredService<ITestHarness>();
        var consumerHarness = _factory.Services.GetRequiredService<IConsumerTestHarness<ProductInventoryAddedConsumer>>();
        
        var productId = Guid.NewGuid();
        var product = new Product
        {
            Id = productId,
            Name = "Test Product",
            Description = "Test Description",
            Price = 10.00m,
            Amount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await dbContext.Products.AddAsync(product);
        await dbContext.SaveChangesAsync();

        var eventId = Guid.NewGuid();
        var quantity = 10;

        var @event = new ProductInventoryAddedEvent
        {
            EventId = eventId,
            ProductId = productId,
            Quantity = quantity,
            OccurredAt = DateTime.UtcNow
        };
        
        await harness.Start();

        try
        {
            // Act - Publish the same event twice
            await harness.Bus.Publish(@event);
            await harness.Bus.Publish(@event);
            
            var sw = Stopwatch.StartNew();
            while (consumerHarness.Consumed.Select<ProductInventoryAddedEvent>().Count() == 2)
            {
                if (sw.Elapsed > TimeSpan.FromSeconds(10))
                {
                    throw new TimeoutException("Not all messages were consumed");
                }
    
                await Task.Delay(1000);
            }
            
            await Task.Delay(2000);

            // Assert
            var updatedProduct = await repository.GetByIdAsync(productId);
            var processedEvents = await dbContext.ProcessedEvents
                .Where(e => e.EventId == eventId)
                .ToListAsync();
            
            using (new AssertionScope())
            {
                updatedProduct.Should().NotBeNull();
                updatedProduct!.Amount.Should().Be(10, "the product amount should be updated only once");
                processedEvents.Should().ContainSingle("the event should be marked as processed only once");    
            }
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task ConsumeEvent_WithDifferentEventIds_ShouldUpdateProductMultipleTimes()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var harness = scope.ServiceProvider.GetRequiredService<ITestHarness>();
        var consumerHarness = _factory.Services.GetRequiredService<IConsumerTestHarness<ProductInventoryAddedConsumer>>();
        
        var productId = Guid.NewGuid();
        var product = new Product
        {
            Id = productId,
            Name = "Test Product",
            Description = "Test Description",
            Price = 10.00m,
            Amount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await dbContext.Products.AddAsync(product);
        await dbContext.SaveChangesAsync();

        var event1 = new ProductInventoryAddedEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = productId,
            Quantity = 10,
            OccurredAt = DateTime.UtcNow
        };

        var event2 = new ProductInventoryAddedEvent
        {
            EventId = Guid.NewGuid(),
            ProductId = productId,
            Quantity = 5,
            OccurredAt = DateTime.UtcNow
        };
        
        await harness.Start();

        try
        {
            // Act - Publish two different events
            await harness.Bus.Publish(event1);
            await harness.Bus.Publish(event2);
            
            await Task.Delay(10000);
            
            // Assert
            var updatedProduct = await repository.GetByIdAsync(productId);
            var processedEvents = await dbContext.ProcessedEvents.ToListAsync();

            using (new AssertionScope())
            {
                updatedProduct.Should().NotBeNull();
                updatedProduct!.Amount.Should().Be(15, "both events should be processed");
                processedEvents.Should().HaveCount(2, "both events should be marked as processed");
            }
        }
        finally
        {
            await harness.Stop();
        }
    }

}
