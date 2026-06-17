using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using PinBoard.Controls;
using PinBoard.Models;
using PinBoard.Services;
using PinBoard.ViewModels;

namespace PinBoard;

public partial class MainWindow : Window
{
    private const double MinZoom = 0.25;
    private const double MaxZoom = 4.0;

    private bool _panning;
    private Point _panStart;
    private double _panOriginX, _panOriginY;

    private bool _ropeDragging;
    private IRopeAnchor? _ropeFrom;

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        CenterOnPrimaryMonitor();
        KeyDown += MainWindow_KeyDown;
        Closing += MainWindow_Closing;
    }

    /// <summary>
    /// Always open centered on the primary monitor's work area (excludes the
    /// taskbar), regardless of cursor position or other monitors.
    /// </summary>
    private void CenterOnPrimaryMonitor()
    {
        var work = SystemParameters.WorkArea; // primary monitor, in WPF units
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Top + (work.Height - Height) / 2;
    }

    // ============================================================
    //  Board pan / zoom
    // ============================================================

    private void BoardHost_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        ZoomBy(e.Delta > 0 ? 1.1 : 1 / 1.1, e.GetPosition(BoardHost));
        e.Handled = true;
    }

    /// <summary>Zooms by a factor, keeping the given anchor point (in BoardHost coords) fixed.</summary>
    private void ZoomBy(double factor, Point anchor)
    {
        double oldZoom = WorldScale.ScaleX;
        double newZoom = Math.Clamp(oldZoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - oldZoom) < 1e-6)
            return;

        double f = newZoom / oldZoom;
        WorldTranslate.X = anchor.X - (anchor.X - WorldTranslate.X) * f;
        WorldTranslate.Y = anchor.Y - (anchor.Y - WorldTranslate.Y) * f;
        WorldScale.ScaleX = WorldScale.ScaleY = newZoom;
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
        => ZoomBy(1.2, new Point(BoardHost.ActualWidth / 2, BoardHost.ActualHeight / 2));

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
        => ZoomBy(1 / 1.2, new Point(BoardHost.ActualWidth / 2, BoardHost.ActualHeight / 2));

    private void BoardHost_MouseDown(object sender, MouseButtonEventArgs e)
    {
        bool overEmpty = ReferenceEquals(e.OriginalSource, WorldCanvas)
                         || ReferenceEquals(e.OriginalSource, BoardHost);

        // Pan: middle button anywhere, or the Hand tool with left button.
        if (e.ChangedButton == MouseButton.Middle ||
            (e.ChangedButton == MouseButton.Left && ViewModel.ActiveTool == ToolMode.Pan))
        {
            BeginPan(e);
            return;
        }

        if (e.ChangedButton != MouseButton.Left || !overEmpty)
            return;

        switch (ViewModel.ActiveTool)
        {
            case ToolMode.AddNote:
                ViewModel.History.Push();
                var p = e.GetPosition(WorldCanvas);
                var note = ViewModel.Board.AddNote(p.X - 90, p.Y - 90);
                ViewModel.Board.Select(note, additive: false);
                ViewModel.ActiveTool = ToolMode.Select; // place one, then back to Select
                break;

            case ToolMode.ImageClip:
                PickAndAddClip(ClipType.Image, e.GetPosition(WorldCanvas));
                ViewModel.ActiveTool = ToolMode.Select;
                break;

            case ToolMode.DocumentClip:
                PickAndAddClip(ClipType.Document, e.GetPosition(WorldCanvas));
                ViewModel.ActiveTool = ToolMode.Select;
                break;

            case ToolMode.Select:
                ViewModel.Board.ClearSelection();
                break;
        }
    }

    private void BeginPan(MouseButtonEventArgs e)
    {
        _panning = true;
        _panStart = e.GetPosition(BoardHost);
        _panOriginX = WorldTranslate.X;
        _panOriginY = WorldTranslate.Y;
        BoardHost.CaptureMouse();
        BoardHost.Cursor = Cursors.SizeAll;
    }

    private void BoardHost_MouseMove(object sender, MouseEventArgs e)
    {
        if (_ropeDragging)
        {
            UpdateRopePreview(e.GetPosition(WorldCanvas));
            return;
        }

        if (!_panning)
            return;
        var p = e.GetPosition(BoardHost);
        WorldTranslate.X = _panOriginX + (p.X - _panStart.X);
        WorldTranslate.Y = _panOriginY + (p.Y - _panStart.Y);
    }

    private void BoardHost_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_ropeDragging)
        {
            var target = HitTestAnchor(e.GetPosition(BoardHost));
            if (target is not null && _ropeFrom is not null && !ReferenceEquals(target, _ropeFrom))
            {
                ViewModel.History.Push();
                ViewModel.Board.CreateRope(_ropeFrom, target);
            }
            EndRopeDrag();
            return;
        }

        if (!_panning)
            return;
        _panning = false;
        BoardHost.ReleaseMouseCapture();
        BoardHost.Cursor = Cursors.Arrow;
    }

    // --- Drag-to-connect ropes ---

    /// <summary>Begins dragging a new rope out from <paramref name="from"/> (a note or clip).</summary>
    public void StartRopeDrag(IRopeAnchor from)
    {
        _ropeFrom = from;
        _ropeDragging = true;

        var preset = ViewModel.Board.ActivePreset;
        RopePreview.Stroke = HexBrush(preset?.ColorHex ?? "#E0564F");
        RopePreview.StrokeDashArray = preset?.Dashed == true ? new DoubleCollection { 4, 3 } : null;
        RopePreview.Visibility = Visibility.Visible;
        UpdateRopePreview(RopeAnchor(from));

        Mouse.Capture(BoardHost);
    }

    private void UpdateRopePreview(Point end)
    {
        if (_ropeFrom is null) return;
        var start = RopeAnchor(_ropeFrom);
        double dx = end.X - start.X, dy = end.Y - start.Y;
        double sag = Math.Clamp(Math.Sqrt(dx * dx + dy * dy) * SettingsService.Current.RopeSag, 0, 400);

        var figure = new PathFigure { StartPoint = start };
        figure.Segments.Add(new BezierSegment(
            new Point(start.X + dx * 0.33, start.Y + dy * 0.33 + sag),
            new Point(start.X + dx * 0.66, start.Y + dy * 0.66 + sag),
            end, true));
        var geo = new PathGeometry();
        geo.Figures.Add(figure);
        RopePreview.Data = geo;
    }

    private void EndRopeDrag()
    {
        _ropeDragging = false;
        _ropeFrom = null;
        RopePreview.Visibility = Visibility.Collapsed;
        RopePreview.Data = null;
        if (ReferenceEquals(Mouse.Captured, BoardHost))
            BoardHost.ReleaseMouseCapture();
    }

    private static Point RopeAnchor(IRopeAnchor a) => new(a.X + a.Width / 2, a.Y + 11);

    /// <summary>Finds the note or clip under a point (in BoardHost coords), if any.</summary>
    private IRopeAnchor? HitTestAnchor(Point pointInHost)
    {
        IRopeAnchor? found = null;
        VisualTreeHelper.HitTest(BoardHost, null, r =>
        {
            FrameworkElement? host = VisualTreeHelpers.FindAncestor<NoteControl>(r.VisualHit);
            host ??= VisualTreeHelpers.FindAncestor<ClipControl>(r.VisualHit);
            if (host?.DataContext is IRopeAnchor anchor)
            {
                found = anchor;
                return HitTestResultBehavior.Stop;
            }
            return HitTestResultBehavior.Continue;
        }, new PointHitTestParameters(pointInHost));
        return found;
    }

    private static SolidColorBrush HexBrush(string hex)
    {
        try
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
            b.Freeze();
            return b;
        }
        catch { return Brushes.IndianRed; }
    }

    // ============================================================
    //  Image / document clips
    // ============================================================

    private void PickAndAddClip(ClipType type, Point worldPoint)
    {
        var dialog = new OpenFileDialog
        {
            Filter = type == ClipType.Image ? ClipFiles.ImageFilter : ClipFiles.DocumentFilter
        };
        if (dialog.ShowDialog(this) == true)
            AddClipFromFile(dialog.FileName, worldPoint);
    }

    private void AddClipFromFile(string path, Point worldPoint)
    {
        var type = ClipFiles.Classify(path);
        if (type is null)
            return;

        var (w, h) = type == ClipType.Image ? MeasureImage(path) : (200.0, 70.0);
        ViewModel.History.Push();
        ViewModel.Board.AddClip(type.Value, path, worldPoint.X - w / 2, worldPoint.Y - h / 2, w, h);
    }

    /// <summary>Returns a sensible framed clip size for an image, preserving aspect ratio.</summary>
    private static (double w, double h) MeasureImage(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            double pw = bmp.PixelWidth, ph = bmp.PixelHeight;
            if (pw <= 0 || ph <= 0)
                return (180, 180);

            const double maxDim = 220;
            double scale = maxDim / Math.Max(pw, ph);
            return (pw * scale + 12, ph * scale + 22); // + frame padding + pin margin
        }
        catch
        {
            return (180, 180);
        }
    }

    private void BoardHost_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void BoardHost_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var pos = e.GetPosition(WorldCanvas);
        double offset = 0;
        foreach (var file in files)
        {
            if (ClipFiles.Classify(file) is null)
                continue;
            AddClipFromFile(file, new Point(pos.X + offset, pos.Y + offset));
            offset += 26; // cascade multiple drops
        }
        e.Handled = true;
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+S saves, Ctrl+O opens, Ctrl+Z/Y undo/redo.
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.S: SaveBoard(promptIfUnsaved: true); e.Handled = true; return;
                case Key.O: OpenBoard(); e.Handled = true; return;
                case Key.Z: ViewModel.History.Undo(); e.Handled = true; return;
                case Key.Y: ViewModel.History.Redo(); e.Handled = true; return;
            }
        }

        // Don't hijack Delete while editing note text.
        if (e.Key == Key.Delete && Keyboard.FocusedElement is not TextBox)
        {
            ViewModel.History.Push();
            ViewModel.Board.DeleteSelected();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && _ropeDragging)
        {
            EndRopeDrag(); // cancel an in-progress rope
            return;
        }

        // Tool hotkeys — only when not typing and not in the settings panel.
        if (Keyboard.FocusedElement is TextBox ||
            Keyboard.Modifiers != ModifierKeys.None ||
            SettingsPanel.Visibility == Visibility.Visible)
            return;

        var key = e.Key.ToString();
        var h = SettingsService.Current.Hotkeys;
        ToolMode? tool =
            key == h.AddNote ? ToolMode.AddNote :
            key == h.Rope ? ToolMode.Rope :
            key == h.ImageClip ? ToolMode.ImageClip :
            key == h.DocumentClip ? ToolMode.DocumentClip :
            null;

        if (tool is not null)
        {
            ViewModel.ActiveTool = tool.Value;
            e.Handled = true;
        }
    }

    private void BoardSwitcher_Click(object sender, RoutedEventArgs e)
    {
        var folder = SettingsService.Current.HomeFolder;
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch { /* folder may be invalid; ignore */ }
    }

    // ============================================================
    //  Save / Open / autosave
    // ============================================================

    private void Save_Click(object sender, RoutedEventArgs e) => SaveBoard(promptIfUnsaved: true);

    /// <summary>
    /// Saves the current board. If it has no file yet and <paramref name="promptIfUnsaved"/>
    /// is true, asks where to save; otherwise auto-names it under the home folder.
    /// Returns true if it was written.
    /// </summary>
    private bool SaveBoard(bool promptIfUnsaved)
    {
        var board = ViewModel.Board;
        string? path = board.FilePath;

        if (path is null)
        {
            if (promptIfUnsaved)
            {
                var dialog = new SaveFileDialog
                {
                    Filter = BoardSerializer.Filter,
                    DefaultExt = BoardSerializer.Extension,
                    InitialDirectory = EnsureHomeFolder(),
                    FileName = BoardSerializer.SafeFileName(board.Name) + BoardSerializer.Extension
                };
                if (dialog.ShowDialog(this) != true)
                    return false;
                path = dialog.FileName;
            }
            else
            {
                path = AutoSavePath(board);
            }
        }

        try
        {
            BoardSerializer.Save(board, path);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save the board:\n{ex.Message}", "PinBoard",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private void OpenBoard()
    {
        var dialog = new OpenFileDialog
        {
            Filter = BoardSerializer.Filter,
            InitialDirectory = EnsureHomeFolder()
        };
        if (dialog.ShowDialog(this) != true)
            return;

        // Persist the current board first (so switching never loses work).
        if (ViewModel.Board.FilePath is not null || !IsBoardEmpty(ViewModel.Board))
            SaveBoard(promptIfUnsaved: false);

        LoadBoardInto(dialog.FileName);
    }

    /// <summary>Loads a .board file at startup (e.g. from a double-clicked file).</summary>
    public void LoadBoardFile(string path) => LoadBoardInto(path);

    private void LoadBoardInto(string path)
    {
        try
        {
            var loaded = BoardSerializer.Load(path);
            MainViewModel.ApplyDefaults(loaded);
            ViewModel.Board = loaded;
            ThemeManager.Apply(loaded.Theme, loaded.CustomBackgroundHex);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not open that board:\n{ex.Message}", "PinBoard",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Autosave on exit: always persist a board that has a file; for a never-saved
        // board, only write one if it actually has content.
        var board = ViewModel.Board;
        if (board.FilePath is not null || !IsBoardEmpty(board))
            SaveBoard(promptIfUnsaved: false);

        SettingsService.Save();
    }

    private static bool IsBoardEmpty(BoardViewModel board)
        => board.Notes.Count == 0 && board.Ropes.Count == 0 && board.Clips.Count == 0;

    private static string EnsureHomeFolder()
    {
        var folder = SettingsService.Current.HomeFolder;
        try { Directory.CreateDirectory(folder); } catch { /* ignore */ }
        return folder;
    }

    /// <summary>A unique auto-save path under the home folder for an unsaved board.</summary>
    private static string AutoSavePath(BoardViewModel board)
    {
        var folder = EnsureHomeFolder();
        var baseName = BoardSerializer.SafeFileName(string.IsNullOrWhiteSpace(board.Name) ? "Untitled" : board.Name);
        var path = System.IO.Path.Combine(folder, baseName + BoardSerializer.Extension);
        int n = 1;
        while (File.Exists(path))
            path = System.IO.Path.Combine(folder, $"{baseName} ({n++}){BoardSerializer.Extension}");
        return path;
    }

    // ============================================================
    //  Titlebar window chrome
    // ============================================================

    private void Titlebar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Minimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    // ============================================================
    //  Bottom bar: rope presets
    // ============================================================

    private string _newPresetColor = RopePalette.Colors[0].Hex;
    private readonly List<Button> _swatchButtons = new();

    private void RopePreset_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RopePresetViewModel preset })
            ViewModel.Board.SelectPreset(preset);
    }

    private void RemovePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RopePresetViewModel preset })
            ViewModel.Board.RemovePreset(preset);
        e.Handled = true;
    }

    private void AddPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_swatchButtons.Count == 0)
            BuildColorSwatches();

        PresetNameInput.Text = string.Empty;
        DashedToggle.IsChecked = false;
        SelectPresetColor(RopePalette.Colors[0].Hex);

        AddPresetPopup.IsOpen = true;
        PresetNameInput.Focus();
    }

    private void BuildColorSwatches()
    {
        ColorSwatchPanel.Children.Clear();
        _swatchButtons.Clear();
        foreach (var swatch in RopePalette.Colors)
            ColorSwatchPanel.Children.Add(MakePresetSwatch(swatch.Hex, swatch.Name));

        // "+" custom color
        var plus = new Button
        {
            Width = 24, Height = 24, Margin = new Thickness(0, 0, 6, 6), Cursor = Cursors.Hand,
            ToolTip = "Custom color", Template = (ControlTemplate)Resources["PlusSwatchTemplate"]
        };
        plus.Click += (_, _) =>
        {
            var picked = ColorPickerWindow.Pick(this, HexColor(_newPresetColor));
            if (picked is Color col)
            {
                var hex = $"#{col.R:X2}{col.G:X2}{col.B:X2}";
                var btn = MakePresetSwatch(hex, hex);
                ColorSwatchPanel.Children.Insert(ColorSwatchPanel.Children.Count - 1, btn);
                SelectPresetColor(hex);
            }
        };
        ColorSwatchPanel.Children.Add(plus);
    }

    private Button MakePresetSwatch(string hex, string name)
    {
        var btn = new Button
        {
            Width = 24, Height = 24, Margin = new Thickness(0, 0, 6, 6), Cursor = Cursors.Hand,
            Tag = hex, ToolTip = name, Background = HexBrush(hex),
            Template = (ControlTemplate)Resources["SwatchButtonTemplate"]
        };
        btn.Click += (_, _) => SelectPresetColor(hex);
        _swatchButtons.Add(btn);
        return btn;
    }

    private static Color HexColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex)!; }
        catch { return Colors.IndianRed; }
    }

    private void SelectPresetColor(string hex)
    {
        _newPresetColor = hex;
        foreach (var b in _swatchButtons)
            b.BorderThickness = (string)b.Tag == hex ? new Thickness(2) : new Thickness(0);
    }

    private void CreatePreset_Click(object sender, RoutedEventArgs e)
    {
        // Blank name = a plain, label-less rope (the pill shows its color instead).
        var name = PresetNameInput.Text.Trim();
        ViewModel.Board.AddPreset(name, _newPresetColor, DashedToggle.IsChecked == true);
        AddPresetPopup.IsOpen = false;
    }

    // ============================================================
    //  Settings slide-out panel
    // ============================================================

    private static readonly string[] BoardColorPalette =
    {
        "#C4A47A", "#B89B73", "#8A7A5C", "#6E5B43",
        "#F4F2EE", "#E8E2D5", "#2B2D33", "#1A1B1F",
        "#20303A", "#283A2E", "#3A2E3D", "#1E2A38",
    };

    private bool _settingsBuilt;
    private readonly List<Button> _appThemeButtons = new();
    private readonly List<Button> _boardThemeButtons = new();
    private readonly List<Button> _noteColorButtons = new();

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (!_settingsBuilt)
        {
            BuildAppThemeSwatches();
            BuildBoardThemeButtons();
            BuildBoardColorSwatches();
            BuildNoteColorSwatches();
            BuildHotkeyRows();
            _settingsBuilt = true;
        }

        var s = SettingsService.Current;
        SagSlider.Value = s.RopeSag;
        ThicknessSlider.Value = s.RopeThickness;
        RopesFrontToggle.IsChecked = s.RopesInFront;
        HomeFolderBox.Text = s.HomeFolder;
        HighlightAppTheme();
        HighlightBoardTheme();
        HighlightNoteColor();

        SettingsPanel.Visibility = Visibility.Visible;
        SettingsScrim.Visibility = Visibility.Visible;
        AnimatePanel(0);
    }

    private void CloseSettings_Click(object sender, RoutedEventArgs e) => CloseSettings();

    private void SettingsScrim_Click(object sender, MouseButtonEventArgs e) => CloseSettings();

    private void CloseSettings()
    {
        SettingsService.Save();
        var anim = new DoubleAnimation(SettingsPanel.Width, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        anim.Completed += (_, _) =>
        {
            SettingsPanel.Visibility = Visibility.Collapsed;
            SettingsScrim.Visibility = Visibility.Collapsed;
        };
        SettingsTransform.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void AnimatePanel(double to)
    {
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(190))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        SettingsTransform.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    // --- Ropes ---

    private void SagSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SagValue is null) return; // during InitializeComponent
        SettingsService.Current.RopeSag = e.NewValue;
        SagValue.Text = e.NewValue.ToString("0.00");
        SettingsService.NotifyChanged();
    }

    private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThicknessValue is null) return;
        SettingsService.Current.RopeThickness = e.NewValue;
        ThicknessValue.Text = e.NewValue.ToString("0.0") + " px";
        SettingsService.NotifyChanged();
    }

    private void RopesFrontToggle_Changed(object sender, RoutedEventArgs e)
    {
        bool front = RopesFrontToggle.IsChecked == true;
        SettingsService.Current.RopesInFront = front;
        ViewModel.Board.DefaultRopesInFront = front;
    }

    // --- Hotkeys ---

    private void BuildHotkeyRows()
    {
        HotkeyPanel.Children.Clear();
        (ToolMode tool, string label)[] rows =
        {
            (ToolMode.AddNote, "Sticky note"),
            (ToolMode.Rope, "Rope"),
            (ToolMode.ImageClip, "Image clip"),
            (ToolMode.DocumentClip, "Document clip"),
        };
        foreach (var (tool, label) in rows)
            HotkeyPanel.Children.Add(MakeHotkeyRow(tool, label));
    }

    private DockPanel MakeHotkeyRow(ToolMode tool, string label)
    {
        var keyButton = new Button
        {
            Content = GetHotkey(tool),
            MinWidth = 52,
            Height = 26,
            Padding = new Thickness(10, 0, 10, 0),
            Cursor = Cursors.Hand,
            FontSize = 11,
            Style = (Style)FindResource("ChromeButton")
        };
        keyButton.Click += (_, _) => keyButton.Content = "Press a key…";
        keyButton.PreviewKeyDown += (_, ev) =>
        {
            ev.Handled = true;
            var k = ev.Key == Key.System ? ev.SystemKey : ev.Key;
            if (k is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
                return; // wait for a real key
            if (k == Key.Escape)
            {
                keyButton.Content = GetHotkey(tool); // cancel
                Keyboard.ClearFocus();
                return;
            }
            SetHotkey(tool, k.ToString());
            keyButton.Content = k.ToString();
            Keyboard.ClearFocus();
        };

        var row = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
        DockPanel.SetDock(keyButton, Dock.Right);
        row.Children.Add(keyButton);
        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("TextFaintBrush")
        });
        return row;
    }

    private static string GetHotkey(ToolMode tool)
    {
        var h = SettingsService.Current.Hotkeys;
        return tool switch
        {
            ToolMode.AddNote => h.AddNote,
            ToolMode.Rope => h.Rope,
            ToolMode.ImageClip => h.ImageClip,
            ToolMode.DocumentClip => h.DocumentClip,
            _ => string.Empty
        };
    }

    private static void SetHotkey(ToolMode tool, string key)
    {
        var h = SettingsService.Current.Hotkeys;
        switch (tool)
        {
            case ToolMode.AddNote: h.AddNote = key; break;
            case ToolMode.Rope: h.Rope = key; break;
            case ToolMode.ImageClip: h.ImageClip = key; break;
            case ToolMode.DocumentClip: h.DocumentClip = key; break;
        }
    }

    // --- Files ---

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (Directory.Exists(SettingsService.Current.HomeFolder))
            dialog.InitialDirectory = SettingsService.Current.HomeFolder;
        if (dialog.ShowDialog(this) == true)
        {
            SettingsService.Current.HomeFolder = dialog.FolderName;
            HomeFolderBox.Text = dialog.FolderName;
        }
    }

    // --- Swatch builders ---

    private void BuildAppThemeSwatches()
    {
        AppThemePanel.Children.Clear();
        _appThemeButtons.Clear();
        foreach (var theme in AppThemeManager.All)
        {
            var (chrome, accent, text) = AppThemeManager.Preview(theme);
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new Ellipse
            {
                Width = 9, Height = 9, Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center, Fill = Frozen(accent)
            });
            content.Children.Add(new TextBlock
            {
                Text = theme.ToString(), FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center, Foreground = Frozen(text)
            });

            var btn = new Button
            {
                Width = 150, Height = 34, Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(10, 0, 10, 0), Cursor = Cursors.Hand,
                Tag = theme, Background = Frozen(chrome), Content = content,
                Template = (ControlTemplate)Resources["PreviewButtonTemplate"]
            };
            btn.Click += (_, _) =>
            {
                SettingsService.Current.UiTheme = theme;
                AppThemeManager.Apply(theme);
                HighlightAppTheme();
            };
            _appThemeButtons.Add(btn);
            AppThemePanel.Children.Add(btn);
        }
    }

    private void BuildBoardThemeButtons()
    {
        BoardThemePanel.Children.Clear();
        _boardThemeButtons.Clear();
        (BoardTheme theme, string label, string color)[] items =
        {
            (BoardTheme.CorkA, "Cork A", "#c9a97a"),
            (BoardTheme.CorkB, "Cork B", "#c4a47a"),
            (BoardTheme.CorkC, "Cork C", "#be9e6e"),
            (BoardTheme.CorkD, "Cork D", "#d4b48a"),
            (BoardTheme.Dark,  "Dark",   "#1a1b1f"),
            (BoardTheme.Plain, "Plain",  "#f4f2ee"),
        };
        foreach (var (theme, label, color) in items)
        {
            var bg = (Color)ColorConverter.ConvertFromString(color)!;
            var ink = Luminance(bg) > 0.55 ? Colors.Black : Colors.White;
            var btn = new Button
            {
                Width = 96, Height = 30, Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(10, 0, 10, 0), Cursor = Cursors.Hand,
                Tag = theme, Background = Frozen(bg),
                Content = new TextBlock { Text = label, FontSize = 11, Foreground = Frozen(ink) },
                Template = (ControlTemplate)Resources["PreviewButtonTemplate"]
            };
            btn.Click += (_, _) =>
            {
                ViewModel.Board.Theme = theme;
                HighlightBoardTheme();
            };
            _boardThemeButtons.Add(btn);
            BoardThemePanel.Children.Add(btn);
        }
    }

    private void BuildBoardColorSwatches()
    {
        BoardColorPanel.Children.Clear();
        foreach (var hex in BoardColorPalette)
        {
            var color = hex;
            var btn = new Button
            {
                Width = 26, Height = 26, Margin = new Thickness(0, 0, 6, 6), Cursor = Cursors.Hand,
                Background = Frozen((Color)ColorConverter.ConvertFromString(hex)!),
                Template = (ControlTemplate)Resources["SwatchButtonTemplate"]
            };
            btn.Click += (_, _) =>
            {
                ViewModel.Board.SetCustomBackground(color);
                HighlightBoardTheme();
            };
            BoardColorPanel.Children.Add(btn);
        }

        var plus = new Button
        {
            Width = 26, Height = 26, Margin = new Thickness(0, 0, 6, 6), Cursor = Cursors.Hand,
            ToolTip = "Custom color", Template = (ControlTemplate)Resources["PlusSwatchTemplate"]
        };
        plus.Click += (_, _) =>
        {
            var seed = HexColor(ViewModel.Board.CustomBackgroundHex);
            var picked = ColorPickerWindow.Pick(this, seed);
            if (picked is Color col)
            {
                ViewModel.Board.SetCustomBackground($"#{col.R:X2}{col.G:X2}{col.B:X2}");
                HighlightBoardTheme();
            }
        };
        BoardColorPanel.Children.Add(plus);
    }

    private void BuildNoteColorSwatches()
    {
        NoteColorPanel.Children.Clear();
        _noteColorButtons.Clear();
        foreach (var color in NoteColors.Palette)
        {
            var btn = new Button
            {
                Width = 26, Height = 26, Margin = new Thickness(0, 0, 6, 6), Cursor = Cursors.Hand,
                Tag = color, Background = (Brush)NoteColors.Body(color, BoardTheme.CorkB),
                Template = (ControlTemplate)Resources["SwatchButtonTemplate"]
            };
            btn.Click += (_, _) =>
            {
                SettingsService.Current.DefaultNoteColor = color;
                ViewModel.Board.DefaultNoteColor = color;
                HighlightNoteColor();
            };
            _noteColorButtons.Add(btn);
            NoteColorPanel.Children.Add(btn);
        }
    }

    // --- Highlight active selections ---

    private void HighlightAppTheme()
    {
        var accent = (Brush)(TryFindResource("AccentBrush") ?? Brushes.DodgerBlue);
        foreach (var b in _appThemeButtons)
        {
            bool active = (UiTheme)b.Tag! == AppThemeManager.Current;
            b.BorderBrush = accent;
            b.BorderThickness = new Thickness(active ? 2 : 0);
        }
    }

    private void HighlightBoardTheme()
    {
        var accent = (Brush)(TryFindResource("AccentBrush") ?? Brushes.DodgerBlue);
        foreach (var b in _boardThemeButtons)
        {
            bool active = (BoardTheme)b.Tag! == ViewModel.Board.Theme;
            b.BorderBrush = accent;
            b.BorderThickness = new Thickness(active ? 2 : 0);
        }
    }

    private void HighlightNoteColor()
    {
        var accent = (Brush)(TryFindResource("AccentBrush") ?? Brushes.DodgerBlue);
        foreach (var b in _noteColorButtons)
        {
            bool active = (NoteColor)b.Tag! == ViewModel.Board.DefaultNoteColor;
            b.BorderBrush = accent;
            b.BorderThickness = new Thickness(active ? 2 : 0);
        }
    }

    private static double Luminance(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
