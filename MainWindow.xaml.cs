using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ICSharpCode.AvalonEdit.Highlighting;

namespace SourceCodeViewer;

public partial class MainWindow : Window
{
    private string? _currentFolderPath;
    
    public MainWindow()
    {
        InitializeComponent();
        SetupSyntaxHighlighting();
    }

    private void SetupSyntaxHighlighting()
    {
        CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Folder to Browse"
        };

        if (dialog.ShowDialog() == true)
        {
            _currentFolderPath = dialog.FolderName;
            LoadFolderStructure(_currentFolderPath);
        }
    }

    private void LoadFolderStructure(string folderPath)
    {
        FileTreeView.Items.Clear();
        
        try
        {
            var rootItem = CreateTreeViewItem(new DirectoryInfo(folderPath));
            FileTreeView.Items.Add(rootItem);
            rootItem.IsExpanded = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading folder: {ex.Message}", "Error", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private TreeViewItem CreateTreeViewItem(DirectoryInfo directoryInfo)
    {
        var item = new TreeViewItem
        {
            Header = directoryInfo.Name,
            Tag = directoryInfo.FullName
        };

        try
        {
            foreach (var directory in directoryInfo.GetDirectories())
            {
                if (!directory.Name.StartsWith("."))
                {
                    item.Items.Add(CreateTreeViewItem(directory));
                }
            }

            foreach (var file in directoryInfo.GetFiles())
            {
                if (IsTextFile(file.Extension))
                {
                    var fileItem = new TreeViewItem
                    {
                        Header = file.Name,
                        Tag = file.FullName
                    };
                    item.Items.Add(fileItem);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
        }

        return item;
    }

    private bool IsTextFile(string extension)
    {
        var textExtensions = new[]
        {
            ".cs", ".cpp", ".c", ".h", ".hpp", ".java", ".js", ".ts", ".py", ".rb", ".php",
            ".html", ".htm", ".css", ".scss", ".sass", ".xml", ".json", ".yaml", ".yml",
            ".txt", ".md", ".sql", ".sh", ".bat", ".ps1", ".vb", ".fs", ".go", ".rs",
            ".swift", ".kt", ".scala", ".clj", ".pl", ".r", ".m", ".mm", ".xaml"
        };
        
        return textExtensions.Contains(extension.ToLower());
    }

    private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem selectedItem && selectedItem.Tag is string filePath)
        {
            if (File.Exists(filePath))
            {
                LoadFile(filePath);
            }
        }
    }

    private void LoadFile(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            CodeEditor.Text = content;
            FilePathTextBlock.Text = filePath;
            
            SetSyntaxHighlightingForFile(filePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading file: {ex.Message}", "Error", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetSyntaxHighlightingForFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        var highlighting = extension switch
        {
            ".cs" => HighlightingManager.Instance.GetDefinition("C#"),
            ".cpp" or ".c" or ".h" or ".hpp" => HighlightingManager.Instance.GetDefinition("C++"),
            ".java" => HighlightingManager.Instance.GetDefinition("Java"),
            ".js" => HighlightingManager.Instance.GetDefinition("JavaScript"),
            ".ts" => HighlightingManager.Instance.GetDefinition("JavaScript"),
            ".py" => HighlightingManager.Instance.GetDefinition("Python"),
            ".html" or ".htm" => HighlightingManager.Instance.GetDefinition("HTML"),
            ".css" => HighlightingManager.Instance.GetDefinition("CSS"),
            ".xml" or ".xaml" => HighlightingManager.Instance.GetDefinition("XML"),
            ".json" => HighlightingManager.Instance.GetDefinition("JavaScript"),
            ".sql" => HighlightingManager.Instance.GetDefinition("SQL"),
            ".php" => HighlightingManager.Instance.GetDefinition("PHP"),
            ".vb" => HighlightingManager.Instance.GetDefinition("VB"),
            _ => null
        };

        CodeEditor.SyntaxHighlighting = highlighting;
    }
}