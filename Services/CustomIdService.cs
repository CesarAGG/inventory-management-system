using InventoryManagementSystem.Data;
using InventoryManagementSystem.Models;
using InventoryManagementSystem.Models.CustomId;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryManagementSystem.Services;

public class CustomIdService : ICustomIdService
{
    private readonly ApplicationDbContext _context;

    public CustomIdService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(string Id, string Boundaries, int newSequenceValue)> GenerateIdAsync(string inventoryId, List<IdSegment> segments, int? sequenceValueOverride = null)
    {
        var idBuilder = new StringBuilder();
        var boundaries = new List<int>();
        int firstSequenceValue = 0;
        bool firstSequenceSet = false;

        foreach (var segment in segments)
        {
            string part;
            if (segment is SequenceSegment s)
            {
                int sequenceValue;
                if (sequenceValueOverride.HasValue && !firstSequenceSet)
                {
                    sequenceValue = sequenceValueOverride.Value;
                }
                else
                {
                    var sequenceTracker = await _context.InventorySequences
                        .FirstOrDefaultAsync(st => st.InventoryId == inventoryId && st.SegmentId == s.Id);

                    if (sequenceTracker == null)
                    {
                        sequenceTracker = new InventorySequence
                        {
                            InventoryId = inventoryId,
                            SegmentId = s.Id,
                            LastValue = s.StartValue
                        };
                        _context.InventorySequences.Add(sequenceTracker);
                    }
                    else
                    {
                        sequenceTracker.LastValue += s.Step;
                    }
                    sequenceValue = sequenceTracker.LastValue;
                }

                if (!firstSequenceSet)
                {
                    firstSequenceValue = sequenceValue;
                    firstSequenceSet = true;
                }
                part = sequenceValue.ToString($"D{s.Padding}");
            }
            else
            {
                part = segment switch
                {
                    FixedTextSegment fts => fts.Value,
                    DateSegment ds => DateTime.UtcNow.ToString(ds.Format),
                    RandomNumbersSegment rns => rns.Format switch
                    {
                        "20-bit" => Random.Shared.Next(0, 1 << 20).ToString(),
                        "32-bit" => Random.Shared.NextInt64(0, 1L << 31).ToString(),
                        "9-digit" => Random.Shared.Next(0, 1000000000).ToString("D9"),
                        _ => Random.Shared.Next(0, 1000000).ToString("D6"), // Default to 6-digit
                    },
                    GuidSegment gs => Guid.NewGuid().ToString(gs.Format),
                    _ => string.Empty
                };
            }

            idBuilder.Append(part);
            boundaries.Add(part.Length);
        }

        return (idBuilder.ToString(), string.Join(",", boundaries), firstSequenceValue);
    }
}