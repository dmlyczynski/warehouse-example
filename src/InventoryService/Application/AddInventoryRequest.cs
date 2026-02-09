using System.ComponentModel.DataAnnotations;

namespace InventoryService.Application;

public record AddInventoryRequest
{
    [Required]
    public Guid ProductId { get; init; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
    public int Quantity { get; init; }
}
