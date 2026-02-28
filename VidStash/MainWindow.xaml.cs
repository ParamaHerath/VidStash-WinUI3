using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using VidStash.Views;
using VidStash.ViewModels;

namespace VidStash
{
    public sealed partial class MainWindow : Window
    {
        private bool _navReady;
        private LibraryViewModel? _libraryVm;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                ExtendsContentIntoTitleBar = true;
                SetTitleBar(AppTitleBar);

                _libraryVm = App.GetService<LibraryViewModel>();

                // Navigate first, then allow SelectionChanged to fire
                ContentFrame.Navigate(typeof(LibraryPage));
                ContentFrame.Navigated += ContentFrame_Navigated;

                // Mark the first nav-item as selected without firing SelectionChanged
                NavView.Loaded += (_, _) =>
                {
                    NavView.SelectedItem = NavView.MenuItems[0];
                    _navReady = true;
                    LoadFolders();
                    _ = StartupScanAsync();
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Constructor failed: {ex}");
                throw;
            }
        }

        private void UpdateSearchPlaceholder(string view)
        {
            TitleBarSearchBox.PlaceholderText = view switch
            {
                "Movies" => "Search movies...",
                "Series" => "Search TV series...",
                "Unwatched" => "Search unwatched...",
                "Folder" => "Search in folder...",
                _ => "Search movies and series..."
            };
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.CanGoBack)
                ContentFrame.GoBack();
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            NavView.IsPaneOpen = !NavView.IsPaneOpen;
        }

        private void TitleBarSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (_libraryVm == null) return;
            _libraryVm.SearchQuery = sender.Text;
        }

        private void AnimateBackButton(bool show)
        {
            var backButtonStoryboard = new Storyboard();
            var appTitleStoryboard = new Storyboard();

            // Animate back button opacity
            var backButtonOpacity = new DoubleAnimation
            {
                To = show ? 1.0 : 0.0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(backButtonOpacity, BackButton);
            Storyboard.SetTargetProperty(backButtonOpacity, "Opacity");
            backButtonStoryboard.Children.Add(backButtonOpacity);

            // Animate app title translation (reduced from 48 to 0 to account for smaller back button)
            var appTitleTranslation = new DoubleAnimation
            {
                To = show ? 0 : 0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(appTitleTranslation, AppTitleTransform);
            Storyboard.SetTargetProperty(appTitleTranslation, "X");
            appTitleStoryboard.Children.Add(appTitleTranslation);

            if (show)
            {
                BackButton.Visibility = Visibility.Visible;
            }
            else
            {
                backButtonStoryboard.Completed += (s, e) => BackButton.Visibility = Visibility.Collapsed;
            }

            backButtonStoryboard.Begin();
            appTitleStoryboard.Begin();
        }

        private async void LoadFolders()
        {
            try
            {
                var libraryVm = App.GetService<LibraryViewModel>();

                // Clear existing folder items (everything after the separator)
                var separatorIndex = -1;
                for (int i = 0; i < NavView.MenuItems.Count; i++)
                {
                    if (NavView.MenuItems[i] is NavigationViewItemSeparator)
                    {
                        separatorIndex = i;
                        break;
                    }
                }

                if (separatorIndex >= 0)
                {
                    // Remove items after header
                    for (int i = NavView.MenuItems.Count - 1; i > separatorIndex + 1; i--)
                    {
                        NavView.MenuItems.RemoveAt(i);
                    }
                }

                // Add folder items
                foreach (var folder in libraryVm.Folders)
                {
                    var folderItem = new NavigationViewItem
                    {
                        Content = folder.Name,
                        Tag = $"Folder:{folder.Path}",
                        Icon = new FontIcon { Glyph = "\uE8B7" }
                    };
                    NavView.MenuItems.Add(folderItem);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] LoadFolders failed: {ex}");
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            var canGoBack = ContentFrame.CanGoBack;
            AnimateBackButton(canGoBack);
            NavView.IsBackEnabled = canGoBack;
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            // Ignore selection events that fire before the initial navigation is done
            if (!_navReady) return;

            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
                return;
            }

            if (args.SelectedItem is NavigationViewItem item)
            {
                var tag = item.Tag?.ToString();

                switch (tag)
                {
                    case "Movies":
                    case "Series":
                    case "Unwatched":
                        UpdateSearchPlaceholder(tag);
                        if (ContentFrame.Content is LibraryPage existingPage)
                        {
                            existingPage.SetView(tag);
                        }
                        else
                        {
                            ContentFrame.Navigate(typeof(LibraryPage));
                            if (ContentFrame.Content is LibraryPage newPage)
                                newPage.SetView(tag);
                        }
                        break;

                    default:
                        if (tag?.StartsWith("Folder:") == true)
                        {
                            var folderPath = tag["Folder:".Length..];
                            UpdateSearchPlaceholder("Folder");
                            if (ContentFrame.Content is LibraryPage folderPage)
                                folderPage.SetFolderView(folderPath);
                            else
                            {
                                ContentFrame.Navigate(typeof(LibraryPage));
                                if (ContentFrame.Content is LibraryPage newFolderPage)
                                    newFolderPage.SetFolderView(folderPath);
                            }
                        }
                        break;
                }
            }
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer is NavigationViewItem item)
            {
                var tag = item.Tag?.ToString();
                switch (tag)
                {
                    case "AddFolder":
                        _ = AddFolderAsync();
                        break;
                    case "RefreshLibrary":
                        _ = ManualRefreshAsync();
                        break;
                }
            }
        }

        private async Task AddFolderAsync()
        {
            try
            {
                var libraryVm = App.GetService<LibraryViewModel>();
                await libraryVm.AddFolderCommand.ExecuteAsync(null);
                LoadFolders(); // Refresh folder list
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] AddFolderAsync failed: {ex}");
            }
        }

        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame.CanGoBack)
                ContentFrame.GoBack();
        }

        public NavigationView GetNavigationView() => NavView;

        public void RefreshFolders()
        {
            LoadFolders();
            if (ContentFrame.Content is LibraryPage page)
            {
                page.RefreshView();
            }
        }

        private async Task StartupScanAsync()
        {
            try
            {
                if (_libraryVm == null) return;

                await _libraryVm.InitializeAsync();

                if (_libraryVm.Folders.Count == 0) return;

                ScanToastInfoBar.Title = "Scanning library";
                ScanToastInfoBar.Message = "Checking for new media...";
                ScanToastInfoBar.Severity = InfoBarSeverity.Informational;
                ScanToastInfoBar.IsClosable = false;
                ScanToastInfoBar.IsOpen = true;

                var (moviesBefore, seriesBefore, moviesAfter, seriesAfter) = await _libraryVm.StartupScanAsync();

                if (ContentFrame.Content is LibraryPage page)
                {
                    page.RefreshView();
                }

                await ShowScanResultAsync(moviesBefore, seriesBefore, moviesAfter, seriesAfter);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] StartupScan failed: {ex}");
                ScanToastInfoBar.IsOpen = false;
            }
        }

        private async Task ManualRefreshAsync()
        {
            try
            {
                if (_libraryVm == null || _libraryVm.Folders.Count == 0) return;

                ScanToastInfoBar.Title = "Refreshing library";
                ScanToastInfoBar.Message = "Scanning all folders...";
                ScanToastInfoBar.Severity = InfoBarSeverity.Informational;
                ScanToastInfoBar.IsClosable = false;
                ScanToastInfoBar.IsOpen = true;

                var (moviesBefore, seriesBefore, moviesAfter, seriesAfter) = await _libraryVm.StartupScanAsync();

                if (ContentFrame.Content is LibraryPage page)
                {
                    page.RefreshView();
                }

                await ShowScanResultAsync(moviesBefore, seriesBefore, moviesAfter, seriesAfter);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] ManualRefresh failed: {ex}");
                ScanToastInfoBar.IsOpen = false;
            }
        }

        private async Task ShowScanResultAsync(int moviesBefore, int seriesBefore, int moviesAfter, int seriesAfter)
        {
            var addedMovies = Math.Max(0, moviesAfter - moviesBefore);
            var removedMovies = Math.Max(0, moviesBefore - moviesAfter);
            var addedSeries = Math.Max(0, seriesAfter - seriesBefore);
            var removedSeries = Math.Max(0, seriesBefore - seriesAfter);

            bool hasChanges = addedMovies > 0 || removedMovies > 0 || addedSeries > 0 || removedSeries > 0;

            if (hasChanges)
            {
                var parts = new List<string>();
                if (addedMovies > 0) parts.Add($"{addedMovies} new movie{(addedMovies != 1 ? "s" : "")}");
                if (addedSeries > 0) parts.Add($"{addedSeries} new series");
                if (removedMovies > 0) parts.Add($"{removedMovies} movie{(removedMovies != 1 ? "s" : "")} removed");
                if (removedSeries > 0) parts.Add($"{removedSeries} series removed");

                ScanToastInfoBar.Title = "Scan complete";
                ScanToastInfoBar.Message = string.Join(", ", parts);
                ScanToastInfoBar.Severity = InfoBarSeverity.Success;
            }
            else
            {
                ScanToastInfoBar.Title = "Library is up to date";
                ScanToastInfoBar.Message = string.Empty;
                ScanToastInfoBar.Severity = InfoBarSeverity.Informational;
            }

            ScanToastInfoBar.IsClosable = true;
            await Task.Delay(4000);
            ScanToastInfoBar.IsOpen = false;
        }
    }
}
