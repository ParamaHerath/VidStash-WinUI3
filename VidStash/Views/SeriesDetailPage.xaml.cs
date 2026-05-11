using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using VidStash.Models;
using VidStash.Services;
using VidStash.ViewModels;

namespace VidStash.Views;

public sealed partial class SeriesDetailPage : Page
{
    public SeriesDetailViewModel ViewModel { get; }

    public SeriesDetailPage()
    {
        ViewModel = App.GetService<SeriesDetailViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is TvSeries series)
        {
            try
            {
                await ViewModel.LoadAsync(series);
                UpdateUI();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeriesDetailPage] load failed: {ex}");
            }
        }
    }

    private void UpdateUI()
    {
        var series = ViewModel.Series;
        if (series == null) return;

        TitleText.Text = series.Title;
        YearText.Text = series.Year ?? "";
        StatusText.Text = series.Status ?? "";
        RatingText.Text = $"{series.Rating:F1}/10";
        SeasonEpCountText.Text = $"{series.TotalSeasons} Seasons · {series.TotalEpisodes} Episodes";
        TotalSeasonsText.Text = series.TotalSeasons.ToString();
        TotalEpisodesText.Text = series.TotalEpisodes.ToString();
        TmdbIdText.Text = series.TmdbId?.ToString() ?? "Not matched";
        FolderText.Text = series.Folder ?? "";
        OverviewText.Text = series.Overview ?? "No overview available.";
        WatchedButtonText.Text = series.Watched ? "Mark Unwatched" : "Mark Watched";

        GenrePills.ItemsSource = ViewModel.Genres;

        if (!string.IsNullOrEmpty(series.Backdrop))
            BackdropImage.Source = new BitmapImage(new Uri(TmdbService.GetBackdropUrl(series.Backdrop)));

        if (!string.IsNullOrEmpty(series.Poster))
            PosterBrush.ImageSource = new BitmapImage(new Uri(TmdbService.GetPosterUrl(series.Poster, "w500")));

        // Season combo
        SeasonCombo.Items.Clear();
        foreach (var s in ViewModel.Seasons)
        {
            SeasonCombo.Items.Add(new ComboBoxItem { Content = $"Season {s}", Tag = s });
        }
        if (SeasonCombo.Items.Count > 0)
            SeasonCombo.SelectedIndex = 0;

        SimilarSection.Visibility = ViewModel.SimilarShows.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;
        RecommendedSection.Visibility = ViewModel.Recommendations.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SeasonCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SeasonCombo.SelectedItem is ComboBoxItem item && item.Tag is int season)
            ViewModel.SelectedSeason = season;
    }

    private async void Episode_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TvEpisode episode)
            await ViewModel.PlayEpisodeCommand.ExecuteAsync(episode);
    }

    private async void PlayEpisode_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is TvEpisode episode)
            await ViewModel.PlayEpisodeCommand.ExecuteAsync(episode);
    }

    private async void ToggleWatched_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ToggleWatchedCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private async void RefreshMetadata_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshMetadataCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private async void ManualSearch_Click(object sender, RoutedEventArgs e)
    {
        // Parse/Manual Search functionality will be added later
    }

    private async void OpenInExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Series?.Folder == null) return;
        try
        {
            var storageFolder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(ViewModel.Series.Folder);
            await Windows.System.Launcher.LaunchFolderAsync(storageFolder);
        }
        catch { }
    }

    private async void SearchInGoogle_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Series == null) return;
        var query = Uri.EscapeDataString($"{ViewModel.Series.Title} {ViewModel.Series.Year} series");
        await Windows.System.Launcher.LaunchUriAsync(new Uri($"https://www.google.com/search?q={query}"));
    }

    // ----- Scroll button handlers -----

    private void SimilarScrollLeft_Click(object sender, RoutedEventArgs e)
    {
        SimilarScrollViewer.ChangeView(Math.Max(0, SimilarScrollViewer.HorizontalOffset - 360), null, null);
    }

    private void SimilarScrollRight_Click(object sender, RoutedEventArgs e)
    {
        SimilarScrollViewer.ChangeView(SimilarScrollViewer.HorizontalOffset + 360, null, null);
    }

    private void RecommendedScrollLeft_Click(object sender, RoutedEventArgs e)
    {
        RecommendedScrollViewer.ChangeView(Math.Max(0, RecommendedScrollViewer.HorizontalOffset - 360), null, null);
    }

    private void RecommendedScrollRight_Click(object sender, RoutedEventArgs e)
    {
        RecommendedScrollViewer.ChangeView(RecommendedScrollViewer.HorizontalOffset + 360, null, null);
    }

    // ----- Episode card hover -----

    private void EpisodeCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement el) return;
        var overlay = FindChild<Grid>(el, "EpisodeHoverOverlay");
        if (overlay != null) overlay.Opacity = 1;
    }

    private void EpisodeCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement el) return;
        var overlay = FindChild<Grid>(el, "EpisodeHoverOverlay");
        if (overlay != null) overlay.Opacity = 0;
    }

    // ----- Card hover animations -----

    private void TmdbCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid grid) return;

        var overlay = FindChild<Grid>(grid, "HoverOverlay");
        if (overlay != null) overlay.Opacity = 1;

        if (grid.RenderTransform is CompositeTransform transform)
        {
            var sb = new Storyboard();
            foreach (var prop in new[] { "ScaleX", "ScaleY" })
            {
                var anim = new DoubleAnimation
                {
                    To = 1.05,
                    Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(anim, transform);
                Storyboard.SetTargetProperty(anim, prop);
                sb.Children.Add(anim);
            }
            sb.Begin();
        }
    }

    private void TmdbCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid grid) return;

        var overlay = FindChild<Grid>(grid, "HoverOverlay");
        if (overlay != null) overlay.Opacity = 0;

        if (grid.RenderTransform is CompositeTransform transform)
        {
            var sb = new Storyboard();
            foreach (var prop in new[] { "ScaleX", "ScaleY" })
            {
                var anim = new DoubleAnimation
                {
                    To = 1.0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(anim, transform);
                Storyboard.SetTargetProperty(anim, prop);
                sb.Children.Add(anim);
            }
            sb.Begin();
        }
    }

    private static T? FindChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T element && element.Name == name)
                return element;
            var result = FindChild<T>(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
