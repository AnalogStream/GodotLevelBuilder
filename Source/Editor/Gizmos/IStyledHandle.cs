using Godot;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Optional on an <see cref="IEditHandle"/>: lets a handle colour/size its gizmo widget so distinct
/// affordances read apart (e.g. horizontal-move blue, height green, insert yellow, remove red).
/// Handles that don't implement it render in the default blue at full size.
/// </summary>
public interface IStyledHandle
{
    Color WidgetColor { get; }
    /// <summary>Multiplier on the widget's visual size (the grab collider stays full-size for clickability).</summary>
    float WidgetScale { get; }
}
