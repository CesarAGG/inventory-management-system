namespace InventoryManagementSystem.ViewModels
{
    public class FieldNameUpdateRequest
    {
        public string Name { get; set; } = string.Empty;
        public uint InventoryVersion { get; set; }
    }

    public class FieldReorderRequest
    {
        public string[] OrderedFieldIds { get; set; } = [];
        public uint InventoryVersion { get; set; }
    }
}