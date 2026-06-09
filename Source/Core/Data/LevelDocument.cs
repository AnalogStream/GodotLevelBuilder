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
    [Export] public Array<StoreyData> Storeys { get; set; } = new();

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
