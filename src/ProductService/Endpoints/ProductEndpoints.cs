using ProductService.Application;
using ProductService.Domain;
using ProductService.Infrastructure;

namespace ProductService.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products")
            .RequireAuthorization();

        group.MapGet("/", GetProducts)
            .RequireAuthorization(policy => policy.RequireRole("read"))
            .WithName("GetProducts")
            .Produces<IEnumerable<Product>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError);

        group.MapGet("/{id:guid}", GetProduct)
            .RequireAuthorization(policy => policy.RequireRole("read"))
            .WithName("GetProduct")
            .Produces<Product>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);

        group.MapPost("/", CreateProduct)
            .WithName("CreateProduct")
            .RequireAuthorization(policy => policy.RequireRole("write"))
            .Produces<Product>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetProducts(
        IProductRepository repository,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("ProductEndpoints");

        try
        {
            var products = await repository.GetAllAsync(cancellationToken);
            return Results.Ok(products);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving products");
            return Results.Problem(
                title: "Internal Server Error",
                detail: "An error occurred while retrieving products",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetProduct(
        Guid id,
        IProductRepository repository,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("ProductEndpoints");

        try
        {
            var product = await repository.GetByIdAsync(id, cancellationToken);

            return product is null
                ? Results.NotFound()
                : Results.Ok(product);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving product {ProductId}", id);
            return Results.Problem(
                title: "Internal Server Error",
                detail: "An error occurred while retrieving the product",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> CreateProduct(
        CreateProductRequest request,
        IProductRepository repository,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("ProductEndpoints");

        try
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description ?? string.Empty,
                Price = request.Price,
                Amount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await repository.InsertAsync(product, cancellationToken);

            logger.LogInformation(
                "Product created: {ProductId} - {ProductName}",
                product.Id, product.Name);

            return Results.Created($"/products/{product.Id}", product);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating product");
            return Results.Problem(
                title: "Internal Server Error",
                detail: "An error occurred while creating the product",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}