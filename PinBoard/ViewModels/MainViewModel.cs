using PinBoard.Models;

namespace PinBoard.ViewModels;

/// <summary>
/// Root ViewModel and DataContext for <c>MainWindow</c>. Owns the open board,
/// the active tool, and the chrome-level commands.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private BoardViewModel _board = new();
    private ToolMode _activeTool = ToolMode.Select;

    public BoardViewModel Board
    {
        get => _board;
        set => SetProperty(ref _board, value);
    }

    public ToolMode ActiveTool
    {
        get => _activeTool;
        set => SetProperty(ref _activeTool, value);
    }

    public RelayCommand SelectToolCommand { get; }
    public RelayCommand SelectRopeCommand { get; }
    public RelayCommand SetThemeCommand { get; }

    /// <summary>Undo/redo for the current board.</summary>
    public BoardHistory History { get; }

    public MainViewModel()
    {
        History = new BoardHistory(this);
        ApplyDefaults(Board);

        SelectToolCommand = new RelayCommand(p =>
        {
            if (p is ToolMode mode) ActiveTool = mode;
            else if (p is string s && Enum.TryParse<ToolMode>(s, out var parsed)) ActiveTool = parsed;
        });

        SelectRopeCommand = new RelayCommand(p =>
        {
            if (p is RopePresetViewModel preset)
                Board.SelectPreset(preset);
        });

        SetThemeCommand = new RelayCommand(p =>
        {
            if (p is BoardTheme theme) Board.Theme = theme;
            else if (p is string s && Enum.TryParse<BoardTheme>(s, out var parsed)) Board.Theme = parsed;
        });
    }

    /// <summary>Applies the user's global default note/rope preferences to a board.</summary>
    public static void ApplyDefaults(BoardViewModel board)
    {
        board.DefaultNoteColor = Services.SettingsService.Current.DefaultNoteColor;
        board.DefaultRopesInFront = Services.SettingsService.Current.RopesInFront;
    }
}
