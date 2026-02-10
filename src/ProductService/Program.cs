using Common.Infrastructure;
using Microsoft.EntityFrameworkCore;
using ProductService;
using ProductService.Consumers;
using ProductService.Endpoints;
using ProductService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var configuration = new ConfigurationBuilder()
    .AddCommandLine(args)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

builder.Configuration.Bind(configuration);

builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IProductRepository, ProductRepository>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.ParseStateValues = true;
});

builder.Services.AddOpenTelemetryConfiguration(Instrumentor.ServiceName);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddAuthenticationConfiguration(configuration, builder.Environment);

builder.Services.AddAuthorization();

// ToDo
builder.Services.AddHealthChecks();

builder.Services.AddMassTransitConfiguration<ProductInventoryAddedConsumer>(configuration, builder.Environment);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(8081);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    db.Database.Migrate();
}

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Open API V");
});

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapProductEndpoints();

app.Run();

namespace ProductService
{
    public partial class Program
    { }
}
