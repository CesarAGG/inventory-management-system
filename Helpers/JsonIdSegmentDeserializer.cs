using InventoryManagementSystem.Models.CustomId;
using System.Text.Json;
using System.Collections.Generic;
using System;
using System.Linq;

namespace InventoryManagementSystem.Helpers;

public static class JsonIdSegmentDeserializer
{
    public static List<IdSegment> Deserialize(string json)
    {
        var segments = new List<IdSegment>();
        using var jsonDoc = JsonDocument.Parse(json);

        foreach (var element in jsonDoc.RootElement.EnumerateArray())
        {
            var props = element.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);

            if (!props.TryGetValue("type", out var typeProperty)) continue;

            var type = typeProperty.GetString();
            IdSegment? segment = type switch
            {
                "FixedText" => MapFixedText(props),
                "Sequence" => MapSequence(props),
                "Date" => MapDate(props),
                "RandomNumbers" => MapRandomNumbers(props),
                "Guid" => MapGuid(props),
                _ => null
            };

            if (segment != null) segments.Add(segment);
        }
        return segments;
    }

    private static string GetString(Dictionary<string, JsonElement> props, string key) =>
        props.TryGetValue(key, out var val) && val.ValueKind == JsonValueKind.String ? val.GetString() ?? "" : "";

    private static int GetInt(Dictionary<string, JsonElement> props, string key, int defaultVal) =>
        props.TryGetValue(key, out var val) && val.TryGetInt32(out var i) ? i : defaultVal;

    private static FixedTextSegment MapFixedText(Dictionary<string, JsonElement> props) =>
        new() { Id = GetString(props, "id"), Value = GetString(props, "value") };

    private static DateSegment MapDate(Dictionary<string, JsonElement> props) =>
        new() { Id = GetString(props, "id"), Format = GetString(props, "format") };

    private static GuidSegment MapGuid(Dictionary<string, JsonElement> props) =>
        new() { Id = GetString(props, "id"), Format = GetString(props, "format") };

    private static RandomNumbersSegment MapRandomNumbers(Dictionary<string, JsonElement> props) =>
        new() { Id = GetString(props, "id"), Format = GetString(props, "format") };

    private static SequenceSegment MapSequence(Dictionary<string, JsonElement> props) =>
        new() { Id = GetString(props, "id"), StartValue = GetInt(props, "startValue", 1), Step = GetInt(props, "step", 1), Padding = GetInt(props, "padding", 1) };
}