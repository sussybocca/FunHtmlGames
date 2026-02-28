using Microsoft.JSInterop;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace FunHtmlGames;

public static class CS_Libraries
{
    // Library: "filecopier" – copies a file within the game's storage
    public static class FileCopier
    {
        public static async Task Copy(string parameters, IJSRuntime js, string gameId)
        {
            // Format: "sourcePath|destPath"
            var parts = parameters.Split('|');
            if (parts.Length != 2)
                throw new ArgumentException("FileCopier requires exactly two parameters separated by '|'.");

            var source = parts[0].Trim();
            var dest = parts[1].Trim();

            var fileBytes = await js.InvokeAsync<byte[]>("gameStore.getGameFile", gameId, source);
            if (fileBytes == null)
                throw new Exception($"Source file '{source}' not found.");

            await js.InvokeVoidAsync("gameStore.saveGameFile", gameId, dest, fileBytes);
        }
    }

    // Library: "windowregister" – parses a .Window file and stores styles
    public static class WindowRegister
    {
        public static async Task Register(string parameters, IJSRuntime js, string gameId)
        {
            var fileName = parameters.Trim();
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("WindowRegister requires a .Window file name.");

            var fileBytes = await js.InvokeAsync<byte[]>("gameStore.getGameFile", gameId, fileName);
            if (fileBytes == null)
                throw new Exception($"Window file '{fileName}' not found.");

            var content = System.Text.Encoding.UTF8.GetString(fileBytes);
            var parser = new Services.WindowFileParser();
            var windowStyles = parser.Parse(content);

            await js.InvokeVoidAsync("gameStore.setWindowStyles", gameId, windowStyles);
        }
    }

    // Library: "pwaregister" – reads and stores the game's manifest.json (or specified manifest)
    public static class PWARegister
    {
        public static async Task Register(string parameters, IJSRuntime js, string gameId)
        {
            // parameters can be the path to the manifest file (default "manifest.json")
            var manifestPath = string.IsNullOrWhiteSpace(parameters) ? "manifest.json" : parameters.Trim();
            var fileBytes = await js.InvokeAsync<byte[]>("gameStore.getGameFile", gameId, manifestPath);
            if (fileBytes == null)
                throw new Exception($"Manifest file '{manifestPath}' not found.");

            var content = System.Text.Encoding.UTF8.GetString(fileBytes);
            // Parse as JsonElement to store the raw manifest object
            using JsonDocument doc = JsonDocument.Parse(content);
            var manifest = doc.RootElement.Clone(); // Detach from document

            await js.InvokeVoidAsync("gameStore.setManifest", gameId, manifest);
        }
    }

    // Library: "examplecs" – example that writes to console
    public static class ExampleCS
    {
        public static async Task Run(string parameters, IJSRuntime js, string gameId)
        {
            await js.InvokeVoidAsync("console.log", $"[ExampleCS] Game {gameId} with params: {parameters}");
        }
    }
}