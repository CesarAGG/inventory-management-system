using InventoryManagementSystem.Models;
using InventoryManagementSystem.Models.CustomId;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InventoryManagementSystem.Services;

public class CustomIdService : ICustomIdService
{
    public (string Id, string Boundaries) GenerateId(Inventory inventory, List<IdSegment> segments)
    {
        var idBuilder = new StringBuilder();
        var boundaries = new List<int>();

        var sequenceSegment = segments.OfType<SequenceSegment>().FirstOrDefault();
        int? sequenceValue = null;
        if (sequenceSegment != null)
        {
            if (inventory.LastSequenceValue < sequenceSegment.StartValue)
            {
                inventory.LastSequenceValue = sequenceSegment.StartValue;
            }
            else
            {
                inventory.LastSequenceValue += sequenceSegment.Step;
            }
            sequenceValue = inventory.LastSequenceValue;
        }

        foreach (var segment in segments)
        {
            string part = segment switch
            {
                FixedTextSegment s => s.Value,
                SequenceSegment s => sequenceValue?.ToString($"D{s.Padding}") ?? string.Empty,
                DateSegment s => DateTime.UtcNow.ToString(s.Format),
                RandomNumbersSegment s => s.Format switch
                {
                    "20-bit" => Random.Shared.Next(0, 1048576).ToString(),
                    "32-bit" => Random.Shared.NextInt64(0, 2147483648L).ToString(),
                    "6-digit" => Random.Shared.Next(0, 1000000).ToString("D6"),
                    _ => Random.Shared.Next(0, 1000000000).ToString("D9"),
                },
                GuidSegment s => Guid.NewGuid().ToString(s.Format),
                _ => string.Empty
            };
            idBuilder.Append(part);
            boundaries.Add(part.Length);
        }

        return (idBuilder.ToString(), string.Join(",", boundaries));
    }
}