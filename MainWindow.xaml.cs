using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using System.Collections.Generic;
using System.Linq;

namespace SourceCodeViewer;

public partial class MainWindow : Window
{
    private string? _currentFolderPath;
    private Dictionary<string, string> _openFiles = new Dictionary<string, string>();
    
    public MainWindow()
    {
        InitializeComponent();
        SetupSyntaxHighlighting();
    }

    private void SetupSyntaxHighlighting()
    {
        
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
                OpenFileInTab(filePath);
            }
        }
    }

    private void OpenFileInTab(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        
        var existingTab = FileTabControl.Items.Cast<TabItem>()
            .FirstOrDefault(tab => tab.Tag?.ToString() == filePath);
        
        if (existingTab != null)
        {
            FileTabControl.SelectedItem = existingTab;
            return;
        }
        
        try
        {
            var content = File.ReadAllText(filePath);
            
            var textEditor = new TextEditor
            {
                Text = content,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30)),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                IsReadOnly = true,
                ShowLineNumbers = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            
            SetSyntaxHighlightingForEditor(textEditor, filePath);
            
            var tabItem = new TabItem
            {
                Header = fileName,
                Content = textEditor,
                Tag = filePath
            };
            
            FileTabControl.Items.Add(tabItem);
            FileTabControl.SelectedItem = tabItem;
            
            _openFiles[filePath] = content;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading file: {ex.Message}", "Error", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetSyntaxHighlightingForEditor(TextEditor editor, string filePath)
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

        editor.SyntaxHighlighting = highlighting;
    }
    
    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is TabItem tabItem)
        {
            if (tabItem.Tag is string filePath)
            {
                _openFiles.Remove(filePath);
            }
            
            FileTabControl.Items.Remove(tabItem);
            
            if (FileTabControl.Items.Count == 1)
            {
                FileTabControl.SelectedItem = WelcomeTab;
            }
        }
    }
}