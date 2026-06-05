using LevelBuilder.Core.Data;

namespace LevelBuilder.Core.Primitives;

/// <summary>Ambient data a primitive may need while building its mesh/collision.</summary>
public sealed class BuildContext
{
    public float CellSize { get; init; } = 1.0f;
    public float StoreyHeight { get; init; } = 3.0f;
    public MaterialLibrary Materials { get; init; }
}
