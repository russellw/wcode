using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace wcode;

public class ProjectConfig
{
    [JsonPropertyName("currentProjectPath")]
    public string CurrentProjectPath { get; set; } = string.Empty;
    
    [JsonPropertyName("recentProjects")]
    public List<string> RecentProjects { get; set; } = new();
    
    [JsonPropertyName("lastOpened")]
    public DateTime LastOpened { get; set; } = DateTime.Now;

    private static string ConfigFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "wcode", 
        "config.json"
    );

    public static ProjectConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<ProjectConfig>(json);
                return config ?? CreateDefault();
            }
        }
        catch (Exception ex)
        {
            // If config is corrupted, create new one
            System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
        }
        
        return CreateDefault();
    }

    public void Save()
    {
        try
        {
            // Ensure directory exists
            var configDir = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            // Update last opened time
            LastOpened = DateTime.Now;

            // Serialize and save
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving config: {ex.Message}");
        }
    }

    private static ProjectConfig CreateDefault()
    {
        var config = new ProjectConfig();
        
        // Try to find a suitable default project directory
        var defaultPath = FindDefaultProjectPath();
        config.CurrentProjectPath = defaultPath;
        
        if (!string.IsNullOrEmpty(defaultPath))
        {
            config.RecentProjects.Add(defaultPath);
        }
        
        return config;
    }

    private static string FindDefaultProjectPath()
    {
        // Priority order for finding a default project directory
        var candidates = new[]
        {
            // Current working directory
            Directory.GetCurrentDirectory(),
            
            // Common development folders
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "source", "repos"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Projects"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Code"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents"),
            
            // Desktop as last resort
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            
            // User profile folder
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        foreach (var candidate in candidates)
        {
            try
            {
                if (!string.IsNullOrEmpty(candidate) && Directory.Exists(candidate))
                {
                    // Check if it looks like a development directory
                    if (IsLikelyProjectDirectory(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
                // Skip invalid paths
                continue;
            }
        }

        // Return first valid directory as fallback
        foreach (var candidate in candidates)
        {
            try
            {
                if (!string.IsNullOrEmpty(candidate) && Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                continue;
            }
        }

        // Ultimate fallback
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static bool IsLikelyProjectDirectory(string path)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);
            var entries = dirInfo.GetFileSystemInfos().Take(20).ToArray(); // Limit for performance
            
            // Look for common project indicators
            var projectIndicators = new[]
            {
                ".git", ".vs", ".vscode", "node_modules", "src", "source",
                "*.sln", "*.csproj", "*.vcxproj", "package.json", "Cargo.toml",
                "pom.xml", "build.gradle", "Makefile", "CMakeLists.txt"
            };

            foreach (var entry in entries)
            {
                var name = entry.Name.ToLowerInvariant();
                
                foreach (var indicator in projectIndicators)
                {
                    if (indicator.StartsWith("*."))
                    {
                        var extension = indicator.Substring(1);
                        if (name.EndsWith(extension))
                        {
                            return true;
                        }
                    }
                    else if (name == indicator.ToLowerInvariant())
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    public void SetCurrentProject(string projectPath)
    {
        if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            return;

        CurrentProjectPath = projectPath;
        
        // Add to recent projects (remove if already exists to avoid duplicates)
        RecentProjects.Remove(projectPath);
        RecentProjects.Insert(0, projectPath);
        
        // Keep only last 10 recent projects
        if (RecentProjects.Count > 10)
        {
            RecentProjects = RecentProjects.Take(10).ToList();
        }
        
        Save();
    }

    public string GetProjectDisplayName()
    {
        if (string.IsNullOrEmpty(CurrentProjectPath))
            return "wcode";

        try
        {
            var dirName = Path.GetFileName(CurrentProjectPath);
            return string.IsNullOrEmpty(dirName) ? CurrentProjectPath : dirName;
        }
        catch
        {
            return "wcode";
        }
    }

    public string GetProjectDisplayPath()
    {
        if (string.IsNullOrEmpty(CurrentProjectPath))
            return "No Project";

        try
        {
            // Show shortened path for long paths
            if (CurrentProjectPath.Length > 60)
            {
                var parts = CurrentProjectPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (parts.Length > 3)
                {
                    return $"{parts[0]}{Path.DirectorySeparatorChar}...{Path.DirectorySeparatorChar}{parts[^2]}{Path.DirectorySeparatorChar}{parts[^1]}";
                }
            }
            
            return CurrentProjectPath;
        }
        catch
        {
            return CurrentProjectPath;
        }
    }
}