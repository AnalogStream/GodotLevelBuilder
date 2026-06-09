using System;
using LevelBuilder.Core;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Commands;

/// <summary>
/// Adds a primitive instance to the elevation layer at a given height, creating that layer lazily if
/// none exists there yet. Undo removes the instance — and, if this command created the layer and it's
/// now empty, removes the layer too (so navigating to a fresh height + drawing + undo leaves no stray
/// empty layers). Redo re-adds the same layer object so its id stays stable.
/// </summary>
public sealed class AddInstanceCommand : ICommand
{
    private readonly LevelDocument _doc;
    private readonly float _elevation;
    private readonly float _defaultHeight;
    private readonly PrimitiveInstanceData _instance;
    private readonly Action _refresh;

    private StoreyData _storey;   // resolved on first Do, reused across undo/redo
    private bool _createdStorey;  // true when we minted the layer (so undo can drop it)

    public AddInstanceCommand(LevelDocument doc, float elevation, float defaultHeight,
        PrimitiveInstanceData instance, Action refresh)
    {
        _doc = doc;
        _elevation = elevation;
        _defaultHeight = defaultHeight;
        _instance = instance;
        _refresh = refresh;
    }

    public string Name => $"Add {_instance.PrimitiveType}";

    public void Do()
    {
        if (_storey == null)
        {
            _storey = _doc.StoreyAt(_elevation);
            if (_storey == null)
            {
                _storey = new StoreyData
                {
                    Id = Ids.New(),
                    Name = $"Layer {_elevation:0.##} m",
                    BaseElevation = _elevation,
                    Height = _defaultHeight,
                };
                _createdStorey = true;
            }
        }
        if (_createdStorey && !_doc.Storeys.Contains(_storey)) _doc.Storeys.Add(_storey); // re-add on redo
        _storey.Instances.Add(_instance);
        _refresh();
    }

    public void Undo()
    {
        _storey.Instances.Remove(_instance);
        if (_createdStorey && _storey.Instances.Count == 0) _doc.Storeys.Remove(_storey);
        _refresh();
    }
}
