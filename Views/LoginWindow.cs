using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using HotelRatingViewer.Models;
using HotelRatingViewer.Services;

namespace HotelRatingViewer.Views
{
    public class LoginWindow : Window
    {
        private TextBox _serverBox = null!;
        private TextBox _portBox = null!;
        private TextBox _serviceBox = null!;
        private TextBox _userBox = null!;
        private TextBox _passBox = null!;
        private Button _loginButton = null!;
        private TextBlock _statusText = null!;
        private int _attemptCount = 0;
        private const int MaxAttempts = 3;

        // Constructor with no parameters (default login)
        public LoginWindow() : this(null, null)
        {
        }

        // Constructor with error message and optional config
        public LoginWindow(string? errorMessage = null, DatabaseConfig? config = null)
        {
            Title = "Hotel Rating System - Database Login";
            Width = 500;
            Height = 400;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            BuildUI();

            // Pre-fill fields if config was provided
            if (config != null)
            {
                _serverBox.Text = config.Server;
                _portBox.Text = config.Port;
                _serviceBox.Text = config.ServiceName;
                _userBox.Text = config.Username;
                _passBox.Text = config.Password;
            }

            // Show error message if provided
            if (!string.IsNullOrEmpty(errorMessage))
            {
                _statusText.Text = errorMessage;
                _statusText.Foreground = Brushes.OrangeRed;
            }
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

            _serverBox = new TextBox
            {
                Watermark = "Server (e.g., localhost)",
                Width = 300,
                Margin = new Thickness(5)
            };

            _portBox = new TextBox
            {
                Watermark = "Port",
                Width = 300,
                Text = "1521",
                Margin = new Thickness(5)
            };

            _serviceBox = new TextBox
            {
                Watermark = "Service Name",
                Width = 300,
                Margin = new Thickness(5)
            };

            _userBox = new TextBox
            {
                Watermark = "Username",
                Width = 300,
                Margin = new Thickness(5)
            };

            _passBox = new TextBox
            {
                Watermark = "Password",
                Width = 300,
                Margin = new Thickness(5),
                PasswordChar = '•'
            };

            _loginButton = new Button
            {
                Content = "Connect",
                Width = 150,
                Height = 35,
                Margin = new Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _loginButton.Click += LoginButton_Click;

            _statusText = new TextBlock
            {
                Text = $"Please enter your database credentials (Attempts: {_attemptCount}/{MaxAttempts})",
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 15, 0, 0),
                FontSize = 12,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 450
            };

            var formPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    CreateFormRow("Server:", _serverBox),
                    CreateFormRow("Port:", _portBox),
                    CreateFormRow("Service:", _serviceBox),
                    CreateFormRow("Username:", _userBox),
                    CreateFormRow("Password:", _passBox)
                }
            };

            var mainPanel = new StackPanel
            {
                Children = { titleText, subtitleText, formPanel, _loginButton, _statusText }
            };

            Content = mainPanel;
        }

        private StackPanel CreateFormRow(string label, TextBox textBox)
        {
            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                Children =
                {
                    new TextBlock
                    {
                        Text = label,
                        Width = 100,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Right,
                        Margin = new Thickness(0, 0, 10, 0)
                    },
                    textBox
                }
            };
        }

        private void LoginButton_Click(object? sender, RoutedEventArgs e)
        {
            var server = _serverBox.Text?.Trim();
            var port = _portBox.Text?.Trim() ?? "1521";
            var service = _serviceBox.Text?.Trim();
            var user = _userBox.Text?.Trim();
            var pass = _passBox.Text ?? "";

            if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(service))
            {
                _statusText.Text = "Please fill in all required fields";
                _statusText.Foreground = Brushes.OrangeRed;
                return;
            }

            _statusText.Text = "Connecting...";
            _statusText.Foreground = Brushes.Blue;
            _loginButton.IsEnabled = false;

            var authService = new DatabaseService();
            var success = authService.ValidateConnection(server, port, service, user, pass,
                                                         out string connectionString,
                                                         out DatabaseMode dbMode);

            if (success)
            {
                if (dbMode.HasHotelRatingSchema)
                {
                    OpenMainWindow(connectionString, authService, dbMode);
                }
                else if (dbMode.HasTables)
                {
                    OpenMainWindow(connectionString, authService, dbMode);
                }
                else
                {
                    HandleFailedLogin($"Connection failed: {dbMode.ErrorMessage}");
                }
            }
            else
            {
                HandleFailedLogin($"Connection failed: {dbMode.ErrorMessage}");
            }
        }

        private void HandleFailedLogin(string message)
        {
            _attemptCount++;

            if (_attemptCount >= MaxAttempts)
            {
                _statusText.Text = $"Maximum login attempts ({MaxAttempts}) exceeded. Application will close.";
                _statusText.Foreground = Brushes.Red;
                _loginButton.IsEnabled = false;

                var timer = new System.Timers.Timer(2000);
                timer.Elapsed += (s, e) =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                        {
                            desktop.Shutdown();
                        }
                    });
                };
                timer.AutoReset = false;
                timer.Start();
            }
            else
            {
                _statusText.Text = $"{message} (Attempts: {_attemptCount}/{MaxAttempts})";
                _statusText.Foreground = Brushes.Red;
                _loginButton.IsEnabled = true;
            }
        }

        private void OpenMainWindow(string connectionString, DatabaseService dbService, DatabaseMode dbMode)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow(connectionString, dbService, dbMode);
                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                Close();
            }
        }
    }
}
