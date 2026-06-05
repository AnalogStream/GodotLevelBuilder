using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Grid;
using LevelBuilder.Editor.Session;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Click a placed primitive to select it (highlighted) and <b>drag its body</b> to move it across
/// the storey floor, grid-snapped. Click a door/window to select the opening (solid placeholder)
/// and drag it along its wall. Either drag commits one undoable move on release; a press that
/// doesn't move is a plain select. Clicking empty space clears the selection. Delete removes the
/// selected instance or opening (handled by ToolManager). No grid cursor.
/// </summary>
public sealed class SelectTool : ITool
{
    private const float Eps = 1e-3f;

    private EditorContext _ctx;

    private enum DragKind { None, Opening, Instance }
    private DragKind _drag;

    // Opening drag (slide along the wall).
    private PrimitiveInstanceData _dragWall;
    private OpeningData _dragOpening;
    private float _dragStartOffset;
    private float _grabDelta; // (opening centre − grab point) in wall-local X, so selecting doesn't recentre it

    // Instance drag (translate across the floor plane).
    private PrimitiveInstanceData _dragInstance;
    private Vector3 _dragStartOrigin;
    private Vector3 _grabGround; // floor-plane point under the cursor at grab time

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
            BeginOpeningDrag(r.InstanceId, r.OpeningId);
        }
        else
        {
            _ctx.Select(r.InstanceId);
            BeginInstanceDrag(r.InstanceId);
        }
    }

    public void OnRelease()
    {
        switch (_drag)
        {
            case DragKind.Opening: EndOpeningDrag(); break;
            case DragKind.Instance: EndInstanceDrag(); break;
        }
    }

    public void OnCancel()
    {
        if (_drag != DragKind.None) { CancelDrag(); return; }
        _ctx.ClearSelection();
    }

    public void UpdatePreview()
    {
        switch (_drag)
        {
            case DragKind.Opening: DragOpening(); break;
            case DragKind.Instance: DragInstance(); break;
        }
    }

    private void CancelDrag()
    {
        switch (_drag)
        {
            case DragKind.Opening:
                _dragOpening.Offset = _dragStartOffset; // roll back the live move
                break;
            case DragKind.Instance:
                SetOrigin(_dragInstance, _dragStartOrigin);
                break;
            default:
                return;
        }
        _drag = DragKind.None;
        _dragWall = null; _dragOpening = null; _dragInstance = null;
        _ctx.View.Rebuild();
    }

    // ---- opening drag ----------------------------------------------------

    private void BeginOpeningDrag(string wallId, string openingId)
    {
        _dragWall = _ctx.GetInstance(wallId);
        _dragOpening = FindOpening(_dragWall, openingId);
        if (_dragOpening == null) { _dragWall = null; return; }
        _dragStartOffset = _dragOpening.Offset;

        // Offset between the opening's centre and where it was grabbed, so a plain select
        // (press without moving) resolves back to the current offset and commits nothing.
        // Bail if the grab raycast misses, else a later hit would recentre the opening on the cursor.
        if (!MouseWallX(out float grabX)) { _dragWall = null; _dragOpening = null; return; }
        float length = GetF(_dragWall, "length", 1f);
        float centerLocalX = _dragStartOffset + _dragOpening.Width * 0.5f - length * 0.5f;
        _grabDelta = centerLocalX - grabX;
        _drag = DragKind.Opening;
    }

    private void DragOpening()
    {
        if (!MouseWallX(out float localX)) return;

        // Move the opening's centre by the cursor delta, keeping the grab point under the cursor.
        if (!OpeningPlacement.TrySnapOffset(_dragWall, localX + _grabDelta, _dragOpening.Width, _dragOpening.Id, out float offset)) return;
        if (Mathf.Abs(offset - _dragOpening.Offset) < Eps) return; // unchanged this frame

        _dragOpening.Offset = offset; // live preview; formalized by MoveOpeningCommand on release
        _ctx.View.Rebuild();
    }

    private void EndOpeningDrag()
    {
        OpeningData opening = _dragOpening;
        float from = _dragStartOffset;
        _drag = DragKind.None;
        _dragWall = null;
        _dragOpening = null;

        if (Mathf.Abs(opening.Offset - from) > Eps)
            _ctx.MoveOpening(opening, from, opening.Offset);
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

    // ---- instance drag ---------------------------------------------------

    private void BeginInstanceDrag(string id)
    {
        _dragInstance = _ctx.GetInstance(id);
        if (_dragInstance == null) return;
        _dragStartOrigin = _dragInstance.LocalTransform.Origin;
        if (!MouseGround(out _grabGround)) { _dragInstance = null; return; }
        _drag = DragKind.Instance;
    }

    private void DragInstance()
    {
        if (!MouseGround(out Vector3 g)) return;

        // Snap the *delta* in whole cells and add to the start origin, so the object keeps its
        // original sub-cell alignment (walls on corners, floors on cell centres) instead of jumping.
        float step = _ctx.Document.Grid.CellSize;
        float dx = Mathf.Round((g.X - _grabGround.X) / step) * step;
        float dz = Mathf.Round((g.Z - _grabGround.Z) / step) * step;
        var origin = new Vector3(_dragStartOrigin.X + dx, _dragStartOrigin.Y, _dragStartOrigin.Z + dz);
        if (origin.IsEqualApprox(_dragInstance.LocalTransform.Origin)) return; // unchanged this frame

        SetOrigin(_dragInstance, origin); // live preview; formalized by MoveInstanceCommand on release
        _ctx.View.Rebuild();
    }

    private void EndInstanceDrag()
    {
        PrimitiveInstanceData inst = _dragInstance;
        Vector3 start = _dragStartOrigin;
        _drag = DragKind.None;
        _dragInstance = null;

        if (!inst.LocalTransform.Origin.IsEqualApprox(start))
        {
            Transform3D from = inst.LocalTransform;
            from.Origin = start;
            _ctx.MoveInstance(inst, from, inst.LocalTransform);
        }
    }

    /// <summary>Floor-plane (storey elevation) point under the mouse; false if the ray is parallel/behind.</summary>
    private bool MouseGround(out Vector3 point)
    {
        point = Vector3.Zero;
        if (!_ctx.Picker.MouseRay(out Vector3 from, out Vector3 dir)) return false;

        float y = _ctx.ElevationOffset.Y;
        if (Mathf.Abs(dir.Y) < 1e-6f) return false; // ray parallel to the floor
        float t = (y - from.Y) / dir.Y;
        if (t < 0f) return false; // plane is behind the camera

        point = from + dir * t;
        return true;
    }

    private static void SetOrigin(PrimitiveInstanceData inst, Vector3 origin)
    {
        Transform3D xf = inst.LocalTransform;
        xf.Origin = origin;
        inst.LocalTransform = xf;
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
