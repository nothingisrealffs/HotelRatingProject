using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using HotelRatingViewer.Models;
using HotelRatingViewer.Services;

namespace HotelRatingViewer.ViewModels
{
    public partial class AdvancedSearchViewModel : ViewModelBase
    {
        private readonly IDatabaseService _databaseService;
        private readonly ILogger<AdvancedSearchViewModel> _logger;

        [ObservableProperty]
        private string? _selectedCity;

        [ObservableProperty]
        private string _selectedOverallRating = "Any";

        [ObservableProperty]
        private string _selectedCleanliness = "Any";

        [ObservableProperty]
        private string _selectedService = "Any";

        [ObservableProperty]
        private string _selectedLocation = "Any";

        [ObservableProperty]
        private string _selectedComfort = "Any";

        [ObservableProperty]
        private string _selectedPrice = "Any";

        [ObservableProperty]
        private bool _isSearching = false;

        public ObservableCollection<string> Cities { get; } = new();
        public ObservableCollection<HotelSearchResult> Results { get; } = new();
        public ObservableCollection<string> RatingOptions { get; } = new()
        {
            "Any", ">1.0", ">2.0", ">3.0", ">3.5", ">4.0", ">4.5"
        };

        public event EventHandler<(string message, string color)>? StatusChanged;

        public AdvancedSearchViewModel(IDatabaseService databaseService, ILogger<AdvancedSearchViewModel> logger)
        {
            _databaseService = databaseService;
            _logger = logger;
            _ = LoadCitiesAsync();
        }

        private async Task LoadCitiesAsync()
        {
            try
            {
                var cities = await _databaseService.GetCitiesAsync();
                Cities.Clear();
                foreach (var city in cities)
                {
                    Cities.Add(city);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading cities");
                UpdateStatus("Error loading cities", "Red");
            }
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            if (string.IsNullOrEmpty(SelectedCity))
            {
                UpdateStatus("Please select a city", "Orange");
                return;
            }

            IsSearching = true;
            UpdateStatus("Searching with filters...", "Blue");

            try
            {
                var criteria = new Dictionary<string, double?>
                {
                    ["overall"] = ParseRatingCriteria(SelectedOverallRating),
                    ["cleanliness"] = ParseRatingCriteria(SelectedCleanliness),
                    ["service"] = ParseRatingCriteria(SelectedService),
                    ["location"] = ParseRatingCriteria(SelectedLocation),
                    ["comfort"] = ParseRatingCriteria(SelectedComfort),
                    ["price"] = ParseRatingCriteria(SelectedPrice)
                };

                var searchResults = await _databaseService.AdvancedSearchAsync(SelectedCity, criteria);

                Results.Clear();
                if (searchResults.Count > 0)
                {
                    foreach (var hotel in searchResults)
                    {
                        Results.Add(new HotelSearchResult
                        {
                            HotelName = hotel.HotelName,
                            Rating = hotel.OverallRating,
                            City = hotel.City,
                            Country = hotel.Country
                        });
                    }
                    UpdateStatus($"Found {searchResults.Count} matching hotels", "Green");
                }
                else
                {
                    UpdateStatus("No matches found", "Orange");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing advanced search");
                UpdateStatus("Error performing search", "Red");
            }
            finally
            {
                IsSearching = false;
            }
        }

        private double? ParseRatingCriteria(string? criteria)
        {
            if (string.IsNullOrEmpty(criteria) || criteria == "Any")
                return null;

            var numStr = criteria.Replace(">", "").Trim();
            if (double.TryParse(numStr, out double val))
                return val;

            return null;
        }

        private void UpdateStatus(string message, string color)
        {
            StatusChanged?.Invoke(this, (message, color));
        }
    }

    public class HotelSearchResult
    {
        public string HotelName { get; set; } = "";
        public double Rating { get; set; }
        public string City { get; set; } = "";
        public string Country { get; set; } = "";
        public string Stars => new string('⭐', Math.Min((int)Math.Round(Rating), 5));
    }
}
