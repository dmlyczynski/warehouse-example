using InventoryService.Domain;

namespace InventoryService.Validators;

using FluentValidation;

public class InventoryValidator : AbstractValidator<Inventory>
{
    public InventoryValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("ProductId is required");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero");

        RuleFor(x => x.AddedBy)
            .NotEmpty().WithMessage("AddedBy cannot be empty")
            .MaximumLength(100);

        RuleFor(x => x.AddedAt)
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("AddedAt cannot be in the future");
    }
}