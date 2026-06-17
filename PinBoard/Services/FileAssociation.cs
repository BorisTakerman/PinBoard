using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PinBoard.Services;

/// <summary>
/// Registers the .board file type with this app for the current user (HKCU,
/// no admin needed), so double-clicking a .board file opens it in PinBoard.
/// Runs on startup and is a no-op once registered.
/// </summary>
public static class FileAssociation
{
    private const string Ext = ".board";
    private const string ProgId = "PinBoard.Board";
    private const string FriendlyName = "PinBoard Board";

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const int SHCNF_IDLIST = 0x0000;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, int flags, nint item1, nint item2);

    public static void EnsureRegistered()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
                return;

            var command = $"\"{exe}\" \"%1\"";

            // Skip if already pointing at this exe (avoids rewriting every launch).
            using (var ext = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{Ext}"))
            using (var cmd = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProgId}\shell\open\command"))
            {
                if (ext?.GetValue(null) as string == ProgId && cmd?.GetValue(null) as string == command)
                    return;
            }

            using (var ext = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Ext}"))
                ext.SetValue(null, ProgId);

            using (var prog = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
            {
                prog.SetValue(null, FriendlyName);
                using (var icon = prog.CreateSubKey("DefaultIcon"))
                    icon.SetValue(null, $"\"{exe}\",0");
                using (var cmd = prog.CreateSubKey(@"shell\open\command"))
                    cmd.SetValue(null, command);
            }

            // Tell Explorer the association changed so it takes effect immediately.
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, nint.Zero, nint.Zero);
        }
        catch
        {
            // Best effort — never block startup over a registration failure.
        }
    }
}
