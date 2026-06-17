using PinBoard.Models;
using PinBoard.Services;

namespace PinBoard.ViewModels;

/// <summary>
/// Undo/redo via whole-board snapshots. Callers mark change boundaries:
/// <see cref="Push"/> for an instant change, or <see cref="Begin"/>/<see cref="Commit"/>
/// to coalesce a gesture (drag, resize, text edit) into a single step.
/// </summary>
public sealed class BoardHistory
{
    private const int MaxDepth = 60;

    private readonly MainViewModel _main;
    private readonly LinkedList<BoardData> _undo = new();
    private readonly Stack<BoardData> _redo = new();
    private BoardData? _pending;
    private bool _restoring;

    public BoardHistory(MainViewModel main) => _main = main;

    private BoardData Snapshot() => _main.Board.ToData();

    /// <summary>Records the current state as an undo point (call before an instant change).</summary>
    public void Push()
    {
        if (_restoring) return;
        Record(Snapshot());
    }

    /// <summary>Captures the pre-gesture state; pair with <see cref="Commit"/> or <see cref="Cancel"/>.</summary>
    public void Begin()
    {
        if (_restoring) return;
        _pending = Snapshot();
    }

    public void Commit()
    {
        if (_restoring || _pending is null) return;
        Record(_pending);
        _pending = null;
    }

    public void Cancel() => _pending = null;

    private void Record(BoardData state)
    {
        _undo.AddLast(state);
        while (_undo.Count > MaxDepth)
            _undo.RemoveFirst();
        _redo.Clear();
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Push(Snapshot());
        var state = _undo.Last!.Value;
        _undo.RemoveLast();
        Restore(state);
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.AddLast(Snapshot());
        Restore(_redo.Pop());
    }

    private void Restore(BoardData data)
    {
        _restoring = true;
        try
        {
            var board = BoardViewModel.FromData(data, _main.Board.FilePath);
            MainViewModel.ApplyDefaults(board);
            _main.Board = board;
            ThemeManager.Apply(board.Theme, board.CustomBackgroundHex);
        }
        finally
        {
            _restoring = false;
        }
    }
}
