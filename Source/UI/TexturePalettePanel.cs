using System.Collections.Generic;
using System.Linq;
using Godot;
using LevelBuilder.Core.Data;

namespace LevelBuilder.UI;

/// <summary>
/// The "Textures" tab of the bottom dock: the texture library as a grid of draggable swatches,
/// grouped by color folder. Pure drag sources — drag a swatch onto an object (viewport) or the
/// inspector's Texture slot to apply it. No editor state needed here.
/// </summary>
public partial class TexturePalettePanel : MarginContainer
{
    public void Setup()
    {
        AddThemeConstantOverride("margin_left", 8);
        AddThemeConstantOverride("margin_top", 8);
        AddThemeConstantOverride("margin_right", 8);
        AddThemeConstantOverride("margin_bottom", 8);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        AddChild(scroll);

        var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(rows);

        List<TextureItem> items = TextureCatalog.Load();
        if (items.Count == 0)
        {
            rows.AddChild(new Label { Text = $"No textures found under {TextureCatalog.Root}", Modulate = new Color(1, 1, 1, 0.6f) });
            return;
        }

        foreach (IGrouping<string, TextureItem> group in items.GroupBy(i => i.Group).OrderBy(g => g.Key))
        {
            rows.AddChild(new Label { Text = group.Key, Modulate = new Color(1, 1, 1, 0.6f) });

            var flow = new HFlowContainer();
            rows.AddChild(flow);

            foreach (TextureItem item in group.OrderBy(i => i.Name))
            {
                var swatch = new TextureSwatch();
                flow.AddChild(swatch);
                swatch.Setup(item);
            }
        }
    }
}
