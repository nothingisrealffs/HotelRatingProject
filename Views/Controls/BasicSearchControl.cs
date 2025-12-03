using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
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

        // Constants used for styling to match the "working" look
        private readonly SolidColorBrush _textColor = new SolidColorBrush(Color.Parse("#E0E0E0"));
        private readonly SolidColorBrush _subtleTextColor = new SolidColorBrush(Color.Parse("#A0A0A0"));
        private readonly SolidColorBrush _cardBackground = new SolidColorBrush(Color.Parse("#2D2D30"));

        public BasicSearchControl(BasicSearchViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = _viewModel;
            BuildUI();
        }

        private void BuildUI()
        {
            var darkBackground = new SolidColorBrush(Color.Parse("#1E1E1E"));
            var cardBorder = new SolidColorBrush(Color.Parse("#3F3F46"));

            // --- 1. SEARCH BAR ---
            var searchLabel = new TextBlock { Text = "Enter Hotel Name:", FontWeight = FontWeight.Bold, Margin = new Thickness(5), VerticalAlignment = VerticalAlignment.Center, Foreground = _textColor };
            var hotelSearchBox = new TextBox { Watermark = "e.g., hampton inn majestic chicago", Width = 400, Margin = new Thickness(5) };
            hotelSearchBox.Bind(TextBox.TextProperty, new Binding("HotelSearchText") { Mode = BindingMode.TwoWay });
            var searchButton = new Button { Content = "🔍 Search", Width = 100, Margin = new Thickness(5) };
            searchButton.Bind(Button.CommandProperty, new Binding("SearchByNameCommand"));
            searchButton.Bind(Button.IsEnabledProperty, new Binding("!IsSearching"));

            var searchByNamePanel = new Border { Background = _cardBackground, BorderBrush = cardBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(15), Margin = new Thickness(10), Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { searchLabel, hotelSearchBox, searchButton } } };
            
            // --- 2. OR DIVIDER ---
            var orText = new TextBlock { Text = "OR", FontSize = 14, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10), Foreground = _subtleTextColor };
            
            // --- 3. CITY SELECTION ---
            var cityLabel = new TextBlock { Text = "Select City:", FontWeight = FontWeight.Bold, Margin = new Thickness(5), VerticalAlignment = VerticalAlignment.Center, Foreground = _textColor };
            var cityComboBox = new ComboBox { Width = 250, Margin = new Thickness(5), PlaceholderText = "Choose a city..." };
            cityComboBox.Bind(ComboBox.ItemsSourceProperty, new Binding("Cities"));
            cityComboBox.Bind(ComboBox.SelectedItemProperty, new Binding("SelectedCity") { Mode = BindingMode.TwoWay });
            var selectCityPanel = new Border { Background = _cardBackground, BorderBrush = cardBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(15), Margin = new Thickness(10), Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { cityLabel, cityComboBox } } };

            // --- 4. HOTEL LIST ---
            var hotelListBox = new ListBox
            {
                Height = 250,
                Margin = new Thickness(10, 5, 10, 10),
                Background = darkBackground,
                ItemTemplate = new FuncDataTemplate<HotelListItem>((item, _) =>
                    item is null ? new Panel() : new Border 
                    {
                        Padding = new Thickness(15), Margin = new Thickness(0, 4), Background = _cardBackground, BorderBrush = cardBorder,
                        BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                        Child = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                            Children =
                            {
                                new StackPanel { Children = { new TextBlock { Text = item.Name, FontWeight = FontWeight.Bold, FontSize = 14, Foreground = _textColor }, new TextBlock { Text = $"⭐ {item.Rating:F2} • {item.ReviewCount} reviews", FontSize = 11, Foreground = _subtleTextColor, Margin = new Thickness(0, 4, 0, 0) } } },
                                new Border { Background = (IBrush)RatingColorHelper.GetRatingColor(item.Rating), CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 6), [Grid.ColumnProperty] = 1, VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = item.Rating.ToString("F1"), Foreground = Brushes.White, FontWeight = FontWeight.Bold, FontSize = 13 } }
                            }
                        }
                    })
            };
            hotelListBox.Bind(ListBox.ItemsSourceProperty, new Binding("Hotels"));
            hotelListBox.SelectionChanged += (s, e) => { if (hotelListBox.SelectedItem is HotelListItem selectedItem) { _viewModel.SelectedHotel = selectedItem.Name; } };

            // --- 5. RATING CARD (DYNAMIC) ---
            var ratingDisplayPanel = new StackPanel { Margin = new Thickness(20), Spacing = 15 };
            // Ensure we handle clean up and re-drawing
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.CurrentRating))
                {
                    ratingDisplayPanel.Children.Clear();
                    if (_viewModel.CurrentRating != null) 
                    { 
                        ratingDisplayPanel.IsVisible = true;
                        DisplayHotelRating(ratingDisplayPanel, _viewModel.CurrentRating); 
                    }
                    else
                    {
                        ratingDisplayPanel.IsVisible = false;
                    }
                }
            };
            // Initial check
            ratingDisplayPanel.IsVisible = false;

            var ratingCard = new Border { BorderBrush = cardBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Margin = new Thickness(10), Padding = new Thickness(15), Background = _cardBackground, BoxShadow = new BoxShadows(new BoxShadow { Blur = 15, Color = Color.FromArgb(40, 0, 0, 0) }), Child = ratingDisplayPanel };
            
            // --- MAIN LAYOUT ---
            var contentGrid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"), Margin = new Thickness(10), Background = darkBackground, Children = { searchByNamePanel, orText, selectCityPanel, hotelListBox, ratingCard } };
            Grid.SetRow(searchByNamePanel, 0); Grid.SetRow(orText, 1); Grid.SetRow(selectCityPanel, 2); Grid.SetRow(hotelListBox, 3); Grid.SetRow(ratingCard, 4);

            var mainScrollViewer = new ScrollViewer { Content = contentGrid, Background = darkBackground };

            // --- REVIEWS OVERLAY (POPUP) ---
            var overlayGrid = BuildReviewsOverlay();
            
            // Use a Grid to stack the Overlay ON TOP of the Main UI
            var rootGrid = new Grid();
            rootGrid.Children.Add(mainScrollViewer);
            rootGrid.Children.Add(overlayGrid);

            Content = rootGrid;
        }

        private void DisplayHotelRating(StackPanel panel, HotelRating rating)
        {
            panel.Children.Add(new TextBlock { Text = rating.HotelName, FontSize = 24, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 0, 0, 5), Foreground = _textColor });
            panel.Children.Add(new TextBlock { Text = $"📍 {rating.City}, {rating.Country}", FontSize = 14, Foreground = _subtleTextColor, Margin = new Thickness(0, 0, 0, 20) });
            
            var overallCard = new Border { Background = (IBrush)RatingColorHelper.GetRatingColor(rating.OverallRating), CornerRadius = new CornerRadius(10), Padding = new Thickness(25), Margin = new Thickness(0, 0, 0, 25), Child = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Children = { new TextBlock { Text = "Overall Rating", Foreground = Brushes.White, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, Opacity = 0.95 }, new TextBlock { Text = rating.OverallRating.ToString("F2"), Foreground = Brushes.White, FontSize = 42, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 5) }, new TextBlock { Text = new string('⭐', Math.Min(Math.Max((int)Math.Round(rating.OverallRating), 0), 5)), FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center } } } };
            panel.Children.Add(overallCard);
            
            panel.Children.Add(new TextBlock { Text = "Detailed Ratings", FontSize = 17, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 10, 0, 15), Foreground = _textColor });
            AddRatingBar(panel, "Cleanliness", rating.CleanlinessScore);
            AddRatingBar(panel, "Service", rating.ServiceScore);
            AddRatingBar(panel, "Location", rating.LocationScore);
            AddRatingBar(panel, "Comfort", rating.ComfortScore);
            AddRatingBar(panel, "Price", rating.PriceScore);

            // *** MODIFIED HYPERLINK BUTTON ***
            var linkButton = new Button
            {
                // Styling to make it look like a text link
                Content = new TextBlock 
                { 
                    Text = $"📊 Based on {rating.TotalReviews:N0} reviews (Click to View)", 
                    TextDecorations = TextDecorations.Underline 
                },
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.SkyBlue,
                Cursor = new Cursor(StandardCursorType.Hand),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 25, 0, 0)
            };
            linkButton.Bind(Button.CommandProperty, new Binding("ShowReviewsCommand"));
            panel.Children.Add(linkButton);
        }
        
        private void AddRatingBar(StackPanel parent, string label, double score)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*,70"), Margin = new Thickness(0, 10, 0, 10) };
            grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.Medium, FontSize = 14, Foreground = _textColor, [Grid.ColumnProperty] = 0 });
            var progressBar = new ProgressBar { Minimum = 1, Maximum = 5, Value = score, Height = 24, Foreground = (IBrush)RatingColorHelper.GetRatingColor(score), [Grid.ColumnProperty] = 1, Margin = new Thickness(15, 0) };
            grid.Children.Add(progressBar);
            grid.Children.Add(new TextBlock { Text = score.ToString("F2"), FontWeight = FontWeight.Bold, FontSize = 15, Foreground = (IBrush)RatingColorHelper.GetRatingColor(score), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, [Grid.ColumnProperty] = 2 });
            parent.Children.Add(grid);
        }

 // File: Views/Controls/BasicSearchControl.cs

        private Grid BuildReviewsOverlay()
        {
            var overlay = new Grid 
            { 
                Background = new SolidColorBrush(Color.Parse("#CC000000")), 
                IsVisible = false 
            };
            overlay.Bind(Grid.IsVisibleProperty, new Binding("IsShowingReviews"));

            var closeButton = new Button 
            { 
                Content = "X", 
                Foreground = Brushes.White,
                Background = Brushes.Red,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0,0,0,10)
            };
            closeButton.Bind(Button.CommandProperty, new Binding("CloseReviewsCommand"));

            var titleBlock = new TextBlock
            {
                Text = "Recent Reviews",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = _textColor,
                VerticalAlignment = VerticalAlignment.Center
            };

            var header = new DockPanel { LastChildFill = false };
            DockPanel.SetDock(titleBlock, Dock.Left);
            DockPanel.SetDock(closeButton, Dock.Right);
            header.Children.Add(titleBlock);
            header.Children.Add(closeButton);

            var reviewsList = new ListBox
            {
                Background = Brushes.Transparent,
                Height = 400,
                // FIXED: Added null check (r is null ? ...) to prevent crash during scrolling
                ItemTemplate = new FuncDataTemplate<HotelReview>((r, _) => 
                    r is null ? new Panel() : new Border
                    {
                        BorderBrush = _subtleTextColor,
                        BorderThickness = new Thickness(0,0,0,1),
                        Padding = new Thickness(5, 10),
                        Child = new StackPanel
                        {
                            Children = {
                                new Grid {
                                    ColumnDefinitions = new ColumnDefinitions("*, Auto"),
                                    Children = {
                                        new TextBlock { Text = r.ReviewerName, FontWeight = FontWeight.Bold, Foreground = _textColor, [Grid.ColumnProperty]=0 },
                                        new TextBlock { Text = $"{r.Rating:F1}/5.0", Foreground = Brushes.Orange, FontWeight=FontWeight.Bold, [Grid.ColumnProperty]=1 }
                                    }
                                },
                                new TextBlock { Text = r.ReviewDate.ToString("yyyy-MM-dd"), FontSize=12, Foreground=_subtleTextColor, Margin=new Thickness(0,2) },
                                new TextBlock { Text = r.ReviewText, TextWrapping = TextWrapping.Wrap, Margin=new Thickness(0,5,0,0), Foreground=_textColor }
                            }
                        }
                    })
            };
            reviewsList.Bind(ListBox.ItemsSourceProperty, new Binding("HotelReviews"));

            var popupCard = new Border
            {
                Width = 600,
                Height = 500,
                Background = _cardBackground,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new StackPanel
                {
                    Children = { header, reviewsList }
                }
            };

            overlay.Children.Add(popupCard);
            return overlay;
                }
            }
        }