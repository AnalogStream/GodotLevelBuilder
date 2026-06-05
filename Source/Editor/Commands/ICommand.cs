namespace LevelBuilder.Editor.Commands;

/// <summary>A reversible edit. All level-state mutations go through one of these.</summary>
public interface ICommand
{
    string Name { get; }
    void Do();
    void Undo();
}
