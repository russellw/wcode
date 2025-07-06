using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace wcode;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Parse command line arguments
        var (projectPath, batchFile) = ParseCommandLineArguments(e.Args);
        
        // If a project path was provided via command line, update the configuration
        if (!string.IsNullOrEmpty(projectPath))
        {
            UpdateProjectConfigFromCommandLine(projectPath);
        }
        
        // If batch mode is requested, run in headless mode
        if (!string.IsNullOrEmpty(batchFile))
        {
            // Run batch mode synchronously to ensure it completes
            Task.Run(async () => await RunBatchModeAsync(batchFile)).Wait();
            Environment.Exit(0);
            return; // Don't call base.OnStartup to avoid showing UI
        }
        
        base.OnStartup(e);
    }
    
    private (string? projectPath, string? batchFile) ParseCommandLineArguments(string[] args)
    {
        if (args == null || args.Length == 0)
            return (null, null);
        
        string? projectPath = null;
        string? batchFile = null;
        
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--batch" && i + 1 < args.Length)
            {
                batchFile = args[i + 1];
                i++; // Skip next argument since we consumed it
            }
            else if (args[i].StartsWith("--batch="))
            {
                batchFile = args[i].Substring("--batch=".Length);
            }
            else if (!args[i].StartsWith("--") && projectPath == null)
            {
                // First non-option argument is project path
                projectPath = args[i];
            }
        }
        
        return (projectPath, batchFile);
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
    
    private async Task RunBatchModeAsync(string batchFile)
    {
        try
        {
            Console.WriteLine($"Running in batch mode with file: {batchFile}");
            
            // Validate batch file exists
            if (!File.Exists(batchFile))
            {
                Console.WriteLine($"Error: Batch file not found: {batchFile}");
                Environment.Exit(1);
                return;
            }
            
            // Read instructions from file
            var instructions = await File.ReadAllTextAsync(batchFile);
            if (string.IsNullOrWhiteSpace(instructions))
            {
                Console.WriteLine("Error: Batch file is empty");
                Environment.Exit(1);
                return;
            }
            
            Console.WriteLine("Initializing batch processor...");
            
            // Load project configuration to get current project path
            var projectConfig = ProjectConfig.Load();
            var projectPath = projectConfig.CurrentProjectPath;
            
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                Console.WriteLine("Error: No valid project directory found. Please set a project directory first.");
                Environment.Exit(1);
                return;
            }
            
            Console.WriteLine($"Using project directory: {projectPath}");
            
            // Initialize services
            var conversationLogger = new ConversationLogger(projectPath);
            var queryService = new ProjectQueryService(projectPath);
            var ollamaClient = new OllamaClient("192.168.0.63", 11434);
            
            // Test Ollama connection
            Console.WriteLine("Testing Ollama connection...");
            if (!await ollamaClient.IsServerAvailableAsync())
            {
                Console.WriteLine("Error: Cannot connect to Ollama server at 192.168.0.63:11434");
                Environment.Exit(1);
                return;
            }
            
            // Get available models
            var models = await ollamaClient.GetModelsAsync();
            if (models == null || !models.Any())
            {
                Console.WriteLine("Error: No models available");
                Environment.Exit(1);
                return;
            }
            
            var modelName = models.First().Name;
            Console.WriteLine($"Using model: {modelName}");
            
            // Process instructions
            Console.WriteLine("Processing instructions...");
            await ProcessBatchInstructions(ollamaClient, queryService, conversationLogger, instructions, modelName, projectPath);
            
            Console.WriteLine("Batch processing completed successfully.");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in batch mode: {ex.Message}");
            Environment.Exit(1);
        }
    }
    
    private async Task ProcessBatchInstructions(OllamaClient ollamaClient, ProjectQueryService queryService, 
        ConversationLogger conversationLogger, string instructions, string modelName, string projectPath)
    {
        // Split instructions into individual prompts (separated by double newlines or explicit separators)
        var prompts = instructions.Split(new[] { "\n\n", "---" }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(p => p.Trim())
                                  .Where(p => !string.IsNullOrEmpty(p))
                                  .ToArray();
        
        if (prompts.Length == 0)
        {
            Console.WriteLine("No valid prompts found in batch file");
            return;
        }
        
        Console.WriteLine($"Found {prompts.Length} prompt(s) to process");
        
        for (int i = 0; i < prompts.Length; i++)
        {
            var prompt = prompts[i];
            Console.WriteLine($"\n--- Processing prompt {i + 1}/{prompts.Length} ---");
            Console.WriteLine($"Prompt: {prompt.Substring(0, Math.Min(100, prompt.Length))}...");
            
            try
            {
                // Create project tools
                var tools = CreateProjectTools();
                
                // Send prompt to LLM with tool calling support
                var response = await ollamaClient.ChatWithToolsAsync(modelName, prompt, null, tools);
                
                if (response?.Message != null)
                {
                    Console.WriteLine($"LLM Response: {response.Message.Content}");
                    
                    // Handle any tool calls
                    if (response.Message.ToolCalls?.Any() == true)
                    {
                        Console.WriteLine($"Processing {response.Message.ToolCalls.Count} tool call(s)...");
                        
                        foreach (var toolCall in response.Message.ToolCalls)
                        {
                            var result = await ExecuteToolCall(queryService, toolCall);
                            Console.WriteLine($"Tool '{toolCall.Function.Name}' result: {result.Substring(0, Math.Min(200, result.Length))}...");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No response received from LLM");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing prompt {i + 1}: {ex.Message}");
            }
            
            // Small delay between prompts
            await Task.Delay(1000);
        }
    }
    
    private async Task<string> ExecuteToolCall(ProjectQueryService queryService, wcode.ToolCall toolCall)
    {
        try
        {
            var functionName = toolCall.Function.Name;
            var arguments = toolCall.Function.Arguments;
            
            return functionName switch
            {
                "read_file" => await HandleReadFile(queryService, arguments),
                "list_files" => await HandleListFiles(queryService, arguments),
                "search_files" => await HandleSearchFiles(queryService, arguments),
                "get_project_structure" => await HandleGetProjectStructure(queryService, arguments),
                "get_system_info" => HandleGetSystemInfo(),
                _ => $"Unknown tool: {functionName}"
            };
        }
        catch (Exception ex)
        {
            return $"Error executing tool: {ex.Message}";
        }
    }
    
    private async Task<string> HandleReadFile(ProjectQueryService queryService, System.Text.Json.JsonElement arguments)
    {
        var filename = arguments.GetProperty("filename").GetString();
        var result = await queryService.ProcessQueryAsync($"read file {filename}");
        return result?.Success == true ? result.Message : $"Error: {result?.Message ?? "Unknown error"}";
    }
    
    private async Task<string> HandleListFiles(ProjectQueryService queryService, System.Text.Json.JsonElement arguments)
    {
        var result = await queryService.ProcessQueryAsync("list files");
        return result?.Success == true ? result.Message : $"Error: {result?.Message ?? "Unknown error"}";
    }
    
    private async Task<string> HandleSearchFiles(ProjectQueryService queryService, System.Text.Json.JsonElement arguments)
    {
        var query = arguments.GetProperty("query").GetString();
        var result = await queryService.ProcessQueryAsync($"search for {query}");
        return result?.Success == true ? result.Message : $"Error: {result?.Message ?? "Unknown error"}";
    }
    
    private async Task<string> HandleGetProjectStructure(ProjectQueryService queryService, System.Text.Json.JsonElement arguments)
    {
        var result = await queryService.ProcessQueryAsync("project structure");
        return result?.Success == true ? result.Message : $"Error: {result?.Message ?? "Unknown error"}";
    }
    
    private string HandleGetSystemInfo()
    {
        return $"System Information:\n" +
               $"- Tool calling: Available\n" +
               $"- Project query service: Available\n" +
               $"- Current time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
               $"- Available tools: read_file, list_files, search_files, get_project_structure, get_system_info";
    }
    
    private List<Tool> CreateProjectTools()
    {
        var tools = new List<Tool>
        {
            new Tool
            {
                Type = "function",
                Function = new ToolFunction
                {
                    Name = "read_file",
                    Description = "Read the contents of a file in the project",
                    Parameters = JsonSerializer.SerializeToElement(new
                    {
                        type = "object",
                        properties = new
                        {
                            filename = new
                            {
                                type = "string",
                                description = "The name or path of the file to read"
                            }
                        },
                        required = new[] { "filename" }
                    })
                }
            },
            new Tool
            {
                Type = "function",
                Function = new ToolFunction
                {
                    Name = "list_files",
                    Description = "List all files in the project directory",
                    Parameters = JsonSerializer.SerializeToElement(new
                    {
                        type = "object",
                        properties = new object(),
                        required = new string[0]
                    })
                }
            },
            new Tool
            {
                Type = "function",
                Function = new ToolFunction
                {
                    Name = "search_files",
                    Description = "Search for content within project files",
                    Parameters = JsonSerializer.SerializeToElement(new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new
                            {
                                type = "string",
                                description = "The text to search for"
                            }
                        },
                        required = new[] { "query" }
                    })
                }
            },
            new Tool
            {
                Type = "function",
                Function = new ToolFunction
                {
                    Name = "get_project_structure",
                    Description = "Get the overall structure of the project",
                    Parameters = JsonSerializer.SerializeToElement(new
                    {
                        type = "object",
                        properties = new object(),
                        required = new string[0]
                    })
                }
            },
            new Tool
            {
                Type = "function",
                Function = new ToolFunction
                {
                    Name = "get_system_info",
                    Description = "Get information about the current system and available capabilities",
                    Parameters = JsonSerializer.SerializeToElement(new
                    {
                        type = "object",
                        properties = new object(),
                        required = new string[0]
                    })
                }
            }
        };
        
        return tools;
    }
}