using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Builds the four edge-resize handles for a selected opening, placed on its rectangle in the wall
/// plane. The opening's 2D move (offset + sill) is grabbed via its body, so it isn't a widget here.
/// </summary>
public static class OpeningHandleProvider
{
    public static List<IEditHandle> Build(PrimitiveInstanceData wall, OpeningData opening, Vector3 elevationOffset)
    {
        var handles = new List<IEditHandle>();
        if (wall == null || opening == null) return handles;

        float length = GetF(wall, "length", 1f);
        float wallHeight = GetF(wall, "height", 3f);
        var world = new Transform3D(wall.LocalTransform.Basis, wall.LocalTransform.Origin + elevationOffset);

        // Edge positions in wall-local space (x = u − length/2, y = height up, z = 0 mid-plane).
        float xLeft = opening.Offset - length * 0.5f;
        float xRight = opening.Offset + opening.Width - length * 0.5f;
        float xMid = opening.Offset + opening.Width * 0.5f - length * 0.5f;
        float yBottom = opening.SillHeight;
        float yTop = opening.SillHeight + opening.Height;
        float yMid = opening.SillHeight + opening.Height * 0.5f;

        Vector3 u = world.Basis.X.Normalized(); // along the wall
        Vector3 up = world.Basis.Y.Normalized();

        Add(handles, wall, opening, OpeningEdge.WidthMax, length, wallHeight, world * new Vector3(xRight, yMid, 0), u);
        Add(handles, wall, opening, OpeningEdge.WidthMin, length, wallHeight, world * new Vector3(xLeft, yMid, 0), -u);
        Add(handles, wall, opening, OpeningEdge.HeightMax, length, wallHeight, world * new Vector3(xMid, yTop, 0), up);
        Add(handles, wall, opening, OpeningEdge.HeightMin, length, wallHeight, world * new Vector3(xMid, yBottom, 0), -up);
        return handles;
    }

    private static void Add(List<IEditHandle> handles, PrimitiveInstanceData wall, OpeningData opening,
        OpeningEdge edge, float length, float wallHeight, Vector3 anchor, Vector3 axis)
        => handles.Add(new OpeningResizeHandle(wall, opening, edge, length, wallHeight, anchor, axis));

    private static float GetF(PrimitiveInstanceData d, string key, float def)
        => d.Parameters.ContainsKey(key) ? d.Parameters[key].AsSingle() : def;
}
