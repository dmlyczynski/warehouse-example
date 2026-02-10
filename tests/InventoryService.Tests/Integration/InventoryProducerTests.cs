using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using IntegrationEvents;
using InventoryService.Application;
using InventoryService.Tests.Helpers;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryService.Tests.Integration;

public class InventoryProducerTests : IClassFixture<InventoryServiceFactory>
{
    private readonly InventoryServiceFactory _factory;
    private readonly HttpClient _client;

    public InventoryProducerTests(InventoryServiceFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AddInventory_WithValidRequest_ShouldReturn201AndPublishEvent()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var request = new AddInventoryRequest
        {
            ProductId = productId,
            Quantity = 10
        };

        var token = JwtTokenHelper.GenerateToken(
            ["write", "read"],
            "a-string-secret-at-least-256-bits-long",
            "Warehouse",
            "Warehouse");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/Inventory", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        // Verify message was published (simplified check)
        var harness = _factory.Services.GetRequiredService<ITestHarness>();
        (await harness.Published.Any<ProductInventoryAddedEvent>()).Should().BeTrue();
    }

    [Fact]
    public async Task AddInventory_WithInvalidQuantity_ShouldReturn400()
    {
        // Arrange
        var request = new AddInventoryRequest
        {
            ProductId = Guid.NewGuid(),
            Quantity = 0  // Invalid
        };

        var token = JwtTokenHelper.GenerateToken(
            ["write", "read"],
            "a-string-secret-at-least-256-bits-long",
            "Warehouse",
            "Warehouse");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/Inventory", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddInventory_WithoutWriteRole_ShouldReturn403()
    {
        // Arrange
        var request = new AddInventoryRequest
        {
            ProductId = Guid.NewGuid(),
            Quantity = 10
        };

        var token = JwtTokenHelper.GenerateToken(
            ["read"],  // Wrong role
            "a-string-secret-at-least-256-bits-long",
            "Warehouse",
            "Warehouse");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/Inventory", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
