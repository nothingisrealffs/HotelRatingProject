using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Media;
using HotelRatingViewer.ViewModels;
using HotelRatingViewer.Services;
using HotelRatingViewer.Models;
using HotelRatingViewer.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace HotelRatingViewer
{
    public class App : Application
    {
        public static IServiceProvider? ServiceProvider { get; private set; }

        public override void Initialize()
        {
            Styles.Add(new FluentTheme());

            // Modern Styles
            var buttonStyle = new Style(x => x.OfType<Button>());
            buttonStyle.Setters.Add(new Setter(Button.CornerRadiusProperty, new CornerRadius(8)));
            buttonStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(15, 8)));
            buttonStyle.Setters.Add(new Setter(Button.FontWeightProperty, FontWeight.SemiBold)); 
            Styles.Add(buttonStyle);

            var boxStyle = new Style(x => x.OfType<TextBox>());
            boxStyle.Setters.Add(new Setter(TextBox.CornerRadiusProperty, new CornerRadius(8)));
            Styles.Add(boxStyle);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs/hotelrating-.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            Log.Information("Application initialized");
        }

        private void ConfigureServices(ServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: true);
            });

            services.AddSingleton<IDatabaseService, DatabaseService>();

            services.AddTransient<LoginViewModel>();
            services.AddTransient<BasicSearchViewModel>();
            services.AddTransient<AdvancedSearchViewModel>();
            services.AddTransient<AdminViewModel>();
            
            // Windows
            // We don't strictly need to register SplashWindow if we create it manually, but it helps if we want DI injection later
            // For now we will resolve LoginViewModel manually
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                try
                {
                    if (ServiceProvider != null)
                    {
                        ConfigService.CreateSampleConfig();

                        // Resolve the ViewModel for the login logic
                        var loginVM = ServiceProvider.GetRequiredService<LoginViewModel>();
                        
                        // Create the Splash Window (which now acts as the Login Screen)
                        var splashWindow = new SplashWindow(loginVM);
                        
                        // Set as Main Window and Show
                        desktop.MainWindow = splashWindow;
                        splashWindow.Show();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error during application initialization");
                }
            }
            
            base.OnFrameworkInitializationCompleted();
        }
    }
}