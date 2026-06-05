using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Primitives;

namespace LevelBuilder.Editor.View;

/// <summary>
/// Live preview of the document: per primitive instance, a visible MeshInstance3D plus a
/// StaticBody3D pick collider tagged with the instance Id (for selection). Regenerated from
/// the same BuildMesh the baker uses. M2 rebuilds everything on any change (tiny levels).
/// Materials are not applied yet (M5); the selected instance gets a highlight override.
/// </summary>
public partial class LevelView : Node3D
{
    private LevelDocument _doc;
    private PrimitiveRegistry _registry;
    private string _selectedId;

    public void Setup(LevelDocument doc, PrimitiveRegistry registry)
    {
        _doc = doc;
        _registry = registry;
    }

    public void SetSelected(string instanceId)
    {
        _selectedId = instanceId;
        Rebuild();
    }

    public void Rebuild()
    {
        foreach (Node child in GetChildren())
            child.QueueFree();

        foreach (StoreyData storey in _doc.Storeys)
        {
            var ctx = new BuildContext
            {
                Materials = _doc.Materials,
                CellSize = _doc.Grid.CellSize,
                StoreyHeight = storey.Height,
            };
            var offset = new Vector3(0, storey.BaseElevation, 0);

            foreach (PrimitiveInstanceData inst in storey.Instances)
            {
                IPrimitive prim = _registry.Get(inst.PrimitiveType);
                if (prim == null) continue;

                Transform3D xform = inst.LocalTransform;
                xform.Origin += offset;

                AddChild(new MeshInstance3D
                {
                    Mesh = prim.BuildMesh(inst, ctx),
                    Transform = xform,
                    MaterialOverride = inst.Id == _selectedId ? HighlightMaterial() : null,
                });

                AddChild(BuildPickBody(inst, prim, ctx, xform));
            }
        }
    }

    private static StaticBody3D BuildPickBody(PrimitiveInstanceData inst, IPrimitive prim, BuildContext ctx, Transform3D xform)
    {
        var body = new StaticBody3D { Transform = xform };
        body.SetMeta("instanceId", inst.Id);
        foreach (Shape3D shape in prim.BuildCollision(inst, ctx))
            body.AddChild(new CollisionShape3D { Shape = shape });
        return body;
    }

    private static StandardMaterial3D HighlightMaterial() => new()
    {
        AlbedoColor = new Color(1.0f, 0.62f, 0.22f),
        EmissionEnabled = true,
        Emission = new Color(0.85f, 0.45f, 0.12f),
        EmissionEnergyMultiplier = 0.5f,
    };
}
