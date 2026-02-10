using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using ProductService.Consumers;

namespace ProductService;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddMassTransitConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!environment.IsEnvironment("Integration"))
        {
            services.AddMassTransit(x =>
            {
                x.AddConsumer<ProductInventoryAddedConsumer>();
        
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
                    {
                        h.Username(configuration["RabbitMQ:Username"] ?? "guest");
                        h.Password(configuration["RabbitMQ:Password"] ?? "guest");
                    });
        
                    cfg.ConfigureEndpoints(context);
                });
            });
        }

        return services;
    }
    
    public static IServiceCollection AddAuthenticationConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
    
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
            };
        });

        return services;
    }
    
    public static IServiceCollection AddOpenTelemetryConfiguration(
        this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(Helpers.ConfigureResource)
            .WithTracing(tracerProviderBuilder =>
                tracerProviderBuilder
                    .AddSource(Instrumentor.ServiceName)
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        opts.Filter = context =>
                        {
                            var ignore = new[] { "/swagger" };
                            return !ignore.Any(s => context.Request.Path.ToString().Contains(s));
                        };
                    })
                    .AddHttpClientInstrumentation())
            .WithMetrics(metricsProviderBuilder =>
                metricsProviderBuilder
                    .AddMeter(Instrumentor.ServiceName)
                    .AddRuntimeInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation())
            .UseOtlpExporter();

        return services;
    }
}