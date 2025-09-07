using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InventoryManagementSystem.ViewModels;

public class DataTablesResponse<T>
{
    [JsonPropertyName("draw")]
    public int Draw { get; set; }

    [JsonPropertyName("recordsTotal")]
    public int RecordsTotal { get; set; }

    [JsonPropertyName("recordsFiltered")]
    public int RecordsFiltered { get; set; }

    [JsonPropertyName("data")]
    public IEnumerable<T> Data { get; set; } = new List<T>();
}