using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using E2E.Tests.Helpers;
using FluentAssertions;
using InventoryService.Infrastructure;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using ProductService.Application;
using ProductService.Consumers;
using ProductService.Domain;
using ProductService.Infrastructure;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace E2E.Tests;

public class WarehouseEndToEndTests
{
    private PostgreSqlContainer _postgresContainer = null!;
    private RabbitMqContainer _rabbitmqContainer = null!;
    private WebApplicationFactory<InventoryService.Program> _inventoryServiceFactory = null!;
    private WebApplicationFactory<ProductService.Program> _productServiceFactory = null!;
    private HttpClient _inventoryClient = null!;
    private HttpClient _productClient = null!;
    private string _productServiceBaseUrl = null!;
    private const string RabbitMqNameKey = "guest";

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:latest")
            .Build();
        await _postgresContainer.StartAsync();

        _rabbitmqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername(RabbitMqNameKey)
            .WithPassword(RabbitMqNameKey)
            .Build();
        await _rabbitmqContainer.StartAsync();
    }

    [SetUp]
    public Task SetUp()
    {
        var connectionString = _postgresContainer.GetConnectionString();
        var rabbitMqHost = _rabbitmqContainer.Hostname;
        var rabbitMqPort = _rabbitmqContainer.GetMappedPublicPort(5672);
        
        _productServiceFactory = new WebApplicationFactory<ProductService.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Integration");
                
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ProductDbContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }
                    
                    services.AddDbContext<ProductDbContext>(options =>
                    {
                        options.UseNpgsql(connectionString);
                    });
                    
                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    
                    services.AddMassTransit(x =>
                    {
                        x.AddConsumer<ProductInventoryAddedConsumer>();

                        x.UsingRabbitMq((context, cfg) =>
                        {
                            cfg.Host(rabbitMqHost, rabbitMqPort, "/", h =>
                            {
                                h.Username(RabbitMqNameKey);
                                h.Password(RabbitMqNameKey);
                            });

                            cfg.ConfigureEndpoints(context);
                        });
                    });
                });

                builder.UseUrls("http://localhost:8081");
            });

        _productClient = _productServiceFactory.CreateClient();
        _productServiceBaseUrl = "http://localhost:8081";
        
        _inventoryServiceFactory = new WebApplicationFactory<InventoryService.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Integration");
                
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<InventoryDbContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDbContext<InventoryDbContext>(options =>
                    {
                        options.UseNpgsql(connectionString);
                    });
                    
                    var productDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IProductServiceClient));
                    if (productDescriptor != null)
                    {
                        services.Remove(productDescriptor);
                    }
                    
                    services.AddSingleton<IProductServiceClient>(sp =>
                    {
                        var logger = sp.GetRequiredService<ILogger<ProductServiceClient>>();
                        return new ProductServiceClient(_productClient, logger);
                    });

                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                    db.Database.EnsureCreated();
                    
                    services.AddMassTransit(x =>
                    {
                        x.UsingRabbitMq((context, cfg) =>
                        {
                            cfg.Host(rabbitMqHost, rabbitMqPort, "/", h =>
                            {
                                h.Username("guest");
                                h.Password("guest");
                            });

                            cfg.ConfigureEndpoints(context);
                        });
                    });
                });

                builder.UseUrls("http://localhost:8080");
            });

        _inventoryClient = _inventoryServiceFactory.CreateClient();
        return Task.CompletedTask;
    }

    [TearDown]
    public void TearDown()
    {
        _inventoryClient?.Dispose();
        _productClient?.Dispose();
        _inventoryServiceFactory?.Dispose();
        _productServiceFactory?.Dispose();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }

        if (_rabbitmqContainer != null)
        {
            await _rabbitmqContainer.DisposeAsync();
        }
    }

    [Test]
    public async Task EndToEnd_AddInventory_ShouldIncreaseProductAmount()
    {
        // Arrange - Create a product in ProductService
        var token = JwtTokenHelper.GenerateToken(
            ["write", "read"],
            "a-string-secret-at-least-256-bits-long",
            "Warehouse",
            "Warehouse");

        var createProductRequest = new CreateProductRequest
        {
            Name = "Test Product E2E",
            Description = "E2E Test Description",
            Price = 99.99m
        };

        _productClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createResponse = await _productClient.PostAsJsonAsync("/products", createProductRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdProduct = await createResponse.Content.ReadFromJsonAsync<Product>();
        createdProduct.Should().NotBeNull();
        createdProduct!.Amount.Should().Be(0, "new product should have zero amount");

        // Act - Add inventory for the product via InventoryService
        var addInventoryRequest = new InventoryService.Application.AddInventoryRequest
        {
            ProductId = createdProduct.Id,
            Quantity = 50
        };

        _inventoryClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var inventoryResponse = await _inventoryClient.PostAsJsonAsync("/inventory", addInventoryRequest);

        // Assert - InventoryService should return 201
        inventoryResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Wait for the event to be processed
        await Task.Delay(3000);

        // Verify - Product amount should be updated in ProductService
        var getProductResponse = await _productClient.GetAsync($"/products/{createdProduct.Id}");
        getProductResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedProduct = await getProductResponse.Content.ReadFromJsonAsync<Product>();
        updatedProduct.Should().NotBeNull();
        updatedProduct!.Amount.Should().Be(50, "product amount should be increased by the inventory quantity");
    }

    [Test]
    public async Task EndToEnd_AddInventoryMultipleTimes_ShouldCumulativelyIncreaseProductAmount()
    {
        // Arrange - Create a product
        var token = JwtTokenHelper.GenerateToken(
        ["write", "read"],
        "a-string-secret-at-least-256-bits-long",
        "Warehouse",
        "Warehouse");

        var createProductRequest = new CreateProductRequest
        {
            Name = "Test Product Multiple Additions",
            Description = "Multiple additions test",
            Price = 49.99m
        };

        _productClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createResponse = await _productClient.PostAsJsonAsync("/products", createProductRequest);
        var createdProduct = await createResponse.Content.ReadFromJsonAsync<Product>();

        // Act - Add inventory multiple times
        _inventoryClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await _inventoryClient.PostAsJsonAsync("/inventory", new InventoryService.Application.AddInventoryRequest
        {
            ProductId = createdProduct!.Id,
            Quantity = 10
        });

        await Task.Delay(2000);

        await _inventoryClient.PostAsJsonAsync("/inventory", new InventoryService.Application.AddInventoryRequest
        {
            ProductId = createdProduct.Id,
            Quantity = 20
        });

        await Task.Delay(2000);

        await _inventoryClient.PostAsJsonAsync("/inventory", new InventoryService.Application.AddInventoryRequest
        {
            ProductId = createdProduct.Id,
            Quantity = 15
        });

        await Task.Delay(3000);

        // Assert - Product amount should be the sum of all additions
        var getProductResponse = await _productClient.GetAsync($"/products/{createdProduct.Id}");
        var updatedProduct = await getProductResponse.Content.ReadFromJsonAsync<Product>();

        updatedProduct.Should().NotBeNull();
        updatedProduct!.Amount.Should().Be(45, "product amount should be the sum of all inventory additions");
    }

    [Test]
    public async Task EndToEnd_AddInventoryForNonExistingProduct_ShouldReturn400()
    {
        // Arrange
        var token = JwtTokenHelper.GenerateToken(
            ["write", "read"],
            "a-string-secret-at-least-256-bits-long",
            "Warehouse",
            "Warehouse");

        var nonExistingProductId = Guid.NewGuid();

        var addInventoryRequest = new InventoryService.Application.AddInventoryRequest
        {
            ProductId = nonExistingProductId,
            Quantity = 10
        };

        // Act
        _inventoryClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _inventoryClient.PostAsJsonAsync("/inventory", addInventoryRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task EndToEnd_AddInventoryWithInvalidQuantity_ShouldReturn400()
    {
        // Arrange
        var token = JwtTokenHelper.GenerateToken(
            ["write", "read"],
            "a-string-secret-at-least-256-bits-long",
            "Warehouse",
            "Warehouse");

        var addInventoryRequest = new InventoryService.Application.AddInventoryRequest
        {
            ProductId = Guid.NewGuid(),
            Quantity = 0  // Invalid
        };

        // Act
        _inventoryClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _inventoryClient.PostAsJsonAsync("/inventory", addInventoryRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task EndToEnd_GetProducts_ShouldReturnAllProducts()
    {
        // Arrange
        var token = JwtTokenHelper.GenerateToken(
            ["read"],
            "a-string-secret-at-least-256-bits-long",
            "Warehouse",
            "Warehouse");

        _productClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _productClient.GetAsync("/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<List<Product>>();
        products.Should().NotBeNull();
    }
}
