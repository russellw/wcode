using System.Text.Json;

namespace OllamaToolCallingDemo;

/// <summary>
/// Demonstration program for Ollama tool calling functionality
/// Shows how to define tools and persuade the model to use them
/// </summary>
public class Program
{
    private static readonly Dictionary<string, Func<JsonElement, object>> AvailableFunctions = new()
    {
        { "calculate", ExecuteCalculate },
        { "get_weather", ExecuteGetWeather }
    };

    /// <summary>
    /// Calculator tool function
    /// </summary>
    private static object ExecuteCalculate(JsonElement arguments)
    {
        try
        {
            var operation = arguments.GetProperty("operation").GetString();
            var a = arguments.GetProperty("a").GetDouble();
            var b = arguments.GetProperty("b").GetDouble();

            return operation switch
            {
                "add" => a + b,
                "subtract" => a - b,
                "multiply" => a * b,
                "divide" => b != 0 ? a / b : throw new ArgumentException("Cannot divide by zero"),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Weather tool function (mock implementation)
    /// </summary>
    private static object ExecuteGetWeather(JsonElement arguments)
    {
        try
        {
            var location = arguments.GetProperty("location").GetString();
            return $"The weather in {location} is sunny with a temperature of 22°C";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Create tool definitions for the Ollama API
    /// </summary>
    private static List<Tool> CreateTools()
    {
        var calculateTool = new Tool
        {
            Type = "function",
            Function = new ToolFunction
            {
                Name = "calculate",
                Description = "Perform basic mathematical operations",
                Parameters = JsonSerializer.Deserialize<JsonElement>("""
                {
                    "type": "object",
                    "properties": {
                        "operation": {
                            "type": "string",
                            "description": "The operation to perform: add, subtract, multiply, or divide"
                        },
                        "a": {
                            "type": "number",
                            "description": "The first number"
                        },
                        "b": {
                            "type": "number", 
                            "description": "The second number"
                        }
                    },
                    "required": ["operation", "a", "b"]
                }
                """)
            }
        };

        var weatherTool = new Tool
        {
            Type = "function",
            Function = new ToolFunction
            {
                Name = "get_weather",
                Description = "Get weather information for a location",
                Parameters = JsonSerializer.Deserialize<JsonElement>("""
                {
                    "type": "object",
                    "properties": {
                        "location": {
                            "type": "string",
                            "description": "The location to get weather for"
                        }
                    },
                    "required": ["location"]
                }
                """)
            }
        };

        return new List<Tool> { calculateTool, weatherTool };
    }

    /// <summary>
    /// Handle the response and execute any tool calls
    /// </summary>
    private static async Task HandleResponse(ChatResponse? response)
    {
        if (response?.Message == null)
        {
            Console.WriteLine("❌ No response received");
            return;
        }

        Console.WriteLine($"\n🤖 Model response: {response.Message.Content}");

        if (response.Message.ToolCalls != null && response.Message.ToolCalls.Count > 0)
        {
            Console.WriteLine("\n🔧 Tool calls detected:");
            foreach (var toolCall in response.Message.ToolCalls)
            {
                var funcName = toolCall.Function.Name;
                var funcArgs = toolCall.Function.Arguments;

                Console.WriteLine($"   - Function: {funcName}");
                Console.WriteLine($"   - Arguments: {funcArgs}");

                if (AvailableFunctions.TryGetValue(funcName, out var function))
                {
                    try
                    {
                        var result = function(funcArgs);
                        Console.WriteLine($"   - Result: {result}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   - Error: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"   - Function not found: {funcName}");
                }
            }
        }
        else
        {
            Console.WriteLine("\n⚠️  No tool calls were made by the model");
            Console.WriteLine("    The model may not support tool calling or needs different prompting");
        }
    }

    /// <summary>
    /// Entry point for the demo
    /// </summary>
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🦙 Ollama Tool Calling Demo (C#)");
        Console.WriteLine("==========================================");

        using var client = new OllamaClient();

        // Test connection
        if (!await client.IsServerAvailableAsync())
        {
            Console.WriteLine("❌ Cannot connect to Ollama server at 192.168.0.63:11434");
            return;
        }

        Console.WriteLine("✅ Connected to Ollama server");

        // Get available models
        var models = await client.GetModelsAsync();
        if (models.Count == 0)
        {
            Console.WriteLine("❌ No models available");
            return;
        }

        var modelName = models[0].Name;
        Console.WriteLine($"📋 Using model: {modelName}");

        var tools = CreateTools();

        // Example 1: Mathematical calculation
        Console.WriteLine("\n📊 Example 1: Mathematical calculation");
        Console.WriteLine("Asking: 'What is 15 multiplied by 7? Please use the calculator tool.'");

        var response1 = await client.ChatWithToolsAsync(
            modelName,
            "What is 15 multiplied by 7? Please use the calculator tool to get the exact result.",
            null,
            tools);

        await HandleResponse(response1);

        // Example 2: Weather query
        Console.WriteLine("\n==========================================");
        Console.WriteLine("🌤️  Example 2: Weather query");
        Console.WriteLine("Asking: 'What's the weather like in Paris? Use the weather tool.'");

        var response2 = await client.ChatWithToolsAsync(
            modelName,
            "What's the weather like in Paris? Please use the weather tool to get current information.",
            null,
            tools);

        await HandleResponse(response2);

        Console.WriteLine("\n==========================================");
        Console.WriteLine("✅ Demo completed!");
        
        // Keep console open
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}