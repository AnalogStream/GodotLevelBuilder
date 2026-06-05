using Godot;
using LevelBuilder.Core;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;
using LevelBuilder.Editor.Grid;

namespace LevelBuilder.Editor.Tools;

/// <summary>A door/window preset: the dimensions an OpeningTool stamps into a wall.</summary>
public readonly struct OpeningPreset
{
    public string Label { get; }
    public float Width { get; }
    public float Height { get; }
    public float Sill { get; }

    public OpeningPreset(string label, float width, float height, float sill)
    {
        Label = label;
        Width = width;
        Height = height;
        Sill = sill;
    }

    public static readonly OpeningPreset Door = new("Door", 0.9f, 2.1f, 0.0f);
    public static readonly OpeningPreset Window = new("Window", 1.2f, 1.2f, 0.9f);
}

/// <summary>
/// Places an opening (door or window) into the wall under the mouse. Hover a wall to preview
/// the hole snapped along its length; click to cut it (undoable). Uses wall picking, not the
/// grid cursor.
/// </summary>
public sealed class OpeningTool : DrawToolBase
{
    private const float SnapStep = 0.25f;
    private const float Margin = 0.05f;

    private readonly OpeningPreset _preset;

    public OpeningTool(OpeningPreset preset) => _preset = preset;

    public override string Name => _preset.Label;
    public override GridSnapMode SnapMode => GridSnapMode.Cell; // unused; cursor hidden
    public override bool UsesGridCursor => false;

    protected override void ResetState() { }

    public override void OnClick()
    {
        if (TryResolve(out PrimitiveInstanceData wall, out OpeningData opening, out _))
            Ctx.AddOpening(wall, opening);
    }

    public override void UpdatePreview()
    {
        if (TryResolve(out _, out _, out (ArrayMesh mesh, Transform3D xform) preview))
            ShowPreview(preview.mesh, preview.xform);
        else
            HidePreview();
    }

    /// <summary>Resolve the wall + opening + preview under the mouse, or false if not on a wall.</summary>
    private bool TryResolve(out PrimitiveInstanceData wall, out OpeningData opening, out (ArrayMesh, Transform3D) preview)
    {
        wall = null;
        opening = null;
        preview = default;

        Session.PickResult hit = Ctx.Picker.Pick();
        if (!hit.Hit) return false;

        wall = Ctx.GetInstance(hit.InstanceId);
        if (wall == null || wall.PrimitiveType != "wall") { wall = null; return false; }

        float length = GetF(wall, "length", 1f);
        float thickness = GetF(wall, "thickness", 0.2f);

        // World transform of the wall (storey-local + active storey elevation).
        var wallWorld = new Transform3D(wall.LocalTransform.Basis, wall.LocalTransform.Origin + Ctx.ElevationOffset);
        Vector3 local = wallWorld.AffineInverse() * hit.Position;

        float centerU = local.X + length * 0.5f;
        centerU = Mathf.Round(centerU / SnapStep) * SnapStep;
        float offset = centerU - _preset.Width * 0.5f;

        float maxOffset = length - _preset.Width - Margin;
        if (maxOffset < Margin) return false; // wall too short for this opening
        offset = Mathf.Clamp(offset, Margin, maxOffset);

        if (OverlapsExisting(wall, offset, _preset.Width)) { wall = null; return false; }

        opening = new OpeningData
        {
            Id = Ids.New(),
            Offset = offset,
            Width = _preset.Width,
            Height = _preset.Height,
            SillHeight = _preset.Sill,
            FrameType = "",
        };

        // Preview box at the opening volume (slightly thicker so it pokes through the wall).
        var size = new Vector3(_preset.Width, _preset.Height, thickness + 0.02f);
        float localX = offset + _preset.Width * 0.5f - length * 0.5f;
        float localY = _preset.Sill + _preset.Height * 0.5f;
        var localCenter = new Transform3D(Basis.Identity, new Vector3(localX, localY, 0));
        preview = (MeshBuilder.Box(size), wallWorld * localCenter);
        return true;
    }

    private static bool OverlapsExisting(PrimitiveInstanceData wall, float offset, float width)
    {
        float start = offset, end = offset + width;
        foreach (OpeningData ex in wall.Openings)
        {
            float exStart = ex.Offset - Margin, exEnd = ex.Offset + ex.Width + Margin;
            if (start < exEnd && end > exStart) return true;
        }
        return false;
    }

    private static float GetF(PrimitiveInstanceData d, string key, float def)
        => d.Parameters.ContainsKey(key) ? d.Parameters[key].AsSingle() : def;
}
