namespace InventoryManagementSystem.ViewModels
{
    public class FieldDeleteRequest
    {
        public string[] FieldIds { get; set; } = [];
        public uint InventoryVersion { get; set; }
    }
}