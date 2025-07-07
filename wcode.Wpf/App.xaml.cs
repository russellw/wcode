using System;
using System.IO;
using System.Windows;
using wcode.Lib;

namespace wcode.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Parse command line arguments for project directory only
        var projectPath = ParseProjectPathArgument(e.Args);
        
        // If a project path was provided via command line, update the configuration
        if (!string.IsNullOrEmpty(projectPath))
        {
            UpdateProjectConfigFromCommandLine(projectPath);
        }
        
        base.OnStartup(e);
    }
    
    private string? ParseProjectPathArgument(string[] args)
    {
        if (args == null || args.Length == 0)
            return null;
        
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--project-dir" && i + 1 < args.Length)
            {
                return args[i + 1];
            }
            else if (args[i].StartsWith("--project-dir="))
            {
                return args[i].Substring("--project-dir=".Length);
            }
            else if (!args[i].StartsWith("--"))
            {
                // First non-option argument is project path
                return args[i];
            }
        }
        
        return null;
    }
    
    private void UpdateProjectConfigFromCommandLine(string projectPath)
    {
        try
        {
            // Resolve to absolute path
            var absolutePath = Path.GetFullPath(projectPath);
            
            // Validate directory exists
            if (!Directory.Exists(absolutePath))
            {
                MessageBox.Show($"Project directory does not exist: {absolutePath}", 
                               "Invalid Project Directory", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Warning);
                return;
            }
            
            // Load existing config and update project path
            var config = ProjectConfig.Load();
            config.SetCurrentProject(absolutePath);
            
            System.Diagnostics.Debug.WriteLine($"Project directory set from command line: {absolutePath}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error setting project directory: {ex.Message}", 
                           "Error", 
                           MessageBoxButton.OK, 
                           MessageBoxImage.Error);
        }
    }
}