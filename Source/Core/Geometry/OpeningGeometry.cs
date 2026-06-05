using Godot;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Core.Geometry;

/// <summary>
/// Shared placement math for an opening's volume within its wall. Wall local space is
/// centred on the origin, running along X (length), thickness along Z, rising from y=0.
/// Used both for the editor's pick box / solid placeholder and the OpeningTool preview.
/// </summary>
public static class OpeningGeometry
{
    /// <summary>
    /// The opening volume as a centred box: its <c>size</c> plus the <c>localCenter</c>
    /// transform relative to the wall origin. Left-multiply by the wall's world transform
    /// to place it. <paramref name="depthPad"/> pokes the box just through both wall faces.
    /// </summary>
    public static (Vector3 size, Transform3D localCenter) LocalBox(
        OpeningData o, float length, float thickness, float depthPad = 0.02f)
    {
        var size = new Vector3(o.Width, o.Height, thickness + depthPad);
        float localX = o.Offset + o.Width * 0.5f - length * 0.5f;
        float localY = o.SillHeight + o.Height * 0.5f;
        return (size, new Transform3D(Basis.Identity, new Vector3(localX, localY, 0)));
    }
}
