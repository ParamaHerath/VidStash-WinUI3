using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using VidStash.Services;

namespace VidStash.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly TmdbService _tmdb;
    private readonly ImageCacheService _imageCache;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _apiKeyStatus = string.Empty;

    [ObservableProperty]
    private bool _isApiKeyValid;

    [ObservableProperty]
    private string _selectedTheme = "System";

    [ObservableProperty]
    private string _cacheSize = "0 B";

    [ObservableProperty]
    private string _appVersion = "1.0.0";

    public SettingsViewModel(DatabaseService db, TmdbService tmdb, ImageCacheService imageCache)
    {
        _db = db;
        _tmdb = tmdb;
        _imageCache = imageCache;
    }

    public async Task InitializeAsync()
    {
        var key = await _db.GetSettingAsync("tmdb_api_key");
        if (!string.IsNullOrEmpty(key))
        {
            ApiKey = key;
            IsApiKeyValid = true;
            ApiKeyStatus = "API key is configured";
        }

        var theme = await _db.GetSettingAsync("theme");
        SelectedTheme = theme ?? "System";

        UpdateCacheSize();
    }

    [RelayCommand]
    private async Task SaveApiKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            ApiKeyStatus = "Please enter an API key";
            IsApiKeyValid = false;
            return;
        }

        ApiKeyStatus = "Testing API key...";
        var isValid = await _tmdb.TestApiKeyAsync(ApiKey);

        if (isValid)
        {
            await _db.SetSettingAsync("tmdb_api_key", ApiKey);
            ApiKeyStatus = "✅ API key is valid and saved";
            IsApiKeyValid = true;
        }
        else
        {
            ApiKeyStatus = "❌ Invalid API key";
            IsApiKeyValid = false;
        }
    }

    [RelayCommand]
    private async Task SetThemeAsync(string theme)
    {
        SelectedTheme = theme;
        await _db.SetSettingAsync("theme", theme);

        if (App.MainWindow?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    [RelayCommand]
    private void ClearCache()
    {
        _imageCache.ClearCache();
        UpdateCacheSize();
    }

    [RelayCommand]
    private async Task ClearDatabaseAsync()
    {
        await _db.ClearAllDataAsync();
        _imageCache.ClearCache();
        UpdateCacheSize();
    }

    private void UpdateCacheSize()
    {
        var size = _imageCache.GetCacheSize();
        CacheSize = Helpers.FileHelpers.FormatFileSize(size);
    }
}
