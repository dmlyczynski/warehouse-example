using InventoryService.Validators;
using Microsoft.Extensions.Primitives;

namespace InventoryService.Endpoints;

using System.Security.Claims;
using IntegrationEvents;
using Infrastructure;
using Application;
using Domain;
using MassTransit;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/inventory")
            .RequireAuthorization();

        group.MapPost("/", AddInventory)
            .RequireAuthorization(policy => policy.RequireRole("write"))
            .WithName("AddInventory")
            .Produces<Inventory>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> AddInventory(
        AddInventoryRequest request,
        IInventoryRepository repository,
        IPublishEndpoint publishEndpoint,
        ILoggerFactory loggerFactory,
        ClaimsPrincipal user,
        CancellationToken cancellationToken,
        HttpRequest requestAuth)
    {
        var logger = loggerFactory.CreateLogger("InventoryEndpoints");

        try
        {
            if (!requestAuth.Headers.TryGetValue("Authorization", out var authHeader))
            {
                return Results.BadRequest(new { error = "Authorization header missing" });
            }

            var inventory = new Inventory
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                AddedAt = DateTime.UtcNow,
                AddedBy = user.Identity?.Name ?? "Unknown"
            };

            var validator = new InventoryValidator();
            var result = await validator.ValidateAsync(inventory, cancellationToken);

            if (!result.IsValid)
            {
                foreach (var error in result.Errors)
                {
                    logger.LogError("Validation error: {propertyName}: {errorMessage}", error.PropertyName, error.ErrorMessage);
                }

                return Results.BadRequest(new { Error = "Invalid inventory data" });
            }

            if (!await repository.ProductExistsAsync(request.ProductId, GetAuthHeader(authHeader), cancellationToken))
            {
                logger.LogWarning("Product {ProductId} does not exist", request.ProductId);
                return Results.BadRequest(new { Error = "Product does not exist" });
            }

            await repository.InsertAsync(inventory, cancellationToken);

            logger.LogInformation(
                "Inventory added: {InventoryId} for Product {ProductId} with Quantity {Quantity}",
                inventory.Id, inventory.ProductId, inventory.Quantity);

            var @event = new ProductInventoryAddedEvent
            {
                EventId = Guid.NewGuid(),
                ProductId = inventory.ProductId,
                Quantity = inventory.Quantity,
                OccurredAt = inventory.AddedAt
            };

            await publishEndpoint.Publish(@event, cancellationToken);

            logger.LogInformation(
                "Published ProductInventoryAddedEvent: {EventId}",
                @event.EventId);

            return Results.Created($"/inventory/{inventory.Id}", inventory);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error adding inventory for Product {ProductId}",
                request.ProductId);

            return Results.Problem(
                title: "Internal Server Error",
                detail: "An error occurred while processing your request",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        string GetAuthHeader(StringValues authHeader)
        {
            return authHeader.ToString().Replace("Bearer", "");
        }
    }
}