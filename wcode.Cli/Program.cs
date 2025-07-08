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
            
            if (string.IsNullOrEmpty(options.BatchFile) && !options.TestTool)
            {
                Console.WriteLine("Error: batch file is required (or use --test-tool)");
                ShowHelp();
                return 1;
            }
            
            if (options.TestTool)
            {
                return await RunToolTestAsync(options);
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
                    
                case "--test-tool" when i + 1 < args.Length:
                    options.TestTool = true;
                    options.ToolName = args[i + 1];
                    i++; // Skip next argument since we consumed it
                    break;
                    
                case var arg when arg.StartsWith("--test-tool="):
                    options.TestTool = true;
                    options.ToolName = arg.Substring("--test-tool=".Length);
                    break;
                    
                case "--project-dir" when i + 1 < args.Length:
                    options.ProjectDirectory = args[i + 1];
                    i++; // Skip next argument since we consumed it
                    break;
                    
                case var arg when arg.StartsWith("--project-dir="):
                    options.ProjectDirectory = arg.Substring("--project-dir=".Length);
                    break;
                    
                case var arg when !arg.StartsWith("--"):
                    // First non-option argument is batch file, second is project directory
                    if (string.IsNullOrEmpty(options.BatchFile))
                    {
                        options.BatchFile = arg;
                    }
                    else if (string.IsNullOrEmpty(options.ProjectDirectory))
                    {
                        options.ProjectDirectory = arg;
                    }
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
        Console.WriteLine("  wcode.cli <batch-file> [project-dir] [options]");
        Console.WriteLine("  wcode.cli --test-tool <tool-name> [project-dir] [options]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  batch-file               Batch file containing LLM instructions");
        Console.WriteLine("  project-dir              Project directory path (optional)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --test-tool <name>       Test individual tool (read_file, write_file, list_files, search_files, get_project_structure, get_system_info)");
        Console.WriteLine("  --project-dir <path>     Project directory path (alternative to positional arg)");
        Console.WriteLine("  --help, -h               Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  wcode.cli instructions.txt");
        Console.WriteLine("  wcode.cli batch.txt /path/to/project");
        Console.WriteLine("  wcode.cli --test-tool read_file /path/to/project");
        Console.WriteLine("  wcode.cli --test-tool=list_files --project-dir=/path/to/project");
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
                // Create project tools using library
                var tools = ProjectToolProvider.CreateProjectTools(queryService);
                var toolExecutor = new ProjectToolExecutor(queryService);
                
                // Initialize conversation history for this prompt
                var conversationHistory = new List<ChatMessage>();
                
                // Process the prompt with conversation loop
                await ProcessPromptWithConversationLoop(ollamaClient, modelName, prompt, tools, toolExecutor, conversationHistory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing prompt {i + 1}: {ex.Message}");
            }
            
            // Small delay between prompts
            await Task.Delay(1000);
        }
    }
    
    private static async Task ProcessPromptWithConversationLoop(
        OllamaClient ollamaClient, 
        string modelName, 
        string initialPrompt, 
        List<Tool> tools, 
        ProjectToolExecutor toolExecutor, 
        List<ChatMessage> conversationHistory)
    {
        // Add initial user message
        conversationHistory.Add(new ChatMessage { Role = "user", Content = initialPrompt });
        
        const int maxIterations = 10; // Prevent infinite loops
        int iteration = 0;
        
        while (iteration < maxIterations)
        {
            iteration++;
            Console.WriteLine($"\n--- Conversation turn {iteration} ---");
            
            // Send current conversation to LLM
            var response = await ollamaClient.ChatWithToolsAsync(modelName, 
                conversationHistory.Last().Content, 
                conversationHistory.Take(conversationHistory.Count - 1).ToList(), 
                tools);
            
            if (response?.Message == null)
            {
                Console.WriteLine("No response received from LLM");
                break;
            }
            
            // Display LLM response
            if (!string.IsNullOrEmpty(response.Message.Content))
            {
                Console.WriteLine($"LLM Response: {response.Message.Content}");
            }
            
            // Check if there are tool calls to process
            if (response.Message.ToolCalls?.Any() == true)
            {
                Console.WriteLine($"Processing {response.Message.ToolCalls.Count} tool call(s)...");
                
                // Add assistant message with tool calls to history
                conversationHistory.Add(new ChatMessage 
                { 
                    Role = "assistant", 
                    Content = response.Message.Content ?? "", 
                    ToolCalls = response.Message.ToolCalls 
                });
                
                // Execute each tool call and add results to conversation
                var allToolResults = new List<string>();
                foreach (var toolCall in response.Message.ToolCalls)
                {
                    var result = await toolExecutor.ExecuteToolCallAsync(toolCall);
                    Console.WriteLine($"Tool '{toolCall.Function.Name}' executed successfully");
                    
                    // Add tool result as user message
                    conversationHistory.Add(new ChatMessage 
                    { 
                        Role = "user", 
                        Content = $"Tool result for {toolCall.Function.Name}: {result}" 
                    });
                    
                    allToolResults.Add(result);
                }
                
                // Continue the conversation loop to let LLM process tool results
                continue;
            }
            else
            {
                // No tool calls - conversation is complete
                conversationHistory.Add(new ChatMessage 
                { 
                    Role = "assistant", 
                    Content = response.Message.Content ?? "" 
                });
                
                Console.WriteLine("Conversation completed - no further tool calls needed.");
                break;
            }
        }
        
        if (iteration >= maxIterations)
        {
            Console.WriteLine("Warning: Reached maximum conversation iterations, stopping.");
        }
    }
    
    private static async Task<int> RunToolTestAsync(CommandLineOptions options)
    {
        try
        {
            Console.WriteLine($"Testing tool: {options.ToolName}");
            
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
                projectPath = Directory.GetCurrentDirectory();
            }
            
            Console.WriteLine($"Using project directory: {projectPath}");
            
            // Create query service and tool executor
            var queryService = new ProjectQueryService(projectPath);
            var toolExecutor = new ProjectToolExecutor(queryService);
            
            // Create test tool call based on tool name
            var toolCall = CreateTestToolCall(options.ToolName!);
            if (toolCall == null)
            {
                Console.WriteLine($"Error: Unknown tool name '{options.ToolName}'. Available tools: read_file, write_file, list_files, search_files, get_project_structure, get_system_info");
                return 1;
            }
            
            Console.WriteLine($"Executing tool '{options.ToolName}'...");
            var result = await toolExecutor.ExecuteToolCallAsync(toolCall);
            
            Console.WriteLine("\n--- Tool Result ---");
            Console.WriteLine(result);
            Console.WriteLine("\n--- End Result ---");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error testing tool: {ex.Message}");
            return 1;
        }
    }
    
    private static ToolCall? CreateTestToolCall(string toolName)
    {
        var toolCall = new ToolCall
        {
            Id = Guid.NewGuid().ToString(),
            Type = "function",
            Function = new ToolCallFunction
            {
                Name = toolName
            }
        };
        
        switch (toolName)
        {
            case "read_file":
                Console.Write("Enter filename to read: ");
                var filename = Console.ReadLine();
                if (string.IsNullOrEmpty(filename))
                {
                    Console.WriteLine("Error: filename is required");
                    return null;
                }
                toolCall.Function.Arguments = JsonSerializer.SerializeToElement(new { filename });
                break;
                
            case "write_file":
                Console.Write("Enter filename to write: ");
                var writeFilename = Console.ReadLine();
                if (string.IsNullOrEmpty(writeFilename))
                {
                    Console.WriteLine("Error: filename is required");
                    return null;
                }
                Console.Write("Enter content to write: ");
                var content = Console.ReadLine();
                if (string.IsNullOrEmpty(content))
                {
                    Console.WriteLine("Error: content is required");
                    return null;
                }
                toolCall.Function.Arguments = JsonSerializer.SerializeToElement(new { filename = writeFilename, content });
                break;
                
            case "search_files":
                Console.Write("Enter search query: ");
                var query = Console.ReadLine();
                if (string.IsNullOrEmpty(query))
                {
                    Console.WriteLine("Error: search query is required");
                    return null;
                }
                toolCall.Function.Arguments = JsonSerializer.SerializeToElement(new { query });
                break;
                
            case "list_files":
            case "get_project_structure":
            case "get_system_info":
                toolCall.Function.Arguments = JsonSerializer.SerializeToElement(new { });
                break;
                
            default:
                return null;
        }
        
        return toolCall;
    }
    
}

public class CommandLineOptions
{
    public string? BatchFile { get; set; }
    public string? ProjectDirectory { get; set; }
    public bool ShowHelp { get; set; }
    public bool TestTool { get; set; }
    public string? ToolName { get; set; }
}