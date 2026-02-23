using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
            await ViewModel.LoadAsync(series);
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        var series = ViewModel.Series;
        if (series == null) return;

        TitleText.Text = series.Title;
        YearText.Text = series.Year ?? "";
        StatusText.Text = series.Status ?? "";
        RatingText.Text = $"⭐ {series.Rating:F1}";
        SeasonEpCountText.Text = $"{series.TotalSeasons} Seasons · {series.TotalEpisodes} Episodes";
        OverviewText.Text = series.Overview ?? "No overview available.";
        TmdbIdText.Text = series.TmdbId.HasValue ? $"TMDB ID: {series.TmdbId}" : "TMDB: Not matched";
        FolderText.Text = $"Folder: {series.Folder}";
        WatchedButtonText.Text = series.Watched ? "Mark Unwatched" : "Mark Watched";

        GenrePills.ItemsSource = ViewModel.Genres;

        if (!string.IsNullOrEmpty(series.Backdrop))
            BackdropImage.Source = new BitmapImage(new Uri(TmdbService.GetBackdropUrl(series.Backdrop)));

        if (!string.IsNullOrEmpty(series.Poster))
            PosterBrush.ImageSource = new BitmapImage(new Uri(TmdbService.GetPosterUrl(series.Poster, "w500")));

        // Populate season combo
        SeasonCombo.Items.Clear();
        foreach (var s in ViewModel.Seasons)
        {
            SeasonCombo.Items.Add(new ComboBoxItem { Content = $"Season {s}", Tag = s });
        }
        if (SeasonCombo.Items.Count > 0)
            SeasonCombo.SelectedIndex = 0;
    }

    private void SeasonCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SeasonCombo.SelectedItem is ComboBoxItem item && item.Tag is int season)
        {
            ViewModel.SelectedSeason = season;
        }
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
}
