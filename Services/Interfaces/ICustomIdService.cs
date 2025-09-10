using InventoryManagementSystem.Models;
using InventoryManagementSystem.Models.CustomId;
using System.Collections.Generic;

namespace InventoryManagementSystem.Services;

public interface ICustomIdService
{
    (string Id, string Boundaries) GenerateId(Inventory inventory, List<IdSegment> segments);
}