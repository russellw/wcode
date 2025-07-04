using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
    
    // Event to notify parent window to open files
    public event Action<string>? FileOpenRequested;

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
    
    private void DirectoryItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is DirectoryItem item)
        {
            if (item.IsDirectory)
            {
                // Navigate to subdirectory
                LoadDirectory(item.Path);
            }
            else
            {
                // Check if it's a known binary type that shouldn't be opened
                if (IsBinaryFile(item.Path))
                {
                    MessageBox.Show($"File type not supported for viewing: {Path.GetExtension(item.Path)}", 
                                  "Unsupported File Type", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Open as text file (either known text type or unknown extension)
                    FileOpenRequested?.Invoke(item.Path);
                }
            }
        }
    }
    
    private bool IsBinaryFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        var binaryExtensions = new[]
        {
            // Executables and libraries
            ".exe", ".dll", ".so", ".dylib", ".bin", ".app",
            
            // Images
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".ico", ".svg", ".webp",
            
            // Audio/Video
            ".mp3", ".wav", ".flac", ".m4a", ".mp4", ".avi", ".mkv", ".mov", ".wmv",
            
            // Archives
            ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2",
            
            // Documents
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            
            // Databases
            ".db", ".sqlite", ".mdb",
            
            // Other binary formats
            ".obj", ".o", ".lib", ".a", ".pdb", ".ilk", ".exp"
        };
        
        return binaryExtensions.Contains(extension);
    }
    
    private bool IsTextFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        var textExtensions = new[]
        {
            ".cs", ".cpp", ".c", ".h", ".hpp", ".java", ".js", ".ts", ".py", ".rb", ".php",
            ".html", ".htm", ".css", ".scss", ".sass", ".xml", ".json", ".yaml", ".yml",
            ".txt", ".md", ".sql", ".sh", ".bat", ".ps1", ".vb", ".fs", ".go", ".rs",
            ".swift", ".kt", ".scala", ".clj", ".pl", ".r", ".m", ".mm", ".xaml"
        };
        
        return textExtensions.Contains(extension);
    }
}