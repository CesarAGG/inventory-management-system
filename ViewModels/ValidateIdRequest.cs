namespace InventoryManagementSystem.ViewModels
{
    public class ValidateIdRequest
    {
        public string CustomId { get; set; } = string.Empty;
        public string? Boundaries { get; set; }
    }
}