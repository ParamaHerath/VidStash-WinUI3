using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        System.Diagnostics.Debug.WriteLine("[LibraryPage] OnNavigatedTo starting...");
        try
        {
            System.Diagnostics.Debug.WriteLine("[LibraryPage] Calling ViewModel.InitializeAsync...");
            await ViewModel.InitializeAsync();
            System.Diagnostics.Debug.WriteLine("[LibraryPage] UpdateGenreCombo...");
            UpdateGenreCombo();
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
    }

    public void SetView(string view)
    {
        ViewModel.SelectedView = view;

        if (view == "Series")
        {
            MoviesGrid.Visibility = Visibility.Collapsed;
            SeriesGrid.Visibility = ViewModel.HasNoFolders ? Visibility.Collapsed : Visibility.Visible;
            ViewModel.ApplySeriesFilters();
        }
        else
        {
            MoviesGrid.Visibility = ViewModel.HasNoFolders ? Visibility.Collapsed : Visibility.Visible;
            SeriesGrid.Visibility = Visibility.Collapsed;
            ViewModel.ApplyFilters();
        }
    }

    public void SetFolderView(string folderPath)
    {
        // Filter movies by folder - simplified for now
        ViewModel.SelectedView = "Movies";
        ViewModel.ApplyFilters();
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _searchDebounce?.Stop();
        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchDebounce.Tick += (s, e) =>
        {
            _searchDebounce.Stop();
            ViewModel.SearchQuery = sender.Text;
        };
        _searchDebounce.Start();
    }

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortCombo.SelectedItem is ComboBoxItem item)
        {
            ViewModel.SelectedSort = item.Content?.ToString() ?? "Title";
        }
    }

    private void GenreCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GenreCombo.SelectedItem is ComboBoxItem item)
        {
            var content = item.Content?.ToString();
            ViewModel.SelectedGenre = content == "All Genres" ? null : content;
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
            if (overlay != null)
            {
                overlay.Opacity = 1;
            }
            grid.Scale = new System.Numerics.Vector3(1.05f, 1.05f, 1f);
        }
    }

    private void PosterCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
        {
            var overlay = FindChild<Grid>(grid, "HoverOverlay");
            if (overlay != null)
            {
                overlay.Opacity = 0;
            }
            grid.Scale = new System.Numerics.Vector3(1f, 1f, 1f);
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

    private static Movie? GetMovieFromContext(object sender) =>
        (sender as FrameworkElement)?.DataContext as Movie;

    private static TvSeries? GetSeriesFromContext(object sender) =>
        (sender as FrameworkElement)?.DataContext as TvSeries;
}
