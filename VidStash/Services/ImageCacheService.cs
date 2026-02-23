namespace VidStash.Services;

public class ImageCacheService
{
    private readonly HttpClient _http;
    private readonly string _cacheDir;

    public ImageCacheService(HttpClient http)
    {
        _http = http;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cacheDir = Path.Combine(appData, "VidStash", "ImageCache");
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<string?> GetCachedImageAsync(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;

        var fileName = GetSafeFileName(url);
        var localPath = Path.Combine(_cacheDir, fileName);

        if (File.Exists(localPath))
            return localPath;

        try
        {
            var bytes = await _http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(localPath, bytes);
            return localPath;
        }
        catch
        {
            return null;
        }
    }

    public string? GetCachedPath(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        var fileName = GetSafeFileName(url);
        var localPath = Path.Combine(_cacheDir, fileName);
        return File.Exists(localPath) ? localPath : null;
    }

    public void ClearCache()
    {
        try
        {
            if (Directory.Exists(_cacheDir))
            {
                Directory.Delete(_cacheDir, true);
                Directory.CreateDirectory(_cacheDir);
            }
        }
        catch { }
    }

    public long GetCacheSize()
    {
        if (!Directory.Exists(_cacheDir)) return 0;
        return new DirectoryInfo(_cacheDir).EnumerateFiles("*", SearchOption.AllDirectories)
            .Sum(f => f.Length);
    }

    private static string GetSafeFileName(string url)
    {
        var uri = new Uri(url);
        var path = uri.AbsolutePath.TrimStart('/').Replace('/', '_');
        return path;
    }
}
