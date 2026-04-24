namespace IoTSpy.Api;

public static class AssetsPaths
{
    public static string AssetsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "data", "assets");
}
