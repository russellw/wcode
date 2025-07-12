using System.Text.Json;

namespace wcode.Lib;

public class ProjectToolExecutor
{
    private readonly ProjectQueryService? _queryService;
    
    public ProjectToolExecutor(ProjectQueryService? queryService = null)
    {
        _queryService = queryService;
    }
    
    public async Task<string> ExecuteToolCallAsync(ToolCall toolCall)
    {
        var functionName = toolCall.Function.Name;
        var arguments = toolCall.Function.Arguments;
        string result;
        
        if (_queryService == null && functionName != "get_system_info")
        {
            result = "Project query service not available";
        }
        else
        {
            try
            {
                switch (functionName)
                {
                    case "read_file":
                        var filename = arguments.GetProperty("filename").GetString();
                        
                        // Validate filename parameter
                        if (string.IsNullOrEmpty(filename))
                        {
                            result = "Error reading file: No file path specified";
                        }
                        else
                        {
                            var readResult = await _queryService!.ProcessQueryAsync($"read file {filename}");
                            result = readResult is { Success: true } ? readResult.Message : $"Error reading file: {readResult?.Message ?? "Unknown error"}";  
                        }
                        break;
                        
                    case "write_file":
                        var writeFilename = arguments.GetProperty("filename").GetString();
                        var content = arguments.GetProperty("content").GetString();
                        
                        // Validate parameters before processing
                        if (string.IsNullOrEmpty(writeFilename))
                        {
                            result = "Error writing file: No file path specified";
                        }
                        else if (string.IsNullOrEmpty(content))
                        {
                            result = "Error writing file: No content specified";
                        }
                        else
                        {
                            var writeQuery = $"write file {writeFilename} content: {content}";
                            Console.WriteLine($"[DEBUG] write_file query: {writeQuery}");
                            var writeResult = await _queryService!.ProcessQueryAsync(writeQuery);
                            Console.WriteLine($"[DEBUG] write_file result: Success={writeResult?.Success}, Message={writeResult?.Message}");
                            result = writeResult is { Success: true } ? writeResult.Message : $"Error writing file: {writeResult?.Message ?? "Unknown error"}";  
                        }
                        break;
                        
                    case "list_files":
                        var listResult = await _queryService!.ProcessQueryAsync("list files");
                        result = listResult is { Success: true } ? listResult.Message : $"Error listing files: {listResult?.Message ?? "Unknown error"}";
                        break;
                        
                    case "search_files":
                        var query = arguments.GetProperty("query").GetString();
                        var searchResult = await _queryService!.ProcessQueryAsync($"search for {query}");
                        result = searchResult is { Success: true } ? searchResult.Message : $"Error searching files: {searchResult?.Message ?? "Unknown error"}";
                        break;
                        
                    case "get_project_structure":
                        var structureResult = await _queryService!.ProcessQueryAsync("project structure");
                        result = structureResult is { Success: true } ? structureResult.Message : $"Error getting project structure: {structureResult?.Message ?? "Unknown error"}";
                        break;
                        
                    case "run_program":
                        var command = arguments.GetProperty("command").GetString();
                        var language = arguments.GetProperty("language").GetString();
                        var timeout = arguments.TryGetProperty("timeout", out var timeoutProp) ? timeoutProp.GetInt32() : 30;
                        
                        if (string.IsNullOrEmpty(command))
                        {
                            result = "Error: No command specified";
                        }
                        else if (string.IsNullOrEmpty(language))
                        {
                            result = "Error: No language specified";
                        }
                        else
                        {
                            result = await ExecuteInDockerAsync(command, language, timeout);
                        }
                        break;
                        
                    case "get_system_info":
                        var sysInfo = $"System Information:\n" +
                                     $"- Tool calling: Available\n" +
                                     $"- Project query service: {(_queryService != null ? "Available" : "Not available")}\n" +
                                     $"- Current time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                     $"- Available tools: {(_queryService != null ? "read_file, write_file, list_files, search_files, get_project_structure, run_program, get_system_info" : "get_system_info only")}";
                        result = sysInfo;
                        break;
                        
                    default:
                        result = $"Unknown tool function: {functionName}";
                        break;
                }
            }
            catch (Exception ex)
            {
                result = $"Error executing tool call: {ex.Message}";
            }
        }
        
        return result;
    }
    
    private async Task<string> ExecuteInDockerAsync(string command, string language, int timeoutSeconds)
    {
        try
        {
            string dockerImage = GetDockerImage(language);
            if (string.IsNullOrEmpty(dockerImage))
            {
                return $"Error: Unsupported language '{language}'";
            }

            // Get project path for volume mounting
            var projectPath = _queryService?.ProjectPath ?? Directory.GetCurrentDirectory();
            var workingDir = "/workspace";
            
            // Use shell execution for complex commands with operators like &&, ;, |, etc.
            var shellCommand = command.Contains("&&") || command.Contains(";") || command.Contains("|") || command.Contains(">") || command.Contains("<")
                ? $"sh -c \"{command.Replace("\"", "\\\"")}\""
                : command;
            
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"run --rm --network=none --memory=256m --cpus=0.5 -v \"{projectPath}\":{workingDir} -w {workingDir} {dockerImage} {shellCommand}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process == null)
            {
                return "Error: Failed to start Docker process";
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var processTask = process.WaitForExitAsync();

            var completedTask = await Task.WhenAny(processTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                process.Kill();
                return $"Error: Command timed out after {timeoutSeconds} seconds";
            }

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode == 0)
            {
                return string.IsNullOrEmpty(output) ? "Command executed successfully (no output)" : output;
            }
            else
            {
                return $"Error (exit code {process.ExitCode}): {error}";
            }
        }
        catch (Exception ex)
        {
            return $"Error executing command: {ex.Message}";
        }
    }
    
    private string GetDockerImage(string language)
    {
        return language.ToLower() switch
        {
            "python" => "python:3.11-slim",
            "node" => "node:18-alpine",
            "javascript" => "node:18-alpine",
            "java" => "openjdk:11-jre-slim",
            "go" => "golang:1.21-alpine",
            "rust" => "rust:1.70-slim",
            "ruby" => "ruby:3.0-alpine",
            "php" => "php:8.1-cli-alpine",
            "c" => "gcc:latest",
            "cpp" => "gcc:latest", 
            "bash" => "ubuntu:22.04",
            "shell" => "ubuntu:22.04",
            _ => ""
        };
    }
}