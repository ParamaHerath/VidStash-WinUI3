using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VidStash.Models;
using VidStash.ViewModels;

namespace VidStash.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }
    private LibraryViewModel? _libraryVm;

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            await ViewModel.InitializeAsync();
            _libraryVm = App.GetService<LibraryViewModel>();

            ApiKeyBox.Password = ViewModel.ApiKey;
            ApiKeyStatusText.Text = ViewModel.ApiKeyStatus;
            CacheSizeText.Text = $"Cache size: {ViewModel.CacheSize}";

            foreach (var item in ThemeRadio.Items)
            {
                if (item is RadioButton rb && rb.Tag?.ToString() == ViewModel.SelectedTheme)
                {
                    rb.IsChecked = true;
                    break;
                }
            }

            // Bind folder list from the already-initialized library VM (no re-init)
            FoldersList.ItemsSource = _libraryVm.Folders;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsPage] load failed: {ex}");
        }
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.ApiKey = ApiKeyBox.Password;
    }

    private async void SaveApiKey_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveApiKeyCommand.ExecuteAsync(null);
        ApiKeyStatusText.Text = ViewModel.ApiKeyStatus;
    }

    private async void ThemeRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeRadio.SelectedItem is RadioButton rb && rb.Tag is string theme)
        {
            await ViewModel.SetThemeCommand.ExecuteAsync(theme);
        }
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_libraryVm != null)
        {
            await _libraryVm.AddFolderCommand.ExecuteAsync(null);
            FoldersList.ItemsSource = _libraryVm.Folders;
        }
    }

    private async void RescanFolder_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is Folder folder && _libraryVm != null)
        {
            await _libraryVm.ScanFolderCommand.ExecuteAsync(folder.Path);
        }
    }

    private async void RemoveFolder_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is Folder folder && _libraryVm != null)
        {
            await _libraryVm.RemoveFolderCommand.ExecuteAsync(folder);
            FoldersList.ItemsSource = _libraryVm.Folders;
        }
    }

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearCacheCommand.Execute(null);
        CacheSizeText.Text = $"Cache size: {ViewModel.CacheSize}";
    }

    private async void ClearDatabase_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear Database",
            Content = "This will remove all movies, series, episodes, and folders from the database. Your actual files will not be deleted.\n\nAre you sure?",
            PrimaryButtonText = "Clear Everything",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.ClearDatabaseCommand.ExecuteAsync(null);
            CacheSizeText.Text = $"Cache size: {ViewModel.CacheSize}";

            if (_libraryVm != null)
            {
                await _libraryVm.RefreshLibraryAsync();
                FoldersList.ItemsSource = _libraryVm.Folders;
            }

            if (App.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RefreshFolders();
            }
        }
    }
}
