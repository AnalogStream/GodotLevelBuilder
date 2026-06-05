using Godot;

namespace LevelBuilder.Core.Primitives;

public enum ParamType { Float, Int, Bool, String }

/// <summary>
/// Declares one parameter of a primitive: drives the inspector UI and supplies
/// the default used when an instance has no explicit value.
/// </summary>
public sealed class ParamSpec
{
    public string Key { get; }
    public string Label { get; }
    public ParamType Type { get; }
    public Variant Default { get; }
    public float Min { get; }
    public float Max { get; }

    public ParamSpec(string key, string label, ParamType type, Variant def,
                     float min = float.NegativeInfinity, float max = float.PositiveInfinity)
    {
        Key = key;
        Label = label;
        Type = type;
        Default = def;
        Min = min;
        Max = max;
    }
}
