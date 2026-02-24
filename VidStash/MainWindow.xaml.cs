using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using VidStash.Views;
using VidStash.ViewModels;

namespace VidStash
{
    public sealed partial class MainWindow : Window
    {
        private bool _navReady;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                ExtendsContentIntoTitleBar = true;
                SetTitleBar(AppTitleBar);

                // Navigate first, then allow SelectionChanged to fire
                ContentFrame.Navigate(typeof(LibraryPage));
                ContentFrame.Navigated += ContentFrame_Navigated;

                // Mark the first nav-item as selected without firing SelectionChanged
                NavView.Loaded += (_, _) =>
                {
                    NavView.SelectedItem = NavView.MenuItems[0];
                    _navReady = true;
                    LoadFolders();
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Constructor failed: {ex}");
                throw;
            }
        }

        private async void LoadFolders()
        {
            try
            {
                var libraryVm = App.GetService<LibraryViewModel>();

                // Clear existing folder items (everything after the separator)
                var separatorIndex = -1;
                for (int i = 0; i < NavView.MenuItems.Count; i++)
                {
                    if (NavView.MenuItems[i] is NavigationViewItemSeparator)
                    {
                        separatorIndex = i;
                        break;
                    }
                }

                if (separatorIndex >= 0)
                {
                    // Remove items after header
                    for (int i = NavView.MenuItems.Count - 1; i > separatorIndex + 1; i--)
                    {
                        NavView.MenuItems.RemoveAt(i);
                    }
                }

                // Add folder items
                foreach (var folder in libraryVm.Folders)
                {
                    var folderItem = new NavigationViewItem
                    {
                        Content = folder.Name,
                        Tag = $"Folder:{folder.Path}",
                        Icon = new FontIcon { Glyph = "\uE8B7" }
                    };
                    NavView.MenuItems.Add(folderItem);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] LoadFolders failed: {ex}");
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            NavView.IsBackEnabled = ContentFrame.CanGoBack;
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            // Ignore selection events that fire before the initial navigation is done
            if (!_navReady) return;

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
                    case "Series":
                    case "Unwatched":
                        if (ContentFrame.Content is LibraryPage existingPage)
                        {
                            existingPage.SetView(tag);
                        }
                        else
                        {
                            ContentFrame.Navigate(typeof(LibraryPage));
                            if (ContentFrame.Content is LibraryPage newPage)
                                newPage.SetView(tag);
                        }
                        break;

                    case "AddFolder":
                        _ = AddFolderAsync();
                        break;

                    default:
                        if (tag?.StartsWith("Folder:") == true)
                        {
                            var folderPath = tag["Folder:".Length..];
                            if (ContentFrame.Content is LibraryPage folderPage)
                                folderPage.SetFolderView(folderPath);
                            else
                            {
                                ContentFrame.Navigate(typeof(LibraryPage));
                                if (ContentFrame.Content is LibraryPage newFolderPage)
                                    newFolderPage.SetFolderView(folderPath);
                            }
                        }
                        break;
                }
            }
        }

        private async Task AddFolderAsync()
        {
            try
            {
                var libraryVm = App.GetService<LibraryViewModel>();
                await libraryVm.AddFolderCommand.ExecuteAsync(null);
                LoadFolders(); // Refresh folder list
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] AddFolderAsync failed: {ex}");
            }
        }

        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame.CanGoBack)
                ContentFrame.GoBack();
        }

        public NavigationView GetNavigationView() => NavView;

        public void RefreshFolders() => LoadFolders();
    }
}
