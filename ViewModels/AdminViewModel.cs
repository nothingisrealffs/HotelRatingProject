using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using HotelRatingViewer.Services;

namespace HotelRatingViewer.ViewModels
{
    public partial class AdminViewModel : ViewModelBase
    {
        private readonly IDatabaseService _databaseService;
        private readonly ILogger<AdminViewModel> _logger;

        [ObservableProperty]
        private string _newFeatureName = "";

        [ObservableProperty]
        private string? _selectedFeature;

        [ObservableProperty]
        private string _newSeedPhrase = "";

        [ObservableProperty]
        private int _selectedWeightIndex = 0;

        [ObservableProperty]
        private bool _isProcessing = false;

        public ObservableCollection<string> Features { get; } = new();
        public ObservableCollection<string> WeightOptions { get; } = new()
        {
            "+1 (Positive)", "-1 (Negative)"
        };

        public event EventHandler<(string message, string color)>? StatusChanged;

        public AdminViewModel(IDatabaseService databaseService, ILogger<AdminViewModel> logger)
        {
            _databaseService = databaseService;
            _logger = logger;
            _ = LoadFeaturesAsync();
        }

        private async Task LoadFeaturesAsync()
        {
            try
            {
                var features = await _databaseService.GetActiveFeaturesAsync();
                Features.Clear();
                foreach (var feature in features)
                {
                    Features.Add(feature);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading features");
                UpdateStatus("Error loading features", "Red");
            }
        }

        [RelayCommand]
        private async Task AddFeatureAsync()
        {
            if (string.IsNullOrWhiteSpace(NewFeatureName))
            {
                UpdateStatus("Please enter a feature name", "Orange");
                return;
            }

            IsProcessing = true;

            try
            {
                var success = await _databaseService.AddFeatureAsync(NewFeatureName);

                if (success)
                {
                    UpdateStatus($"✓ Feature '{NewFeatureName}' added successfully", "Green");
                    NewFeatureName = "";
                    await LoadFeaturesAsync();
                }
                else
                {
                    UpdateStatus("Error adding feature", "Red");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding feature: {FeatureName}", NewFeatureName);
                UpdateStatus("Error adding feature", "Red");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task AddSeedWordAsync()
        {
            if (string.IsNullOrEmpty(SelectedFeature))
            {
                UpdateStatus("Please select a feature", "Orange");
                return;
            }

            if (string.IsNullOrWhiteSpace(NewSeedPhrase))
            {
                UpdateStatus("Please enter a seed phrase", "Orange");
                return;
            }

            IsProcessing = true;

            try
            {
                var weight = SelectedWeightIndex == 0 ? 1 : -1;
                var success = await _databaseService.AddSeedWordAsync(SelectedFeature, NewSeedPhrase, weight);

                if (success)
                {
                    UpdateStatus($"✓ Seed word '{NewSeedPhrase}' added to {SelectedFeature}", "Green");
                    NewSeedPhrase = "";
                }
                else
                {
                    UpdateStatus("Error adding seed word", "Red");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding seed word: {Phrase} to {Feature}", NewSeedPhrase, SelectedFeature);
                UpdateStatus("Error adding seed word", "Red");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void UpdateStatus(string message, string color)
        {
            StatusChanged?.Invoke(this, (message, color));
        }
    }
}
