using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using HotelRatingViewer.Services;

namespace HotelRatingViewer.Views.Controls
{
    public class AdvancedSearchControl : UserControl
    {
        private readonly DatabaseService _dbService;
        private readonly Action<string, string> _updateStatus;

        private ComboBox _advCityComboBox = null!;
        private ComboBox _overallRatingComboBox = null!;
        private ComboBox _cleanlinessComboBox = null!;
        private ComboBox _serviceComboBox = null!;
        private ComboBox _locationComboBox = null!;
        private ComboBox _comfortComboBox = null!;
        private ComboBox _priceComboBox = null!;
        private Button _advSearchButton = null!;
        private ListBox _advResultsListBox = null!;

        public AdvancedSearchControl(DatabaseService dbService, Action<string, string> updateStatus)
        {
            _dbService = dbService;
            _updateStatus = updateStatus;
            BuildUI();
            LoadCities();
        }

        private void BuildUI()
        {
            var advLabel = new TextBlock
            {
                Text = "Advanced Search",
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(10)
            };

            var cityLabel = new TextBlock
            {
                Text = "City:",
                Width = 100,
                Margin = new Thickness(5)
            };

            _advCityComboBox = new ComboBox
            {
                Width = 200,
                Margin = new Thickness(5)
            };

            var cityPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10),
                Children = { cityLabel, _advCityComboBox }
            };

            var criteriaPanel = new StackPanel
            {
                Margin = new Thickness(20, 10, 10, 10),
                Spacing = 10
            };

            string[] ratingOptions = { "Any", ">1.0", ">2.0", ">3.0", ">3.5", ">4.0", ">4.5" };

            criteriaPanel.Children.Add(CreateRatingComboBox("Overall Rating:", ratingOptions, out _overallRatingComboBox));
            criteriaPanel.Children.Add(CreateRatingComboBox("Cleanliness:", ratingOptions, out _cleanlinessComboBox));
            criteriaPanel.Children.Add(CreateRatingComboBox("Service:", ratingOptions, out _serviceComboBox));
            criteriaPanel.Children.Add(CreateRatingComboBox("Location:", ratingOptions, out _locationComboBox));
            criteriaPanel.Children.Add(CreateRatingComboBox("Comfort:", ratingOptions, out _comfortComboBox));
            criteriaPanel.Children.Add(CreateRatingComboBox("Price:", ratingOptions, out _priceComboBox));

            _advSearchButton = new Button
            {
                Content = "Search",
                Width = 150,
                Margin = new Thickness(20, 10, 10, 10)
            };
            _advSearchButton.Click += AdvSearchButton_Click;

            _advResultsListBox = new ListBox
            {
                Margin = new Thickness(20, 10, 10, 10)
            };

            var scrollViewer = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        advLabel,
                        cityPanel,
                        criteriaPanel,
                        _advSearchButton,
                        new TextBlock
                        {
                            Text = "Results:",
                            FontWeight = FontWeight.Bold,
                            Margin = new Thickness(20, 10, 10, 5)
                        },
                        _advResultsListBox
                    }
                }
            };

            Content = scrollViewer;
        }

        private StackPanel CreateRatingComboBox(string label, string[] options, out ComboBox comboBox)
        {
            comboBox = new ComboBox
            {
                Width = 150,
                Margin = new Thickness(5)
            };

            foreach (var opt in options)
            {
                comboBox.Items.Add(opt);
            }
            comboBox.SelectedIndex = 0;

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock { Text = label, Width = 120, Margin = new Thickness(5) },
                    comboBox
                }
            };

            return panel;
        }

        private async void LoadCities()
        {
            await Task.Run(() =>
            {
                var cities = _dbService.GetCities();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _advCityComboBox.Items.Clear();
                    foreach (var city in cities)
                    {
                        _advCityComboBox.Items.Add(city);
                    }
                });
            });
        }

        private async void AdvSearchButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_advCityComboBox.SelectedItem == null)
            {
                _updateStatus("Please select a city", "Orange");
                return;
            }

            var city = _advCityComboBox.SelectedItem.ToString();
            if (string.IsNullOrEmpty(city)) return;

            var criteria = new Dictionary<string, double?>
            {
                ["overall"] = ParseRatingCriteria(_overallRatingComboBox.SelectedItem?.ToString()),
                ["cleanliness"] = ParseRatingCriteria(_cleanlinessComboBox.SelectedItem?.ToString()),
                ["service"] = ParseRatingCriteria(_serviceComboBox.SelectedItem?.ToString()),
                ["location"] = ParseRatingCriteria(_locationComboBox.SelectedItem?.ToString()),
                ["comfort"] = ParseRatingCriteria(_comfortComboBox.SelectedItem?.ToString()),
                ["price"] = ParseRatingCriteria(_priceComboBox.SelectedItem?.ToString())
            };

            _updateStatus("Searching with filters...", "Blue");

            await Task.Run(() =>
            {
                var results = _dbService.AdvancedSearch(city, criteria);

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _advResultsListBox.Items.Clear();

                    if (results.Count > 0)
                    {
                        foreach (var hotel in results)
                        {
                            _advResultsListBox.Items.Add($"{hotel.HotelName} (Rating: {hotel.OverallRating:F2})");
                        }
                        _updateStatus($"Found {results.Count} matching hotels", "Green");
                    }
                    else
                    {
                        _advResultsListBox.Items.Add("No hotels match the criteria");
                        _updateStatus("No matches found", "Orange");
                    }
                });
            });
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
    }
}
