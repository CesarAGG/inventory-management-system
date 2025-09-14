namespace InventoryManagementSystem.ViewModels;

public class TransferOwnershipResponse
{
    public string Message { get; set; } = string.Empty;
    public bool ShouldRedirect { get; set; }
    public uint NewInventoryVersion { get; set; }
}