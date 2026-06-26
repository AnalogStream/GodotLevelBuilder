using Godot;
using Godot.Collections;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Draws the selected path_sweep's control polyline as a thin, semi-transparent overlay line (like
/// Godot's Path3D curve), so the path reads even where the swept geometry is thin or hidden. Purely
/// visual — point selection + dragging live on the handle widgets. Rebuilt from
/// <c>EditorContext.Refresh</c>: <see cref="Show"/> when a single path is selected, <see cref="Clear"/>
/// otherwise. Drawn on top (no depth test) so it stays visible through the meshes it traces.
/// </summary>
public partial class PathOverlay : Node3D
{
    private static readonly Color LineColor = new(0.3f, 1.0f, 0.85f, 0.55f);

    private MeshInstance3D _line;
    private ImmediateMesh _mesh;

    public override void _Ready()
    {
        _mesh = new ImmediateMesh();
        _line = new MeshInstance3D
        {
            Mesh = _mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            GIMode = GeometryInstance3D.GIModeEnum.Disabled,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = LineColor,
                NoDepthTest = true,
            },
        };
        AddChild(_line);
    }

    /// <summary>Draws the polyline through <paramref name="points"/> (local space) offset to world by
    /// <paramref name="offset"/>; closes the loop back to the first point when <paramref name="closed"/>.</summary>
    public void Show(Array<Vector3> points, Vector3 offset, bool closed)
    {
        if (_mesh == null) return;
        _mesh.ClearSurfaces();
        AddRing(points, offset, closed);
    }

    /// <summary>Draws several rings (outline + holes), each as its OWN line strip — separate surfaces so no
    /// spurious segment connects the outline to a hole.</summary>
    public void ShowMany(System.Collections.Generic.IEnumerable<(System.Collections.Generic.IReadOnlyList<Vector3> pts, bool closed)> rings, Vector3 offset)
    {
        if (_mesh == null) return;
        _mesh.ClearSurfaces();
        foreach ((System.Collections.Generic.IReadOnlyList<Vector3> pts, bool closed) in rings) AddRing(pts, offset, closed);
    }

    private void AddRing(System.Collections.Generic.IReadOnlyList<Vector3> points, Vector3 offset, bool closed)
    {
        if (points.Count < 2) return;
        _mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        foreach (Vector3 p in points) _mesh.SurfaceAddVertex(offset + p);
        if (closed && points.Count >= 3) _mesh.SurfaceAddVertex(offset + points[0]);
        _mesh.SurfaceEnd();
    }

    public void Clear() => _mesh?.ClearSurfaces();
}
