using IoTSpy.Core.Models;
using Jint;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Manipulation;

/// <summary>
/// Executes JavaScript scripts (via Jint) against HTTP messages.
/// Scripts receive 'message' as a JS object and can modify its properties.
/// Set 'modified = true' to signal the message was changed.
/// </summary>
public class JavaScriptEngine(ILogger<JavaScriptEngine> logger)
{
    public Task<bool> ExecuteAsync(string scriptCode, HttpMessage message, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            using var engine = new Engine(opts => opts
                .TimeoutInterval(TimeSpan.FromSeconds(5))
                .MaxStatements(10_000)
                .Strict());

            engine.SetValue("message", message);
            engine.SetValue("modified", false);

            engine.Execute(scriptCode);

            var modified = engine.GetValue("modified").AsBoolean();
            return Task.FromResult(modified);
        }
        catch (Jint.Runtime.JavaScriptException ex)
        {
            logger.LogWarning("JavaScript execution error: {Error}", ex.Message);
            return Task.FromResult(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "JavaScript engine error");
            return Task.FromResult(false);
        }
    }
}
