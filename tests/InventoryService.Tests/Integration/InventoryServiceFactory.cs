using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using InventoryService.Infrastructure;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace InventoryService.Tests.Integration;

public class InventoryServiceFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RabbitMqContainer _rabbitmqContainer;

    public InventoryServiceFactory()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .Build();

        _rabbitmqContainer = new RabbitMqBuilder()
            .Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
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
                options.UseNpgsql(_postgresContainer.GetConnectionString());
            });

            services.AddMassTransit(x =>
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(_rabbitmqContainer.Hostname, _rabbitmqContainer.GetMappedPublicPort(5672), "/", h =>
                    {
                        h.Username(_rabbitmqContainer.GetConnectionString().Split(';')[1].Split('=')[1]);
                        h.Password(_rabbitmqContainer.GetConnectionString().Split(';')[2].Split('=')[1]);
                    });
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
