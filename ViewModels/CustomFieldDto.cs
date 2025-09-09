namespace InventoryManagementSystem.ViewModels;

public class CustomFieldDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsVisibleInTable { get; set; }
    public string Type { get; set; } = string.Empty;
    public string DataKey { get; set; } = string.Empty;
    public uint NewInventoryVersion { get; set; }
}