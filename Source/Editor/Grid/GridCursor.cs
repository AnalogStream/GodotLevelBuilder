using Godot;
using LevelBuilder.Core.Geometry;
using LevelBuilder.Core.Grid;

namespace LevelBuilder.Editor.Grid;

/// <summary>Whether the cursor snaps to whole cells (faces) or to grid corners (intersections).</summary>
public enum GridSnapMode { Cell, Corner }

/// <summary>
/// Highlights what's under the mouse on the editing plane (y = <see cref="Elevation"/>):
///   • Cell mode   → the 1-cell face under the cursor (for floors).
///   • Corner mode → the nearest grid intersection (for walls and edge-aligned things).
/// Tab toggles the mode for now; drawing tools will set it programmatically later.
/// Exposes <see cref="HoveredCell"/> / <see cref="HoveredCorner"/> for those tools.
/// </summary>
public partial class GridCursor : Node3D
{
    [Export] public float CellSize { get; set; } = 1.0f;
    [Export] public float Elevation { get; set; } = 0.0f;
    [Export] public GridSnapMode Mode { get; set; } = GridSnapMode.Cell;

    private const float CornerMarkerHalf = 0.12f;

    private readonly Snapper _snapper = new();
    private MeshInstance3D _cellHighlight;
    private MeshInstance3D _cornerHighlight;

    /// <summary>Lower-corner of the hovered cell in Cell mode; null on a miss or in Corner mode.</summary>
    public Vector3? HoveredCell { get; private set; }
    /// <summary>The hovered grid intersection in Corner mode; null on a miss or in Cell mode.</summary>
    public Vector3? HoveredCorner { get; private set; }

    public override void _Ready()
    {
        _snapper.CellSize = CellSize; // Subdivisions = 1 → snap to whole-cell corners

        _cellHighlight = MakeHighlight("CellHighlight",
            CornerCenteredQuad(0f, CellSize), new Color(0.35f, 0.70f, 1.0f, 0.28f));
        _cornerHighlight = MakeHighlight("CornerHighlight",
            CornerCenteredQuad(-CornerMarkerHalf, CornerMarkerHalf * 2f), new Color(1.0f, 0.80f, 0.25f, 0.9f));

        AddChild(_cellHighlight);
        AddChild(_cornerHighlight);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Tab)
        {
            Mode = Mode == GridSnapMode.Cell ? GridSnapMode.Corner : GridSnapMode.Cell;
            GD.Print($"[cursor] snap mode: {Mode}");
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        Camera3D cam = GetViewport().GetCamera3D();
        if (cam == null) { Miss(); return; }

        Vector2 mouse = GetViewport().GetMousePosition();
        Vector3 origin = cam.ProjectRayOrigin(mouse);
        Vector3 dir = cam.ProjectRayNormal(mouse);
        if (!GridPlane.RayToPlane(origin, dir, Elevation, out Vector3 hit)) { Miss(); return; }

        if (Mode == GridSnapMode.Cell)
        {
            Vector3 cell = _snapper.CellOrigin(hit);
            HoveredCell = cell;
            HoveredCorner = null;
            Show(_cellHighlight, cell);
            _cornerHighlight.Visible = false;
        }
        else
        {
            Vector3 corner = _snapper.Snap(hit);
            HoveredCorner = corner;
            HoveredCell = null;
            Show(_cornerHighlight, corner);
            _cellHighlight.Visible = false;
        }
    }

    private void Show(MeshInstance3D node, Vector3 at)
    {
        node.Position = new Vector3(at.X, Elevation + 0.01f, at.Z); // lift to avoid z-fighting
        node.Visible = true;
    }

    private void Miss()
    {
        HoveredCell = null;
        HoveredCorner = null;
        if (_cellHighlight != null) _cellHighlight.Visible = false;
        if (_cornerHighlight != null) _cornerHighlight.Visible = false;
    }

    private static MeshInstance3D MakeHighlight(string name, ArrayMesh mesh, Color color) => new()
    {
        Name = name,
        Mesh = mesh,
        Visible = false,
        MaterialOverride = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = color,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        },
    };

    /// <summary>A flat XZ quad with its lower corner at (<paramref name="origin"/>, <paramref name="origin"/>).</summary>
    private static ArrayMesh CornerCenteredQuad(float origin, float size)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        MeshBuilder.AddQuad(st,
            new Vector3(origin, 0, origin), new Vector3(origin, 0, origin + size),
            new Vector3(origin + size, 0, origin + size), new Vector3(origin + size, 0, origin),
            Vector3.Up);
        var mesh = new ArrayMesh();
        st.Commit(mesh);
        return mesh;
    }
}
