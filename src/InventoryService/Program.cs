using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Common.Infrastructure;
using InventoryService;
using InventoryService.Endpoints;
using InventoryService.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var configuration = new ConfigurationBuilder()
    .AddCommandLine(args)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

builder.Configuration.Bind(configuration);

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();

builder.Services.AddHttpClient<IProductServiceClient, ProductServiceClient>(client =>
{
    var baseUrl = builder.Configuration["ProductService:BaseUrl"] ?? "http://localhost:5001";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

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

builder.Services.AddMassTransitConfiguration(configuration, builder.Environment);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(8080);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
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

app.MapInventoryEndpoints();

app.Run();

namespace InventoryService
{
    public partial class Program
    { }
}