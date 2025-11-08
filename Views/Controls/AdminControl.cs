using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using HotelRatingViewer.Services;

namespace HotelRatingViewer.Views.Controls
{
    public class AdminControl : UserControl
    {
        private readonly DatabaseService _dbService;
        private readonly Action<string, string> _updateStatus;

        private TextBox _newFeatureNameBox = null!;
        private TextBox _newSeedPhraseBox = null!;
        private ComboBox _seedFeatureComboBox = null!;
        private ComboBox _seedWeightComboBox = null!;
        private Button _addFeatureButton = null!;
        private Button _addSeedButton = null!;

        public AdminControl(DatabaseService dbService, Action<string, string> updateStatus)
        {
            _dbService = dbService;
            _updateStatus = updateStatus;
            BuildUI();
            LoadFeatures();
        }

        private void BuildUI()
        {
            var adminLabel = new TextBlock
            {
                Text = "Admin Functions (Bonus Features)",
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(10)
            };

            var warningText = new TextBlock
            {
                Text = "⚠️ Admin privileges required",
                Foreground = Brushes.OrangeRed,
                Margin = new Thickness(10, 0, 10, 10)
            };

            _newFeatureNameBox = new TextBox { Width = 200, Watermark = "e.g., Amenities" };
            _addFeatureButton = new Button { Content = "Add Feature", Margin = new Thickness(10, 0, 0, 0) };
            _addFeatureButton.Click += AddFeature_Click;

            var featureSection = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(10),
                Padding = new Thickness(10),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = "Add New Feature", FontWeight = FontWeight.Bold },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Margin = new Thickness(0, 10, 0, 0),
                            Children =
                            {
                                new TextBlock { Text = "Feature Name:", Width = 120 },
                                _newFeatureNameBox,
                                _addFeatureButton
                            }
                        }
                    }
                }
            };

            _seedFeatureComboBox = new ComboBox { Width = 200 };
            _newSeedPhraseBox = new TextBox { Width = 200, Watermark = "e.g., excellent wifi" };
            _seedWeightComboBox = new ComboBox { Width = 200, SelectedIndex = 0 };
            _seedWeightComboBox.Items.Add("+1 (Positive)");
            _seedWeightComboBox.Items.Add("-1 (Negative)");

            _addSeedButton = new Button { Content = "Add Seed Word", Margin = new Thickness(0, 10, 0, 0) };
            _addSeedButton.Click += AddSeed_Click;

            var seedSection = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(10),
                Padding = new Thickness(10),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = "Add New Seed Word", FontWeight = FontWeight.Bold },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Margin = new Thickness(0, 10, 0, 5),
                            Children =
                            {
                                new TextBlock { Text = "Feature:", Width = 120 },
                                _seedFeatureComboBox
                            }
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Margin = new Thickness(0, 5, 0, 5),
                            Children =
                            {
                                new TextBlock { Text = "Seed Phrase:", Width = 120 },
                                _newSeedPhraseBox
                            }
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Margin = new Thickness(0, 5, 0, 5),
                            Children =
                            {
                                new TextBlock { Text = "Weight:", Width = 120 },
                                _seedWeightComboBox
                            }
                        },
                        _addSeedButton
                    }
                }
            };

            var scrollViewer = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Children = { adminLabel, warningText, featureSection, seedSection }
                }
            };

            Content = scrollViewer;
        }

        private async void LoadFeatures()
        {
            await Task.Run(() =>
            {
                var features = _dbService.GetActiveFeatures();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _seedFeatureComboBox.Items.Clear();
                    foreach (var feature in features)
                    {
                        _seedFeatureComboBox.Items.Add(feature);
                    }
                });
            });
        }

        private async void AddFeature_Click(object? sender, RoutedEventArgs e)
        {
            var featureName = _newFeatureNameBox.Text?.Trim();

            if (string.IsNullOrEmpty(featureName))
            {
                _updateStatus("Please enter a feature name", "Orange");
                return;
            }

            await Task.Run(() =>
            {
                var success = _dbService.AddFeature(featureName);

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (success)
                    {
                        _updateStatus($"✓ Feature '{featureName}' added successfully", "Green");
                        _newFeatureNameBox.Text = "";
                        LoadFeatures();
                    }
                    else
                    {
                        _updateStatus("Error adding feature", "Red");
                    }
                });
            });
        }

        private async void AddSeed_Click(object? sender, RoutedEventArgs e)
        {
            if (_seedFeatureComboBox.SelectedItem == null)
            {
                _updateStatus("Please select a feature", "Orange");
                return;
            }

            var feature = _seedFeatureComboBox.SelectedItem.ToString();
            var phrase = _newSeedPhraseBox.Text?.Trim();
            var weight = _seedWeightComboBox.SelectedIndex == 0 ? 1 : -1;

            if (string.IsNullOrEmpty(phrase) || string.IsNullOrEmpty(feature))
            {
                _updateStatus("Please enter a seed phrase", "Orange");
                return;
            }

            await Task.Run(() =>
            {
                var success = _dbService.AddSeedWord(feature, phrase, weight);

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (success)
                    {
                        _updateStatus($"✓ Seed word '{phrase}' added to {feature}", "Green");
                        _newSeedPhraseBox.Text = "";
                    }
                    else
                    {
                        _updateStatus("Error adding seed word", "Red");
                    }
                });
            });
        }
    }
}
