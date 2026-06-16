using Godot;
using LevelBuilder.Editor.Session;

namespace LevelBuilder.UI;

/// <summary>
/// Transient toast notifications, bottom-right over the whole app. Subscribed to
/// <see cref="EditorContext.Notified"/> by Main — save/bake/export results and warnings show here
/// instead of console-only. MouseFilter Ignore everywhere so toasts never block clicks beneath.
/// </summary>
public partial class ToastLayer : Control
{
    private const int MaxVisible = 5;
    private const float HoldSeconds = 3.0f;
    private const float FadeSeconds = 0.6f;

    private VBoxContainer _stack;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _stack = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.End,
        };
        _stack.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
        _stack.GrowHorizontal = GrowDirection.Begin; // grow leftwards from the right edge
        _stack.GrowVertical = GrowDirection.Begin;   // grow upwards from the bottom
        _stack.Position -= new Vector2(16, 40);      // clear of the status bar
        AddChild(_stack);
    }

    public void Show(NotifyLevel level, string message)
    {
        // Cap the stack: drop the oldest when full.
        if (_stack.GetChildCount() >= MaxVisible)
            _stack.GetChild(0).QueueFree();

        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat { BgColor = ColorFor(level) };
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(10);
        panel.AddThemeStyleboxOverride("panel", style);

        panel.AddChild(new Label
        {
            Text = message,
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(240, 0),
        });
        _stack.AddChild(panel);

        Tween tween = panel.CreateTween();
        tween.TweenInterval(HoldSeconds);
        tween.TweenProperty(panel, "modulate:a", 0f, FadeSeconds);
        tween.TweenCallback(Callable.From(panel.QueueFree));
    }

    private static Color ColorFor(NotifyLevel level) => level switch
    {
        NotifyLevel.Success => UiConstants.ToastSuccess,
        NotifyLevel.Warning => UiConstants.ToastWarning,
        NotifyLevel.Error => UiConstants.ToastError,
        _ => UiConstants.ToastInfo,
    };
}
