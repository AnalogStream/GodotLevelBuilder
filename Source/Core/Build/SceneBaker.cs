using System.Linq;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Primitives;

namespace LevelBuilder.Core.Build;

/// <summary>
/// Bakes a LevelDocument into a runtime Node3D tree (and a PackedScene/.tscn).
/// Node names are derived from stable IDs so consumer material overrides survive
/// rebake. Library materials are written onto the mesh SURFACE, leaving
/// surface_material_override free for the consuming game. See docs/EXPORT.md.
///
/// Milestone 1 keeps one MeshInstance3D per primitive instance; merge-by-material
/// is a later optimisation (docs/ROADMAP.md M7).
/// </summary>
public sealed class SceneBaker
{
    private readonly PrimitiveRegistry _registry;

    public SceneBaker(PrimitiveRegistry registry) => _registry = registry;

    /// <summary>Builds the node tree. Caller owns the returned root (free or add to a tree).</summary>
    public Node3D Bake(LevelDocument doc)
    {
        var ctx = new BuildContext { Materials = doc.Materials, CellSize = doc.Grid.CellSize };
        var materials = new MaterialResolver();

        var root = new Node3D { Name = SanitizeName(doc.Name) };

        foreach (StoreyData storey in doc.Storeys)
        {
            var storeyNode = new Node3D
            {
                Name = $"Storey_{storey.Id}",
                Position = new Vector3(0, storey.BaseElevation, 0),
            };
            root.AddChild(storeyNode);

            var body = new StaticBody3D { Name = $"Collision_{storey.Id}" };
            storeyNode.AddChild(body);

            ctx = new BuildContext
            {
                Materials = doc.Materials,
                CellSize = doc.Grid.CellSize,
                StoreyHeight = storey.Height,
            };

            // Deterministic emission order regardless of in-memory ordering.
            foreach (PrimitiveInstanceData inst in storey.Instances.OrderBy(i => i.Id))
            {
                IPrimitive prim = _registry.Get(inst.PrimitiveType);
                if (prim == null)
                {
                    GD.PushWarning($"SceneBaker: unknown primitive '{inst.PrimitiveType}' (instance {inst.Id}) — skipped.");
                    continue;
                }

                ArrayMesh mesh = prim.BuildMesh(inst, ctx);
                materials.AssignSurfaceMaterials(mesh, prim, inst, doc.Materials);

                var mi = new MeshInstance3D
                {
                    Name = $"Mesh_{inst.Id}",
                    Mesh = mesh,
                    Transform = inst.LocalTransform,
                };
                storeyNode.AddChild(mi);

                Shape3D[] shapes = prim.BuildCollision(inst, ctx);
                for (int s = 0; s < shapes.Length; s++)
                {
                    var cs = new CollisionShape3D
                    {
                        Name = $"Shape_{inst.Id}_{s}",
                        Shape = shapes[s],
                        Transform = inst.LocalTransform,
                    };
                    body.AddChild(cs);
                }
            }
        }

        return root;
    }

    /// <summary>Bakes and writes a .tscn to <paramref name="path"/>. Returns the save Error.</summary>
    public Error BakeToFile(LevelDocument doc, string path)
    {
        Node3D root = Bake(doc);
        SetOwnerRecursive(root, root);

        var packed = new PackedScene();
        Error packErr = packed.Pack(root);
        if (packErr != Error.Ok)
        {
            root.QueueFree();
            return packErr;
        }

        Error saveErr = ResourceSaver.Save(packed, path);
        root.QueueFree();
        return saveErr;
    }

    private static void SetOwnerRecursive(Node node, Node owner)
    {
        foreach (Node child in node.GetChildren())
        {
            child.Owner = owner;
            SetOwnerRecursive(child, owner);
        }
    }

    /// <summary>Godot node names disallow . : @ / %. Keep IDs/ASCII; replace the rest.</summary>
    private static string SanitizeName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Level";
        var chars = raw.Select(c => ".:@/%".IndexOf(c) >= 0 ? '_' : c).ToArray();
        return new string(chars);
    }
}
