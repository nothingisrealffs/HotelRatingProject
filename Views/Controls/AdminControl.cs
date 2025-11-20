using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using HotelRatingViewer.ViewModels;

namespace HotelRatingViewer.Views.Controls
{
    public class AdminControl : UserControl
    {
        private readonly AdminViewModel _viewModel;

        public AdminControl(AdminViewModel viewModel)
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
            var warningBackground = new SolidColorBrush(Color.Parse("#3A3A1F"));
            var warningBorder = new SolidColorBrush(Color.Parse("#FFC107"));

            var adminLabel = new TextBlock
            {
                Text = "🔐 Admin Functions (Bonus Features)",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(10, 10, 10, 5),
                Foreground = textColor
            };

            var warningBanner = new Border
            {
                Background = warningBackground,
                BorderBrush = warningBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15),
                Margin = new Thickness(10),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock 
                        { 
                            Text = "⚠️", 
                            FontSize = 24,
                            Margin = new Thickness(0, 0, 10, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = "Admin privileges required. Changes affect the database directly.",
                            TextWrapping = TextWrapping.Wrap,
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = new SolidColorBrush(Color.Parse("#FFC107"))
                        }
                    }
                }
            };

            var newFeatureNameBox = new TextBox 
            { 
                Width = 250, 
                Watermark = "e.g., Amenities",
                Margin = new Thickness(5)
            };
            newFeatureNameBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("NewFeatureName") { Mode = Avalonia.Data.BindingMode.TwoWay });

            var addFeatureButton = new Button 
            { 
                Content = "➕ Add Feature", 
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(15, 8)
            };
            addFeatureButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("AddFeatureCommand"));
            addFeatureButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("!IsProcessing"));

            var featureSection = new Border
            {
                BorderBrush = cardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(10),
                Padding = new Thickness(20),
                Background = cardBackground,
                Child = new StackPanel
                {
                    Spacing = 15,
                    Children =
                    {
                        new TextBlock 
                        { 
                            Text = "Add New Feature", 
                            FontWeight = FontWeight.Bold,
                            FontSize = 16,
                            Foreground = textColor
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Children =
                            {
                                new TextBlock 
                                { 
                                    Text = "Feature Name:", 
                                    Width = 120,
                                    VerticalAlignment = VerticalAlignment.Center,
                                    Foreground = textColor
                                },
                                newFeatureNameBox,
                                addFeatureButton
                            }
                        }
                    }
                }
            };

            var seedFeatureComboBox = new ComboBox { Width = 250, Margin = new Thickness(5) };
            seedFeatureComboBox.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding("Features"));
            seedFeatureComboBox.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding("SelectedFeature") { Mode = Avalonia.Data.BindingMode.TwoWay });

            var newSeedPhraseBox = new TextBox { Width = 250, Watermark = "e.g., excellent wifi", Margin = new Thickness(5) };
            newSeedPhraseBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("NewSeedPhrase") { Mode = Avalonia.Data.BindingMode.TwoWay });

            var seedWeightComboBox = new ComboBox { Width = 250, Margin = new Thickness(5) };
            seedWeightComboBox.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding("WeightOptions"));
            seedWeightComboBox.Bind(ComboBox.SelectedIndexProperty, new Avalonia.Data.Binding("SelectedWeightIndex") { Mode = Avalonia.Data.BindingMode.TwoWay });

            var addSeedButton = new Button 
            { 
                Content = "➕ Add Seed Word",
                Padding = new Thickness(15, 8),
                Margin = new Thickness(0, 15, 0, 0)
            };
            addSeedButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("AddSeedWordCommand"));
            addSeedButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("!IsProcessing"));

            var seedSection = new Border
            {
                BorderBrush = cardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(10),
                Padding = new Thickness(20),
                Background = cardBackground,
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock 
                        { 
                            Text = "Add New Seed Word", 
                            FontWeight = FontWeight.Bold,
                            FontSize = 16,
                            Foreground = textColor
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Children =
                            {
                                new TextBlock 
                                { 
                                    Text = "Feature:", 
                                    Width = 120,
                                    VerticalAlignment = VerticalAlignment.Center,
                                    Foreground = textColor
                                },
                                seedFeatureComboBox
                            }
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Children =
                            {
                                new TextBlock 
                                { 
                                    Text = "Seed Phrase:", 
                                    Width = 120,
                                    VerticalAlignment = VerticalAlignment.Center,
                                    Foreground = textColor
                                },
                                newSeedPhraseBox
                            }
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Children =
                            {
                                new TextBlock 
                                { 
                                    Text = "Weight:", 
                                    Width = 120,
                                    VerticalAlignment = VerticalAlignment.Center,
                                    Foreground = textColor
                                },
                                seedWeightComboBox
                            }
                        },
                        addSeedButton
                    }
                }
            };

            var scrollViewer = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Background = darkBackground,
                    Children = { adminLabel, warningBanner, featureSection, seedSection }
                },
                Background = darkBackground
            };

            Content = scrollViewer;
        }
    }
}
