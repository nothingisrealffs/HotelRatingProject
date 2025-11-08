using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Themes.Fluent;
using HotelRatingViewer.Models;
using HotelRatingViewer.Services;
using HotelRatingViewer.Views;

namespace HotelRatingViewer
{
    public class App : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
        }
        
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Try to load config file
                var config = ConfigService.LoadConfig();

                if (config != null)
                {
                    // Config file found - attempt automatic login
                    var dbService = new DatabaseService();
                    var success = dbService.ValidateConnection(
                        config.Server,
                        config.Port,
                        config.ServiceName,
                        config.Username,
                        config.Password,
                        out string connectionString,
                        out DatabaseMode dbMode);

                    if (success)
                    {
                        // Auto-login successful - go directly to main window
                        desktop.MainWindow = new MainWindow(connectionString, dbService, dbMode);
                    }
                    else
                    {
                        // Auto-login failed - show login window with error
                        desktop.MainWindow = new LoginWindow(
                            $"Auto-login failed: {dbMode.ErrorMessage}",
                            config);
                    }
                }
                else
                {
                    // No config file - show login window
                    desktop.MainWindow = new LoginWindow();
                    
                    // Optionally create a sample config file for reference
                    ConfigService.CreateSampleConfig();
                }
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
