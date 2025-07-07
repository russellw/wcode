using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using System.Collections.Generic;
using System.Linq;
using wcode.Lib;

namespace wcode.Wpf;

public partial class MainWindow : Window
{
    private string? _currentFolderPath;
    private Dictionary<string, string> _openFiles = new Dictionary<string, string>();
    private Dictionary<TabItem, TabInfo> _tabInfos = new Dictionary<TabItem, TabInfo>();
    private ProjectConfig _projectConfig = null!;
    
    public MainWindow()
    {
        InitializeComponent();
        SetupSyntaxHighlighting();
        InitializeProject();
        OpenDefaultTabs();
    }

    private void SetupSyntaxHighlighting()
    {
        
    }

    private void InitializeProject()
    {
        // Load project configuration
        _projectConfig = ProjectConfig.Load();
        
        // Set current folder path from config
        if (!string.IsNullOrEmpty(_projectConfig.CurrentProjectPath) && 
            Directory.Exists(_projectConfig.CurrentProjectPath))
        {
            _currentFolderPath = _projectConfig.CurrentProjectPath;
        }
        
        // Update title
        UpdateTitle();
    }

    private void OpenDefaultTabs()
    {
        // Open terminal tab first
        OpenTerminalTab();
        var terminalTab = FileTabControl.SelectedItem; // Remember the terminal tab
        
        // Open directory tab for current project if available
        if (!string.IsNullOrEmpty(_currentFolderPath) && Directory.Exists(_currentFolderPath))
        {
            OpenDirectoryTab(_currentFolderPath);
            // Restore focus to terminal tab
            FileTabControl.SelectedItem = terminalTab;
        }
    }

    private void UpdateTitle()
    {
        if (_projectConfig != null)
        {
            this.Title = _projectConfig.GetProjectDisplayPath();
        }
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

    private void OpenFileInTab(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        
        var existingTab = _tabInfos.FirstOrDefault(kvp => 
            kvp.Value.Type == TabType.File && kvp.Value.Path == filePath).Key;
        
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
                Header = $"📄 {fileName}",
                Content = textEditor,
                Tag = filePath
            };
            
            var tabInfo = new TabInfo
            {
                Type = TabType.File,
                Title = fileName,
                Path = filePath,
                Content = textEditor,
                Icon = "📄"
            };
            
            _tabInfos[tabItem] = tabInfo;
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
            if (_tabInfos.TryGetValue(tabItem, out var tabInfo))
            {
                if (tabInfo.Type == TabType.File && tabInfo.Path != null)
                {
                    _openFiles.Remove(tabInfo.Path);
                }
                _tabInfos.Remove(tabItem);
            }
            
            FileTabControl.Items.Remove(tabItem);
        }
    }

    private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open File",
            Filter = "All Supported Files|*.cs;*.cpp;*.c;*.h;*.hpp;*.java;*.js;*.ts;*.py;*.rb;*.php;*.html;*.htm;*.css;*.scss;*.sass;*.xml;*.json;*.yaml;*.yml;*.txt;*.md;*.sql;*.sh;*.bat;*.ps1;*.vb;*.fs;*.go;*.rs;*.swift;*.kt;*.scala;*.clj;*.pl;*.r;*.m;*.mm;*.xaml|" +
                   "C# Files (*.cs)|*.cs|" +
                   "C++ Files (*.cpp;*.c;*.h;*.hpp)|*.cpp;*.c;*.h;*.hpp|" +
                   "Java Files (*.java)|*.java|" +
                   "JavaScript Files (*.js)|*.js|" +
                   "TypeScript Files (*.ts)|*.ts|" +
                   "Python Files (*.py)|*.py|" +
                   "HTML Files (*.html;*.htm)|*.html;*.htm|" +
                   "CSS Files (*.css)|*.css|" +
                   "XML Files (*.xml;*.xaml)|*.xml;*.xaml|" +
                   "JSON Files (*.json)|*.json|" +
                   "Text Files (*.txt)|*.txt|" +
                   "Markdown Files (*.md)|*.md|" +
                   "All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            OpenFileInTab(dialog.FileName);
        }
    }

    private void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Folder to Browse"
        };

        if (dialog.ShowDialog() == true)
        {
            _currentFolderPath = dialog.FolderName;
            _projectConfig?.SetCurrentProject(_currentFolderPath);
            OpenDirectoryTab(_currentFolderPath);
            UpdateTitle();
        }
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
    
    private void OpenDirectoryTab(string directoryPath)
    {
        var directoryName = Path.GetFileName(directoryPath) ?? "Directory";
        
        var existingTab = _tabInfos.FirstOrDefault(kvp => 
            kvp.Value.Type == TabType.Directory && kvp.Value.Path == directoryPath).Key;
        
        if (existingTab != null)
        {
            FileTabControl.SelectedItem = existingTab;
            return;
        }
        
        var directoryControl = new DirectoryTabControl();
        directoryControl.LoadDirectory(directoryPath);
        
        // Subscribe to file open requests
        directoryControl.FileOpenRequested += OpenFileInTab;
        
        // Subscribe to directory open requests
        directoryControl.DirectoryOpenRequested += OpenDirectoryTab;
        
        var tabItem = new TabItem
        {
            Header = $"📁 {directoryName}",
            Content = directoryControl
        };
        
        var tabInfo = new TabInfo
        {
            Type = TabType.Directory,
            Title = directoryName,
            Path = directoryPath,
            Content = directoryControl,
            Icon = "📁"
        };
        
        _tabInfos[tabItem] = tabInfo;
        FileTabControl.Items.Add(tabItem);
        FileTabControl.SelectedItem = tabItem;
    }
    
    private void OpenTerminalTab()
    {
        var terminalControl = new TerminalTabControl();
        
        var tabItem = new TabItem
        {
            Header = "🤖 LLM Chat",
            Content = terminalControl
        };
        
        var tabInfo = new TabInfo
        {
            Type = TabType.Terminal,
            Title = "LLM Chat",
            Content = terminalControl,
            Icon = "🤖"
        };
        
        _tabInfos[tabItem] = tabInfo;
        FileTabControl.Items.Add(tabItem);
        FileTabControl.SelectedItem = tabItem;
    }
    
    private void NewTerminalMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenTerminalTab();
    }
    
    private void NewDirectoryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentFolderPath))
        {
            OpenDirectoryTab(_currentFolderPath);
        }
        else
        {
            MessageBox.Show("Please open a folder first using File > Open Folder", "No Folder Selected", 
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    
    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        // Handle Alt+F4 to close the window
        if (e.Key == Key.F4 && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            this.Close();
            e.Handled = true;
        }
    }
    
    private void ChangeProjectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Project Directory",
            InitialDirectory = _projectConfig?.CurrentProjectPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog() == true)
        {
            _currentFolderPath = dialog.FolderName;
            _projectConfig?.SetCurrentProject(_currentFolderPath);
            UpdateTitle();
            
            // Optionally refresh any open directory tabs
            RefreshDirectoryTabs();
        }
    }
    
    private void RefreshDirectoryTabs()
    {
        // Find and refresh any existing directory tabs
        foreach (var kvp in _tabInfos.ToList())
        {
            if (kvp.Value.Type == TabType.Directory && kvp.Value.Content is DirectoryTabControl directoryControl)
            {
                if (!string.IsNullOrEmpty(_currentFolderPath))
                {
                    directoryControl.LoadDirectory(_currentFolderPath);
                }
            }
        }
    }
}