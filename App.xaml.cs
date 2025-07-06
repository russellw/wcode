using System.IO;
using System.Linq;
using System.Windows;

namespace wcode;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Parse command line arguments
        var projectPath = ParseCommandLineArguments(e.Args);
        
        // If a project path was provided via command line, update the configuration
        if (!string.IsNullOrEmpty(projectPath))
        {
            UpdateProjectConfigFromCommandLine(projectPath);
        }
        
        base.OnStartup(e);
    }
    
    private string? ParseCommandLineArguments(string[] args)
    {
        if (args == null || args.Length == 0)
            return null;
            
        // First argument is treated as project directory path
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            return args[0];
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