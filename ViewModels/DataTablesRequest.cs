using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace InventoryManagementSystem.ViewModels;

public class DataTablesRequest
{
    [FromForm(Name = "draw")]
    public int Draw { get; set; }

    [FromForm(Name = "start")]
    public int Start { get; set; }

    [FromForm(Name = "length")]
    public int Length { get; set; }

    [FromForm(Name = "search[value]")]
    public string? SearchValue { get; set; }

    [FromForm(Name = "order")]
    public List<DataTablesOrder> Order { get; set; } = new List<DataTablesOrder>();
}

public class DataTablesOrder
{
    [FromForm(Name = "column")]
    public int Column { get; set; }

    [FromForm(Name = "dir")]
    public string Dir { get; set; } = "asc";
}