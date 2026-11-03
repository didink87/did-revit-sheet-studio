namespace SheetStudio.Core.Changes;

/// <summary>Linear history of staged states for panel undo/redo (max 100 steps).</summary>
public sealed class UndoStack
{
    public const int Limit = 100;
    private readonly List<ChangeSet> _history = [];
    private int _index = -1;

    public bool CanUndo => _index > 0;
    public bool CanRedo => _index >= 0 && _index < _history.Count - 1;

    public void Reset(ChangeSet baseline)
    {
        _history.Clear();
        _history.Add(baseline);
        _index = 0;
    }

    public void Push(ChangeSet state)
    {
        if (_index < _history.Count - 1) _history.RemoveRange(_index + 1, _history.Count - _index - 1);
        _history.Add(state);
        if (_history.Count > Limit + 1) _history.RemoveAt(0);
        _index = _history.Count - 1;
    }

    public ChangeSet? Undo() => CanUndo ? _history[--_index] : null;

    public ChangeSet? Redo() => CanRedo ? _history[++_index] : null;
}
