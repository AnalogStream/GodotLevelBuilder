using Godot;

namespace LevelBuilder.UI;

/// <summary>
/// Shared UI layout + palette constants. One place for the dock sizes, paddings and the accent
/// color so the panels and <see cref="UiTheme"/> stay consistent (no per-panel magic numbers).
/// </summary>
public static class UiConstants
{
    // ---- layout ------------------------------------------------------------
    public const int SceneTreeWidth = 240;
    public const int InspectorWidth = 260;
    public const int BottomDockHeight = 180;
    public const int Margin = 8;

    public static readonly Vector2 ButtonMin = new(96, 36);
    public static readonly Vector2 SmallButtonMin = new(28, 28);
    public static readonly Vector2I FileDialogSize = new(900, 600);

    // ---- palette (dark pro-tool) --------------------------------------------
    /// <summary>Single accent used for pressed/active/selected states across the app.</summary>
    public static readonly Color Accent = new(0.26f, 0.53f, 0.96f);

    public static readonly Color PanelBg = new(0.135f, 0.145f, 0.165f);
    public static readonly Color PanelBgDarker = new(0.105f, 0.115f, 0.13f);
    public static readonly Color ControlBg = new(0.185f, 0.20f, 0.225f);
    public static readonly Color ControlBgHover = new(0.235f, 0.25f, 0.28f);
    public static readonly Color BorderColor = new(0.30f, 0.32f, 0.36f);
    public static readonly Color FontColor = new(0.88f, 0.89f, 0.91f);
    public static readonly Color FontDim = new(1, 1, 1, 0.6f);

    // ---- toast colors --------------------------------------------------------
    public static readonly Color ToastInfo = new(0.25f, 0.28f, 0.33f);
    public static readonly Color ToastSuccess = new(0.16f, 0.38f, 0.22f);
    public static readonly Color ToastWarning = new(0.45f, 0.35f, 0.10f);
    public static readonly Color ToastError = new(0.48f, 0.16f, 0.16f);
}
