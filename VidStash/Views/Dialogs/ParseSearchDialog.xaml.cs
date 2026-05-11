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
    /// <summary>Pre-fill the search box and auto-search on open.</summary>
    public string InitialQuery { get; set; } = string.Empty;

    /// <summary>Locks search to Movie or TV. Set by the caller before ShowAsync.</summary>
    public MediaType? LockedMediaType { get; set; }

    /// <summary>Read this after ShowAsync returns ContentDialogResult.Primary.</summary>
    public ParseSearchResult? SelectedResult { get; private set; }

    private readonly TmdbService _tmdb;

    // True = search TV shows; False = search movies. Determined by LockedMediaType.
    private bool _searchTv;

    public ParseSearchDialog()
    {
        _tmdb = App.GetService<TmdbService>();
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _searchTv = LockedMediaType == MediaType.TvEpisode;
        SearchBox.Text = InitialQuery;

        if (!string.IsNullOrWhiteSpace(InitialQuery))
            _ = RunSearchAsync();
    }

    // ── Search bar ────────────────────────────────────────────────────────────

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => SearchButton.IsEnabled = !string.IsNullOrWhiteSpace(SearchBox.Text);

    private void SearchBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && SearchButton.IsEnabled)
            _ = RunSearchAsync();
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e) => _ = RunSearchAsync();

    // ── Search execution ──────────────────────────────────────────────────────

    private async Task RunSearchAsync()
    {
        var query = SearchBox.Text.Trim();
        if (string.IsNullOrEmpty(query)) return;

        // Reset
        SelectedResult = null;
        IsPrimaryButtonEnabled = false;
        NoResultsContainer.Visibility = Visibility.Collapsed;
        ResultsContainer.Visibility = Visibility.Collapsed;
        ResultsPanel.Children.Clear();
        SearchProgress.Visibility = Visibility.Visible;

        try
        {
            if (_searchTv)
                BuildResults(await _tmdb.SearchTvAsync(query));
            else
                BuildResults(await _tmdb.SearchMovieAsync(query));
        }
        finally
        {
            SearchProgress.Visibility = Visibility.Collapsed;
        }
    }

    // ── Result card building ──────────────────────────────────────────────────

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

        // Poster
        var posterBorder = new Border
        {
            Width = 50,
            Height = 75,
            CornerRadius = new CornerRadius(6),
            Background = GetBrush("ControlFillColorDefaultBrush")
        };
        if (!string.IsNullOrEmpty(posterPath))
        {
            posterBorder.Background = new Microsoft.UI.Xaml.Media.ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(TmdbService.GetPosterUrl(posterPath, "w92"))),
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
            };
        }

        // Year + rating
        var metaPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (!string.IsNullOrEmpty(year))
            metaPanel.Children.Add(MakeLabel(year, 12, 0.55));
        if (!string.IsNullOrEmpty(ratingStr))
            metaPanel.Children.Add(MakeLabel(ratingStr, 12, 0.65));

        // Info column
        var infoStack = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
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

        // Grid layout
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(posterBorder, 0);
        Grid.SetColumn(infoStack, 1);
        grid.Children.Add(posterBorder);
        grid.Children.Add(infoStack);

        // Card
        var card = new Border
        {
            Background = GetBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = GetBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Child = grid,
            Tag = item
        };

        card.PointerEntered += (s, _) =>
        {
            if (s is Border b && !IsSelected(b))
                b.Background = GetBrush("SubtleFillColorSecondaryBrush");
        };
        card.PointerExited += (s, _) =>
        {
            if (s is Border b && !IsSelected(b))
                b.Background = GetBrush("CardBackgroundFillColorDefaultBrush");
        };
        card.PointerPressed += (_, _) => SelectCard(card, item);

        return card;
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    private bool IsSelected(Border card) =>
        (SelectedResult?.Movie != null && card.Tag is TmdbMovie m && m == SelectedResult.Movie) ||
        (SelectedResult?.TvShow != null && card.Tag is TmdbTv t && t == SelectedResult.TvShow);

    private void SelectCard<T>(Border selectedCard, T item)
    {
        // Reset all cards
        foreach (var child in ResultsPanel.Children)
        {
            if (child is Border b)
            {
                b.Background = GetBrush("CardBackgroundFillColorDefaultBrush");
                b.BorderBrush = GetBrush("CardStrokeColorDefaultBrush");
                b.BorderThickness = new Thickness(1);
            }
        }

        // Highlight selection with accent border
        selectedCard.BorderBrush = GetBrush("AccentFillColorDefaultBrush");
        selectedCard.BorderThickness = new Thickness(1.5);
        selectedCard.Background = GetBrush("SubtleFillColorTertiaryBrush");

        SelectedResult = item is TmdbMovie movie
            ? new ParseSearchResult { Movie = movie }
            : item is TmdbTv tv
                ? new ParseSearchResult { TvShow = tv }
                : null;

        IsPrimaryButtonEnabled = SelectedResult != null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Microsoft.UI.Xaml.Media.Brush GetBrush(string key) =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[key];

    private static TextBlock MakeLabel(string text, double size, double opacity) =>
        new() { Text = text, FontSize = size, Opacity = opacity, VerticalAlignment = VerticalAlignment.Center };
}
