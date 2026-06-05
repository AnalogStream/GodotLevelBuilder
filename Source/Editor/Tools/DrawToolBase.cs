using Godot;
using LevelBuilder.Editor.Grid;
using LevelBuilder.Editor.Session;

namespace LevelBuilder.Editor.Tools;

/// <summary>Shared plumbing for draw tools: a single reusable translucent preview mesh.</summary>
public abstract class DrawToolBase : ITool
{
    protected EditorContext Ctx;
    private MeshInstance3D _preview;

    public abstract string Name { get; }
    public abstract GridSnapMode SnapMode { get; }
    public virtual bool UsesGridCursor => true;

    public virtual void Activate(EditorContext ctx)
    {
        Ctx = ctx;
        Ctx.Cursor.Mode = SnapMode;
        ResetState();
    }

    public virtual void Deactivate()
    {
        ResetState();
        HidePreview();
    }

    public abstract void OnClick();
    public abstract void UpdatePreview();

    public virtual void OnCancel()
    {
        ResetState();
        HidePreview();
    }

    /// <summary>Clear any in-progress draw state (e.g. the pending start point).</summary>
    protected abstract void ResetState();

    protected void ShowPreview(ArrayMesh mesh, Transform3D worldTransform)
    {
        if (_preview == null)
        {
            _preview = new MeshInstance3D { Name = "DrawPreview", MaterialOverride = MakeMaterial() };
            Ctx.PreviewLayer.AddChild(_preview);
        }
        _preview.Mesh = mesh;
        _preview.Transform = worldTransform;
        _preview.Visible = true;
    }

    protected void HidePreview()
    {
        if (_preview != null) _preview.Visible = false;
    }

    private static StandardMaterial3D MakeMaterial() => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        AlbedoColor = new Color(0.40f, 1.0f, 0.55f, 0.35f),
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };
}
