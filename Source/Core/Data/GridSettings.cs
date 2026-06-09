using Godot;

namespace LevelBuilder.Core.Data;

/// <summary>Grid + snapping configuration for a level. Metres.</summary>
[GlobalClass]
public partial class GridSettings : Resource
{
    [Export] public float CellSize { get; set; } = 1.0f;
    [Export] public int Subdivisions { get; set; } = 1;
    /// <summary>Vertical increment for the draw-height indicator's nudge/scrub, metres. Matches the 0.25 resize snap.</summary>
    [Export] public float HeightStep { get; set; } = 0.25f;
}
