using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using HotelRatingViewer.Helpers;
using HotelRatingViewer.ViewModels;

namespace HotelRatingViewer.Views.Controls
{
    public class AdvancedSearchControl : UserControl
    {
        private readonly AdvancedSearchViewModel _viewModel;

        public AdvancedSearchControl(AdvancedSearchViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = _viewModel;
            BuildUI();
        }

        private void BuildUI()
        {
            // Dark mode colors
            var darkBackground = new SolidColorBrush(Color.Parse("#1E1E1E"));
            var cardBackground = new SolidColorBrush(Color.Parse("#2D2D30"));
            var cardBorder = new SolidColorBrush(Color.Parse("#3F3F46"));
            var textColor = new SolidColorBrush(Color.Parse("#E0E0E0"));
            var subtleTextColor = new SolidColorBrush(Color.Parse("#A0A0A0"));

            var advLabel = new TextBlock
            {
                Text = "🔬 Advanced Search",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(10, 10, 10, 20),
                Foreground = textColor
            };

            // City selection
            var cityCard = new Border
            {
                BorderBrush = cardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Margin = new Thickness(10),
                Background = cardBackground,
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock 
                        { 
                            Text = "Location", 
                            FontWeight = FontWeight.Bold, 
                            FontSize = 15,
                            Margin = new Thickness(0, 0, 0, 12),
                            Foreground = textColor
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Children =
                            {
                                new TextBlock 
                                { 
                                    Text = "City:", 
                                    Width = 100, 
                                    VerticalAlignment = VerticalAlignment.Center,
                                    Foreground = textColor
                                },
                                CreateComboBox("Cities", "SelectedCity", 250)
                            }
                        }
                    }
                }
            };

            // Rating criteria card
            var criteriaCard = new Border
            {
                BorderBrush = cardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Margin = new Thickness(10),
                Background = cardBackground,
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock 
                        { 
                            Text = "Rating Criteria", 
                            FontWeight = FontWeight.Bold, 
                            FontSize = 15,
                            Margin = new Thickness(0, 0, 0, 12),
                            Foreground = textColor
                        },
                        CreateRatingRow("Overall Rating:", "SelectedOverallRating", textColor),
                        CreateRatingRow("Cleanliness:", "SelectedCleanliness", textColor),
                        CreateRatingRow("Service:", "SelectedService", textColor),
                        CreateRatingRow("Location:", "SelectedLocation", textColor),
                        CreateRatingRow("Comfort:", "SelectedComfort", textColor),
                        CreateRatingRow("Price:", "SelectedPrice", textColor)
                    }
                }
            };

            var searchButton = new Button
            {
                Content = "🔍 Search Hotels",
                Width = 200,
                Height = 42,
                Margin = new Thickness(10, 15, 10, 10),
                FontSize = 14,
                FontWeight = FontWeight.Bold
            };
            searchButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("SearchCommand"));
            searchButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("!IsSearching"));

            // Results header
            var resultsHeader = new TextBlock
            {
                Text = "Search Results",
                FontWeight = FontWeight.Bold,
                FontSize = 17,
                Margin = new Thickness(10, 20, 10, 10),
                Foreground = textColor
            };

            var resultsListBox = new ListBox
            {
                Margin = new Thickness(10),
                Background = darkBackground,
                ItemTemplate = new FuncDataTemplate<HotelSearchResult>((item, _) =>
                    new Border
                    {
                        Padding = new Thickness(18),
                        Margin = new Thickness(0, 4),
                        Background = cardBackground,
                        BorderBrush = cardBorder,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(8),
                        Child = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                            Children =
                            {
                                new StackPanel
                                {
                                    Children =
                                    {
                                        new TextBlock 
                                        { 
                                            Text = item.HotelName, 
                                            FontWeight = FontWeight.Bold,
                                            FontSize = 16,
                                            Foreground = textColor
                                        },
                                        new TextBlock 
                                        { 
                                            Text = $"📍 {item.City}, {item.Country}",
                                            FontSize = 13,
                                            Foreground = subtleTextColor,
                                            Margin = new Thickness(0, 6, 0, 0)
                                        },
                                        new TextBlock
                                        {
                                            Text = item.Stars,
                                            FontSize = 15,
                                            Margin = new Thickness(0, 6, 0, 0)
                                        }
                                    }
                                },
                                new Border
                                {
                                    Background = (IBrush)RatingColorHelper.GetRatingColor(item.Rating),
                                    CornerRadius = new CornerRadius(20),
                                    Padding = new Thickness(16, 10),
                                    [Grid.ColumnProperty] = 1,
                                    VerticalAlignment = VerticalAlignment.Center,
                                    Child = new TextBlock
                                    {
                                        Text = item.Rating.ToString("F2"),
                                        Foreground = Brushes.White,
                                        FontWeight = FontWeight.Bold,
                                        FontSize = 17
                                    }
                                }
                            }
                        }
                    })
            };
            resultsListBox.Bind(ListBox.ItemsSourceProperty, new Avalonia.Data.Binding("Results"));

            var scrollViewer = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Background = darkBackground,
                    Children =
                    {
                        advLabel,
                        cityCard,
                        criteriaCard,
                        searchButton,
                        resultsHeader,
                        resultsListBox
                    }
                },
                Background = darkBackground
            };

            Content = scrollViewer;
        }

        private ComboBox CreateComboBox(string itemsSource, string selectedItemBinding, int width)
        {
            var comboBox = new ComboBox { Width = width };
            comboBox.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding(itemsSource));
            comboBox.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding(selectedItemBinding) { Mode = Avalonia.Data.BindingMode.TwoWay });
            return comboBox;
        }

        private StackPanel CreateRatingRow(string label, string bindingPath, SolidColorBrush textColor)
        {
            var comboBox = new ComboBox { Width = 150 };
            comboBox.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding("RatingOptions"));
            comboBox.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding(bindingPath) { Mode = Avalonia.Data.BindingMode.TwoWay });

            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock 
                    { 
                        Text = label, 
                        Width = 150, 
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = textColor
                    },
                    comboBox
                }
            };
        }
    }
}
