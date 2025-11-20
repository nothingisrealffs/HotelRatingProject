using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Data; 
using HotelRatingViewer.ViewModels;
using HotelRatingViewer.Views.Controls;
using HotelRatingViewer.Services;
using HotelRatingViewer.Converters;
using Microsoft.Extensions.DependencyInjection;

namespace HotelRatingViewer.Views
{
    public class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        private TabControl _mainTabs = null!;
        private TextBlock _statusText = null!;
        private Panel _loadingOverlay = null!;
        private TabItem? _adminTab;
        private Button? _adminToggleButton;

        public MainWindow(MainWindowViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = _viewModel;

            Title = "Hotel Rating Analysis System - CIT 44400";
            Width = 1400;
            Height = 800;
            MinWidth = 1000;
            MinHeight = 600;

            BuildUI();
        }

        private void BuildUI()
        {
            _statusText = new TextBlock
            {
                Margin = new Thickness(10),
                FontSize = 13,
                FontWeight = FontWeight.Medium
            };
            
            _statusText.Bind(TextBlock.TextProperty, new Binding("StatusMessage"));
            _statusText.Bind(TextBlock.ForegroundProperty, new Binding("StatusColor")
            {
                Converter = new ColorNameToBrushConverter()
            });

            var statusIcon = new TextBlock
            {
                Text = "●",
                Margin = new Thickness(10, 10, 5, 10),
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            statusIcon.Bind(TextBlock.ForegroundProperty, new Binding("StatusColor")
            {
                Converter = new ColorNameToBrushConverter()
            });

            var statusPanel = new DockPanel
            {
                Background = new SolidColorBrush(Color.Parse("#F5F5F5")),
                Height = 40,
                Children = { statusIcon, _statusText }
            };

            _mainTabs = new TabControl
            {
                Margin = new Thickness(10),
                TabStripPlacement = Dock.Top
            };

            Control mainContent;

            if (_viewModel.HasHotelRatingSchema)
            {
                _mainTabs.Items.Add(new TabItem
                {
                    Header = CreateTabHeader("🔍", "Basic Search"),
                    Content = new BasicSearchControl(_viewModel.BasicSearchViewModel)
                });

                _mainTabs.Items.Add(new TabItem
                {
                    Header = CreateTabHeader("🔬", "Advanced Search"),
                    Content = new AdvancedSearchControl(_viewModel.AdvancedSearchViewModel)
                });

                _adminTab = new TabItem
                {
                    Header = CreateTabHeader("🔐", "Admin (Bonus)"),
                    Content = new AdminControl(_viewModel.AdminViewModel),
                    IsEnabled = false
                };
                _mainTabs.Items.Add(_adminTab);

                // Build admin toggle button
                _adminToggleButton = new Button
                {
                    Content = "🔓 Admin",
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top, 
                    Margin = new Thickness(0, 10, 15, 0),
                    FontSize = 12
                };

                _adminToggleButton.Click += async (s, e) =>
                {
                    var dbService = App.ServiceProvider!.GetRequiredService<IDatabaseService>();
                    var isSystem = string.Equals(dbService.CurrentSchema, "SYSTEM", StringComparison.OrdinalIgnoreCase);

                    if (!isSystem)
                    {
                        await AuthenticateAdmin();
                        UpdateAdminButtonState();
                    }
                    else
                    {
                        _adminToggleButton.IsEnabled = false;
                        _statusText.Text = "Restoring previous user...";
                        var restored = await dbService.RestoreUserSessionAsync();
                        if (restored)
                        {
                            _viewModel.AuthenticateAdmin(false);
                            _adminTab!.IsEnabled = false;
                            _mainTabs.SelectedIndex = 0;
                            _statusText.Text = "Restored previous user session.";
                        }
                        else
                        {
                            _statusText.Text = "Failed to restore user session. Please relogin.";
                        }
                        UpdateAdminButtonState();
                        _adminToggleButton.IsEnabled = true;
                    }
                };

                var contentGrid = new Grid();
                contentGrid.Children.Add(_mainTabs);
                contentGrid.Children.Add(_adminToggleButton);

                mainContent = contentGrid;
            }
            else
            {
                var retryBtn = new Button
                {
                    Content = "Retry Login",
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                retryBtn.Bind(Button.CommandProperty, new Binding("ReturnToLoginCommand"));

                mainContent = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 20,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "⚠️ Database Connection Issue",
                            FontSize = 24,
                            FontWeight = FontWeight.Bold,
                            Foreground = Brushes.Orange
                        },
                        new TextBlock
                        {
                            Text = "The required Hotel Rating schema could not be found.\nPlease check your database configuration and try again.",
                            TextAlignment = TextAlignment.Center
                        },
                        retryBtn
                    }
                };
            }

            _loadingOverlay = new Grid
            {
                Background = new SolidColorBrush(Color.Parse("#80000000")),
                IsVisible = false,
                Children =
                {
                    new Border
                    {
                        Background = Brushes.White,
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(20),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Child = new StackPanel
                        {
                            Spacing = 10,
                            Children =
                            {
                                new ProgressBar { IsIndeterminate = true, Width = 200 },
                                new TextBlock { Text = "Processing...", HorizontalAlignment = HorizontalAlignment.Center }
                            }
                        }
                    }
                }
            };
            
            _loadingOverlay.Bind(Visual.IsVisibleProperty, new Binding("IsLoading"));

            Content = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
                Children = 
                { 
                    mainContent, 
                    statusPanel,
                    _loadingOverlay 
                }
            };
            
            ((Control)mainContent).SetValue(Grid.RowProperty, 0);
            statusPanel.SetValue(Grid.RowProperty, 1);
            _loadingOverlay.SetValue(Grid.RowSpanProperty, 2);

            UpdateAdminButtonState();
        }

        private object CreateTabHeader(string icon, string text)
        {
            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Children =
                {
                    new TextBlock { Text = icon },
                    new TextBlock { Text = text }
                }
            };
        }

        private async Task AuthenticateAdmin()
        {
            // FIXED: Create controls explicitly so we have references to them.
            // We cannot use FindControl<T>() on a window created purely in C# without a NameScope.
            var passBox = new TextBox { PasswordChar = '•' };
            var loginBtn = new Button { Content = "Login", IsDefault = true };
            var cancelBtn = new Button { Content = "Cancel" };

            var passwordDialog = new Window
            {
                Title = "Admin Authentication",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.BorderOnly,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = "Enter SYSTEM Password:", FontWeight = FontWeight.Bold },
                        passBox,
                        new StackPanel 
                        { 
                            Orientation = Orientation.Horizontal, 
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                cancelBtn,
                                loginBtn
                            }
                        }
                    }
                }
            };

            string? password = null;

            loginBtn.Click += (s, e) => { password = passBox.Text; passwordDialog.Close(); };
            cancelBtn.Click += (s, e) => passwordDialog.Close();

            await passwordDialog.ShowDialog(this);

            if (!string.IsNullOrEmpty(password))
            {
                _statusText.Text = "Authenticating as SYSTEM...";
                try
                {
                    var dbService = App.ServiceProvider!.GetRequiredService<IDatabaseService>();
                    var success = await dbService.ElevateToSystemAsync(password);
                    if (success)
                    {
                        _viewModel.AuthenticateAdmin(true);
                        _adminTab!.IsEnabled = true;
                        _mainTabs.SelectedItem = _adminTab;
                        _statusText.Text = "Authenticated as SYSTEM.";
                    }
                    else
                    {
                        _statusText.Text = "Authentication failed.";
                    }
                }
                catch (Exception ex)
                {
                    _statusText.Text = "Error: " + ex.Message;
                }
            }
        }

        private void UpdateAdminButtonState()
        {
            if (_adminToggleButton == null) return;

            if (_viewModel.IsAdminAuthenticated)
            {
                _adminToggleButton.Content = "🔒 Logout Admin";
                _adminToggleButton.Background = Brushes.OrangeRed;
                _adminToggleButton.Foreground = Brushes.White;
            }
            else
            {
                _adminToggleButton.Content = "🔓 Admin";
                _adminToggleButton.Background = Brushes.LightGray;
                _adminToggleButton.Foreground = Brushes.Black;
            }
        }
    }
}
