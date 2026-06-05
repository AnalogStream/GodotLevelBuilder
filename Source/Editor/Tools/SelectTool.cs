using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Grid;
using LevelBuilder.Editor.Session;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Click a placed primitive to select it (highlighted); clicking a door/window selects the
/// opening (shown as a solid placeholder) and <b>dragging</b> it slides it along its wall —
/// committed as one undoable move on release. Clicking empty space clears the selection.
/// Delete removes the selected instance or opening (handled by ToolManager). No grid cursor.
/// </summary>
public sealed class SelectTool : ITool
{
    private const float Eps = 1e-3f;

    private EditorContext _ctx;

    // In-progress opening drag.
    private bool _dragging;
    private PrimitiveInstanceData _dragWall;
    private OpeningData _dragOpening;
    private float _dragStartOffset;
    private float _grabDelta; // (opening centre − grab point) in wall-local X, so selecting doesn't recentre it

    public string Name => "Select";
    public GridSnapMode SnapMode => GridSnapMode.Cell; // unused (cursor hidden)
    public bool UsesGridCursor => false;

    public void Activate(EditorContext ctx) => _ctx = ctx;
    public void Deactivate() => CancelDrag(); // switching tools mid-drag must not leave an uncommitted move

    public void OnClick()
    {
        PickResult r = _ctx.Picker.Pick();
        if (!r.Hit) { _ctx.ClearSelection(); return; }

        if (r.IsOpening)
        {
            _ctx.SelectOpening(r.InstanceId, r.OpeningId);
            BeginDrag(r.InstanceId, r.OpeningId);
        }
        else
        {
            _ctx.Select(r.InstanceId);
        }
    }

    public void OnRelease()
    {
        if (!_dragging) return;

        OpeningData opening = _dragOpening;
        float from = _dragStartOffset;
        _dragging = false;
        _dragWall = null;
        _dragOpening = null;

        // Only commit if it actually moved; otherwise it was a plain click-select.
        if (Mathf.Abs(opening.Offset - from) > Eps)
            _ctx.MoveOpening(opening, from, opening.Offset);
    }

    public void OnCancel()
    {
        if (_dragging) { CancelDrag(); return; }
        _ctx.ClearSelection();
    }

    public void UpdatePreview()
    {
        if (!_dragging) return;
        if (!MouseWallX(out float localX)) return;

        // Move the opening's centre by the cursor delta, keeping the grab point under the cursor.
        if (!OpeningPlacement.TrySnapOffset(_dragWall, localX + _grabDelta, _dragOpening.Width, _dragOpening.Id, out float offset)) return;
        if (Mathf.Abs(offset - _dragOpening.Offset) < Eps) return; // unchanged this frame

        _dragOpening.Offset = offset; // live preview; formalized by MoveOpeningCommand on release
        _ctx.View.Rebuild();
    }

    private void BeginDrag(string wallId, string openingId)
    {
        _dragWall = _ctx.GetInstance(wallId);
        _dragOpening = FindOpening(_dragWall, openingId);
        if (_dragOpening == null) { _dragWall = null; return; }
        _dragStartOffset = _dragOpening.Offset;
        _dragging = true;

        // Offset between the opening's centre and where it was grabbed, so a plain select
        // (press without moving) resolves back to the current offset and commits nothing.
        float length = GetF(_dragWall, "length", 1f);
        float centerLocalX = _dragStartOffset + _dragOpening.Width * 0.5f - length * 0.5f;
        _grabDelta = MouseWallX(out float grabX) ? centerLocalX - grabX : 0f;
    }

    /// <summary>Wall-local X where the mouse ray meets the dragged wall's mid-plane; false if no hit.</summary>
    private bool MouseWallX(out float localX)
    {
        localX = 0f;
        if (!_ctx.Picker.MouseRay(out Vector3 from, out Vector3 dir)) return false;

        var wallWorld = new Transform3D(_dragWall.LocalTransform.Basis, _dragWall.LocalTransform.Origin + _ctx.ElevationOffset);
        Vector3 n = wallWorld.Basis.Z.Normalized();
        float denom = dir.Dot(n);
        if (Mathf.Abs(denom) < 1e-6f) return false; // camera edge-on to the wall
        float t = (wallWorld.Origin - from).Dot(n) / denom;
        if (t < 0f) return false; // plane is behind the camera

        localX = (wallWorld.AffineInverse() * (from + dir * t)).X;
        return true;
    }

    private void CancelDrag()
    {
        if (!_dragging) return;
        _dragOpening.Offset = _dragStartOffset; // roll back the live move
        _dragging = false;
        _dragWall = null;
        _dragOpening = null;
        _ctx.View.Rebuild();
    }

    private static OpeningData FindOpening(PrimitiveInstanceData wall, string id)
    {
        if (wall == null) return null;
        foreach (OpeningData o in wall.Openings)
            if (o.Id == id) return o;
        return null;
    }

    private static float GetF(PrimitiveInstanceData d, string key, float def)
        => d.Parameters.ContainsKey(key) ? d.Parameters[key].AsSingle() : def;
}
