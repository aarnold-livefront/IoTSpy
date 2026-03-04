using IoTSpy.Core.Models;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Manipulation;

/// <summary>
/// Executes C# Roslyn scripts against HTTP messages.
/// Scripts receive a global <see cref="ScriptGlobals"/> object and can modify the message in-place.
/// </summary>
public class CSharpScriptEngine(ILogger<CSharpScriptEngine> logger)
{
    private static readonly ScriptOptions DefaultOptions = ScriptOptions.Default
        .AddReferences(typeof(HttpMessage).Assembly)
        .AddImports("System", "System.Text.RegularExpressions", "IoTSpy.Core.Models");

    public async Task<bool> ExecuteAsync(string scriptCode, HttpMessage message, CancellationToken ct = default)
    {
        try
        {
            var globals = new ScriptGlobals { Message = message };
            await CSharpScript.RunAsync(scriptCode, DefaultOptions, globals, cancellationToken: ct);
            return globals.Modified;
        }
        catch (CompilationErrorException ex)
        {
            logger.LogWarning("C# script compilation error: {Errors}", string.Join(", ", ex.Diagnostics));
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "C# script execution error");
            return false;
        }
    }
}

/// <summary>
/// Global variables exposed to C# scripts.
/// Scripts set <see cref="Modified"/> to true to signal that the message was changed.
/// </summary>
public class ScriptGlobals
{
    public HttpMessage Message { get; set; } = null!;
    public bool Modified { get; set; }
}
