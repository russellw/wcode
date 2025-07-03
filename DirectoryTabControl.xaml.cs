using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace wcode;

public partial class DirectoryTabControl : UserControl
{
    public class DirectoryItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
    }

    private ObservableCollection<DirectoryItem> _items = new();
    private string? _currentPath;

    public DirectoryTabControl()
    {
        InitializeComponent();
        DirectoryItemsControl.ItemsSource = _items;
    }

    public void LoadDirectory(string path)
    {
        _currentPath = path;
        DirectoryPathText.Text = path;
        RefreshDirectory();
    }

    private void RefreshDirectory()
    {
        if (_currentPath == null) return;

        _items.Clear();

        try
        {
            var dirInfo = new DirectoryInfo(_currentPath);

            foreach (var dir in dirInfo.GetDirectories().OrderBy(d => d.Name))
            {
                if (!dir.Name.StartsWith("."))
                {
                    _items.Add(new DirectoryItem
                    {
                        Name = dir.Name,
                        Path = dir.FullName,
                        Icon = "📁",
                        Size = "Folder",
                        IsDirectory = true
                    });
                }
            }

            foreach (var file in dirInfo.GetFiles().OrderBy(f => f.Name))
            {
                _items.Add(new DirectoryItem
                {
                    Name = file.Name,
                    Path = file.FullName,
                    Icon = GetFileIcon(file.Extension),
                    Size = FormatFileSize(file.Length),
                    IsDirectory = false
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading directory: {ex.Message}", "Error", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string GetFileIcon(string extension)
    {
        return extension.ToLower() switch
        {
            ".cs" => "📄",
            ".cpp" or ".c" or ".h" or ".hpp" => "⚙️",
            ".js" or ".ts" => "📜",
            ".py" => "🐍",
            ".html" or ".htm" => "🌐",
            ".css" => "🎨",
            ".json" => "📋",
            ".xml" or ".xaml" => "📰",
            ".txt" or ".md" => "📝",
            ".exe" => "⚡",
            ".dll" => "🔧",
            _ => "📄"
        };
    }

    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024)} MB";
        return $"{bytes / (1024 * 1024 * 1024)} GB";
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshDirectory();
    }
}