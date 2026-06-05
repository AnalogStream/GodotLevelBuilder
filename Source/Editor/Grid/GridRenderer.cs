using Godot;

namespace LevelBuilder.Editor.Grid;

/// <summary>
/// Draws the editor reference grid on the XZ plane (like Godot's 3D editor grid):
/// minor lines every <see cref="CellSize"/>, brighter major lines every
/// <see cref="MajorEvery"/> cells, and coloured X/Z axis lines through the origin.
/// Set this node's Y position to the active storey's elevation.
///
/// M2.1: a finite vertex-coloured line mesh. A shader-based infinite/fading grid
/// is a later upgrade (see docs/ROADMAP.md).
/// </summary>
public partial class GridRenderer : Node3D
{
    [Export] public float CellSize { get; set; } = 1.0f;
    /// <summary>Number of cells from the origin in each direction.</summary>
    [Export] public int Extent { get; set; } = 50;
    /// <summary>A major (brighter) line is drawn every N cells.</summary>
    [Export] public int MajorEvery { get; set; } = 10;

    private static readonly Color MinorColor = new(0.30f, 0.30f, 0.33f, 0.35f);
    private static readonly Color MajorColor = new(0.55f, 0.55f, 0.60f, 0.55f);
    private static readonly Color XAxisColor = new(0.80f, 0.30f, 0.32f, 0.85f);
    private static readonly Color ZAxisColor = new(0.30f, 0.45f, 0.82f, 0.85f);

    private MeshInstance3D _lines;

    public override void _Ready() => Rebuild();

    public void Rebuild()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Lines);

        float max = Extent * CellSize;
        for (int i = -Extent; i <= Extent; i++)
        {
            float p = i * CellSize;
            Color baseColor = (i % MajorEvery == 0) ? MajorColor : MinorColor;

            // Line at x = p, running along Z. At x = 0 this is the Z axis.
            Color zLine = (i == 0) ? ZAxisColor : baseColor;
            AddLine(st, new Vector3(p, 0, -max), new Vector3(p, 0, max), zLine);

            // Line at z = p, running along X. At z = 0 this is the X axis.
            Color xLine = (i == 0) ? XAxisColor : baseColor;
            AddLine(st, new Vector3(-max, 0, p), new Vector3(max, 0, p), xLine);
        }

        var mesh = new ArrayMesh();
        st.Commit(mesh);

        if (_lines == null)
        {
            _lines = new MeshInstance3D { Name = "GridLines", MaterialOverride = MakeMaterial() };
            AddChild(_lines);
        }
        _lines.Mesh = mesh;
    }

    private static void AddLine(SurfaceTool st, Vector3 a, Vector3 b, Color color)
    {
        st.SetColor(color);
        st.AddVertex(a);
        st.SetColor(color);
        st.AddVertex(b);
    }

    private static StandardMaterial3D MakeMaterial() => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
    };
}
