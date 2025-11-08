using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using HotelRatingViewer.Models;
using HotelRatingViewer.Services;

namespace HotelRatingViewer.Views.Controls
{
    public class DatabaseExplorerControl : UserControl
    {
        private readonly DatabaseService _dbService;
        private readonly Action<string, string> _updateStatus;

        private TreeView _databaseTree = null!;
        private DataGrid _dataGrid = null!;

        public DatabaseExplorerControl(DatabaseService dbService, Action<string, string> updateStatus)
        {
            _dbService = dbService;
            _updateStatus = updateStatus;
            BuildUI();
            LoadDatabaseStructure();
        }

        private void BuildUI()
        {
            var infoText = new TextBlock
            {
                Text = "Browse database schemas and tables",
                Margin = new Thickness(10),
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Gray
            };

            _databaseTree = new TreeView
            {
                Width = 250,
                Margin = new Thickness(10)
            };
            _databaseTree.SelectionChanged += DatabaseTree_SelectionChanged;

            _dataGrid = new DataGrid
            {
                IsReadOnly = true,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                Margin = new Thickness(10)
            };

            var mainContent = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("250,*"),
                Children = { _databaseTree, _dataGrid }
            };

            Grid.SetColumn(_databaseTree, 0);
            Grid.SetColumn(_dataGrid, 1);

            var stackPanel = new StackPanel
            {
                Children = { infoText, mainContent }
            };

            Content = stackPanel;
        }

        private async void LoadDatabaseStructure()
        {
            await Task.Run(() =>
            {
                var schemaData = _dbService.GetAllSchemasAndTables();

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _databaseTree.Items.Clear();

                    foreach (var (schemaName, tables) in schemaData)
                    {
                        var schemaNode = new TreeViewItem
                        {
                            Header = schemaName,
                            FontWeight = FontWeight.Bold
                        };

                        foreach (var tableName in tables)
                        {
                            var tableNode = new TreeViewItem
                            {
                                Header = tableName,
                                Tag = new TableInfo { Database = schemaName, Table = tableName }
                            };
                            schemaNode.Items.Add(tableNode);
                        }

                        _databaseTree.Items.Add(schemaNode);
                    }
                });
            });
        }

        private void DatabaseTree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_databaseTree.SelectedItem is TreeViewItem item && item.Tag is TableInfo tableInfo)
            {
                LoadTableData(tableInfo);
            }
        }

        private void LoadTableData(TableInfo tableInfo)
        {
            try
            {
                _updateStatus($"Loading {tableInfo.Database}.{tableInfo.Table}...", "Blue");

                var rows = _dbService.GetTableData(tableInfo.Database, tableInfo.Table, out var columnNames);

                var columns = new List<DataGridTextColumn>();
                for (int i = 0; i < columnNames.Count; i++)
                {
                    columns.Add(new DataGridTextColumn
                    {
                        Header = columnNames[i],
                        Binding = new Avalonia.Data.Binding($"[{i}]"),
                        Width = new DataGridLength(150)
                    });
                }

                _dataGrid.Columns.Clear();
                foreach (var col in columns)
                {
                    _dataGrid.Columns.Add(col);
                }
                _dataGrid.ItemsSource = rows;

                _updateStatus($"Loaded {rows.Count} rows from {tableInfo.Database}.{tableInfo.Table}", "Green");
            }
            catch (Exception ex)
            {
                _updateStatus($"Error loading table: {ex.Message}", "Red");
            }
        }
    }
}
