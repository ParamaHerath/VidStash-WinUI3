using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using VidStash.Models;
using VidStash.Services;
using VidStash.ViewModels;

namespace VidStash.Views;

public sealed partial class MovieDetailPage : Page
{
    public MovieDetailViewModel ViewModel { get; }

    public MovieDetailPage()
    {
        ViewModel = App.GetService<MovieDetailViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Movie movie)
        {
            await ViewModel.LoadAsync(movie);
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        var movie = ViewModel.Movie;
        if (movie == null) return;

        TitleText.Text = movie.Title;
        YearText.Text = movie.Year ?? "";
        RuntimeText.Text = movie.Runtime.HasValue ? $"{movie.Runtime}min" : "";
        RatingText.Text = $"⭐ {movie.Rating:F1}";
        OverviewText.Text = movie.Overview ?? "No overview available.";
        FilePathText.Text = $"Path: {movie.Path}";
        FileSizeText.Text = $"Size: {Helpers.FileHelpers.FormatFileSize(movie.Size)}";
        TmdbIdText.Text = movie.TmdbId.HasValue ? $"TMDB ID: {movie.TmdbId}" : "TMDB: Not matched";
        WatchedButtonText.Text = movie.Watched ? "Mark Unwatched" : "Mark Watched";

        GenrePills.ItemsSource = ViewModel.Genres;

        if (!string.IsNullOrEmpty(movie.Backdrop))
        {
            BackdropImage.Source = new BitmapImage(new Uri(TmdbService.GetBackdropUrl(movie.Backdrop)));
        }

        if (!string.IsNullOrEmpty(movie.Poster))
        {
            PosterBrush.ImageSource = new BitmapImage(new Uri(TmdbService.GetPosterUrl(movie.Poster, "w500")));
        }
    }

    private async void Play_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.PlayCommand.ExecuteAsync(null);

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
        var dialog = new ContentDialog
        {
            Title = "Manual TMDB Search",
            PrimaryButtonText = "Search",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };

        var searchBox = new TextBox
        {
            PlaceholderText = "Enter movie title...",
            Text = ViewModel.Movie?.Title ?? ""
        };
        dialog.Content = searchBox;

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(searchBox.Text))
        {
            var tmdb = App.GetService<TmdbService>();
            var results = await tmdb.SearchMovieAsync(searchBox.Text);

            if (results.Count > 0)
            {
                var selectDialog = new ContentDialog
                {
                    Title = "Select Match",
                    CloseButtonText = "Cancel",
                    XamlRoot = XamlRoot
                };

                var list = new ListView
                {
                    ItemsSource = results.Take(10),
                    MaxHeight = 400
                };
                list.ItemTemplate = (DataTemplate)Resources["MovieSearchResultTemplate"]
                    ?? CreateSearchResultTemplate();

                selectDialog.Content = list;

                list.ItemClick += async (s, args) =>
                {
                    if (args.ClickedItem is TmdbMovie match)
                    {
                        await ViewModel.ApplyManualMatchAsync(match);
                        UpdateUI();
                        selectDialog.Hide();
                    }
                };
                list.IsItemClickEnabled = true;

                await selectDialog.ShowAsync();
            }
        }
    }

    private static DataTemplate CreateSearchResultTemplate()
    {
        // Fallback simple template
        var template = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            @"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                           xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                <TextBlock Text=""{Binding Title}"" Margin=""8"" />
            </DataTemplate>");
        return template;
    }
}
