using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using Avalonia.Input; // <--- ADDED THIS NAMESPACE
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using HotelRatingViewer.Services;
using HotelRatingViewer.ViewModels;
using HotelRatingViewer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace HotelRatingViewer.Views
{
    public class SplashWindow : Window
    {
        private readonly LoginViewModel _viewModel;
        
        // UI Controls
        private ProgressBar _progressBar = null!;
        private TextBlock _loadingText = null!;
        private StackPanel _loginPanel = null!;
        private Button _connectButton = null!;
        private TextBlock _statusText = null!;
        
        // Inputs
        private TextBox _serverBox = null!;
        private TextBox _userBox = null!;
        private TextBox _passBox = null!;

        public SplashWindow(LoginViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = _viewModel;

            // Listen for successful login
            _viewModel.LoginSucceeded += OnLoginSucceeded;
            _viewModel.MaxAttemptsExceeded += (s, e) => Close();

            // Window Setup
            SystemDecorations = SystemDecorations.None;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = 700; 
            Height = 500; 
            CanResize = false;
            Background = new SolidColorBrush(Color.Parse("#2D3436")); 
            CornerRadius = new CornerRadius(20);
            TransparencyLevelHint = [WindowTransparencyLevel.Mica, WindowTransparencyLevel.None];

            BuildUI();
            
            BindViewModel();

            LoadConfiguration();

            Dispatcher.UIThread.Post(AnimateToPrompt);
        }

        private void BuildUI()
        {
            // 1. Close Button
            var closeBtn = new Button
            {
                Content = "✕",
                Foreground = Brushes.Gray,
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 10, 10, 0),
                Padding = new Thickness(5),
                // FIXED: Use StandardCursorType
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            closeBtn.Click += (s, e) => Close();

            // 2. Header
            var title = new TextBlock
            {
                Text = "Hotel Rating Viewer",
                FontSize = 32,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = "Segoe UI, Helvetica, Arial"
            };

            var subtitle = new TextBlock
            {
                Text = "Secure Database Access",
                FontSize = 14,
                Foreground = Brush.Parse("#A0A0A0"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 20)
            };

            _progressBar = new ProgressBar
            {
                Width = 500,
                Height = 6,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Foreground = Brush.Parse("#00B894"),
                Background = Brush.Parse("#636e72"),
                CornerRadius = new CornerRadius(3)
            };

            _loadingText = new TextBlock
            {
                Text = "Initializing...",
                FontSize = 12,
                Foreground = Brushes.LightGray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };

            _loginPanel = new StackPanel
            {
                Spacing = 15,
                Margin = new Thickness(50, 30, 50, 0),
                IsVisible = false, 
                Children =
                {
                    CreateModernInput("Server Address", "localhost", false, out _serverBox),
                    CreateModernInput("Username", "User", false, out _userBox),
                    CreateModernInput("Password", "Password", true, out _passBox)
                }
            };

            _connectButton = new Button
            {
                Content = "Connect to Database",
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 200,
                Margin = new Thickness(0, 20, 0, 0),
                Background = Brush.Parse("#0984e3"),
                Foreground = Brushes.White
            };
            
            _statusText = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.OrangeRed,
                Margin = new Thickness(0, 10, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };

            _loginPanel.Children.Add(_connectButton);
            _loginPanel.Children.Add(_statusText);

            // Main Layout
            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = "🏨", FontSize = 50, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,0,0,10) },
                    title,
                    subtitle,
                    _progressBar,
                    _loadingText,
                    _loginPanel
                }
            };

            Content = new Panel
            {
                Children =
                {
                    closeBtn,
                    new Border { Padding = new Thickness(40), Child = stack }
                }
            };
        }

        private StackPanel CreateModernInput(string label, string watermark, bool isPassword, out TextBox textBox)
        {
            textBox = new TextBox
            {
                Watermark = watermark,
                PasswordChar = isPassword ? '•' : '\0',
                Width = 300,
                Background = Brush.Parse("#dfe6e9"),
                Foreground = Brushes.Black
            };

            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 10,
                Children =
                {
                    new TextBlock 
                    { 
                        Text = label, 
                        Foreground = Brushes.White, 
                        Width = 100, 
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Right
                    },
                    textBox
                }
            };
        }

        private void BindViewModel()
        {
            _serverBox.Bind(TextBox.TextProperty, new Binding("Server") { Mode = BindingMode.TwoWay });
            _userBox.Bind(TextBox.TextProperty, new Binding("Username") { Mode = BindingMode.TwoWay });
            _passBox.Bind(TextBox.TextProperty, new Binding("Password") { Mode = BindingMode.TwoWay });
            
            _connectButton.Click += (s, e) => 
            {
                if (_viewModel.ConnectCommand.CanExecute(null))
                    _viewModel.ConnectCommand.Execute(null);
            };

            _statusText.Bind(TextBlock.TextProperty, new Binding("StatusMessage"));
        }

        private void LoadConfiguration()
        {
            var config = ConfigService.LoadConfig();
            if (config != null)
            {
                _viewModel.SetConfigValues(config);
            }
        }

        private async void AnimateToPrompt()
        {
            string[] steps = { "Loading Components...", "Checking Configuration...", "Establishing Secure Channel..." };
            
            // Go to 75%
            for (int i = 0; i <= 75; i++)
            {
                _progressBar.Value = i;
                if (i < 25) _loadingText.Text = steps[0];
                else if (i < 50) _loadingText.Text = steps[1];
                else _loadingText.Text = steps[2];

                await Task.Delay(15);
            }

            // PAUSE HERE
            _loadingText.Text = "Authentication Required";
            _progressBar.Foreground = Brushes.Orange;
            _loginPanel.IsVisible = true;
        }

        private async void OnLoginSucceeded(object? sender, (string connectionString, DatabaseMode dbMode) e)
        {
            // 1. Hide the login panel
            _loginPanel.IsVisible = false;
            _loadingText.Text = "Access Granted. Launching Viewer...";
            _progressBar.Foreground = Brushes.Green;

            // 2. Animate 75% -> 100%
            for (int i = 75; i <= 100; i++)
            {
                _progressBar.Value = i;
                await Task.Delay(10);
            }

            await Task.Delay(200);

            // 3. Launch Main Window
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var services = App.ServiceProvider!;
                
                // Create MainWindowViewModel using the DatabaseMode from the login event
                var mainViewModel = new MainWindowViewModel(
                    services.GetRequiredService<IDatabaseService>(),
                    e.dbMode,
                    services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MainWindowViewModel>>(),
                    services.GetRequiredService<BasicSearchViewModel>(),
                    services.GetRequiredService<AdvancedSearchViewModel>(),
                    services.GetRequiredService<AdminViewModel>()
                );

                var mainWindow = new MainWindow(mainViewModel);
                mainWindow.Show();
                
                desktop.MainWindow = mainWindow;
                this.Close();
            }
        }
    }
}
