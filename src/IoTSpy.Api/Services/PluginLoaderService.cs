using System.Reflection;
using System.Runtime.Loader;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;

namespace IoTSpy.Api.Services;

public sealed class PluginLoaderService : IPluginRegistry
{
    private readonly string _pluginsDirectory;
    private readonly ILogger<PluginLoaderService> _logger;

    private readonly Lock _lock = new();
    private readonly List<PluginInfo> _plugins = new();
    private readonly Dictionary<string, IPluginDecoder> _decoders = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AssemblyLoadContext> _contexts = new();

    public IReadOnlyList<PluginInfo> LoadedPlugins
    {
        get { lock (_lock) { return _plugins.ToList(); } }
    }

    public PluginLoaderService(IConfiguration configuration, ILogger<PluginLoaderService> logger)
    {
        _pluginsDirectory = configuration["Plugins:Directory"]
            ?? Path.Combine(AppContext.BaseDirectory, "plugins");
        _logger = logger;
    }

    public IPluginDecoder? GetDecoder(string protocol)
    {
        lock (_lock)
        {
            return _decoders.GetValueOrDefault(protocol);
        }
    }

    public void Reload()
    {
        lock (_lock)
        {
            UnloadAll();
            LoadAll();
        }
    }

    private void LoadAll()
    {
        _plugins.Clear();
        _decoders.Clear();

        if (!Directory.Exists(_pluginsDirectory))
        {
            _logger.LogDebug("Plugin directory {Dir} does not exist — no plugins loaded", _pluginsDirectory);
            return;
        }

        foreach (var dll in Directory.EnumerateFiles(_pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            TryLoadAssembly(dll);
        }

        _logger.LogInformation("Loaded {Count} plugin decoder(s) from {Dir}", _decoders.Count, _pluginsDirectory);
    }

    private void TryLoadAssembly(string path)
    {
        var ctx = new PluginAssemblyLoadContext(path);
        _contexts.Add(ctx);

        try
        {
            var assembly = ctx.LoadFromAssemblyPath(path);
            var decoderInterface = typeof(IPluginDecoder);

            foreach (var type in assembly.GetExportedTypes())
            {
                if (!decoderInterface.IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                    continue;

                try
                {
                    var decoder = (IPluginDecoder)Activator.CreateInstance(type)!;
                    _decoders[decoder.Protocol] = decoder;
                    _plugins.Add(new PluginInfo
                    {
                        Protocol = decoder.Protocol,
                        Name = decoder.Name,
                        Version = decoder.Version,
                        AssemblyPath = path,
                        IsLoaded = true
                    });
                    _logger.LogInformation("Registered plugin decoder '{Name}' v{Version} for protocol '{Protocol}'",
                        decoder.Name, decoder.Version, decoder.Protocol);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to instantiate plugin type {Type} from {Path}", type.FullName, path);
                    _plugins.Add(new PluginInfo
                    {
                        Name = type.FullName ?? type.Name,
                        AssemblyPath = path,
                        IsLoaded = false,
                        LoadError = ex.Message
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin assembly {Path}", path);
            _plugins.Add(new PluginInfo
            {
                Name = Path.GetFileNameWithoutExtension(path),
                AssemblyPath = path,
                IsLoaded = false,
                LoadError = ex.Message
            });
        }
    }

    private void UnloadAll()
    {
        foreach (var ctx in _contexts)
        {
            try { ctx.Unload(); }
            catch { /* collectible contexts unload on GC */ }
        }
        _contexts.Clear();
    }

    public void Initialize() => LoadAll();
}

internal sealed class PluginAssemblyLoadContext(string pluginPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }
}
