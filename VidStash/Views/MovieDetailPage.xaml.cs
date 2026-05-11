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
            try
            {
                await ViewModel.LoadAsync(movie);
                UpdateUI();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MovieDetailPage] load failed: {ex}");
            }
        }
    }

    private void UpdateUI()
    {
        var movie = ViewModel.Movie;
        if (movie == null) return;

        TitleText.Text = movie.Title;
        YearText.Text = movie.Year ?? "";

        if (movie.Runtime.HasValue)
        {
            var hours = movie.Runtime.Value / 60;
            var mins = movie.Runtime.Value % 60;
            RuntimeText.Text = hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
        }
        else
        {
            RuntimeText.Text = "";
        }

        RatingText.Text = $"{movie.Rating:F1}/10";
        OverviewText.Text = movie.Overview ?? "No overview available.";
        FilenameText.Text = movie.Filename ?? "";
        FilePathText.Text = System.IO.Path.GetDirectoryName(movie.Path) ?? movie.Path;
        FileSizeText.Text = Helpers.FileHelpers.FormatFileSize(movie.Size);
        TmdbIdText.Text = movie.TmdbId?.ToString() ?? "Not matched";
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

        SimilarSection.Visibility = ViewModel.SimilarMovies.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;
        RecommendedSection.Visibility = ViewModel.Recommendations.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;
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

    private async void OpenInExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Movie == null) return;
        try
        {
            var folder = System.IO.Path.GetDirectoryName(ViewModel.Movie.Path);
            if (folder != null)
            {
                var storageFolder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(folder);
                await Windows.System.Launcher.LaunchFolderAsync(storageFolder);
            }
        }
        catch { }
    }

    private async void SearchInGoogle_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Movie == null) return;
        var query = Uri.EscapeDataString($"{ViewModel.Movie.Title} {ViewModel.Movie.Year} movie");
        await Windows.System.Launcher.LaunchUriAsync(new Uri($"https://www.google.com/search?q={query}"));
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

    private void SimilarScrollLeft_Click(object sender, RoutedEventArgs e)
    {
        var sv = SimilarScrollViewer;
        sv.ChangeView(Math.Max(0, sv.HorizontalOffset - 360), null, null);
    }

    private void SimilarScrollRight_Click(object sender, RoutedEventArgs e)
    {
        var sv = SimilarScrollViewer;
        sv.ChangeView(sv.HorizontalOffset + 360, null, null);
    }

    private void RecommendedScrollLeft_Click(object sender, RoutedEventArgs e)
    {
        var sv = RecommendedScrollViewer;
        sv.ChangeView(Math.Max(0, sv.HorizontalOffset - 360), null, null);
    }

    private void RecommendedScrollRight_Click(object sender, RoutedEventArgs e)
    {
        var sv = RecommendedScrollViewer;
        sv.ChangeView(sv.HorizontalOffset + 360, null, null);
    }

    private void TmdbCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid grid) return;

        var overlay = FindChild<Grid>(grid, "HoverOverlay");
        if (overlay != null)
            overlay.Opacity = 1;

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
        if (overlay != null)
            overlay.Opacity = 0;

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
