using Godot;
using Godot.Collections;
using LevelBuilder.Core;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Grid;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Draws a half-pipe / U-channel: first click sets the entry (the channel's local origin), the second
/// the initial heading — the channel leaves the entry along start→end with horizontal length = the
/// distance. Curve, rise, radius, arc, thickness and tessellation take their defaults and are tuned
/// afterwards in the inspector. Like the ramp/curve tools, the heading is fixed at draw time (there is
/// no rotate gizmo); a freshly drawn channel is straight + flat (curve = rise = 0).
/// </summary>
public sealed class HalfPipeDrawTool : DrawToolBase
{
    private const float MinLength = 0.001f;

    public override string Name => "Half-Pipe";
    public override GridSnapMode SnapMode => GridSnapMode.Corner;

    private Vector3? _start;

    protected override void ResetState() => _start = null;

    public override void OnClick()
    {
        Vector3? corner = Ctx.Cursor.HoveredCorner;
        if (corner == null) return;
        if (_start == null) { _start = corner; return; }

        PrimitiveInstanceData pipe = BuildPipe(_start.Value, corner.Value);
        if (pipe != null) Ctx.AddInstance(pipe);
        _start = null;
        HidePreview();
    }

    public override void UpdatePreview()
    {
        if (_start == null) { HidePreview(); return; }
        if (Ctx.Cursor.HoveredCorner == null) return;

        PrimitiveInstanceData inst = BuildPipe(_start.Value, Ctx.Cursor.HoveredCorner.Value);
        if (inst == null) { HidePreview(); return; }

        Transform3D world = inst.LocalTransform;
        world.Origin += Ctx.ElevationOffset;
        ShowPreview(Ctx.Registry.Get("half_pipe").BuildMesh(inst, Ctx.BuildCtx()), world);
    }

    private PrimitiveInstanceData BuildPipe(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        d.Y = 0;
        float length = d.Length();
        if (length < MinLength) return null;

        float angle = Mathf.Atan2(-d.Z, d.X);     // rotate local +X onto the channel direction
        var basis = new Basis(Vector3.Up, angle);
        var origin = new Vector3(a.X, 0, a.Z);     // entry corner is the channel's local origin (path f=0)

        return new PrimitiveInstanceData
        {
            Id = Ids.New(),
            PrimitiveType = "half_pipe",
            LocalTransform = new Transform3D(basis, origin),
            Parameters = new Dictionary
            {
                { "length", (double)length },
                { "radius", 1.5 },
                { "arc", 180.0 },
                { "curve", 0.0 },
                { "rise", 0.0 },
                { "deck", false },     // banks off by default — toggle the Deck checkbox in the inspector
                { "deckWidth", 1.0 },  // platform width used when Deck is enabled
                { "thickness", 0.2 },
                { "sides", 12 },
                { "segments", 16 },
            },
        };
    }
}
