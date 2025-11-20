using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using HotelRatingViewer.Helpers;
using HotelRatingViewer.Models;
using HotelRatingViewer.ViewModels;

namespace HotelRatingViewer.Views.Controls
{
    public class BasicSearchControl : UserControl
    {
        private readonly BasicSearchViewModel _viewModel;

        public BasicSearchControl(BasicSearchViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = _viewModel;
            BuildUI();
        }

        private void BuildUI()
        {
            var darkBackground = new SolidColorBrush(Color.Parse("#1E1E1E"));
            var cardBackground = new SolidColorBrush(Color.Parse("#2D2D30"));
            var cardBorder = new SolidColorBrush(Color.Parse("#3F3F46"));
            var textColor = new SolidColorBrush(Color.Parse("#E0E0E0"));
            var subtleTextColor = new SolidColorBrush(Color.Parse("#A0A0A0"));

            var searchLabel = new TextBlock { Text = "Enter Hotel Name:", FontWeight = FontWeight.Bold, Margin = new Thickness(5), VerticalAlignment = VerticalAlignment.Center, Foreground = textColor };
            var hotelSearchBox = new TextBox { Watermark = "e.g., hampton inn majestic chicago", Width = 400, Margin = new Thickness(5) };
            hotelSearchBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("HotelSearchText") { Mode = Avalonia.Data.BindingMode.TwoWay });
            var searchButton = new Button { Content = "🔍 Search", Width = 100, Margin = new Thickness(5) };
            searchButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("SearchByNameCommand"));
            searchButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("!IsSearching"));

            var searchByNamePanel = new Border { Background = cardBackground, BorderBrush = cardBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(15), Margin = new Thickness(10), Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { searchLabel, hotelSearchBox, searchButton } } };
            var orText = new TextBlock { Text = "OR", FontSize = 14, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10), Foreground = subtleTextColor };
            var cityLabel = new TextBlock { Text = "Select City:", FontWeight = FontWeight.Bold, Margin = new Thickness(5), VerticalAlignment = VerticalAlignment.Center, Foreground = textColor };
            var cityComboBox = new ComboBox { Width = 250, Margin = new Thickness(5), PlaceholderText = "Choose a city..." };
            cityComboBox.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding("Cities"));
            cityComboBox.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding("SelectedCity") { Mode = Avalonia.Data.BindingMode.TwoWay });
            var selectCityPanel = new Border { Background = cardBackground, BorderBrush = cardBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(15), Margin = new Thickness(10), Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { cityLabel, cityComboBox } } };

            var hotelListBox = new ListBox
            {
                Height = 250,
                Margin = new Thickness(10, 5, 10, 10),
                Background = darkBackground,
                
                // *** THIS IS THE CORRECTED LOGIC ***
                // Add a null-check to the template to prevent crashes on rapid data changes.
                ItemTemplate = new FuncDataTemplate<HotelListItem>((item, _) =>
                    item is null ? new Panel() : new Border // If item is null, draw nothing. Otherwise, draw the border.
                    {
                        Padding = new Thickness(15), Margin = new Thickness(0, 4), Background = cardBackground, BorderBrush = cardBorder,
                        BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                        Child = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                            Children =
                            {
                                new StackPanel { Children = { new TextBlock { Text = item.Name, FontWeight = FontWeight.Bold, FontSize = 14, Foreground = textColor }, new TextBlock { Text = $"⭐ {item.Rating:F2} • {item.ReviewCount} reviews", FontSize = 11, Foreground = subtleTextColor, Margin = new Thickness(0, 4, 0, 0) } } },
                                new Border { Background = (IBrush)RatingColorHelper.GetRatingColor(item.Rating), CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 6), [Grid.ColumnProperty] = 1, VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = item.Rating.ToString("F1"), Foreground = Brushes.White, FontWeight = FontWeight.Bold, FontSize = 13 } }
                            }
                        }
                    })
            };

            hotelListBox.Bind(ListBox.ItemsSourceProperty, new Avalonia.Data.Binding("Hotels"));
            hotelListBox.SelectionChanged += (s, e) => { if (hotelListBox.SelectedItem is HotelListItem selectedItem) { _viewModel.SelectedHotel = selectedItem.Name; } };

            var ratingDisplayPanel = new StackPanel { Margin = new Thickness(20), Spacing = 15 };
            ratingDisplayPanel.Bind(StackPanel.IsVisibleProperty, new Avalonia.Data.Binding("CurrentRating") { Converter = new NullToVisibilityConverter() });

            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.CurrentRating))
                {
                    ratingDisplayPanel.Children.Clear();
                    if (_viewModel.CurrentRating != null) { DisplayHotelRating(ratingDisplayPanel, _viewModel.CurrentRating, textColor, subtleTextColor); }
                }
            };

            var ratingCard = new Border { BorderBrush = cardBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Margin = new Thickness(10), Padding = new Thickness(15), Background = cardBackground, BoxShadow = new BoxShadows(new BoxShadow { Blur = 15, Color = Color.FromArgb(40, 0, 0, 0) }), Child = ratingDisplayPanel };
            var contentGrid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"), Margin = new Thickness(10), Background = darkBackground, Children = { searchByNamePanel, orText, selectCityPanel, hotelListBox, ratingCard } };
            Grid.SetRow(searchByNamePanel, 0); Grid.SetRow(orText, 1); Grid.SetRow(selectCityPanel, 2); Grid.SetRow(hotelListBox, 3); Grid.SetRow(ratingCard, 4);
            Content = new ScrollViewer { Content = contentGrid, Background = darkBackground };
        }

        private void DisplayHotelRating(StackPanel panel, HotelRating rating, SolidColorBrush textColor, SolidColorBrush subtleTextColor)
        {
            panel.Children.Add(new TextBlock { Text = rating.HotelName, FontSize = 24, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 0, 0, 5), Foreground = textColor });
            panel.Children.Add(new TextBlock { Text = $"📍 {rating.City}, {rating.Country}", FontSize = 14, Foreground = subtleTextColor, Margin = new Thickness(0, 0, 0, 20) });
            var overallCard = new Border { Background = (IBrush)RatingColorHelper.GetRatingColor(rating.OverallRating), CornerRadius = new CornerRadius(10), Padding = new Thickness(25), Margin = new Thickness(0, 0, 0, 25), Child = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Children = { new TextBlock { Text = "Overall Rating", Foreground = Brushes.White, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.95 }, new TextBlock { Text = rating.OverallRating.ToString("F2"), Foreground = Brushes.White, FontSize = 42, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 5) }, new TextBlock { Text = new string('⭐', Math.Min(Math.Max((int)Math.Round(rating.OverallRating), 0), 5)), FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center } } } };
            panel.Children.Add(overallCard);
            panel.Children.Add(new TextBlock { Text = "Detailed Ratings", FontSize = 17, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 10, 0, 15), Foreground = textColor });
            AddRatingBar(panel, "Cleanliness", rating.CleanlinessScore, textColor);
            AddRatingBar(panel, "Service", rating.ServiceScore, textColor);
            AddRatingBar(panel, "Location", rating.LocationScore, textColor);
            AddRatingBar(panel, "Comfort", rating.ComfortScore, textColor);
            AddRatingBar(panel, "Price", rating.PriceScore, textColor);
            panel.Children.Add(new TextBlock { Text = $"📊 Based on {rating.TotalReviews:N0} reviews", FontSize = 13, Foreground = subtleTextColor, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 25, 0, 0) });
        }
        
        private void AddRatingBar(StackPanel parent, string label, double score, SolidColorBrush textColor)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*,70"), Margin = new Thickness(0, 10, 0, 10) };
            grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.Medium, FontSize = 14, Foreground = textColor, [Grid.ColumnProperty] = 0 });
            var progressBar = new ProgressBar { Minimum = 1, Maximum = 5, Value = score, Height = 24, Foreground = (IBrush)RatingColorHelper.GetRatingColor(score), [Grid.ColumnProperty] = 1, Margin = new Thickness(15, 0) };
            grid.Children.Add(progressBar);
            grid.Children.Add(new TextBlock { Text = score.ToString("F2"), FontWeight = FontWeight.Bold, FontSize = 15, Foreground = (IBrush)RatingColorHelper.GetRatingColor(score), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, [Grid.ColumnProperty] = 2 });
            parent.Children.Add(grid);
        }
    }

    public class NullToVisibilityConverter : Avalonia.Data.Converters.IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value != null;
        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }
}
