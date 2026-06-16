using Godot;

namespace LevelBuilder.UI;

/// <summary>
/// Builds the app-wide dark theme in code (no .tres — the whole UI is code-built, and a code
/// theme keeps the palette next to <see cref="UiConstants"/>). Applied once to the root Control
/// in <see cref="App.Main"/>; Godot propagates it down the entire Control branch. The 3D image
/// rendered by the SubViewport is untouched — Theme only affects Control drawing.
/// </summary>
public static class UiTheme
{
    public static Theme Build()
    {
        var theme = new Theme();

        // ---- shared styleboxes -------------------------------------------
        StyleBoxFlat panel = Flat(UiConstants.PanelBg);
        StyleBoxFlat panelDark = Flat(UiConstants.PanelBgDarker);

        StyleBoxFlat btnNormal = Button(UiConstants.ControlBg);
        StyleBoxFlat btnHover = Button(UiConstants.ControlBgHover);
        StyleBoxFlat btnPressed = Button(UiConstants.Accent with { A = 0.55f });
        btnPressed.BorderColor = UiConstants.Accent;
        StyleBoxFlat btnDisabled = Button(UiConstants.ControlBg with { A = 0.4f });

        // ---- Button (and toggle buttons in the palette) -------------------
        theme.SetStylebox("normal", "Button", btnNormal);
        theme.SetStylebox("hover", "Button", btnHover);
        theme.SetStylebox("pressed", "Button", btnPressed);
        theme.SetStylebox("hover_pressed", "Button", btnPressed);
        theme.SetStylebox("disabled", "Button", btnDisabled);
        theme.SetColor("font_color", "Button", UiConstants.FontColor);
        theme.SetColor("font_hover_color", "Button", Colors.White);
        theme.SetColor("font_pressed_color", "Button", Colors.White);
        theme.SetColor("font_disabled_color", "Button", UiConstants.FontColor with { A = 0.35f });

        // CheckBox inherits Button styling for its plate but keeps its own check icons.
        theme.SetStylebox("normal", "CheckBox", Empty());
        theme.SetColor("font_color", "CheckBox", UiConstants.FontColor);

        // ---- Panels / docks ------------------------------------------------
        theme.SetStylebox("panel", "PanelContainer", panel);
        theme.SetStylebox("panel", "Panel", panelDark);

        // ---- TabContainer (bottom dock) ------------------------------------
        StyleBoxFlat tabSelected = Flat(UiConstants.PanelBg);
        tabSelected.BorderWidthTop = 2;
        tabSelected.BorderColor = UiConstants.Accent;
        tabSelected.SetContentMarginAll(8);
        StyleBoxFlat tabUnselected = Flat(UiConstants.PanelBgDarker);
        tabUnselected.SetContentMarginAll(8);
        theme.SetStylebox("panel", "TabContainer", panel);
        theme.SetStylebox("tab_selected", "TabContainer", tabSelected);
        theme.SetStylebox("tab_unselected", "TabContainer", tabUnselected);
        theme.SetStylebox("tab_hovered", "TabContainer", tabUnselected);
        theme.SetColor("font_selected_color", "TabContainer", Colors.White);
        theme.SetColor("font_unselected_color", "TabContainer", UiConstants.FontDim);

        // ---- PopupMenu (menu bar dropdowns) --------------------------------
        StyleBoxFlat popup = Flat(UiConstants.PanelBgDarker);
        popup.BorderColor = UiConstants.BorderColor;
        popup.SetBorderWidthAll(1);
        popup.SetContentMarginAll(6);
        theme.SetStylebox("panel", "PopupMenu", popup);
        theme.SetStylebox("hover", "PopupMenu", Button(UiConstants.Accent with { A = 0.45f }));
        theme.SetColor("font_color", "PopupMenu", UiConstants.FontColor);
        theme.SetColor("font_hover_color", "PopupMenu", Colors.White);

        // ---- MenuBar --------------------------------------------------------
        theme.SetStylebox("normal", "MenuBar", Empty(6, 2));
        theme.SetStylebox("hover", "MenuBar", Button(UiConstants.ControlBgHover));
        theme.SetStylebox("pressed", "MenuBar", Button(UiConstants.Accent with { A = 0.45f }));
        theme.SetColor("font_color", "MenuBar", UiConstants.FontColor);
        theme.SetColor("font_hover_color", "MenuBar", Colors.White);

        // ---- Tree (scene dock) ----------------------------------------------
        theme.SetStylebox("panel", "Tree", panelDark);
        theme.SetColor("font_color", "Tree", UiConstants.FontColor);
        StyleBoxFlat treeSelected = Flat(UiConstants.Accent with { A = 0.35f });
        theme.SetStylebox("selected", "Tree", treeSelected);
        theme.SetStylebox("selected_focus", "Tree", treeSelected);

        // ---- text fields (LineEdit + SpinBox's internal LineEdit) ------------
        StyleBoxFlat field = Flat(UiConstants.PanelBgDarker);
        field.BorderColor = UiConstants.BorderColor;
        field.SetBorderWidthAll(1);
        field.SetContentMarginAll(4);
        StyleBoxFlat fieldFocus = (StyleBoxFlat)field.Duplicate();
        fieldFocus.BorderColor = UiConstants.Accent;
        theme.SetStylebox("normal", "LineEdit", field);
        theme.SetStylebox("focus", "LineEdit", fieldFocus);
        theme.SetColor("font_color", "LineEdit", UiConstants.FontColor);

        // ---- misc -------------------------------------------------------------
        theme.SetColor("font_color", "Label", UiConstants.FontColor);
        theme.SetStylebox("panel", "TooltipPanel", popup);
        theme.SetColor("font_color", "TooltipLabel", UiConstants.FontColor);

        return theme;
    }

    /// <summary>Plain rounded flat box, no border.</summary>
    private static StyleBoxFlat Flat(Color bg)
    {
        var box = new StyleBoxFlat { BgColor = bg };
        box.SetCornerRadiusAll(3);
        return box;
    }

    /// <summary>Button-shaped box: rounded, subtle border, comfortable padding.</summary>
    private static StyleBoxFlat Button(Color bg)
    {
        StyleBoxFlat box = Flat(bg);
        box.BorderColor = UiConstants.BorderColor with { A = 0.6f };
        box.SetBorderWidthAll(1);
        box.ContentMarginLeft = 10;
        box.ContentMarginRight = 10;
        box.ContentMarginTop = 4;
        box.ContentMarginBottom = 4;
        return box;
    }

    private static StyleBoxEmpty Empty(int marginH = 0, int marginV = 0) => new()
    {
        ContentMarginLeft = marginH,
        ContentMarginRight = marginH,
        ContentMarginTop = marginV,
        ContentMarginBottom = marginV,
    };
}
