using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using HotelRatingViewer.Models;
using HotelRatingViewer.Services;

namespace HotelRatingViewer.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IDatabaseService _databaseService;
        private readonly ILogger<MainWindowViewModel> _logger;

        [ObservableProperty]
        private string _statusMessage = "Connected";

        [ObservableProperty]
        private string _statusColor = "Green";

        [ObservableProperty]
        private bool _hasHotelRatingSchema;

        [ObservableProperty]
        private bool _isAdminAuthenticated = false;

        [ObservableProperty]
        private bool _isLoading = false;

        public BasicSearchViewModel BasicSearchViewModel { get; }
        public AdvancedSearchViewModel AdvancedSearchViewModel { get; }
        public AdminViewModel AdminViewModel { get; }

        public MainWindowViewModel(
            IDatabaseService databaseService,
            DatabaseMode dbMode,
            ILogger<MainWindowViewModel> logger,
            BasicSearchViewModel basicSearchViewModel,
            AdvancedSearchViewModel advancedSearchViewModel,
            AdminViewModel adminViewModel)
        {
            _databaseService = databaseService;
            _logger = logger;
            HasHotelRatingSchema = dbMode.HasHotelRatingSchema;

            BasicSearchViewModel = basicSearchViewModel;
            AdvancedSearchViewModel = advancedSearchViewModel;
            AdminViewModel = adminViewModel;

            // Subscribe to status updates from child view models
            BasicSearchViewModel.StatusChanged += OnStatusChanged;
            AdvancedSearchViewModel.StatusChanged += OnStatusChanged;
            AdminViewModel.StatusChanged += OnStatusChanged;

            // Subscribe to loading states
            BasicSearchViewModel.PropertyChanged += (s, e) => UpdateLoadingState();
            AdvancedSearchViewModel.PropertyChanged += (s, e) => UpdateLoadingState();
            AdminViewModel.PropertyChanged += (s, e) => UpdateLoadingState();

            if (HasHotelRatingSchema)
            {
                StatusMessage = $"✓ Connected to Hotel Rating System (Schema: {_databaseService.CurrentSchema})";
                StatusColor = "Green";
            }
            else
            {
                StatusMessage = "⚠ Hotel schema not found. Fallback mode active.";
                StatusColor = "Orange";
            }

            _logger.LogInformation("Main window initialized. HasHotelSchema: {HasSchema}", HasHotelRatingSchema);
        }

        private void OnStatusChanged(object? sender, (string message, string color) status)
        {
            StatusMessage = status.message;
            StatusColor = status.color;
        }

        private void UpdateLoadingState()
        {
            IsLoading = BasicSearchViewModel.IsSearching ||
                       AdvancedSearchViewModel.IsSearching ||
                       AdminViewModel.IsProcessing;
        }

        public void AuthenticateAdmin(bool authenticated)
        {
            IsAdminAuthenticated = authenticated;
        }
    }
}
