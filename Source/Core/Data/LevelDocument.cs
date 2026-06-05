using Godot;
using Godot.Collections;

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
}
