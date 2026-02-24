using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using VidStash.Services;
using VidStash.ViewModels;

namespace VidStash
{
    public partial class App : Application
    {
        public static Window? MainWindow { get; private set; }

        private static ServiceProvider? _serviceProvider;

        public App()
        {
            InitializeComponent();
            ConfigureServices();

            UnhandledException += (_, e) =>
            {
                e.Handled = true;
                System.Diagnostics.Debug.WriteLine($"[VidStash] Unhandled: {e.Exception}");
            };
        }

        private static void ConfigureServices()
        {
            try
            {
                var services = new ServiceCollection();

                // Database — factory so each operation gets its own fresh context
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dir = System.IO.Path.Combine(appData, "VidStash");
                System.IO.Directory.CreateDirectory(dir);
                var dbPath = System.IO.Path.Combine(dir, "vidstash.db");

                services.AddDbContextFactory<VidStashDbContext>(options =>
                    options.UseSqlite($"Data Source={dbPath}"));
                services.AddSingleton<DatabaseService>();

                // Services
                services.AddSingleton<ParserService>();
                services.AddHttpClient<TmdbService>(c => c.Timeout = TimeSpan.FromSeconds(30));
                services.AddHttpClient<ImageCacheService>(c => c.Timeout = TimeSpan.FromSeconds(30));
                services.AddSingleton<ScannerService>();

                // ViewModels
                services.AddSingleton<LibraryViewModel>();
                services.AddTransient<MovieDetailViewModel>();
                services.AddTransient<SeriesDetailViewModel>();
                services.AddTransient<SettingsViewModel>();

                _serviceProvider = services.BuildServiceProvider();

                // Ensure DB schema exists (fast SQLite operation)
                using var scope = _serviceProvider.CreateScope();
                var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<VidStashDbContext>>();

                try
                {
                    using var db = factory.CreateDbContext();
                    db.Database.EnsureCreated();
                }
                catch (Exception dbEx)
                {
                    // If database is corrupted from old version, delete and recreate
                    System.Diagnostics.Debug.WriteLine($"[VidStash] DB init failed, attempting reset: {dbEx.Message}");

                    if (System.IO.File.Exists(dbPath))
                    {
                        System.IO.File.Delete(dbPath);
                        System.Diagnostics.Debug.WriteLine($"[VidStash] Deleted corrupted DB, recreating...");

                        using var db = factory.CreateDbContext();
                        db.Database.EnsureCreated();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VidStash] ConfigureServices failed: {ex}");
                throw;
            }
        }

        public static T GetService<T>() where T : notnull =>
            _serviceProvider!.GetRequiredService<T>();

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                MainWindow = new MainWindow();
                MainWindow.Activate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VidStash] OnLaunched failed: {ex}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}
