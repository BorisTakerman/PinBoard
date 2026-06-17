using System.IO;
using System.Text.Json;
using PinBoard.Models;
using PinBoard.ViewModels;

namespace PinBoard.Services;

/// <summary>
/// Reads and writes boards as JSON (.board files). The ViewModel already maps
/// to/from <see cref="BoardData"/>; this just handles the file I/O.
/// </summary>
public static class BoardSerializer
{
    public const string Extension = ".board";
    public const string Filter = "PinBoard board (*.board)|*.board";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Writes the board to <paramref name="path"/>, updating its FilePath/Name.</summary>
    public static void Save(BoardViewModel board, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Set FilePath first so clip paths serialize relative to the new location.
        board.FilePath = path;
        board.Name = Path.GetFileNameWithoutExtension(path);

        File.WriteAllText(path, JsonSerializer.Serialize(board.ToData(), Options));
    }

    /// <summary>Loads a board from <paramref name="path"/>.</summary>
    public static BoardViewModel Load(string path)
    {
        var data = JsonSerializer.Deserialize<BoardData>(File.ReadAllText(path)) ?? new BoardData();
        return BoardViewModel.FromData(data, path);
    }

    /// <summary>Strips characters that aren't valid in a file name.</summary>
    public static string SafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Untitled";
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
