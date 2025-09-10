namespace InventoryManagementSystem.ViewModels
{
    public class RegenerateIdRequest
    {
        public uint InventoryVersion { get; set; }
        public int? LastKnownSequenceValue { get; set; }
    }
}