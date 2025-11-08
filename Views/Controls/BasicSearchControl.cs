using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using HotelRatingViewer.Helpers;
using HotelRatingViewer.Models;
using HotelRatingViewer.Services;

namespace HotelRatingViewer.Views.Controls
{
    public class BasicSearchControl : UserControl
    {
        private readonly DatabaseService _dbService;
        private readonly Action<string, string> _updateStatus;

        private TextBox _hotelSearchBox = null!;
        private ComboBox _cityComboBox = null!;
        private ListBox _hotelListBox = null!;
        private Button _searchByNameButton = null!;
        private StackPanel _ratingDisplayPanel = null!;

        public BasicSearchControl(DatabaseService dbService, Action<string, string> updateStatus)
        {
            _dbService = dbService;
            _updateStatus = updateStatus;
            BuildUI();
            LoadCities();
        }

        private void BuildUI()
        {
            var searchLabel = new TextBlock
            {
                Text = "Enter Hotel Name:",
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(5)
            };

            _hotelSearchBox = new TextBox
            {
                Watermark = "e.g., hampton inn majestic chicago",
                Width = 400,
                Margin = new Thickness(5)
            };

            _searchByNameButton = new Button
            {
                Content = "Search",
                Width = 100,
                Margin = new Thickness(5)
            };
            _searchByNameButton.Click += SearchByName_Click;

            var searchByNamePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10),
                Children = { searchLabel, _hotelSearchBox, _searchByNameButton }
            };

            var orText = new TextBlock
            {
                Text = "OR",
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };

            var cityLabel = new TextBlock
            {
                Text = "Select City:",
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(5)
            };

            _cityComboBox = new ComboBox
            {
                Width = 200,
                Margin = new Thickness(5),
                PlaceholderText = "Choose a city..."
            };
            _cityComboBox.SelectionChanged += CityComboBox_SelectionChanged;

            var selectCityPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10),
                Children = { cityLabel, _cityComboBox }
            };

            _hotelListBox = new ListBox
            {
                Height = 150,
                Margin = new Thickness(10, 5, 10, 10)
            };
            _hotelListBox.SelectionChanged += HotelListBox_SelectionChanged;

            _ratingDisplayPanel = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 10
            };

            var scrollViewer = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        searchByNamePanel,
                        orText,
                        selectCityPanel,
                        _hotelListBox,
                        new Border
                        {
                            BorderBrush = Brushes.LightGray,
                            BorderThickness = new Thickness(1),
                            Margin = new Thickness(10),
                            Padding = new Thickness(10),
                            Child = _ratingDisplayPanel
                        }
                    }
                }
            };

            Content = scrollViewer;
        }

        private async void LoadCities()
        {
            await Task.Run(() =>
            {
                var cities = _dbService.GetCities();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _cityComboBox.Items.Clear();
                    foreach (var city in cities)
                    {
                        _cityComboBox.Items.Add(city);
                    }
                });
            });
        }

        private async void SearchByName_Click(object? sender, RoutedEventArgs e)
        {
            var hotelName = _hotelSearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(hotelName))
            {
                _updateStatus("Please enter a hotel name", "Orange");
                return;
            }

            _updateStatus("Searching...", "Blue");

            await Task.Run(() =>
            {
                var rating = _dbService.GetHotelRating(hotelName);

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (rating != null)
                    {
                        DisplayHotelRating(rating);
                        _updateStatus($"Found: {rating.HotelName}", "Green");
                    }
                    else
                    {
                        _ratingDisplayPanel.Children.Clear();
                        _ratingDisplayPanel.Children.Add(new TextBlock
                        {
                            Text = "Hotel not found. Please try a different name.",
                            Foreground = Brushes.Red
                        });
                        _updateStatus("Hotel not found", "Red");
                    }
                });
            });
        }

        private async void CityComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_cityComboBox.SelectedItem == null) return;

            var city = _cityComboBox.SelectedItem.ToString();
            if (string.IsNullOrEmpty(city)) return;

            await Task.Run(() =>
            {
                var hotels = _dbService.GetHotelsByCity(city);

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _hotelListBox.Items.Clear();
                    foreach (var hotel in hotels)
                    {
                        _hotelListBox.Items.Add(hotel);
                    }

                    if (hotels.Count > 0)
                    {
                        _updateStatus($"Found {hotels.Count} hotels in {city}", "Green");
                    }
                });
            });
        }

        private async void HotelListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_hotelListBox.SelectedItem == null) return;

            var hotelName = _hotelListBox.SelectedItem.ToString();
            if (string.IsNullOrEmpty(hotelName)) return;

            await Task.Run(() =>
            {
                var rating = _dbService.GetHotelRating(hotelName);

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (rating != null)
                    {
                        DisplayHotelRating(rating);
                    }
                });
            });
        }

        private void DisplayHotelRating(HotelRating rating)
        {
            _ratingDisplayPanel.Children.Clear();

            _ratingDisplayPanel.Children.Add(new TextBlock
            {
                Text = rating.HotelName,
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            });

            _ratingDisplayPanel.Children.Add(new TextBlock
            {
                Text = $"{rating.City}, {rating.Country}",
                FontSize = 12,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 15)
            });

            var overallPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 15)
            };
            overallPanel.Children.Add(new TextBlock
            {
                Text = "Overall Rating:",
                FontWeight = FontWeight.Bold,
                Width = 150
            });
            overallPanel.Children.Add(new TextBlock
            {
                Text = rating.OverallRating.ToString("F2"),
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = (IBrush)RatingColorHelper.GetRatingColor(rating.OverallRating)
            });
            _ratingDisplayPanel.Children.Add(overallPanel);

            AddRatingRow("Cleanliness:", rating.CleanlinessScore);
            AddRatingRow("Service:", rating.ServiceScore);
            AddRatingRow("Location:", rating.LocationScore);
            AddRatingRow("Comfort:", rating.ComfortScore);
            AddRatingRow("Price:", rating.PriceScore);

            _ratingDisplayPanel.Children.Add(new TextBlock
            {
                Text = $"Based on {rating.TotalReviews} reviews",
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 15, 0, 0)
            });
        }

        private void AddRatingRow(string label, double score)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 3, 0, 3)
            };

            panel.Children.Add(new TextBlock
            {
                Text = label,
                Width = 150
            });

            panel.Children.Add(new TextBlock
            {
                Text = score.ToString("F2"),
                FontWeight = FontWeight.Bold,
                Foreground = (IBrush)RatingColorHelper.GetRatingColor(score)
            });

            _ratingDisplayPanel.Children.Add(panel);
        }
    }
}
