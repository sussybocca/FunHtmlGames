using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace FunHtmlGames.Services;

public class CustomInstallParser
{
    private readonly LibraryLoader _loader;
    private readonly IJSRuntime _js;
    private readonly string _gameId;

    public CustomInstallParser(LibraryLoader loader, IJSRuntime js, string gameId)
    {
        _loader = loader;
        _js = js;
        _gameId = gameId;
    }

    // Expected line format:
    // )*FUNCTION(* )O<IMPORT_1)>CS):Library*libraryName|parameters
    public async Task ParseAndExecuteAsync(string scriptContent)
    {
        var lines = scriptContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var match = Regex.Match(line, @"^\)\*FUNCTION\(\*.*?\)O<IMPORT_\d+>\)CS\):Library\*(?<lib>[a-zA-Z0-9_]+)(\|(?<params>.*))?$");
            if (match.Success)
            {
                var libName = match.Groups["lib"].Value;
                var parameters = match.Groups["params"].Success ? match.Groups["params"].Value : "";
                await _loader.ExecuteAsync(libName, parameters, _js, _gameId);
            }
            else
            {
                // Log unrecognized lines for debugging
                await _js.InvokeVoidAsync("console.warn", $"Unrecognized install line: {line}");
            }
        }
    }
}