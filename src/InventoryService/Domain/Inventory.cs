namespace InventoryService.Domain;

public class Inventory
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; }
    public string AddedBy { get; set; }
}