namespace InventoryManagementSystem.ViewModels
{
    public class GrantPermissionRequest
    {
        public string UserId { get; set; } = string.Empty;
        public uint InventoryVersion { get; set; }
    }
}