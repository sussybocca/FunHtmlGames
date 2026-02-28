using Microsoft.JSInterop;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace FunHtmlGames.Services;

public class GameManager
{
    private readonly IJSRuntime _js;
    private readonly LibraryLoader _loader;

    public GameManager(IJSRuntime js, LibraryLoader loader)
    {
        _js = js;
        _loader = loader;
    }

    public async Task AddGameFromUpload(string gameId, string gameName, List<string> filePaths, List<byte[]> fileContents)
    {
        await _js.InvokeVoidAsync("gameStore.saveGame", gameId, gameName, filePaths, fileContents);
    }

    public async Task<List<GameInfo>> GetAllGames()
    {
        return await _js.InvokeAsync<List<GameInfo>>("gameStore.getAllGames");
    }

    public async Task<GameInfo?> GetGame(string gameId)
    {
        return await _js.InvokeAsync<GameInfo?>("gameStore.getGame", gameId);
    }

    public async Task<byte[]?> GetGameFile(string gameId, string filePath)
    {
        return await _js.InvokeAsync<byte[]?>("gameStore.getGameFile", gameId, filePath);
    }

    public async Task InstallGame(string gameId)
    {
        var game = await GetGame(gameId);
        if (game == null) throw new Exception("Game not found.");

        // Look for custom-installation.txt
        var installScriptBytes = await GetGameFile(gameId, "custom-installation.txt");
        if (installScriptBytes != null)
        {
            var script = System.Text.Encoding.UTF8.GetString(installScriptBytes);
            var parser = new CustomInstallParser(_loader, _js, gameId);
            await parser.ParseAndExecuteAsync(script);
        }

        // Mark as installed
        await _js.InvokeVoidAsync("gameStore.setInstalled", gameId, true);
    }

    public async Task<Dictionary<string, string>?> GetWindowStyles(string gameId)
    {
        return await _js.InvokeAsync<Dictionary<string, string>?>("gameStore.getWindowStyles", gameId);
    }

    public async Task<JsonElement?> GetManifest(string gameId)
    {
        return await _js.InvokeAsync<JsonElement?>("gameStore.getManifest", gameId);
    }
}

public class GameInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsInstalled { get; set; }
    public List<string> Files { get; set; } = new();
    public Dictionary<string, string>? WindowStyles { get; set; }
    public JsonElement? Manifest { get; set; } // Stores the game's manifest.json content
}