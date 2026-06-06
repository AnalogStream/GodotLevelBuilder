using System;
using Godot;

namespace LevelBuilder.UI;

/// <summary>
/// SubViewportContainer for the 3D view that also accepts dropped texture swatches. The drop is
/// handled by a child <see cref="ViewportDropOverlay"/> rather than this container directly —
/// SubViewportContainer tends to forward the drag into the SubViewport (whose empty GUI eats it),
/// so a Pass overlay on top is the reliable catcher. Input forwarding for orbit/draw is untouched.
/// </summary>
public partial class ViewportDropContainer : SubViewportContainer
{
    public void Setup(SubViewport viewport, Action<string, string> onDropOnInstance)
    {
        var overlay = new ViewportDropOverlay();
        AddChild(overlay);
        overlay.Setup(viewport, onDropOnInstance);
    }
}
