using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using HotelRatingViewer.Models;
using HotelRatingViewer.Services;

namespace HotelRatingViewer.ViewModels
{
    public partial class BasicSearchViewModel : ViewModelBase
    {
        private readonly IDatabaseService _databaseService;
        private readonly ILogger<BasicSearchViewModel> _logger;

        [ObservableProperty]
        private string _hotelSearchText = "";

        [ObservableProperty]
        private string? _selectedCity;

        [ObservableProperty]
        private string? _selectedHotel;

        [ObservableProperty]
        private HotelRating? _currentRating;

        [ObservableProperty]
        private bool _isSearching = false;

        // -- NEW PROPERTIES FOR REVIEW POPUP --
        [ObservableProperty]
        private bool _isShowingReviews = false;

        public ObservableCollection<string> Cities { get; } = new();
        public ObservableCollection<HotelListItem> Hotels { get; } = new();
        public ObservableCollection<HotelReview> HotelReviews { get; } = new();

        public event EventHandler<(string message, string color)>? StatusChanged;

        public BasicSearchViewModel(IDatabaseService databaseService, ILogger<BasicSearchViewModel> logger)
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
        private async Task SearchByNameAsync()
        {
            if (string.IsNullOrWhiteSpace(HotelSearchText))
            {
                UpdateStatus("Please enter a hotel name", "Orange");
                return;
            }

            IsSearching = true;
            UpdateStatus("Searching...", "Blue");

            try
            {
                // Reset reviews when NEW search happens
                CloseReviews();

                var rating = await _databaseService.GetHotelRatingAsync(HotelSearchText);

                if (rating != null)
                {
                    CurrentRating = rating;
                    UpdateStatus($"Found: {rating.HotelName}", "Green");
                }
                else
                {
                    CurrentRating = null;
                    UpdateStatus("Hotel not found", "Red");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for hotel: {HotelName}", HotelSearchText);
                UpdateStatus("Error performing search", "Red");
            }
            finally
            {
                IsSearching = false;
            }
        }

        // -- NEW COMMAND TO LOAD REVIEWS --
        [RelayCommand]
        private async Task ShowReviewsAsync()
        {
            if (CurrentRating == null) return;

            IsSearching = true;
            UpdateStatus($"Loading reviews for {CurrentRating.HotelName}...", "Blue");

            try
            {
                HotelReviews.Clear();
                var reviews = await _databaseService.GetHotelReviewsAsync(CurrentRating.HotelName);
                
                foreach(var rev in reviews)
                {
                    HotelReviews.Add(rev);
                }

                IsShowingReviews = true;
                UpdateStatus($"Displaying {reviews.Count} reviews", "Green");
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error loading reviews");
                UpdateStatus("Error loading reviews", "Red");
            }
            finally
            {
                IsSearching = false;
            }
        }

        [RelayCommand]
        private void CloseReviews()
        {
            IsShowingReviews = false;
        }

        partial void OnSelectedCityChanged(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _ = LoadHotelsAsync(value);
            }
        }

        private async Task LoadHotelsAsync(string city)
        {
            try
            {
                IsSearching = true;
                UpdateStatus($"Loading hotels in {city}...", "Blue");
                Hotels.Clear();
                
                var hotelItems = await _databaseService.GetHotelListItemsByCityAsync(city);

                foreach (var item in hotelItems)
                {
                    Hotels.Add(item);
                }

                if (Hotels.Count > 0)
                {
                    UpdateStatus($"Found {Hotels.Count} hotels in {city}", "Green");
                }
                else
                {
                    UpdateStatus($"No hotels found for {city}", "Orange");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading hotels for city: {City}", city);
                UpdateStatus("Error loading hotels", "Red");
            }
            finally
            {
                IsSearching = false;
            }
        }

        partial void OnSelectedHotelChanged(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                CloseReviews(); // Reset review popup if they change selection while open
                _ = LoadHotelRatingAsync(value);
            }
        }

        private async Task LoadHotelRatingAsync(string hotelName)
        {
            try
            {
                IsSearching = true;
                var rating = await _databaseService.GetHotelRatingAsync(hotelName);
                CurrentRating = rating;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading hotel rating: {HotelName}", hotelName);
            }
            finally
            {
                IsSearching = false;
            }
        }

        private void UpdateStatus(string message, string color)
        {
            StatusChanged?.Invoke(this, (message, color));
        }
    }
}