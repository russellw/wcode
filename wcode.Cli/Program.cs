using System.Text.Json;
using wcode.Lib;

namespace wcode.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            var options = ParseCommandLineArguments(args);
            
            if (options.ShowHelp)
            {
                ShowHelp();
                return 0;
            }
            
            if (string.IsNullOrEmpty(options.BatchFile))
            {
                Console.WriteLine("Error: --batch option is required");
                ShowHelp();
                return 1;
            }
            
            return await RunBatchModeAsync(options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }
    
    private static CommandLineOptions ParseCommandLineArguments(string[] args)
    {
        var options = new CommandLineOptions();
        
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    options.ShowHelp = true;
                    break;
                    
                case "--batch" when i + 1 < args.Length:
                    options.BatchFile = args[i + 1];
                    i++; // Skip next argument since we consumed it
                    break;
                    
                case var arg when arg.StartsWith("--batch="):
                    options.BatchFile = arg.Substring("--batch=".Length);
                    break;
                    
                case "--project-dir" when i + 1 < args.Length:
                    options.ProjectDirectory = args[i + 1];
                    i++; // Skip next argument since we consumed it
                    break;
                    
                case var arg when arg.StartsWith("--project-dir="):
                    options.ProjectDirectory = arg.Substring("--project-dir=".Length);
                    break;
                    
                case var arg when !arg.StartsWith("--") && string.IsNullOrEmpty(options.ProjectDirectory):
                    // First non-option argument is project directory
                    options.ProjectDirectory = arg;
                    break;
            }
        }
        
        return options;
    }
    
    private static void ShowHelp()
    {
        Console.WriteLine("wcode CLI - Command line interface for LLM-assisted development");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  wcode.cli --batch <file> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --batch <file>           Batch file containing LLM instructions");
        Console.WriteLine("  --project-dir <path>     Project directory path (optional)");
        Console.WriteLine("  --help, -h               Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  wcode.cli --batch instructions.txt");
        Console.WriteLine("  wcode.cli --batch=batch.txt --project-dir=/path/to/project");
        Console.WriteLine("  wcode.cli /path/to/project --batch instructions.txt");
        Console.WriteLine();
        Console.WriteLine("Batch file format:");
        Console.WriteLine("  Multiple prompts can be separated by double newlines (\\n\\n) or '---'");
        Console.WriteLine("  Each prompt will be processed sequentially with full tool calling support");
    }
    
    private static async Task<int> RunBatchModeAsync(CommandLineOptions options)
    {
        Console.WriteLine($"Running in batch mode with file: {options.BatchFile}");
        
        // Validate batch file exists
        if (!File.Exists(options.BatchFile))
        {
            Console.WriteLine($"Error: Batch file not found: {options.BatchFile}");
            return 1;
        }
        
        // Read instructions from file
        var instructions = await File.ReadAllTextAsync(options.BatchFile);
        if (string.IsNullOrWhiteSpace(instructions))
        {
            Console.WriteLine("Error: Batch file is empty");
            return 1;
        }
        
        Console.WriteLine("Initializing batch processor...");
        
        // Determine project directory
        string projectPath;
        if (!string.IsNullOrEmpty(options.ProjectDirectory))
        {
            projectPath = Path.GetFullPath(options.ProjectDirectory);
            if (!Directory.Exists(projectPath))
            {
                Console.WriteLine($"Error: Project directory does not exist: {projectPath}");
                return 1;
            }
        }
        else
        {
            // Load project configuration to get current project path
            var projectConfig = ProjectConfig.Load();
            projectPath = projectConfig.CurrentProjectPath;
            
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                Console.WriteLine("Error: No valid project directory found. Please specify --project-dir or set a project directory first.");
                return 1;
            }
        }
        
        Console.WriteLine($"Using project directory: {projectPath}");
        
        // Initialize services
        var conversationLogger = new ConversationLogger(projectPath);
        var queryService = new ProjectQueryService(projectPath);
        var ollamaClient = new OllamaClient("192.168.0.63", 11434);
        
        try
        {
            // Test Ollama connection
            Console.WriteLine("Testing Ollama connection...");
            if (!await ollamaClient.IsServerAvailableAsync())
            {
                Console.WriteLine("Error: Cannot connect to Ollama server at 192.168.0.63:11434");
                return 1;
            }
            
            // Get available models
            var models = await ollamaClient.GetModelsAsync();
            if (models == null || !models.Any())
            {
                Console.WriteLine("Error: No models available");
                return 1;
            }
            
            var modelName = models.First().Name;
            Console.WriteLine($"Using model: {modelName}");
            
            // Process instructions
            Console.WriteLine("Processing instructions...");
            await ProcessBatchInstructions(ollamaClient, queryService, conversationLogger, instructions, modelName);
            
            Console.WriteLine("Batch processing completed successfully.");
            return 0;
        }
        finally
        {
            ollamaClient.Dispose();
        }
    }
    
    private static async Task ProcessBatchInstructions(OllamaClient ollamaClient, ProjectQueryService queryService, 
        ConversationLogger conversationLogger, string instructions, string modelName)
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
    
    private static async Task<string> ExecuteToolCall(ProjectQueryService queryService, ToolCall toolCall)
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
    
    private static async Task<string> HandleReadFile(ProjectQueryService queryService, JsonElement arguments)
    {
        var filename = arguments.GetProperty("filename").GetString();
        var result = await queryService.ProcessQueryAsync($"read file {filename}");
        return result?.Success == true ? result.Message : $"Error: {result?.Message ?? "Unknown error"}";
    }
    
    private static async Task<string> HandleListFiles(ProjectQueryService queryService, JsonElement arguments)
    {
        var result = await queryService.ProcessQueryAsync("list files");
        return result?.Success == true ? result.Message : $"Error: {result?.Message ?? "Unknown error"}";
    }
    
    private static async Task<string> HandleSearchFiles(ProjectQueryService queryService, JsonElement arguments)
    {
        var query = arguments.GetProperty("query").GetString();
        var result = await queryService.ProcessQueryAsync($"search for {query}");
        return result?.Success == true ? result.Message : $"Error: {result?.Message ?? "Unknown error"}";
    }
    
    private static async Task<string> HandleGetProjectStructure(ProjectQueryService queryService, JsonElement arguments)
    {
        var result = await queryService.ProcessQueryAsync("project structure");
        return result?.Success == true ? result.Message : $"Error: {result?.Message ?? "Unknown error"}";
    }
    
    private static string HandleGetSystemInfo()
    {
        return $"System Information:\n" +
               $"- Tool calling: Available\n" +
               $"- Project query service: Available\n" +
               $"- Current time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
               $"- Available tools: read_file, list_files, search_files, get_project_structure, get_system_info";
    }
    
    private static List<Tool> CreateProjectTools()
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

public class CommandLineOptions
{
    public string? BatchFile { get; set; }
    public string? ProjectDirectory { get; set; }
    public bool ShowHelp { get; set; }
}