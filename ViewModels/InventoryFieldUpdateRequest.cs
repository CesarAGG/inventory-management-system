namespace InventoryManagementSystem.ViewModels
{
    public class UpdateFieldRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsVisibleInTable { get; set; }
        public uint InventoryVersion { get; set; }
    }

    public class FieldReorderRequest
    {
        public string[] OrderedFieldIds { get; set; } = [];
        public uint InventoryVersion { get; set; }
    }
}