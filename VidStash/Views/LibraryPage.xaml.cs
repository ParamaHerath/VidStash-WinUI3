using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using VidStash.Models;
using VidStash.ViewModels;

namespace VidStash.Views;

public sealed partial class LibraryPage : Page
{
    public LibraryViewModel ViewModel { get; }

    private DispatcherTimer? _searchDebounce;

    public LibraryPage()
    {
        ViewModel = App.GetService<LibraryViewModel>();
        InitializeComponent();

        // Subscribe to collection changes to update count
        ViewModel.Movies.CollectionChanged += (s, e) => UpdateCountCue();
        ViewModel.Series.CollectionChanged += (s, e) => UpdateCountCue();

        // Refresh view state when folder availability changes
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        System.Diagnostics.Debug.WriteLine("[LibraryPage] OnNavigatedTo starting...");
        try
        {
            System.Diagnostics.Debug.WriteLine("[LibraryPage] Calling ViewModel.InitializeAsync...");
            await ViewModel.InitializeAsync();
            UpdateGenreCombo();
            UpdateCountCue();
            System.Diagnostics.Debug.WriteLine("[LibraryPage] OnNavigatedTo complete");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LibraryPage] OnNavigatedTo FAILED: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[LibraryPage] Stack: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"[LibraryPage] Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
        }
    }

    private void UpdateGenreCombo()
    {
        GenreCombo.Items.Clear();
        GenreCombo.Items.Add(new ComboBoxItem { Content = "All Genres" });
        foreach (var genre in ViewModel.AvailableGenres)
        {
            GenreCombo.Items.Add(new ComboBoxItem { Content = genre });
        }
        GenreCombo.SelectedIndex = 0;
    }

    private void UpdateCountCue()
    {
        var movieCount = ViewModel.Movies.Count;
        var seriesCount = ViewModel.Series.Count;

        if (ViewModel.SelectedView == "Movies")
        {
            CountCueText.Text = movieCount == 1 ? "1 Movie found" : $"{movieCount} Movies found";
        }
        else if (ViewModel.SelectedView == "Series")
        {
            CountCueText.Text = seriesCount == 1 ? "1 TV Series found" : $"{seriesCount} TV Series found";
        }
        else if (ViewModel.SelectedView == "Unwatched" || ViewModel.SelectedView == "Folder")
        {
            var parts = new List<string>();
            if (movieCount > 0)
                parts.Add(movieCount == 1 ? "1 Movie" : $"{movieCount} Movies");
            if (seriesCount > 0)
                parts.Add(seriesCount == 1 ? "1 TV Series" : $"{seriesCount} TV Series");

            if (parts.Count == 0)
                CountCueText.Text = "No media found";
            else
                CountCueText.Text = string.Join(" and ", parts) + " found";
        }
        else
        {
            CountCueText.Text = $"{movieCount + seriesCount} items found";
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LibraryViewModel.HasNoFolders))
        {
            if (ViewModel.SelectedView == "Folder" && !string.IsNullOrEmpty(ViewModel.SelectedFolderPath))
                SetFolderView(ViewModel.SelectedFolderPath);
            else
                SetView(ViewModel.SelectedView);
        }
    }

    public void RefreshView()
    {
        UpdateGenreCombo();
        if (ViewModel.SelectedView == "Folder" && !string.IsNullOrEmpty(ViewModel.SelectedFolderPath))
            SetFolderView(ViewModel.SelectedFolderPath);
        else
            SetView(ViewModel.SelectedView);
    }

    public void SetView(string view)
    {
        ViewModel.SelectedView = view;
        ViewModel.SelectedFolderPath = null; // Clear folder filter

        if (view == "Series")
        {
            MoviesGrid.Visibility = Visibility.Collapsed;
            SeriesGrid.Visibility = ViewModel.HasNoFolders ? Visibility.Collapsed : Visibility.Visible;
            MoviesSectionHeader.Visibility = Visibility.Collapsed;
            SeriesSectionHeader.Visibility = Visibility.Collapsed;
            ViewModel.ApplySeriesFilters();
        }
        else if (view == "Unwatched")
        {
            // Show both Movies and Series for Unwatched with headers
            MoviesGrid.Visibility = ViewModel.HasNoFolders ? Visibility.Collapsed : Visibility.Visible;
            SeriesGrid.Visibility = ViewModel.HasNoFolders ? Visibility.Collapsed : Visibility.Visible;
            MoviesSectionHeader.Visibility = Visibility.Visible;
            SeriesSectionHeader.Visibility = Visibility.Visible;
            ViewModel.ApplyFilters();
            ViewModel.ApplySeriesFilters();
        }
        else
        {
            MoviesGrid.Visibility = ViewModel.HasNoFolders ? Visibility.Collapsed : Visibility.Visible;
            SeriesGrid.Visibility = Visibility.Collapsed;
            MoviesSectionHeader.Visibility = Visibility.Collapsed;
            SeriesSectionHeader.Visibility = Visibility.Collapsed;
            ViewModel.ApplyFilters();
        }

        UpdateCountCue();
    }

    public void SetFolderView(string folderPath)
    {
        ViewModel.SelectedView = "Folder";
        ViewModel.SelectedFolderPath = folderPath;

        // Show both Movies and Series for folder view with headers
        MoviesGrid.Visibility = Visibility.Visible;
        SeriesGrid.Visibility = Visibility.Visible;
        MoviesSectionHeader.Visibility = Visibility.Visible;
        SeriesSectionHeader.Visibility = Visibility.Visible;

        ViewModel.ApplyFilters();
        ViewModel.ApplySeriesFilters();
        UpdateCountCue();
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // Search now handled in MainWindow titlebar
    }

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortCombo.SelectedItem is ComboBoxItem item)
        {
            ViewModel.SelectedSort = item.Content?.ToString() ?? "Title";
            UpdateCountCue();
        }
    }

    private void GenreCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GenreCombo.SelectedItem is ComboBoxItem item)
        {
            var content = item.Content?.ToString();
            ViewModel.SelectedGenre = content == "All Genres" ? null : content;
            UpdateCountCue();
        }
    }

    private void MoviesGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Movie movie)
        {
            Frame.Navigate(typeof(MovieDetailPage), movie, new SlideNavigationTransitionInfo
            {
                Effect = SlideNavigationTransitionEffect.FromRight
            });
        }
    }

    private void SeriesGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TvSeries series)
        {
            Frame.Navigate(typeof(SeriesDetailPage), series, new SlideNavigationTransitionInfo
            {
                Effect = SlideNavigationTransitionEffect.FromRight
            });
        }
    }

    private void PosterCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
        {
            var overlay = FindChild<Grid>(grid, "HoverOverlay");
            var playButton = FindChild<Button>(grid, "PlayButton");
            var unwatchedBadge = FindChild<Button>(grid, "UnwatchedBadgeButton");

            if (overlay != null)
            {
                overlay.Opacity = 1;
            }

            if (playButton != null)
            {
                playButton.Visibility = Visibility.Visible;
            }

            if (unwatchedBadge != null)
            {
                bool isWatched = false;
                if (grid.DataContext is Movie m) isWatched = m.Watched;
                else if (grid.DataContext is TvSeries s) isWatched = s.Watched;

                if (!isWatched)
                {
                    unwatchedBadge.Visibility = Visibility.Visible;
                }
            }

            // Animate the scale transform
            if (grid.RenderTransform is CompositeTransform transform)
            {
                var scaleXAnim = new DoubleAnimation
                {
                    To = 1.05,
                    Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var scaleYAnim = new DoubleAnimation
                {
                    To = 1.05,
                    Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var storyboard = new Storyboard();
                Storyboard.SetTarget(scaleXAnim, transform);
                Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
                Storyboard.SetTarget(scaleYAnim, transform);
                Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");

                storyboard.Children.Add(scaleXAnim);
                storyboard.Children.Add(scaleYAnim);
                storyboard.Begin();
            }
        }
    }

    private void PosterCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
        {
            var overlay = FindChild<Grid>(grid, "HoverOverlay");
            var playButton = FindChild<Button>(grid, "PlayButton");
            var unwatchedBadge = FindChild<Button>(grid, "UnwatchedBadgeButton");

            if (overlay != null)
            {
                overlay.Opacity = 0;
            }

            if (playButton != null)
            {
                playButton.Visibility = Visibility.Collapsed;
            }

            if (unwatchedBadge != null)
            {
                unwatchedBadge.Visibility = Visibility.Collapsed;
            }

            // Animate back to normal scale
            if (grid.RenderTransform is CompositeTransform transform)
            {
                var scaleXAnim = new DoubleAnimation
                {
                    To = 1.0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var scaleYAnim = new DoubleAnimation
                {
                    To = 1.0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var storyboard = new Storyboard();
                Storyboard.SetTarget(scaleXAnim, transform);
                Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
                Storyboard.SetTarget(scaleYAnim, transform);
                Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");

                storyboard.Children.Add(scaleXAnim);
                storyboard.Children.Add(scaleYAnim);
                storyboard.Begin();
            }
        }
    }

    private static T? FindChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T element && element.Name == name)
                return element;
            var result = FindChild<T>(child, name);
            if (result != null) return result;
        }
        return null;
    }

    // Context menu handlers
    private async void PlayMovie_Click(object sender, RoutedEventArgs e)
    {
        if (GetMovieFromContext(sender) is Movie movie)
            await ViewModel.PlayMovieCommand.ExecuteAsync(movie);
    }

    private async void ToggleWatchedMovie_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Name == "UnwatchedBadgeButton")
        {
            element.Visibility = Visibility.Collapsed;
        }

        if (GetMovieFromContext(sender) is Movie movie)
            await ViewModel.ToggleWatchedMovieCommand.ExecuteAsync(movie);
    }

    private void MovieDetails_Click(object sender, RoutedEventArgs e)
    {
        if (GetMovieFromContext(sender) is Movie movie)
        {
            Frame.Navigate(typeof(MovieDetailPage), movie, new SlideNavigationTransitionInfo
            {
                Effect = SlideNavigationTransitionEffect.FromRight
            });
        }
    }

    private void RefreshMovie_Click(object sender, RoutedEventArgs e)
    {
        // Would need to re-fetch TMDB metadata
    }

    private async void DeleteMovie_Click(object sender, RoutedEventArgs e)
    {
        if (GetMovieFromContext(sender) is Movie movie)
            await ViewModel.DeleteMovieCommand.ExecuteAsync(movie);
    }

    private async void ToggleWatchedSeries_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Name == "UnwatchedBadgeButton")
        {
            element.Visibility = Visibility.Collapsed;
        }

        if (GetSeriesFromContext(sender) is TvSeries series)
            await ViewModel.ToggleWatchedSeriesCommand.ExecuteAsync(series);
    }

    private void SeriesDetails_Click(object sender, RoutedEventArgs e)
    {
        if (GetSeriesFromContext(sender) is TvSeries series)
        {
            Frame.Navigate(typeof(SeriesDetailPage), series, new SlideNavigationTransitionInfo
            {
                Effect = SlideNavigationTransitionEffect.FromRight
            });
        }
    }

    private void RefreshSeries_Click(object sender, RoutedEventArgs e) { }

    private async void DeleteSeries_Click(object sender, RoutedEventArgs e)
    {
        if (GetSeriesFromContext(sender) is TvSeries series)
            await ViewModel.DeleteSeriesCommand.ExecuteAsync(series);
    }

    private void MovieCard_RightTapped(object sender, RightTappedRoutedEventArgs e) { }
    private void SeriesCard_RightTapped(object sender, RightTappedRoutedEventArgs e) { }

    private async void PlayMovieButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetMovieFromContext(sender) is Movie movie)
            await ViewModel.PlayMovieCommand.ExecuteAsync(movie);
    }

    private async void PlaySeries_Click(object sender, RoutedEventArgs e)
    {
        if (GetSeriesFromContext(sender) is TvSeries series)
        {
            // Navigate to series detail where user can select episode
            Frame.Navigate(typeof(SeriesDetailPage), series, new SlideNavigationTransitionInfo
            {
                Effect = SlideNavigationTransitionEffect.FromRight
            });
        }
    }

    private void MoviesGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (MoviesGrid.ItemsPanelRoot is ItemsWrapGrid wrapGrid)
        {
            double availableWidth = e.NewSize.Width - 48; // Padding is 24 on left and right
            if (availableWidth <= 0) return;

            int columns = Math.Max(1, (int)Math.Floor(availableWidth / 190.0));
            double itemWidth = availableWidth / columns;
            double itemHeight = itemWidth * (275.0 / 190.0);

            wrapGrid.ItemWidth = itemWidth;
            wrapGrid.ItemHeight = itemHeight;
        }
    }

    private void SeriesGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (SeriesGrid.ItemsPanelRoot is ItemsWrapGrid wrapGrid)
        {
            double availableWidth = e.NewSize.Width - 48; // Padding is 24 on left and right
            if (availableWidth <= 0) return;

            int columns = Math.Max(1, (int)Math.Floor(availableWidth / 190.0));
            double itemWidth = availableWidth / columns;
            double itemHeight = itemWidth * (275.0 / 190.0);

            wrapGrid.ItemWidth = itemWidth;
            wrapGrid.ItemHeight = itemHeight;
        }
    }

    private static Movie? GetMovieFromContext(object sender) =>
        (sender as FrameworkElement)?.DataContext as Movie;

    private static TvSeries? GetSeriesFromContext(object sender) =>
        (sender as FrameworkElement)?.DataContext as TvSeries;
}
