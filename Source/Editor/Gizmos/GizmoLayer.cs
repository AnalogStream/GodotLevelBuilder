using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Renders the current selection's resize handles: a small widget cube plus a pick collider per
/// handle, the colliders on a dedicated physics layer so the picker can prefer them over bodies.
/// Rebuilt (via EditorContext.Refresh) on selection change, after edits, and every live-drag frame
/// so the widgets track the geometry as it resizes. The dragged handle is held by SelectTool, so
/// these per-frame rebuilds don't disturb it.
/// </summary>
public partial class GizmoLayer : Node3D
{
    /// <summary>Physics layer the handle colliders live on (distinct from bodies on layer 1).</summary>
    public const uint HandleLayer = 2;

    private const float WidgetSize = 0.16f;
    private const float GrabSize = 0.30f;

    public void Rebuild(IReadOnlyList<IEditHandle> handles)
    {
        foreach (Node child in GetChildren())
            child.QueueFree();

        for (int i = 0; i < handles.Count; i++)
        {
            var xform = new Transform3D(Basis.Identity, handles[i].Anchor);
            var styled = handles[i] as IStyledHandle;
            float scale = styled?.WidgetScale ?? 1f;

            AddChild(new MeshInstance3D
            {
                Mesh = MeshBuilder.Box(Vector3.One * WidgetSize * scale),
                Transform = xform,
                MaterialOverride = WidgetMaterial(styled?.WidgetColor),
            });

            var body = new StaticBody3D
            {
                Transform = xform,
                CollisionLayer = HandleLayer, // detectable by the handle pass
                CollisionMask = 0,            // handles detect nothing themselves
            };
            body.SetMeta("handleIndex", i);
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One * GrabSize } });
            AddChild(body);
        }
    }

    private static StandardMaterial3D WidgetMaterial(Color? color) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = color ?? new Color(0.3f, 0.85f, 1.0f),
        // Draw on top: opening edge handles sit between the placeholder and the intact wall, and the
        // far-side instance handles sit behind the body — both opaque, so without this they're buried.
        NoDepthTest = true,
    };
}
