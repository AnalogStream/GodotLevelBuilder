using System;
using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Primitives;
using LevelBuilder.Editor.Commands;
using LevelBuilder.Editor.Session;

namespace LevelBuilder.UI;

/// <summary>
/// Right-hand properties dock for the selected object or opening. Shows identity, a Texture drop
/// target (instances only), and an editable list of the selection's parameters — the primitive's
/// ParamSpecs for an instance, or offset/width/height/sill for an opening. Every field edit routes
/// through the command stack (<see cref="SetParameterCommand"/> / <see cref="EditOpeningCommand"/>)
/// so it undoes like any other edit.
///
/// Reacts to <see cref="EditorContext.Changed"/>. The property rows are rebuilt only when the
/// SELECTION changes (keyed on id|openingId); a same-selection Changed — including the re-entrant one
/// fired by a field's own edit, and every live gizmo-drag frame — just pushes current values back into
/// the existing controls. Rebuilding on a same-selection refresh would QueueFree the very SpinBox whose
/// signal we're still inside. All programmatic value writes run under <see cref="_suppress"/> so they
/// don't echo back as fresh commands.
/// </summary>
public partial class InspectorPanel : PanelContainer
{
    private EditorContext _ctx;
    private Label _title;
    private Label _details;
    private TextureDropZone _texture;
    private HBoxContainer _tilingRow;
    private SpinBox _tilingSpin;
    private HBoxContainer _tintRow;
    private ColorPickerButton _tintPicker;
    private Label _propsHeader;
    private VBoxContainer _propsBox;

    /// <summary>Pushes the live data value into each control (SpinBox); rebuilt with the rows.</summary>
    private readonly List<Action> _syncers = new();
    private string _shownKey = "\0"; // sentinel: differs from any real selection so the first Refresh builds
    private bool _suppress;           // true while we write controls programmatically — ignore their signals

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

        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        margin.AddChild(body);

        _title = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _title.AddThemeFontSizeOverride("font_size", 16);
        body.AddChild(_title);

        // Autowrap so long help text ("Select an object…") wraps within the fixed 260px dock
        // instead of forcing the PanelContainer wider. Without this the panel width swings between
        // selected (short "id …") and empty (long sentence) states, resizing the SubViewport and
        // visibly reframing the camera on every select/deselect.
        _details = new Label { Modulate = new Color(1, 1, 1, 0.7f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        body.AddChild(_details);

        body.AddChild(new HSeparator());
        body.AddChild(new Label { Text = "Texture", Modulate = new Color(1, 1, 1, 0.6f) });

        _texture = new TextureDropZone();
        body.AddChild(_texture);
        _texture.Setup(OnDropTexture, CanDropTexture);

        // Per-texture render properties (shared across every instance using this texture). Shown only
        // for texture-built entries; a loaded .material's settings live in its own resource. Synced in
        // UpdateTextureProps under _suppress so programmatic writes don't echo as edits.
        _tilingSpin = new SpinBox
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            // Godot snaps Range values to min + n*step, so the floor must be a multiple of step or
            // round numbers (0.5, 1.0) become unreachable — with min 0.05/step 0.25 they snapped to
            // 0.55/1.05. Floor 0.05 IS a multiple of step 0.05, so the grid is every 0.05.
            MinValue = 0.05, MaxValue = 64, Step = 0.05, Value = 1,
            TooltipText = "Texture tiling: tiles per metre (higher = smaller, more-repeated tiles).",
        };
        _tilingSpin.ValueChanged += OnTiling;
        _tilingRow = MakeRow("Tiling", _tilingSpin);
        body.AddChild(_tilingRow);

        _tintPicker = new ColorPickerButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 24),
            FocusMode = FocusModeEnum.None, // don't let it swallow tool hotkeys
            Color = Colors.White,
        };
        _tintPicker.ColorChanged += OnTint;
        _tintRow = MakeRow("Tint", _tintPicker);
        body.AddChild(_tintRow);

        body.AddChild(new HSeparator());
        _propsHeader = new Label { Text = "Properties", Modulate = new Color(1, 1, 1, 0.6f) };
        body.AddChild(_propsHeader);

        _propsBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(_propsBox);

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

    // ---- refresh ---------------------------------------------------------

    private void Refresh()
    {
        string key = $"{_ctx.SelectedId}|{_ctx.SelectedOpeningId}";

        _suppress = true; // building rows + syncing values must not re-emit as edits
        if (key != _shownKey)
        {
            RebuildProps();
            _shownKey = key;
        }
        foreach (Action sync in _syncers) sync();
        UpdateIdentity();
        UpdateTextureProps();
        _suppress = false;
    }

    /// <summary>Title / id / texture swatch — derived from the current selection, no controls rebuilt.</summary>
    private void UpdateIdentity()
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
            OpeningData o = SelectedOpening();
            _title.Text = o != null && o.SillHeight > 0 ? "Window" : "Opening";
            _details.Text = o != null ? $"id {Short(o.Id)}" : "";
            _texture.Display(null, "—"); // texturing openings isn't supported yet
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

    // ---- property rows ---------------------------------------------------

    private void RebuildProps()
    {
        _syncers.Clear();
        foreach (Node child in _propsBox.GetChildren()) child.QueueFree();

        if (_ctx.SelectedId == null) { _propsHeader.Visible = false; return; }

        if (_ctx.SelectedOpeningId != null) { BuildOpeningRows(); return; }

        PrimitiveInstanceData inst = _ctx.GetInstance(_ctx.SelectedId);
        IPrimitive prim = inst != null ? _ctx.Registry.Get(inst.PrimitiveType) : null;
        if (prim == null) { _propsHeader.Visible = false; return; }

        _propsHeader.Visible = prim.Parameters.Count > 0;
        foreach (ParamSpec spec in prim.Parameters) BuildInstanceRow(spec);
    }

    private void BuildInstanceRow(ParamSpec spec)
    {
        if (spec.Type != ParamType.Float && spec.Type != ParamType.Int)
        {
            // No editor for Bool/String yet (no primitive declares one) — show read-only.
            _propsBox.AddChild(new Label { Text = $"{spec.Label}: {ReadInstanceValue(spec)}" });
            return;
        }

        bool isInt = spec.Type == ParamType.Int;
        SpinBox sb = BuildSpin(spec.Min, spec.Max, isInt);
        AddRow(spec.Label, sb);

        ParamSpec s = spec; // capture per-iteration
        sb.ValueChanged += v => OnInstanceParam(s, v);
        _syncers.Add(() => sb.Value = ReadInstanceValue(s));
    }

    private void OnInstanceParam(ParamSpec spec, double v)
    {
        if (_suppress) return;
        PrimitiveInstanceData inst = _ctx.GetInstance(_ctx.SelectedId);
        if (inst == null) return;

        Variant from = inst.Parameters.ContainsKey(spec.Key) ? inst.Parameters[spec.Key] : spec.Default;
        Variant to = spec.Type == ParamType.Int ? (Variant)Mathf.RoundToInt(v) : (Variant)v;
        if (Mathf.Abs(from.AsDouble() - to.AsDouble()) < 1e-9) return; // no-op: don't log an empty undo

        _ctx.Commands.Execute(new SetParameterCommand(inst, spec.Key, from, to, _ctx.Refresh));
    }

    private double ReadInstanceValue(ParamSpec spec)
    {
        PrimitiveInstanceData inst = _ctx.GetInstance(_ctx.SelectedId);
        if (inst != null && inst.Parameters.ContainsKey(spec.Key)) return inst.Parameters[spec.Key].AsDouble();
        return spec.Default.AsDouble();
    }

    // ---- opening rows ----------------------------------------------------

    private enum OField { Offset, Width, Height, Sill }

    private void BuildOpeningRows()
    {
        _propsHeader.Visible = true;
        OpeningRow("Offset", OField.Offset, 0f);
        OpeningRow("Width", OField.Width, 0.01f);
        OpeningRow("Height", OField.Height, 0.01f);
        OpeningRow("Sill", OField.Sill, 0f);
    }

    private void OpeningRow(string label, OField which, float min)
    {
        SpinBox sb = BuildSpin(min, 100000f, isInt: false);
        AddRow(label, sb);
        sb.ValueChanged += v => OnOpeningParam(which, v);
        _syncers.Add(() => { OpeningData o = SelectedOpening(); if (o != null) sb.Value = Read(o, which); });
    }

    private void OnOpeningParam(OField which, double v)
    {
        if (_suppress) return;
        OpeningData o = SelectedOpening();
        if (o == null) return;

        OpeningState from = OpeningState.From(o);
        float f = (float)v;
        OpeningState to = which switch
        {
            OField.Offset => new OpeningState(f, from.Width, from.Height, from.Sill),
            OField.Width => new OpeningState(from.Offset, f, from.Height, from.Sill),
            OField.Height => new OpeningState(from.Offset, from.Width, f, from.Sill),
            OField.Sill => new OpeningState(from.Offset, from.Width, from.Height, f),
            _ => from,
        };
        if (to == from) return;

        _ctx.Commands.Execute(new EditOpeningCommand(o, from, to, _ctx.Refresh));
    }

    private static double Read(OpeningData o, OField which) => which switch
    {
        OField.Offset => o.Offset,
        OField.Width => o.Width,
        OField.Height => o.Height,
        OField.Sill => o.SillHeight,
        _ => 0,
    };

    private OpeningData SelectedOpening()
    {
        PrimitiveInstanceData wall = _ctx.GetInstance(_ctx.SelectedId);
        if (wall == null) return null;
        foreach (OpeningData o in wall.Openings)
            if (o.Id == _ctx.SelectedOpeningId) return o;
        return null;
    }

    // ---- widget helpers --------------------------------------------------

    /// <summary>SpinBox with finite bounds (ParamSpec min/max may be infinite — clamp to a sane range).</summary>
    private static SpinBox BuildSpin(float min, float max, bool isInt) => new()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        MinValue = float.IsInfinity(min) ? (isInt ? 0 : -100000) : min,
        MaxValue = float.IsInfinity(max) ? 100000 : max,
        Step = isInt ? 1 : 0.01,
        Rounded = isInt,
    };

    private void AddRow(string label, Control field) => _propsBox.AddChild(MakeRow(label, field));

    private static HBoxContainer MakeRow(string label, Control field)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(80, 0) });
        row.AddChild(field);
        return row;
    }

    // ---- texture properties ----------------------------------------------

    /// <summary>The library entry of the selection's primary slot, or null (no selection / opening / unset slot).</summary>
    private MaterialEntry CurrentEntry(out string id)
    {
        id = null;
        if (_ctx.SelectedId == null || _ctx.SelectedOpeningId != null) return null;
        PrimitiveInstanceData inst = _ctx.GetInstance(_ctx.SelectedId);
        if (inst == null) return null;
        IPrimitive prim = _ctx.Registry.Get(inst.PrimitiveType);
        if (prim == null || prim.MaterialSlots.Count == 0) return null;
        string slot = prim.MaterialSlots[0];
        if (!inst.MaterialSlots.ContainsKey(slot)) return null;
        id = inst.MaterialSlots[slot].AsString();
        return _ctx.Document.Materials.Find(id);
    }

    /// <summary>Show + sync the tiling/tint controls for a texture entry; hide them otherwise.</summary>
    private void UpdateTextureProps()
    {
        MaterialEntry entry = CurrentEntry(out _);
        bool show = entry != null && !string.IsNullOrEmpty(entry.TexturePath);
        _tilingRow.Visible = show;
        _tintRow.Visible = show;
        if (!show) return;

        _tilingSpin.Value = entry.UvScale <= 0 ? 1 : entry.UvScale;
        _tintPicker.Color = entry.Tint;
    }

    private void OnTiling(double v)
    {
        if (_suppress) return;
        MaterialEntry entry = CurrentEntry(out string id);
        if (entry == null) return;
        if (Mathf.Abs(entry.UvScale - v) < 1e-9) return;
        _ctx.EditMaterial(id, (float)v, entry.Tint);
    }

    private void OnTint(Color c)
    {
        if (_suppress) return;
        MaterialEntry entry = CurrentEntry(out string id);
        if (entry == null) return;
        if (entry.Tint == c) return;
        _ctx.EditMaterial(id, entry.UvScale, c);
    }

    // ---- texture (unchanged) ---------------------------------------------

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

        if (!string.IsNullOrEmpty(entry.TexturePath))
        {
            Texture2D t = TextureLoader.Load(entry.TexturePath); // raw-decode fallback, like the swatch
            if (t != null) return (t, entry.DisplayName);
        }

        return (null, entry.DisplayName); // a .material-based entry (e.g. a proto) — name only
    }

    private static string Short(string id)
        => string.IsNullOrEmpty(id) ? "?" : id.Length <= 6 ? id : id[^6..];
}
