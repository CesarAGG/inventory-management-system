using System;
using System.Text.Json.Serialization;

namespace InventoryManagementSystem.Models.CustomId;

public abstract class IdSegment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public abstract string Type { get; }
}

public class FixedTextSegment : IdSegment
{
    public override string Type => "FixedText";
    [JsonInclude]
    public string Value { get; set; } = string.Empty;
}

public class SequenceSegment : IdSegment
{
    public override string Type => "Sequence";
    [JsonInclude]
    public int StartValue { get; set; } = 1;
    [JsonInclude]
    public int Step { get; set; } = 1;
    [JsonInclude]
    public int Padding { get; set; } = 1;
}

public class DateSegment : IdSegment
{
    public override string Type => "Date";
    [JsonInclude]
    public string Format { get; set; } = "yyyyMMdd";
}

public class RandomNumbersSegment : IdSegment
{
    public override string Type => "RandomNumbers";
    [JsonInclude]
    public int Length { get; set; } = 4;
}

public class GuidSegment : IdSegment
{
    public override string Type => "Guid";
    [JsonInclude]
    public string Format { get; set; } = "N";
}