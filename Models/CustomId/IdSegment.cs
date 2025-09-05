using System;
using System.Text.Json.Serialization;

namespace InventoryManagementSystem.Models.CustomId;

[JsonDerivedType(typeof(FixedTextSegment), typeDiscriminator: "fixed")]
[JsonDerivedType(typeof(SequenceSegment), typeDiscriminator: "sequence")]
[JsonDerivedType(typeof(DateSegment), typeDiscriminator: "date")]
[JsonDerivedType(typeof(RandomNumbersSegment), typeDiscriminator: "random")]
[JsonDerivedType(typeof(GuidSegment), typeDiscriminator: "guid")]
public abstract class IdSegment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public abstract string Type { get; }
}

public class FixedTextSegment : IdSegment
{
    public override string Type => "FixedText";
    public string Value { get; set; } = string.Empty;
}

public class SequenceSegment : IdSegment
{
    public override string Type => "Sequence";
    public int StartValue { get; set; } = 1;
    public int Step { get; set; } = 1;
    public int Padding { get; set; } = 1;
}

public class DateSegment : IdSegment
{
    public override string Type => "Date";
    public string Format { get; set; } = "yyyyMMdd";
}

public class RandomNumbersSegment : IdSegment
{
    public override string Type => "RandomNumbers";
    public int Length { get; set; } = 4;
}

public class GuidSegment : IdSegment
{
    public override string Type => "Guid";
    public string Format { get; set; } = "N"; // N, D, B, P
}