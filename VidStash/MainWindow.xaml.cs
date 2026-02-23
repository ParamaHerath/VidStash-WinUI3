using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VidStash.Views;
using VidStash.ViewModels;

namespace VidStash
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ExtendsContentIntoTitleBar = true;

            ContentFrame.Navigate(typeof(LibraryPage));
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
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
                        ContentFrame.Navigate(typeof(LibraryPage));
                        if (ContentFrame.Content is LibraryPage moviesPage)
                            moviesPage.SetView("Movies");
                        break;

                    case "Series":
                        ContentFrame.Navigate(typeof(LibraryPage));
                        if (ContentFrame.Content is LibraryPage seriesPage)
                            seriesPage.SetView("Series");
                        break;

                    case "Unwatched":
                        ContentFrame.Navigate(typeof(LibraryPage));
                        if (ContentFrame.Content is LibraryPage unwatchedPage)
                            unwatchedPage.SetView("Unwatched");
                        break;

                    case "AddFolder":
                        _ = App.GetService<LibraryViewModel>().AddFolderCommand.ExecuteAsync(null);
                        break;

                    default:
                        // Folder items
                        if (tag?.StartsWith("Folder:") == true)
                        {
                            var folderPath = tag["Folder:".Length..];
                            ContentFrame.Navigate(typeof(LibraryPage));
                            if (ContentFrame.Content is LibraryPage folderPage)
                                folderPage.SetFolderView(folderPath);
                        }
                        break;
                }
            }
        }

        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }

        public NavigationView GetNavigationView() => NavView;
    }
}
