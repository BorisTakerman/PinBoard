using System.IO;
using System.Linq;
using System.Windows;
using PinBoard.Models;
using PinBoard.Services;

namespace PinBoard;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Associate .board files with this app (per-user; opens them on double-click).
        FileAssociation.EnsureRegistered();

        // Load global preferences, then apply the app-chrome theme and the
        // default board background before the window shows.
        SettingsService.Load();
        ThemeManager.Apply(BoardTheme.CorkB);
        AppThemeManager.Apply(SettingsService.Current.UiTheme);

        // App.xaml has no StartupUri (it defines merged resource dictionaries),
        // so create and show the main window explicitly here.
        var window = new MainWindow();

        // If launched by opening a .board file, load it.
        var file = e.Args.FirstOrDefault(a =>
            a.EndsWith(BoardSerializer.Extension, StringComparison.OrdinalIgnoreCase) && File.Exists(a));
        if (file is not null)
            window.LoadBoardFile(file);

        window.Show();
    }
}
