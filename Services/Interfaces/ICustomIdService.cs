using InventoryManagementSystem.Models;
using InventoryManagementSystem.Models.CustomId;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services;

public interface ICustomIdService
{
    Task<(string Id, string Boundaries, int newSequenceValue)> GenerateIdAsync(string inventoryId, List<IdSegment> segments, int? sequenceValueOverride = null);
}