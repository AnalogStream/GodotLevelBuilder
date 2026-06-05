using Godot;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Shared snap/clamp/overlap rules for positioning an opening along a wall — used both when
/// placing a new one (OpeningTool) and when dragging an existing one (SelectTool).
/// </summary>
public static class OpeningPlacement
{
    public const float SnapStep = 0.25f;
    public const float Margin = 0.05f;

    /// <summary>
    /// Snap+clamp a desired along-wall centre (<paramref name="localX"/>, wall-local) to a valid
    /// <paramref name="offset"/> for an opening of <paramref name="width"/>. False only if the wall
    /// is too short — overlap is a separate 2D check (see <see cref="Overlaps"/>) so callers can fold
    /// in the vertical position too.
    /// </summary>
    public static bool SnapOffset(PrimitiveInstanceData wall, float localX, float width, out float offset)
    {
        float length = GetF(wall, "length", 1f);
        float centerU = localX + length * 0.5f;
        centerU = Mathf.Round(centerU / SnapStep) * SnapStep;
        offset = centerU - width * 0.5f;

        float maxOffset = length - width - Margin;
        if (maxOffset < Margin) return false; // wall too short for this opening
        offset = Mathf.Clamp(offset, Margin, maxOffset);
        return true;
    }

    /// <summary>
    /// True if the rectangle [offset, offset+width] × [sill, sill+height] (padded by Margin on all
    /// sides) hits another opening on the wall. The 2D test lets openings stack vertically — two are
    /// only in conflict when they overlap both along the wall AND in height. The one with
    /// <paramref name="ignoreOpeningId"/> (if any) is excluded, so a dragged opening ignores itself.
    /// </summary>
    public static bool Overlaps(PrimitiveInstanceData wall, float offset, float width, float sill, float height, string ignoreOpeningId)
    {
        float aL = offset - Margin, aR = offset + width + Margin;
        float aB = sill - Margin, aT = sill + height + Margin;
        foreach (OpeningData ex in wall.Openings)
        {
            if (ignoreOpeningId != null && ex.Id == ignoreOpeningId) continue;
            float bL = ex.Offset, bR = ex.Offset + ex.Width;
            float bB = ex.SillHeight, bT = ex.SillHeight + ex.Height;
            if (aL < bR && aR > bL && aB < bT && aT > bB) return true;
        }
        return false;
    }

    private static float GetF(PrimitiveInstanceData d, string key, float def)
        => d.Parameters.ContainsKey(key) ? d.Parameters[key].AsSingle() : def;
}
