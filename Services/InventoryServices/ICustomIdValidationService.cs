using InventoryManagementSystem.Models.CustomId;
using System.Collections.Generic;

namespace InventoryManagementSystem.Services.InventoryServices
{
    public interface ICustomIdValidationService
    {
        bool IsIdValid(string id, List<IdSegment> formatSegments);
    }
}