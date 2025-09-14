namespace InventoryManagementSystem.ViewModels
{
    public class RevokePermissionsRequest
    {
        public string[] UserIds { get; set; } = [];
        public uint InventoryVersion { get; set; }
    }
}