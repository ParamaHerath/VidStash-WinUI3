using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using VidStash.Models;
using VidStash.Services;

namespace VidStash.Views.Dialogs;

/// <summary>
/// Result returned when the user confirms a TMDB match.
/// </summary>
public sealed class ParseSearchResult
{
    public TmdbMovie? Movie { get; init; }
    public TmdbTv? TvShow { get; init; }
    public bool IsMovie => Movie != null;
}

public sealed partial class ParseSearchDialog : ContentDialog
{
    /// <summary>Pre-fill the search box when the dialog opens.</summary>
    public string InitialQuery { get; set; } = string.Empty;

    /// <summary>Lock search type: pass MediaType.Movie or MediaType.TvEpisode.</summary>
    public MediaType? LockedMediaType { get; set; }

    /// <summary>Read this after ShowAsync returns ContentDialogResult.Primary.</summary>
    public ParseSearchResult? SelectedResult { get; private set; }

    private readonly TmdbService _tmdb;

    public ParseSearchDialog()
    {
        _tmdb = App.GetService<TmdbService>();
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // -------------------------------------------------------------------------
    // Initialisation
    // -------------------------------------------------------------------------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = InitialQuery;

        if (LockedMediaType == MediaType.Movie)
        {
            MovieRadio.IsChecked = true;
            TvRadio.IsEnabled = false;
        }
        else if (LockedMediaType == MediaType.TvEpisode)
        {
            TvRadio.IsChecked = true;
            MovieRadio.IsEnabled = false;
        }

        // Auto-search with the pre-filled query
        if (!string.IsNullOrWhiteSpace(InitialQuery))
            _ = RunSearchAsync();
    }

    // -------------------------------------------------------------------------
    // Title-bar X button
    // -------------------------------------------------------------------------

    private void TitleClose_Click(object sender, RoutedEventArgs e) => Hide();

    // -------------------------------------------------------------------------
    // Search bar interaction
    // -------------------------------------------------------------------------

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchButton.IsEnabled = !string.IsNullOrWhiteSpace(SearchBox.Text);
    }

    private void SearchBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && SearchButton.IsEnabled)
            _ = RunSearchAsync();
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e) => _ = RunSearchAsync();

    // -------------------------------------------------------------------------
    // Search execution
    // -------------------------------------------------------------------------

    private async Task RunSearchAsync()
    {
        var query = SearchBox.Text.Trim();
        if (string.IsNullOrEmpty(query)) return;

        // Reset UI state
        SelectedResult = null;
        IsPrimaryButtonEnabled = false;
        SelectionPreview.Visibility = Visibility.Collapsed;
        NoResultsContainer.Visibility = Visibility.Collapsed;
        ResultsContainer.Visibility = Visibility.Collapsed;
        ResultsPanel.Children.Clear();
        SearchProgress.Visibility = Visibility.Visible;

        bool searchTv = TvRadio.IsChecked == true;

        try
        {
            if (searchTv)
                BuildResults(await _tmdb.SearchTvAsync(query));
            else
                BuildResults(await _tmdb.SearchMovieAsync(query));
        }
        finally
        {
            SearchProgress.Visibility = Visibility.Collapsed;
        }
    }

    // -------------------------------------------------------------------------
    // Result card building
    // -------------------------------------------------------------------------

    private void BuildResults<T>(List<T> results)
    {
        if (results.Count == 0)
        {
            NoResultsContainer.Visibility = Visibility.Visible;
            return;
        }

        foreach (var item in results.Take(10))
            ResultsPanel.Children.Add(CreateResultCard(item));

        ResultsContainer.Visibility = Visibility.Visible;
    }

    private Border CreateResultCard<T>(T item)
    {
        string title = "";
        string? year = null;
        string? overview = null;
        string? posterPath = null;
        string? ratingStr = null;

        if (item is TmdbMovie m)
        {
            title = m.Title;
            year = m.ReleaseDate?.Length >= 4 ? m.ReleaseDate[..4] : null;
            overview = m.Overview;
            posterPath = m.PosterPath;
            ratingStr = $"⭐ {m.VoteAverage:F1}";
        }
        else if (item is TmdbTv t)
        {
            title = t.Name;
            year = t.FirstAirDate?.Length >= 4 ? t.FirstAirDate[..4] : null;
            overview = t.Overview;
            posterPath = t.PosterPath;
            ratingStr = $"⭐ {t.VoteAverage:F1}";
        }

        // --- Poster thumbnail ---
        var posterBorder = new Border
        {
            Width = 54,
            Height = 81,
            CornerRadius = new CornerRadius(6),
        };
        if (!string.IsNullOrEmpty(posterPath))
        {
            posterBorder.Background = new Microsoft.UI.Xaml.Media.ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(TmdbService.GetPosterUrl(posterPath, "w92"))),
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
            };
        }
        else
        {
            posterBorder.Background = GetBrush("ControlFillColorDefaultBrush");
        }

        // --- Meta row: year + rating ---
        var metaPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (!string.IsNullOrEmpty(year))
            metaPanel.Children.Add(MakeLabel(year, 12, 0.55));
        if (!string.IsNullOrEmpty(ratingStr))
            metaPanel.Children.Add(MakeLabel(ratingStr, 12, 0.65));

        // --- Info column ---
        var infoStack = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        infoStack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 14,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        });
        infoStack.Children.Add(metaPanel);
        infoStack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrEmpty(overview) ? "No overview available." : overview,
            Opacity = 0.6,
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 2,
            TextWrapping = TextWrapping.WrapWholeWords,
            LineHeight = 18
        });

        // --- Layout grid ---
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(posterBorder, 0);
        Grid.SetColumn(infoStack, 1);
        grid.Children.Add(posterBorder);
        grid.Children.Add(infoStack);

        // --- Card border ---
        var card = new Border
        {
            Background = GetBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = GetBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Child = grid,
            Tag = item
        };

        // Hover effects
        card.PointerEntered += (s, _) =>
        {
            if (s is Border b && b.Tag != SelectedResult?.Movie && b.Tag != SelectedResult?.TvShow)
                b.Background = GetBrush("SubtleFillColorSecondaryBrush");
        };
        card.PointerExited += (s, _) =>
        {
            if (s is Border b && b.Tag != SelectedResult?.Movie && b.Tag != SelectedResult?.TvShow)
                b.Background = GetBrush("CardBackgroundFillColorDefaultBrush");
        };

        card.PointerPressed += (_, _) => SelectCard(card, item, title, year, overview, posterPath);

        return card;
    }

    // -------------------------------------------------------------------------
    // Selection handling
    // -------------------------------------------------------------------------

    private void SelectCard<T>(Border selectedCard, T item, string title,
        string? year, string? overview, string? posterPath)
    {
        // Reset all card styling
        foreach (var child in ResultsPanel.Children)
        {
            if (child is Border b)
            {
                b.Background = GetBrush("CardBackgroundFillColorDefaultBrush");
                b.BorderBrush = GetBrush("CardStrokeColorDefaultBrush");
                b.BorderThickness = new Thickness(1);
            }
        }

        // Highlight the selected card
        selectedCard.Background = GetBrush("SubtleFillColorTertiaryBrush");
        selectedCard.BorderBrush = GetBrush("AccentFillColorDefaultBrush");
        selectedCard.BorderThickness = new Thickness(1.5);

        // Store result
        SelectedResult = item is TmdbMovie movie
            ? new ParseSearchResult { Movie = movie }
            : item is TmdbTv tv
                ? new ParseSearchResult { TvShow = tv }
                : null;

        // Populate preview panel
        PreviewTitle.Text = title;
        PreviewYear.Text = string.IsNullOrEmpty(year) ? "" : $"Released / First aired: {year}";
        PreviewOverview.Text = string.IsNullOrEmpty(overview) ? "No overview available." : overview;
        PreviewPosterBrush.ImageSource = !string.IsNullOrEmpty(posterPath)
            ? new BitmapImage(new Uri(TmdbService.GetPosterUrl(posterPath, "w154")))
            : null;

        SelectionPreview.Visibility = Visibility.Visible;
        IsPrimaryButtonEnabled = true;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Microsoft.UI.Xaml.Media.Brush GetBrush(string key) =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[key];

    private static TextBlock MakeLabel(string text, double fontSize, double opacity) =>
        new() { Text = text, FontSize = fontSize, Opacity = opacity, VerticalAlignment = VerticalAlignment.Center };
}
