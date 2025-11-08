using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using HotelRatingViewer.Models;
using HotelRatingViewer.Services;
using HotelRatingViewer.Views.Controls;

namespace HotelRatingViewer.Views
{
    public class MainWindow : Window
    {
        private readonly string _connectionString;
        private readonly DatabaseService _dbService;
        private readonly DatabaseMode _dbMode;
        private TabControl _mainTabs = null!;
        private TextBlock _statusText = null!;

        public MainWindow(string connectionString, DatabaseService dbService, DatabaseMode dbMode)
        {
            _connectionString = connectionString;
            _dbService = dbService;
            _dbMode = dbMode;

            Title = "Hotel Rating Analysis System - CIT 44400";
            Width = 1200;
            Height = 700;

            BuildUI();
            InitializeApplication();
        }

        private void BuildUI()
        {
            _statusText = new TextBlock
            {
                Text = "Connected",
                Margin = new Thickness(10),
                FontSize = 12,
                Foreground = Brushes.Green
            };

            var statusPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = Brushes.LightGray,
                Children = { _statusText }
            };

            _mainTabs = new TabControl
            {
                Margin = new Thickness(10)
            };

            var rootPanel = new DockPanel
            {
                Children = { statusPanel, _mainTabs }
            };

            DockPanel.SetDock(statusPanel, Dock.Top);

            Content = rootPanel;
        }

        private void InitializeApplication()
        {
            if (_dbMode.HasHotelRatingSchema)
            {
                _statusText.Text = $"✓ Connected to Hotel Rating System (Schema: {_dbService.CurrentSchema})";
                _statusText.Foreground = Brushes.Green;

                _mainTabs.Items.Add(new TabItem
                {
                    Header = "Basic Search",
                    Content = new BasicSearchControl(_dbService, UpdateStatus)
                });

                _mainTabs.Items.Add(new TabItem
                {
                    Header = "Advanced Search",
                    Content = new AdvancedSearchControl(_dbService, UpdateStatus)
                });

                _mainTabs.Items.Add(new TabItem
                {
                    Header = "Admin (Bonus)",
                    Content = new AdminControl(_dbService, UpdateStatus)
                });

                _mainTabs.Items.Add(new TabItem
                {
                    Header = "Database Explorer",
                    Content = new DatabaseExplorerControl(_dbService, UpdateStatus)
                });
            }
            else
            {
                _statusText.Text = "⚠ Hotel schema not found. Fallback mode active.";
                _statusText.Foreground = Brushes.Orange;

                _mainTabs.Items.Add(new TabItem
                {
                    Header = "Database Explorer",
                    Content = new DatabaseExplorerControl(_dbService, UpdateStatus)
                });
            }
        }

        public void UpdateStatus(string message, string colorName = "Gray")
        {
            _statusText.Text = message;

            _statusText.Foreground = colorName switch
            {
                "Green" => Brushes.Green,
                "Red" => Brushes.Red,
                "Orange" => Brushes.Orange,
                "Blue" => Brushes.Blue,
                _ => Brushes.Gray
            };
        }
    }
}
