using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using HotelRatingViewer.Models;
using HotelRatingViewer.Services;

namespace HotelRatingViewer.ViewModels
{
    public partial class LoginViewModel : ViewModelBase
    {
        private readonly IDatabaseService _databaseService;
        private readonly ILogger<LoginViewModel> _logger;

        [ObservableProperty]
        private string _server = "";

        [ObservableProperty]
        private string _port = "1521";

        [ObservableProperty]
        private string _serviceName = "";

        [ObservableProperty]
        private string _username = "";

        [ObservableProperty]
        private string _password = "";

        [ObservableProperty]
        private string _statusMessage = "Please enter your database credentials";

        [ObservableProperty]
        private string _statusColor = "Gray";

        [ObservableProperty]
        private bool _isConnecting = false;

        private int _attemptCount = 0;
        private const int MaxAttempts = 3;

        public event EventHandler<(string connectionString, DatabaseMode dbMode)>? LoginSucceeded;
        public event EventHandler? MaxAttemptsExceeded;

        public LoginViewModel(IDatabaseService databaseService, ILogger<LoginViewModel> logger)
        {
            _databaseService = databaseService;
            _logger = logger;
        }

        public void SetConfigValues(DatabaseConfig config)
        {
            Server = config.Server;
            Port = config.Port;
            ServiceName = config.ServiceName;
            Username = config.Username;
            Password = config.Password;
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            if (string.IsNullOrWhiteSpace(Server) || string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(ServiceName))
            {
                StatusMessage = "Please fill in all required fields";
                StatusColor = "Orange";
                return;
            }

            IsConnecting = true;
            StatusMessage = "Connecting...";
            StatusColor = "Blue";

            try
            {
                var success = await _databaseService.ValidateConnectionAsync(Server, Port, ServiceName, Username, Password);

                if (success)
                {
                    var dbMode = await _databaseService.GetDatabaseModeAsync();
                    
                    if (dbMode.HasHotelRatingSchema || dbMode.HasTables)
                    {
                        _logger.LogInformation("Login successful for user: {Username}", Username);
                        LoginSucceeded?.Invoke(this, ("", dbMode));
                    }
                    else
                    {
                        HandleFailedLogin($"Connection failed: {dbMode.ErrorMessage}");
                    }
                }
                else
                {
                    HandleFailedLogin("Connection failed. Please check your credentials.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for user: {Username}", Username);
                HandleFailedLogin($"Connection error: {ex.Message}");
            }
            finally
            {
                IsConnecting = false;
            }
        }

        private void HandleFailedLogin(string message)
        {
            _attemptCount++;

            if (_attemptCount >= MaxAttempts)
            {
                StatusMessage = $"Maximum login attempts ({MaxAttempts}) exceeded. Application will close.";
                StatusColor = "Red";
                _logger.LogWarning("Maximum login attempts exceeded for user: {Username}", Username);
                MaxAttemptsExceeded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                StatusMessage = $"{message} (Attempts: {_attemptCount}/{MaxAttempts})";
                StatusColor = "Red";
            }
        }
    }
}
