using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InventoryManagementSystem.ViewModels;

public class SupportTicketJsonModel
{
    [JsonPropertyName("reportedBy")]
    public string ReportedBy { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("inventory")]
    public string? Inventory { get; set; }

    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;

    [JsonPropertyName("adminEmails")]
    public List<string> AdminEmails { get; set; } = new();
}