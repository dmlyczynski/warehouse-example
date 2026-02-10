using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using InventoryService.Infrastructure;
using InventoryService.Tests.Helpers;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace InventoryService.Tests.Integration;

public class InventoryServiceFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RabbitMqContainer _rabbitmqContainer;
    private const string RabbitMqNameKey = "guest";

    public InventoryServiceFactory()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .Build();

        _rabbitmqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername(RabbitMqNameKey)
            .WithPassword(RabbitMqNameKey)
            .Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var rabbitMqHost = _rabbitmqContainer.Hostname;
        var rabbitMqPort = _rabbitmqContainer.GetMappedPublicPort(5672);

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
                options.UseNpgsql(_postgresContainer.GetConnectionString());
            });

            var productDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IProductServiceClient));
            if (productDescriptor != null)
            {
                services.Remove(productDescriptor);
            }

            services.AddSingleton<IProductServiceClient>(sp => new MockProductServiceClient());

            services.AddMassTransitTestHarness(x =>
            {
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
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        await _rabbitmqContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        await _rabbitmqContainer.DisposeAsync();
    }
}
