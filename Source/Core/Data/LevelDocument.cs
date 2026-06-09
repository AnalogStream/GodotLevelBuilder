using Godot;
using Godot.Collections;
using LevelBuilder.Core;

namespace LevelBuilder.Core.Data;

/// <summary>
/// The whole editable level/building. The authoritative state: the viewport mesh
/// and the baked .tscn are both derived from this. Serialized to a single .tres.
/// </summary>
[GlobalClass]
public partial class LevelDocument : Resource
{
    /// <summary>Bumped on breaking changes; load runs migrations when behind.</summary>
    [Export] public string SchemaVersion { get; set; } = "1";
    [Export] public string Name { get; set; } = "Untitled";
    [Export] public GridSettings Grid { get; set; } = new();
    [Export] public MaterialLibrary Materials { get; set; } = new();
    /// <summary>
    /// Elevation layers. Each <see cref="StoreyData"/> is an elevation bookmark (its
    /// <see cref="StoreyData.BaseElevation"/> is where its geometry sits); they're created lazily when
    /// you place something at a new height and disappear if emptied by undo. Not building "floors".
    /// </summary>
    [Export] public Array<StoreyData> Storeys { get; set; } = new();

    /// <summary>Default wall/ramp/stairs height for freshly drawn primitives + new layers, metres.</summary>
    [Export] public float DefaultStoreyHeight { get; set; } = 3.0f;

    private const float ElevationEpsilon = 0.001f;

    /// <summary>The layer whose base sits at <paramref name="elevation"/> (within a tolerance), or null.</summary>
    public StoreyData StoreyAt(float elevation)
    {
        foreach (StoreyData s in Storeys)
            if (Mathf.Abs(s.BaseElevation - elevation) < ElevationEpsilon) return s;
        return null;
    }

    /// <summary>A blank document with one ground storey and the seeded placeholder material library.
    /// The single factory for "New Level" (Main bootstrap and the Project tab both route here).</summary>
    public static LevelDocument CreateEmpty(string name = "Untitled")
    {
        var doc = new LevelDocument { Name = name };
        DefaultMaterials.Seed(doc.Materials);
        doc.Storeys.Add(new StoreyData
        {
            Id = Ids.New(),
            Name = "Ground Floor",
            BaseElevation = 0f,
            Height = 3f,
        });
        return doc;
    }
}
