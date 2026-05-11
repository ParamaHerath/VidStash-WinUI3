using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using VidStash.Models;
using VidStash.Services;

namespace VidStash.Views.Dialogs;

/// <summary>
/// Result returned when the user selects a TMDB match.
/// Carries either a TmdbMovie or TmdbTv depending on the chosen type.
/// </summary>
public sealed class ParseSearchResult
{
    public TmdbMovie? Movie { get; init; }
    public TmdbTv? TvShow { get; init; }
    public bool IsMovie => Movie != null;
}

public sealed partial class ParseSearchDialog : ContentDialog
{
    // Pre-fill the search box when the dialog opens
    public string InitialQuery { get; set; } = string.Empty;

    // Force-lock the search type (pass MediaType.Movie or MediaType.TvEpisode)
    public MediaType? LockedMediaType { get; set; }

    // The result the caller should read after ShowAsync returns Primary
    public ParseSearchResult? SelectedResult { get; private set; }

    private readonly TmdbService _tmdb;

    public ParseSearchDialog()
    {
        _tmdb = App.GetService<TmdbService>();
        InitializeComponent();
        Loaded += OnLoaded;
    }

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

        // Auto-search if we have an initial query
        if (!string.IsNullOrWhiteSpace(InitialQuery))
            _ = RunSearchAsync();
    }

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

    private async Task RunSearchAsync()
    {
        var query = SearchBox.Text.Trim();
        if (string.IsNullOrEmpty(query)) return;

        // Reset state
        SelectedResult = null;
        IsPrimaryButtonEnabled = false;
        SelectionPreview.Visibility = Visibility.Collapsed;
        NoResultsText.Visibility = Visibility.Collapsed;
        ResultsScrollViewer.Visibility = Visibility.Collapsed;
        ResultsPanel.Children.Clear();
        SearchProgress.Visibility = Visibility.Visible;

        bool searchTv = TvRadio.IsChecked == true;

        try
        {
            if (searchTv)
            {
                var results = await _tmdb.SearchTvAsync(query);
                BuildResults(results);
            }
            else
            {
                var results = await _tmdb.SearchMovieAsync(query);
                BuildResults(results);
            }
        }
        finally
        {
            SearchProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void BuildResults<T>(List<T> results)
    {
        if (results.Count == 0)
        {
            NoResultsText.Visibility = Visibility.Visible;
            return;
        }

        ResultsScrollViewer.Visibility = Visibility.Visible;

        foreach (var item in results.Take(10))
        {
            var card = CreateResultCard(item);
            ResultsPanel.Children.Add(card);
        }
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

        // Poster image
        Border posterBorder = new()
        {
            Width = 54,
            Height = 81,
            CornerRadius = new CornerRadius(4),
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ControlFillColorDefaultBrush"]
        };
        if (!string.IsNullOrEmpty(posterPath))
        {
            posterBorder.Background = new Microsoft.UI.Xaml.Media.ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(TmdbService.GetPosterUrl(posterPath, "w92"))),
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
            };
        }

        // Title
        var titleBlock = new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 14,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };

        // Year + rating row
        var metaPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        if (!string.IsNullOrEmpty(year))
            metaPanel.Children.Add(new TextBlock { Text = year, Opacity = 0.6, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        if (!string.IsNullOrEmpty(ratingStr))
            metaPanel.Children.Add(new TextBlock { Text = ratingStr, Opacity = 0.7, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });

        // Overview snippet
        var overviewBlock = new TextBlock
        {
            Text = string.IsNullOrEmpty(overview) ? "No overview available." : overview,
            Opacity = 0.65,
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 2,
            TextWrapping = TextWrapping.WrapWholeWords,
            LineHeight = 18
        };

        var infoStack = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        infoStack.Children.Add(titleBlock);
        infoStack.Children.Add(metaPanel);
        infoStack.Children.Add(overviewBlock);

        var contentGrid = new Grid { ColumnSpacing = 12 };
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(posterBorder, 0);
        Grid.SetColumn(infoStack, 1);
        contentGrid.Children.Add(posterBorder);
        contentGrid.Children.Add(infoStack);

        var card = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10),
            Child = contentGrid,
            Tag = item
        };

        // Selection visual: pointer cursor
        card.PointerEntered += (s, _) =>
        {
            if (s is Border b)
                b.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        };
        card.PointerExited += (s, _) =>
        {
            if (s is Border b && b.Tag != SelectedResult?.Movie && b.Tag != SelectedResult?.TvShow)
                b.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        };

        card.PointerPressed += (s, _) => SelectCard(card, item, title, year, overview, posterPath);

        return card;
    }

    private void SelectCard<T>(Border selectedCard, T item, string title, string? year, string? overview, string? posterPath)
    {
        // Reset all card backgrounds
        foreach (var child in ResultsPanel.Children)
        {
            if (child is Border b)
                b.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        }

        // Highlight selected
        selectedCard.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"];
        selectedCard.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];

        // Set result
        if (item is TmdbMovie movie)
            SelectedResult = new ParseSearchResult { Movie = movie };
        else if (item is TmdbTv tv)
            SelectedResult = new ParseSearchResult { TvShow = tv };

        // Update preview panel
        PreviewTitle.Text = title;
        PreviewYear.Text = string.IsNullOrEmpty(year) ? "" : $"First aired / released: {year}";
        PreviewOverview.Text = string.IsNullOrEmpty(overview) ? "No overview available." : overview;

        if (!string.IsNullOrEmpty(posterPath))
            PreviewPosterBrush.ImageSource = new BitmapImage(new Uri(TmdbService.GetPosterUrl(posterPath, "w154")));
        else
            PreviewPosterBrush.ImageSource = null;

        SelectionPreview.Visibility = Visibility.Visible;
        IsPrimaryButtonEnabled = true;
    }
}
