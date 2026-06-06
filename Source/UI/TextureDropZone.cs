using System;
using Godot;

namespace LevelBuilder.UI;

/// <summary>
/// A framed slot that shows the current texture and accepts a dropped swatch. Reusable target —
/// the owner supplies what to do on a drop and whether dropping is currently allowed.
/// </summary>
public partial class TextureDropZone : PanelContainer
{
    private TextureRect _preview;
    private Label _caption;
    private Action<string> _onDrop;
    private Func<bool> _canDrop;

    public void Setup(Action<string> onDrop, Func<bool> canDrop)
    {
        _onDrop = onDrop;
        _canDrop = canDrop;
        CustomMinimumSize = new Vector2(0, 96);

        // Children must be transparent to the mouse, else they intercept the drop and this
        // PanelContainer's _DropData never fires.
        var vbox = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(vbox);

        _preview = new TextureRect
        {
            CustomMinimumSize = new Vector2(72, 72),
            // IgnoreSize: otherwise the control sizes to the texture's native px and blows up the pane.
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _caption = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        vbox.AddChild(_preview);
        vbox.AddChild(_caption);
    }

    /// <summary>Show a texture + caption (texture may be null for "none").</summary>
    public void Display(Texture2D texture, string caption)
    {
        _preview.Texture = texture;
        _caption.Text = caption;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
        => (_canDrop?.Invoke() ?? true) && TextureDrag.TryParse(data, out _);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (TextureDrag.TryParse(data, out string path)) _onDrop?.Invoke(path);
    }
}
