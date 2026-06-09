using Godot;
using LevelBuilder.Editor.Session;

namespace LevelBuilder.UI;

/// <summary>
/// A small overlay in the top-left of the 3D view showing the current draw-plane elevation, with
/// fine ▲/▼ nudge buttons, a Blender-style drag-scrub on the readout, and a "Step" field for the
/// nudge/scrub increment. Drives <see cref="EditorContext.SetDrawHeight"/> — view state only, never
/// undoable. Keyboard +/- still switches whole storeys; this is the sub-storey fine control.
///
/// Added as a Control child of the SubViewportContainer (same overlay pattern as
/// <see cref="ViewportDropOverlay"/>); only its own small rect captures the mouse.
/// </summary>
public partial class HeightIndicatorPanel : PanelContainer
{
    private const int PixelsPerStep = 12; // vertical drag distance that advances one height step

    private EditorContext _ctx;
    private Label _readout;
    private Label _storey;
    private SpinBox _step;
    private bool _suppress;

    private bool _scrubbing;
    private float _scrubAccumY;

    public void Setup(EditorContext ctx)
    {
        _ctx = ctx;

        // Top-left corner overlay; the rest of the viewport stays free for orbit/draw.
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        Position = new Vector2(12, 12);

        var vbox = new VBoxContainer();
        AddChild(vbox);

        // Height row: ▼  [readout]  ▲
        var row = new HBoxContainer();
        vbox.AddChild(row);

        var down = new Button { Text = "▼", FocusMode = Control.FocusModeEnum.None, TooltipText = "Lower draw height one step" };
        down.Pressed += () => _ctx.NudgeDrawHeight(-1);
        row.AddChild(down);

        _readout = new Label
        {
            Text = "0.00 m",
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(84, 0),
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.Vsize,
            TooltipText = "Drag up/down to change draw height",
        };
        _readout.GuiInput += OnReadoutGuiInput;
        row.AddChild(_readout);

        var up = new Button { Text = "▲", FocusMode = Control.FocusModeEnum.None, TooltipText = "Raise draw height one step" };
        up.Pressed += () => _ctx.NudgeDrawHeight(+1);
        row.AddChild(up);

        // Step row: the nudge/scrub increment (persists in the document's GridSettings).
        var stepRow = new HBoxContainer();
        vbox.AddChild(stepRow);
        stepRow.AddChild(new Label { Text = "Step" });
        _step = new SpinBox
        {
            MinValue = 0.05, MaxValue = 10, Step = 0.05,
            Value = ctx.Document.Grid.HeightStep,
            CustomMinimumSize = new Vector2(72, 0),
            Suffix = "m",
        };
        _step.ValueChanged += OnStepChanged;
        stepRow.AddChild(_step);

        _storey = new Label { Modulate = new Color(1, 1, 1, 0.6f) };
        vbox.AddChild(_storey);

        ctx.Changed += Refresh; // storey switch / height change / doc swap all refire this
        Refresh();
    }

    /// <summary>Blender-style scrub: hold LMB on the readout and drag vertically (up = raise).</summary>
    private void OnReadoutGuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed) { _scrubbing = true; _scrubAccumY = 0f; }
            else _scrubbing = false;
            _readout.AcceptEvent();
        }
        else if (e is InputEventMouseMotion mm && _scrubbing)
        {
            _scrubAccumY += mm.Relative.Y; // screen Y grows downward → dragging up lowers accum → raises height
            while (_scrubAccumY <= -PixelsPerStep) { _ctx.NudgeDrawHeight(+1); _scrubAccumY += PixelsPerStep; }
            while (_scrubAccumY >= PixelsPerStep) { _ctx.NudgeDrawHeight(-1); _scrubAccumY -= PixelsPerStep; }
            _readout.AcceptEvent();
        }
    }

    private void OnStepChanged(double value)
    {
        if (_suppress || _ctx == null) return;
        _ctx.Document.Grid.HeightStep = (float)value;
    }

    private void Refresh()
    {
        if (_ctx == null) return;
        _readout.Text = $"{_ctx.DrawHeight:0.00} m";

        // Name the layer at this height, or mark a fresh elevation (no layer until you place something).
        Core.Data.StoreyData layer = _ctx.Storey;
        _storey.Text = layer != null ? layer.Name : "— new layer —";

        // Keep the step field in sync after a document swap, without echoing back a command.
        _suppress = true;
        if (!Mathf.IsEqualApprox((float)_step.Value, _ctx.Document.Grid.HeightStep))
            _step.Value = _ctx.Document.Grid.HeightStep;
        _suppress = false;
    }
}
