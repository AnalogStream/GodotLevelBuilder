using Godot;
using Godot.Collections;

namespace LevelBuilder.Core.Data;

/// <summary>The level's palette of named materials. Slots reference entries by Id.</summary>
[GlobalClass]
public partial class MaterialLibrary : Resource
{
    [Export] public Array<MaterialEntry> Entries { get; set; } = new();

    public MaterialEntry Find(string id)
    {
        foreach (MaterialEntry e in Entries)
        {
            if (e.Id == id) return e;
        }
        return null;
    }
}
