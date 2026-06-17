using System.IO;
using PinBoard.Models;

namespace PinBoard.Services;

/// <summary>Maps file extensions to clip kinds and supplies file-dialog filters.</summary>
public static class ClipFiles
{
    private static readonly HashSet<string> ImageExts =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };

    private static readonly HashSet<string> DocExts =
        new(StringComparer.OrdinalIgnoreCase) { ".txt", ".pdf", ".md", ".doc", ".docx", ".rtf", ".csv" };

    public const string ImageFilter = "Images|*.jpg;*.jpeg;*.png;*.gif;*.bmp";
    public const string DocumentFilter = "Documents|*.txt;*.pdf;*.md;*.doc;*.docx;*.rtf;*.csv";

    public static bool IsImage(string path) => ImageExts.Contains(Path.GetExtension(path));

    /// <summary>Returns the clip type for a path, or null if it isn't a supported file.</summary>
    public static ClipType? Classify(string path)
    {
        var ext = Path.GetExtension(path);
        if (ImageExts.Contains(ext)) return ClipType.Image;
        if (DocExts.Contains(ext)) return ClipType.Document;
        return null;
    }
}
