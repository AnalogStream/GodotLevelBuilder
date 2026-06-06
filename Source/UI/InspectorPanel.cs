using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Primitives;
using LevelBuilder.Editor.Session;

namespace LevelBuilder.UI;

/// <summary>
/// Right-hand properties dock for the selected object. For now it shows identity + the object's
/// Texture, which doubles as a drop target: drag a swatch from the Textures tab onto it (or onto
/// the object in the viewport) to paint every slot. Parameter editing (width/height/...) comes next.
///
/// Reacts to <see cref="EditorContext.Changed"/> so it follows selection and edits.
/// </summary>
public partial class InspectorPanel : PanelContainer
{
    private EditorContext _ctx;
    private Label _title;
    private Label _details;
    private TextureDropZone _texture;

    public void Setup(EditorContext ctx)
    {
        _ctx = ctx;
        CustomMinimumSize = new Vector2(260, 0);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        AddChild(margin);

        var body = new VBoxContainer();
        margin.AddChild(body);

        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 16);
        body.AddChild(_title);

        _details = new Label { Modulate = new Color(1, 1, 1, 0.7f) };
        body.AddChild(_details);

        body.AddChild(new HSeparator());
        body.AddChild(new Label { Text = "Texture", Modulate = new Color(1, 1, 1, 0.6f) });

        _texture = new TextureDropZone();
        body.AddChild(_texture);
        _texture.Setup(OnDropTexture, CanDropTexture);

        _ctx.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        if (_ctx != null) _ctx.Changed -= Refresh;
    }

    private bool CanDropTexture() => _ctx.SelectedId != null && _ctx.SelectedOpeningId == null;

    private void OnDropTexture(string texturePath)
    {
        if (CanDropTexture()) _ctx.AssignTextureToInstance(_ctx.SelectedId, texturePath);
    }

    private void Refresh()
    {
        if (_ctx.SelectedId == null)
        {
            _title.Text = "No selection";
            _details.Text = "Select an object to see its properties.";
            _texture.Display(null, "—");
            return;
        }

        if (_ctx.SelectedOpeningId != null)
        {
            _title.Text = "Opening";
            _details.Text = "Texturing openings isn't supported yet.";
            _texture.Display(null, "—");
            return;
        }

        PrimitiveInstanceData inst = _ctx.GetInstance(_ctx.SelectedId);
        if (inst == null) { _title.Text = "No selection"; _details.Text = ""; _texture.Display(null, "—"); return; }

        string type = string.IsNullOrEmpty(inst.PrimitiveType) ? "Object" : inst.PrimitiveType;
        _title.Text = $"{char.ToUpperInvariant(type[0])}{type[1..]}";
        _details.Text = $"id {Short(inst.Id)}";

        (Texture2D tex, string caption) = CurrentTexture(inst);
        _texture.Display(tex, caption);
    }

    /// <summary>Resolve the texture shown for an instance: its primary slot's library entry, if any.</summary>
    private (Texture2D, string) CurrentTexture(PrimitiveInstanceData inst)
    {
        IPrimitive prim = _ctx.Registry.Get(inst.PrimitiveType);
        if (prim == null || prim.MaterialSlots.Count == 0) return (null, "—");

        string slot = prim.MaterialSlots[0];
        if (!inst.MaterialSlots.ContainsKey(slot)) return (null, "Drag a texture here");

        string materialId = inst.MaterialSlots[slot].AsString();
        MaterialEntry entry = _ctx.Document.Materials.Find(materialId);
        if (entry == null) return (null, materialId);

        if (!string.IsNullOrEmpty(entry.TexturePath) && ResourceLoader.Exists(entry.TexturePath))
            return (GD.Load<Texture2D>(entry.TexturePath), entry.DisplayName);

        return (null, entry.DisplayName); // a .material-based entry (e.g. a proto) — name only
    }

    private static string Short(string id)
        => string.IsNullOrEmpty(id) ? "?" : id.Length <= 6 ? id : id[^6..];
}
