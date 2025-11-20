using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using HotelRatingViewer.Models;
using HotelRatingViewer.Services;
using HotelRatingViewer.ViewModels;
using HotelRatingViewer.Converters; // ADDED THIS NAMESPACE
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HotelRatingViewer.Views
{
    public class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel;
        
        private TextBox _serverBox = null!;
        private TextBox _portBox = null!;
        private TextBox _serviceBox = null!;
        private TextBox _userBox = null!;
        private TextBox _passBox = null!;
        private Button _loginButton = null!;
        private TextBlock _statusText = null!;

        public LoginWindow()
        {
            // Retrieve the LoginViewModel from the DI container
            _viewModel = App.ServiceProvider!.GetRequiredService<LoginViewModel>();
            DataContext = _viewModel;

            _viewModel.LoginSucceeded += OnLoginSucceeded;
            _viewModel.MaxAttemptsExceeded += OnMaxAttemptsExceeded;

            Title = "Hotel Rating System - Database Login";
            Width = 500;
            Height = 400;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            BuildUI();
            BindViewModel();
        }

        private void BuildUI()
        {
            var titleText = new TextBlock
            {
                Text = "Hotel Rating Analysis System",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 10)
            };

            var subtitleText = new TextBlock
            {
                Text = "Database Connection",
                FontSize = 14,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 30)
            };

            _serverBox = new TextBox { Watermark = "Server (e.g., localhost)", Width = 300, Margin = new Thickness(5) };
            _portBox = new TextBox { Watermark = "Port (e.g., 1521)", Width = 300, Margin = new Thickness(5) };
            _serviceBox = new TextBox { Watermark = "Service Name (SID)", Width = 300, Margin = new Thickness(5) };
            _userBox = new TextBox { Watermark = "Username", Width = 300, Margin = new Thickness(5) };
            _passBox = new TextBox { Watermark = "Password", PasswordChar = '•', Width = 300, Margin = new Thickness(5) };

            _loginButton = new Button
            {
                Content = "Connect",
                Width = 150,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 10),
                IsDefault = true
            };

            _statusText = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };

            var mainPanel = new StackPanel
            {
                Children =
                {
                    titleText,
                    subtitleText,
                    CreateInputRow("Server:", _serverBox),
                    CreateInputRow("Port:", _portBox),
                    CreateInputRow("Service:", _serviceBox),
                    CreateInputRow("Username:", _userBox),
                    CreateInputRow("Password:", _passBox),
                    _loginButton,
                    _statusText
                }
            };

            Content = mainPanel;
        }

        private StackPanel CreateInputRow(string label, Control textBox)
        {
            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = label, Width = 80, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) },
                    textBox
                }
            };
        }

        private void BindViewModel()
        {
            _serverBox.Bind(TextBox.TextProperty, new Binding("Server") { Mode = BindingMode.TwoWay });
            _portBox.Bind(TextBox.TextProperty, new Binding("Port") { Mode = BindingMode.TwoWay });
            _serviceBox.Bind(TextBox.TextProperty, new Binding("ServiceName") { Mode = BindingMode.TwoWay });
            _userBox.Bind(TextBox.TextProperty, new Binding("Username") { Mode = BindingMode.TwoWay });
            _passBox.Bind(TextBox.TextProperty, new Binding("Password") { Mode = BindingMode.TwoWay });

            _loginButton.Bind(Button.CommandProperty, new Binding("ConnectCommand"));
            _loginButton.Bind(Button.IsEnabledProperty, new Binding("!IsConnecting"));

            _statusText.Bind(TextBlock.TextProperty, new Binding("StatusMessage"));
            _statusText.Bind(TextBlock.ForegroundProperty, new Binding("StatusColor")
            {
                Converter = new ColorNameToBrushConverter()
            });
        }

        private void OnLoginSucceeded(object? sender, (string connectionString, DatabaseMode dbMode) e)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var services = App.ServiceProvider!;
                
                var mainViewModel = new MainWindowViewModel(
                    services.GetRequiredService<IDatabaseService>(),
                    e.dbMode,
                    services.GetRequiredService<ILogger<MainWindowViewModel>>(),
                    services.GetRequiredService<BasicSearchViewModel>(),
                    services.GetRequiredService<AdvancedSearchViewModel>(),
                    services.GetRequiredService<AdminViewModel>()
                );

                var mainWindow = new MainWindow(mainViewModel);
                mainWindow.Show();
                
                desktop.MainWindow = mainWindow;
                Close();
            }
        }

        private void OnMaxAttemptsExceeded(object? sender, EventArgs e)
        {
            Close();
        }
    }
}
