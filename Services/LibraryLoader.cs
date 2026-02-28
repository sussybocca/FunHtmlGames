using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace FunHtmlGames.Services;

public class LibraryLoader
{
    public async Task ExecuteAsync(string libraryName, string parameters, IJSRuntime js, string gameId)
    {
        var type = typeof(CS_Libraries).GetNestedType(libraryName, BindingFlags.Public | BindingFlags.Static);
        if (type == null)
            throw new Exception($"Library '{libraryName}' not found.");

        // Look for a public static method with signature (string, IJSRuntime, string)
        // We'll try common names: Run, Copy, Register
        string[] possibleMethodNames = { "Run", "Copy", "Register" };
        MethodInfo? method = null;
        foreach (var name in possibleMethodNames)
        {
            method = type.GetMethod(name, new[] { typeof(string), typeof(IJSRuntime), typeof(string) });
            if (method != null) break;
        }

        if (method == null)
            throw new Exception($"No suitable method found in library '{libraryName}'.");

        var task = (Task)method.Invoke(null, new object[] { parameters, js, gameId })!;
        await task;
    }
}