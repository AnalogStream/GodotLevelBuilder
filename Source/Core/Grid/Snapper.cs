using Godot;

namespace LevelBuilder.Core.Grid;

/// <summary>
/// Snaps world positions to the grid. Sub-cell snapping is supported via
/// <see cref="Subdivisions"/> (e.g. 2 → snap to half-cells) without changing the
/// visible 1-cell grid. Y is left untouched (callers set elevation).
/// </summary>
public sealed class Snapper
{
    public float CellSize { get; set; } = 1.0f;
    public int Subdivisions { get; set; } = 1;

    public float Step => CellSize / Mathf.Max(1, Subdivisions);

    /// <summary>Nearest snap point (cell/sub-cell corner).</summary>
    public Vector3 Snap(Vector3 p)
    {
        float s = Step;
        return new Vector3(Mathf.Round(p.X / s) * s, p.Y, Mathf.Round(p.Z / s) * s);
    }

    /// <summary>Lower-corner (min X/Z) of the cell containing <paramref name="p"/>.</summary>
    public Vector3 CellOrigin(Vector3 p)
    {
        return new Vector3(Mathf.Floor(p.X / CellSize) * CellSize, p.Y, Mathf.Floor(p.Z / CellSize) * CellSize);
    }
}
