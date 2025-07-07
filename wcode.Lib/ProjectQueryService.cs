using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace wcode.Lib;

public class ProjectQueryService
{
    private readonly string _projectPath;
    private readonly ProjectConfig _projectConfig;

    public ProjectQueryService(string projectPath)
    {
        _projectPath = projectPath;
        _projectConfig = ProjectConfig.Load();
    }

    public async Task<QueryResult> ProcessQueryAsync(string query)
    {
        // Parse the query to determine what the LLM wants to do
        var command = ParseQuery(query);
        
        try
        {
            return command.Type switch
            {
                QueryType.ReadFile => await ReadFileAsync(command.Target),
                QueryType.WriteFile => await WriteFileAsync(command.Target, command.SearchTerm),
                QueryType.ListFiles => await ListFilesAsync(command.Target),
                QueryType.SearchFiles => await SearchFilesAsync(command.Target, command.SearchTerm),
                QueryType.GetProjectStructure => await GetProjectStructureAsync(),
                QueryType.FindFunction => await FindFunctionAsync(command.SearchTerm),
                QueryType.GetFileInfo => await GetFileInfoAsync(command.Target),
                _ => new QueryResult { Success = false, Message = "Unknown query type" }
            };
        }
        catch (Exception ex)
        {
            return new QueryResult 
            { 
                Success = false, 
                Message = $"Error processing query: {ex.Message}" 
            };
        }
    }

    private QueryCommand ParseQuery(string query)
    {
        var lowerQuery = query.ToLower();
        
        // Simple pattern matching for now - could be enhanced with more sophisticated parsing
        if (lowerQuery.Contains("read file") || lowerQuery.Contains("show me") || lowerQuery.Contains("content of"))
        {
            var filePath = ExtractFilePath(query);
            return new QueryCommand { Type = QueryType.ReadFile, Target = filePath };
        }
        
        if (lowerQuery.Contains("write file") || lowerQuery.Contains("create file") || lowerQuery.Contains("save file"))
        {
            var filePath = ExtractFilePath(query);
            var content = ExtractFileContent(query);
            return new QueryCommand { Type = QueryType.WriteFile, Target = filePath, SearchTerm = content };
        }
        
        if (lowerQuery.Contains("list files") || lowerQuery.Contains("what files"))
        {
            var directory = ExtractDirectoryPath(query) ?? _projectPath;
            return new QueryCommand { Type = QueryType.ListFiles, Target = directory };
        }
        
        if (lowerQuery.Contains("search for") || lowerQuery.Contains("find files"))
        {
            var searchTerm = ExtractSearchTerm(query);
            return new QueryCommand { Type = QueryType.SearchFiles, Target = _projectPath, SearchTerm = searchTerm };
        }
        
        if (lowerQuery.Contains("project structure") || lowerQuery.Contains("folder structure"))
        {
            return new QueryCommand { Type = QueryType.GetProjectStructure };
        }
        
        if (lowerQuery.Contains("find function") || lowerQuery.Contains("where is function"))
        {
            var functionName = ExtractFunctionName(query);
            return new QueryCommand { Type = QueryType.FindFunction, SearchTerm = functionName };
        }
        
        if (lowerQuery.Contains("file info") || lowerQuery.Contains("details about"))
        {
            var filePath = ExtractFilePath(query);
            return new QueryCommand { Type = QueryType.GetFileInfo, Target = filePath };
        }
        
        // Default to listing files if no specific command is detected
        return new QueryCommand { Type = QueryType.ListFiles, Target = _projectPath };
    }

    private async Task<QueryResult> ReadFileAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return new QueryResult { Success = false, Message = "No file path specified" };
        }

        var fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(_projectPath, filePath);
        
        if (!File.Exists(fullPath))
        {
            return new QueryResult { Success = false, Message = $"File not found: {filePath}" };
        }

        if (!IsTextFile(fullPath))
        {
            return new QueryResult { Success = false, Message = $"File is not a text file: {filePath}" };
        }

        var content = await File.ReadAllTextAsync(fullPath);
        var relativePath = Path.GetRelativePath(_projectPath, fullPath);
        
        return new QueryResult 
        { 
            Success = true, 
            Message = $"Content of {relativePath}:\n\n```\n{content}\n```",
            Data = new { FilePath = relativePath, Content = content }
        };
    }
    
    private async Task<QueryResult> WriteFileAsync(string filePath, string content)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return new QueryResult { Success = false, Message = "No file path specified" };
        }
        
        if (string.IsNullOrEmpty(content))
        {
            return new QueryResult { Success = false, Message = "No content specified" };
        }

        try
        {
            var fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(_projectPath, filePath);
            
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            await File.WriteAllTextAsync(fullPath, content);
            var relativePath = Path.GetRelativePath(_projectPath, fullPath);
            
            return new QueryResult 
            { 
                Success = true, 
                Message = $"Successfully wrote {content.Length} characters to {relativePath}" 
            };
        }
        catch (Exception ex)
        {
            return new QueryResult 
            { 
                Success = false, 
                Message = $"Error writing file {filePath}: {ex.Message}" 
            };
        }
    }

    private Task<QueryResult> ListFilesAsync(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return Task.FromResult(new QueryResult { Success = false, Message = $"Directory not found: {directory}" });
        }

        var files = new List<string>();
        var directories = new List<string>();

        try
        {
            var dirInfo = new DirectoryInfo(directory);
            
            foreach (var dir in dirInfo.GetDirectories().Where(d => !d.Name.StartsWith(".")))
            {
                directories.Add(dir.Name + "/");
            }
            
            foreach (var file in dirInfo.GetFiles().Where(f => IsTextFile(f.FullName)))
            {
                files.Add(file.Name);
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(new QueryResult { Success = false, Message = "Access denied to directory" });
        }

        var relativePath = Path.GetRelativePath(_projectPath, directory);
        var result = new System.Text.StringBuilder();
        result.AppendLine($"Contents of {(relativePath == "." ? "project root" : relativePath)}:");
        result.AppendLine();
        
        if (directories.Any())
        {
            result.AppendLine("Directories:");
            foreach (var dir in directories.OrderBy(d => d))
            {
                result.AppendLine($"  📁 {dir}");
            }
            result.AppendLine();
        }
        
        if (files.Any())
        {
            result.AppendLine("Files:");
            foreach (var file in files.OrderBy(f => f))
            {
                result.AppendLine($"  📄 {file}");
            }
        }

        return Task.FromResult(new QueryResult 
        { 
            Success = true, 
            Message = result.ToString(),
            Data = new { Directory = relativePath, Files = files, Directories = directories }
        });
    }

    private async Task<QueryResult> SearchFilesAsync(string searchPath, string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm))
        {
            return new QueryResult { Success = false, Message = "No search term specified" };
        }

        var results = new List<SearchResult>();
        
        try
        {
            var files = Directory.GetFiles(searchPath, "*", SearchOption.AllDirectories)
                               .Where(f => IsTextFile(f) && !f.Contains("\\."))
                               .ToArray();

            foreach (var file in files)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var lines = content.Split('\n');
                    
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                        {
                            var relativePath = Path.GetRelativePath(_projectPath, file);
                            results.Add(new SearchResult
                            {
                                FilePath = relativePath,
                                LineNumber = i + 1,
                                LineContent = lines[i].Trim()
                            });
                        }
                    }
                }
                catch
                {
                    // Skip files that can't be read
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            return new QueryResult { Success = false, Message = $"Search failed: {ex.Message}" };
        }

        var message = $"Search results for '{searchTerm}':\n\n";
        if (results.Any())
        {
            foreach (var result in results.Take(20)) // Limit results
            {
                message += $"📄 {result.FilePath}:{result.LineNumber}\n";
                message += $"   {result.LineContent}\n\n";
            }
            
            if (results.Count > 20)
            {
                message += $"... and {results.Count - 20} more results";
            }
        }
        else
        {
            message += "No results found.";
        }

        return new QueryResult 
        { 
            Success = true, 
            Message = message,
            Data = new { SearchTerm = searchTerm, Results = results.Take(20).ToArray() }
        };
    }

    private async Task<QueryResult> GetProjectStructureAsync()
    {
        var structure = await Task.Run(() => BuildDirectoryTree(_projectPath, "", 0, 3)); // Max depth 3
        
        return new QueryResult 
        { 
            Success = true, 
            Message = $"Project structure:\n\n{structure}",
            Data = new { ProjectPath = _projectPath, Structure = structure }
        };
    }

    private async Task<QueryResult> FindFunctionAsync(string functionName)
    {
        if (string.IsNullOrEmpty(functionName))
        {
            return new QueryResult { Success = false, Message = "No function name specified" };
        }

        // Search for function definitions in common patterns
        var patterns = new[]
        {
            $"function {functionName}",
            $"def {functionName}",
            $"public.*{functionName}",
            $"private.*{functionName}",
            $"static.*{functionName}",
            $"{functionName}\\s*\\("
        };

        var results = new List<SearchResult>();
        
        try
        {
            var files = Directory.GetFiles(_projectPath, "*", SearchOption.AllDirectories)
                               .Where(f => IsTextFile(f) && !f.Contains("\\."))
                               .ToArray();

            foreach (var file in files)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var lines = content.Split('\n');
                    
                    for (int i = 0; i < lines.Length; i++)
                    {
                        foreach (var pattern in patterns)
                        {
                            if (System.Text.RegularExpressions.Regex.IsMatch(lines[i], pattern, 
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            {
                                var relativePath = Path.GetRelativePath(_projectPath, file);
                                results.Add(new SearchResult
                                {
                                    FilePath = relativePath,
                                    LineNumber = i + 1,
                                    LineContent = lines[i].Trim()
                                });
                                break; // Don't match multiple patterns on same line
                            }
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            return new QueryResult { Success = false, Message = $"Function search failed: {ex.Message}" };
        }

        var message = $"Function '{functionName}' found in:\n\n";
        if (results.Any())
        {
            foreach (var result in results)
            {
                message += $"📄 {result.FilePath}:{result.LineNumber}\n";
                message += $"   {result.LineContent}\n\n";
            }
        }
        else
        {
            message += "Function not found.";
        }

        return new QueryResult 
        { 
            Success = true, 
            Message = message,
            Data = new { FunctionName = functionName, Results = results.ToArray() }
        };
    }

    private async Task<QueryResult> GetFileInfoAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return new QueryResult { Success = false, Message = "No file path specified" };
        }

        var fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(_projectPath, filePath);
        
        if (!File.Exists(fullPath))
        {
            return new QueryResult { Success = false, Message = $"File not found: {filePath}" };
        }

        var fileInfo = new FileInfo(fullPath);
        var relativePath = Path.GetRelativePath(_projectPath, fullPath);
        
        var message = $"File information for {relativePath}:\n\n";
        message += $"📄 Name: {fileInfo.Name}\n";
        message += $"📁 Directory: {Path.GetDirectoryName(relativePath)}\n";
        message += $"📏 Size: {FormatFileSize(fileInfo.Length)}\n";
        message += $"📅 Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}\n";
        message += $"🔧 Extension: {fileInfo.Extension}\n";
        
        if (IsTextFile(fullPath))
        {
            try
            {
                var content = await File.ReadAllTextAsync(fullPath);
                var lines = content.Split('\n').Length;
                message += $"📝 Lines: {lines}\n";
            }
            catch
            {
                // Ignore if can't read
            }
        }

        return new QueryResult 
        { 
            Success = true, 
            Message = message,
            Data = new { 
                FilePath = relativePath, 
                Size = fileInfo.Length, 
                Modified = fileInfo.LastWriteTime,
                Extension = fileInfo.Extension 
            }
        };
    }

    // Helper methods
    private string BuildDirectoryTree(string path, string indent, int currentDepth, int maxDepth)
    {
        if (currentDepth >= maxDepth) return "";
        
        var result = new System.Text.StringBuilder();
        
        try
        {
            var dirInfo = new DirectoryInfo(path);
            var dirs = dirInfo.GetDirectories().Where(d => !d.Name.StartsWith(".")).OrderBy(d => d.Name);
            var files = dirInfo.GetFiles().Where(f => IsTextFile(f.FullName)).OrderBy(f => f.Name);

            foreach (var dir in dirs)
            {
                result.AppendLine($"{indent}📁 {dir.Name}/");
                if (currentDepth < maxDepth - 1)
                {
                    result.Append(BuildDirectoryTree(dir.FullName, indent + "  ", currentDepth + 1, maxDepth));
                }
            }

            foreach (var file in files.Take(20)) // Limit files shown
            {
                result.AppendLine($"{indent}📄 {file.Name}");
            }
        }
        catch
        {
            result.AppendLine($"{indent}❌ Access denied");
        }

        return result.ToString();
    }

    private bool IsTextFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        var textExtensions = new[]
        {
            ".cs", ".cpp", ".c", ".h", ".hpp", ".java", ".js", ".ts", ".py", ".rb", ".php",
            ".html", ".htm", ".css", ".scss", ".sass", ".xml", ".json", ".yaml", ".yml",
            ".txt", ".md", ".sql", ".sh", ".bat", ".ps1", ".vb", ".fs", ".go", ".rs",
            ".swift", ".kt", ".scala", ".clj", ".pl", ".r", ".m", ".mm", ".xaml", ".config"
        };
        
        return textExtensions.Contains(extension);
    }

    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024)} MB";
        return $"{bytes / (1024 * 1024 * 1024)} GB";
    }

    private string ExtractFilePath(string query)
    {
        // Simple extraction - look for quoted strings or common file patterns
        var matches = System.Text.RegularExpressions.Regex.Matches(query, @"""([^""]+)""");
        if (matches.Any())
        {
            return matches[0].Groups[1].Value;
        }
        
        // Look for file extensions
        var fileMatch = System.Text.RegularExpressions.Regex.Match(query, @"(\w+\.\w+)");
        if (fileMatch.Success)
        {
            return fileMatch.Groups[1].Value;
        }
        
        return "";
    }
    
    private string ExtractFileContent(string query)
    {
        // Look for content after "content:" or similar patterns
        var contentMatch = System.Text.RegularExpressions.Regex.Match(query, @"content[:\s]+(.+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        if (contentMatch.Success)
        {
            return contentMatch.Groups[1].Value.Trim();
        }
        
        // Look for content in quotes after the filename
        var quotedMatch = System.Text.RegularExpressions.Regex.Match(query, @"\w+\.\w+\s+[""']([^""']+)[""']");
        if (quotedMatch.Success)
        {
            return quotedMatch.Groups[1].Value;
        }
        
        return "";
    }

    private string? ExtractDirectoryPath(string query)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(query, @"""([^""]+)""");
        if (matches.Any())
        {
            return matches[0].Groups[1].Value;
        }
        return null;
    }

    private string ExtractSearchTerm(string query)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(query, @"""([^""]+)""");
        if (matches.Any())
        {
            return matches[0].Groups[1].Value;
        }
        
        // Look for "search for X" pattern
        var match = System.Text.RegularExpressions.Regex.Match(query, @"search for (.+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }
        
        return "";
    }

    private string ExtractFunctionName(string query)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(query, @"""([^""]+)""");
        if (matches.Any())
        {
            return matches[0].Groups[1].Value;
        }
        
        // Look for "function X" pattern
        var match = System.Text.RegularExpressions.Regex.Match(query, @"function (\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        
        return "";
    }
}

public enum QueryType
{
    ReadFile,
    WriteFile,
    ListFiles,
    SearchFiles,
    GetProjectStructure,
    FindFunction,
    GetFileInfo
}

public class QueryCommand
{
    public QueryType Type { get; set; }
    public string Target { get; set; } = "";
    public string SearchTerm { get; set; } = "";
}

public class QueryResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public object? Data { get; set; }
}

public class SearchResult
{
    public string FilePath { get; set; } = "";
    public int LineNumber { get; set; }
    public string LineContent { get; set; } = "";
}