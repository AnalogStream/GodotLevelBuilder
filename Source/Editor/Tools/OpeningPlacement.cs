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
    /// <paramref name="offset"/> for an opening of <paramref name="width"/>. False if the wall is
    /// too short or the result overlaps another opening — the one with
    /// <paramref name="ignoreOpeningId"/> (if any) is excluded, so a dragged opening ignores itself.
    /// </summary>
    public static bool TrySnapOffset(
        PrimitiveInstanceData wall, float localX, float width, string ignoreOpeningId, out float offset)
    {
        float length = GetF(wall, "length", 1f);
        float centerU = localX + length * 0.5f;
        centerU = Mathf.Round(centerU / SnapStep) * SnapStep;
        offset = centerU - width * 0.5f;

        float maxOffset = length - width - Margin;
        if (maxOffset < Margin) return false; // wall too short for this opening
        offset = Mathf.Clamp(offset, Margin, maxOffset);

        return !Overlaps(wall, offset, width, ignoreOpeningId);
    }

    /// <summary>True if [offset, offset+width] (padded by Margin) hits another opening on the wall.</summary>
    public static bool Overlaps(PrimitiveInstanceData wall, float offset, float width, string ignoreOpeningId)
    {
        float start = offset, end = offset + width;
        foreach (OpeningData ex in wall.Openings)
        {
            if (ignoreOpeningId != null && ex.Id == ignoreOpeningId) continue;
            float exStart = ex.Offset - Margin, exEnd = ex.Offset + ex.Width + Margin;
            if (start < exEnd && end > exStart) return true;
        }
        return false;
    }

    private static float GetF(PrimitiveInstanceData d, string key, float def)
        => d.Parameters.ContainsKey(key) ? d.Parameters[key].AsSingle() : def;
}
