using System.ComponentModel.DataAnnotations;

namespace ProductService.Application;

public record CreateProductRequest
{
    [Required(ErrorMessage = "Name is required")]
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
    public decimal Price { get; init; }
}
