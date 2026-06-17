using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PinBoard;
using PinBoard.Models;
using PinBoard.ViewModels;

namespace PinBoard.Controls;

public partial class ClipControl : UserControl
{
    private const double MinSize = 60;

    private ClipViewModel? _vm;
    private MainViewModel? _main;
    private BoardViewModel? _board;
    private Canvas? _canvas;

    private bool _dragging, _resizing, _gestureMoved;
    private Point _start;
    private double _originX, _originY, _originW, _originH;

    public ClipControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _canvas = VisualTreeHelpers.FindAncestor<Canvas>(this);
        _main = VisualTreeHelpers.FindAncestor<ItemsControl>(this)?.DataContext as MainViewModel;
        _board = _main?.Board;
        Configure();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = DataContext as ClipViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVmChanged;
        Configure();
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClipViewModel.IsSelected))
            UpdateSelectionVisual();
        else if (e.PropertyName == nameof(ClipViewModel.AbsolutePath))
            Configure();
    }

    private void Configure()
    {
        if (_vm is null) return;

        bool isImage = _vm.Type == ClipType.Image;
        ImageFrame.Visibility = isImage ? Visibility.Visible : Visibility.Collapsed;
        DocCard.Visibility = isImage ? Visibility.Collapsed : Visibility.Visible;
        ResizeHandle.Visibility = isImage ? Visibility.Visible : Visibility.Collapsed;

        if (isImage)
            LoadImage();
        else
        {
            DocName.Text = _vm.FileName;
            DocExt.Text = _vm.Extension.TrimStart('.').ToUpperInvariant();
        }

        UpdateSelectionVisual();
    }

    private void LoadImage()
    {
        if (_vm is null) return;
        try
        {
            if (!File.Exists(_vm.AbsolutePath))
            {
                ClipImage.Source = null;
                MissingText.Visibility = Visibility.Visible;
                return;
            }
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(_vm.AbsolutePath);
            bmp.EndInit();
            bmp.Freeze();
            ClipImage.Source = bmp;
            MissingText.Visibility = Visibility.Collapsed;
        }
        catch
        {
            ClipImage.Source = null;
            MissingText.Visibility = Visibility.Visible;
        }
    }

    private void UpdateSelectionVisual()
        => SelectionRing.Visibility = _vm?.IsSelected == true ? Visibility.Visible : Visibility.Collapsed;

    // --- Dragging ---

    private void Drag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null || _board is null || _canvas is null) return;

        // Rope tool: drag from this clip to another item to connect them.
        if (_main?.ActiveTool == Models.ToolMode.Rope)
        {
            (Window.GetWindow(this) as MainWindow)?.StartRopeDrag(_vm);
            e.Handled = true;
            return;
        }

        // Double-click opens the file in its default app.
        if (e.ClickCount == 2)
        {
            OpenFile();
            e.Handled = true;
            return;
        }

        bool additive = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        _board.SelectClip(_vm, additive);

        _dragging = true;
        _gestureMoved = false;
        _main?.History.Begin();
        _start = e.GetPosition(_canvas);
        _originX = _vm.X;
        _originY = _vm.Y;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void Drag_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || _vm is null || _canvas is null) return;
        var p = e.GetPosition(_canvas);
        _vm.X = _originX + (p.X - _start.X);
        _vm.Y = _originY + (p.Y - _start.Y);
        _gestureMoved = true;
    }

    private void Drag_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ((UIElement)sender).ReleaseMouseCapture();
        if (_gestureMoved) _main?.History.Commit(); else _main?.History.Cancel();
    }

    // --- Resizing (images) ---

    private void Resize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm is null || _canvas is null) return;
        _resizing = true;
        _gestureMoved = false;
        _main?.History.Begin();
        _start = e.GetPosition(_canvas);
        _originW = _vm.Width;
        _originH = _vm.Height;
        ResizeHandle.CaptureMouse();
        e.Handled = true;
    }

    private void Resize_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_resizing || _vm is null || _canvas is null) return;
        var p = e.GetPosition(_canvas);
        double scale = _originH / Math.Max(1, _originW); // keep aspect ratio
        double newW = Math.Max(MinSize, _originW + (p.X - _start.X));
        _vm.Width = newW;
        _vm.Height = Math.Max(MinSize, newW * scale);
        _gestureMoved = true;
    }

    private void Resize_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_resizing) return;
        _resizing = false;
        ResizeHandle.ReleaseMouseCapture();
        if (_gestureMoved) _main?.History.Commit(); else _main?.History.Cancel();
        e.Handled = true;
    }

    // --- Context menu ---

    private void OpenFile_Click(object sender, RoutedEventArgs e) => OpenFile();

    private void OpenFile()
    {
        if (_vm is null || string.IsNullOrEmpty(_vm.AbsolutePath)) return;
        try
        {
            Process.Start(new ProcessStartInfo(_vm.AbsolutePath) { UseShellExecute = true });
        }
        catch { /* file may have moved; ignore */ }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) { _main?.History.Push(); _board?.RemoveClip(_vm); }
    }
}
