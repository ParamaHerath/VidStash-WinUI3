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
        }

        private static void ConfigureServices()
        {
            var services = new ServiceCollection();

            // Database
            services.AddSingleton<VidStashDbContext>();
            services.AddSingleton<DatabaseService>();

            // Services
            services.AddSingleton<ParserService>();
            services.AddHttpClient<TmdbService>();
            services.AddHttpClient<ImageCacheService>();
            services.AddSingleton<ScannerService>();

            // ViewModels
            services.AddSingleton<LibraryViewModel>();
            services.AddTransient<MovieDetailViewModel>();
            services.AddTransient<SeriesDetailViewModel>();
            services.AddTransient<SettingsViewModel>();

            _serviceProvider = services.BuildServiceProvider();
        }

        public static T GetService<T>() where T : notnull =>
            _serviceProvider!.GetRequiredService<T>();

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
    }
}
