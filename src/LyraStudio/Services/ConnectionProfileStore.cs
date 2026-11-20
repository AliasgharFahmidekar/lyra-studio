using System.IO;
using System.Text.Json;
using LyraStudio.Models;

namespace LyraStudio.Services;

public sealed class ConnectionProfileStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LyraStudio");
    private static readonly string FilePath = Path.Combine(Folder, "connection.json");

    public async Task<StudioConnectionProfile> LoadAsync()
    {
        if (!File.Exists(FilePath)) return new StudioConnectionProfile();
        await using var stream = File.OpenRead(FilePath);
        return await JsonSerializer.DeserializeAsync<StudioConnectionProfile>(stream)
            ?? new StudioConnectionProfile();
    }

    public async Task SaveAsync(StudioConnectionProfile profile)
    {
        Directory.CreateDirectory(Folder);
        await using var stream = File.Create(FilePath);
        await JsonSerializer.SerializeAsync(stream, profile, new JsonSerializerOptions { WriteIndented = true });
    }
}
