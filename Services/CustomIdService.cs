using InventoryManagementSystem.Models;
using InventoryManagementSystem.Models.CustomId;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace InventoryManagementSystem.Services;

public interface ICustomIdService
{
    string GenerateId(Inventory inventory, List<IdSegment> segments);
}

public class CustomIdService : ICustomIdService
{
    public string GenerateId(Inventory inventory, List<IdSegment> segments)
    {
        var newIdBuilder = new StringBuilder();
        int? sequenceValue = null;

        var sequenceSegment = segments.OfType<SequenceSegment>().FirstOrDefault();
        if (sequenceSegment != null)
        {
            // If this is the first item, use the start value. Otherwise, increment.
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
            switch (segment)
            {
                case FixedTextSegment s:
                    newIdBuilder.Append(s.Value);
                    break;
                case SequenceSegment s:
                    newIdBuilder.Append(sequenceValue?.ToString($"D{s.Padding}"));
                    break;
                case DateSegment s:
                    newIdBuilder.Append(DateTime.UtcNow.ToString(s.Format));
                    break;
                case RandomNumbersSegment s:
                    for (int i = 0; i < s.Length; i++)
                    {
                        newIdBuilder.Append(Random.Shared.Next(0, 10).ToString());
                    }
                    break;
                case GuidSegment s:
                    newIdBuilder.Append(Guid.NewGuid().ToString(s.Format));
                    break;
            }
        }

        return newIdBuilder.ToString();
    }
}