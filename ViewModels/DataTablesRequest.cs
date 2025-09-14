using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InventoryManagementSystem.ViewModels;

public class DataTablesRequest
{
    public int Draw { get; set; }
    public int Start { get; set; }
    public int Length { get; set; }
    public Search? Search { get; set; }
    public List<DataTablesOrder> Order { get; set; } = new List<DataTablesOrder>();
}

public class Search
{
    public string? Value { get; set; }
    public bool Regex { get; set; }
}

public class DataTablesOrder
{
    public int Column { get; set; }
    public string Dir { get; set; } = "asc";
}