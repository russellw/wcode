using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;

namespace wcode;

public partial class TerminalTabControl : UserControl
{
    public class ChatMessage : INotifyPropertyChanged
    {
        private string _message = string.Empty;
        
        public string Sender { get; set; } = string.Empty;
        
        public string Message 
        { 
            get => _message;
            set
            {
                if (_message != value)
                {
                    _message = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public string Timestamp { get; set; } = string.Empty;
        public Brush SenderColor { get; set; } = Brushes.White;
        public Brush BackgroundColor { get; set; } = Brushes.Transparent;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private ObservableCollection<ChatMessage> _messages = new();
    private OllamaClient? _ollamaClient;
    private List<wcode.ChatMessage> _conversationHistory = new();
    private string _currentModel = "";
    private bool _isConnected = false;
    private ConversationLogger? _conversationLogger;
    private ProjectQueryService? _queryService;

    public TerminalTabControl()
    {
        InitializeComponent();
        ChatMessagesControl.ItemsSource = _messages;
        
        InitializeOllamaClient();
        InitializeLogging();
    }

    private void InitializeLogging()
    {
        try
        {
            // Get current project path from config
            var projectConfig = ProjectConfig.Load();
            if (projectConfig.LoggingEnabled && 
                !string.IsNullOrEmpty(projectConfig.CurrentProjectPath) && 
                Directory.Exists(projectConfig.CurrentProjectPath))
            {
                _conversationLogger = new ConversationLogger(projectConfig.CurrentProjectPath);
                _queryService = new ProjectQueryService(projectConfig.CurrentProjectPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize conversation logging: {ex.Message}");
        }
    }

    private async void InitializeOllamaClient()
    {
        _ollamaClient = new OllamaClient();
        
        AddSystemMessage("Connecting to Ollama server...");
        
        _isConnected = await _ollamaClient.IsServerAvailableAsync();
        
        if (_isConnected)
        {
            var models = await _ollamaClient.GetModelsAsync();
            if (models.Any())
            {
                _currentModel = models.First().Name;
                HeaderText.Text = $"LLM Chat - {_currentModel}";
                AddSystemMessage($"✅ Connected to Ollama!\nUsing model: {_currentModel}\nAvailable models: {string.Join(", ", models.Select(m => m.Name))}");
                
                // Add system context about project capabilities
                if (_queryService != null)
                {
                    var systemContext = "You have access to project files and can help users with code analysis, file exploration, and project understanding. When users ask about project files, the system will automatically provide you with the relevant file contents or project information to analyze and discuss.";
                    _conversationHistory.Add(new wcode.ChatMessage { Role = "system", Content = systemContext });
                }
            }
            else
            {
                HeaderText.Text = "LLM Chat - No Models";
                AddSystemMessage("⚠️ Connected to Ollama but no models found.\nPlease install a model: ollama pull llama2");
                _isConnected = false;
            }
        }
        else
        {
            HeaderText.Text = "LLM Chat - Disconnected";
            AddSystemMessage("❌ Could not connect to Ollama server at 192.168.0.63:11434\n\nPlease ensure:\n• Ollama is running\n• Server is accessible on the network\n• Firewall allows connections");
        }
    }
    
    private void AddSystemMessage(string message)
    {
        _messages.Add(new ChatMessage
        {
            Sender = "System",
            Message = message,
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            SenderColor = new SolidColorBrush(Color.FromRgb(0, 120, 204)),
            BackgroundColor = new SolidColorBrush(Color.FromRgb(45, 45, 48))
        });
        ScrollToBottom();
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SendMessage();
    }

    private void MessageInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SendMessage();
        }
    }

    private void SendMessage()
    {
        var message = MessageInput.Text.Trim();
        if (string.IsNullOrEmpty(message)) return;

        _messages.Add(new ChatMessage
        {
            Sender = "You",
            Message = message,
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            SenderColor = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            BackgroundColor = new SolidColorBrush(Color.FromRgb(30, 30, 30))
        });

        // Note: User message logging moved to SendToOllamaAsync to include tool definitions

        MessageInput.Clear();
        
        ScrollToBottom();
        
        if (_isConnected && _ollamaClient != null)
        {
            _ = SendToOllamaAsync(message);
        }
        else
        {
            AddSystemMessage("❌ Not connected to Ollama server. Please check connection.");
        }
    }

    private async Task SendToOllamaAsync(string userMessage)
    {
        if (_ollamaClient == null) return;
        
        // Add user message to conversation history
        _conversationHistory.Add(new wcode.ChatMessage { Role = "user", Content = userMessage });
        
        // Create project tools if available
        var tools = CreateProjectTools();
        
        // Debug: Log tool creation
        var debugInfo = $"Created {tools?.Count ?? 0} tools for user message: {userMessage}";
        System.Diagnostics.Debug.WriteLine(debugInfo);
        
        if (tools != null)
        {
            foreach (var tool in tools)
            {
                var toolInfo = $"Tool: {tool.Function.Name} - {tool.Function.Description}";
                System.Diagnostics.Debug.WriteLine(toolInfo);
                debugInfo += $"\n{toolInfo}";
            }
        }
        
        // Write debug info to file
        _ = Task.Run(async () =>
        {
            try
            {
                await File.WriteAllTextAsync("ollama_debug_tools.txt", debugInfo);
            }
            catch
            {
                // Ignore file write errors
            }
        });
        
        // Log user message with tools
        _ = Task.Run(async () =>
        {
            try
            {
                if (_conversationLogger != null)
                {
                    var userMessageWithTools = new
                    {
                        message = userMessage,
                        tools = tools?.Select(t => new
                        {
                            type = t.Type,
                            function = new
                            {
                                name = t.Function.Name,
                                description = t.Function.Description,
                                parameters = t.Function.Parameters
                            }
                        }).ToArray()
                    };
                    
                    var messageJson = JsonSerializer.Serialize(userMessageWithTools, new JsonSerializerOptions { WriteIndented = true });
                    await _conversationLogger.LogConversationAsync("User", messageJson);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to log user message: {ex.Message}");
            }
        });
        
        // Show "thinking" indicator
        var thinkingMessage = new ChatMessage
        {
            Sender = "LLM",
            Message = "🤔 Thinking...",
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            SenderColor = new SolidColorBrush(Color.FromRgb(255, 165, 0)),
            BackgroundColor = new SolidColorBrush(Color.FromRgb(25, 25, 35))
        };
        _messages.Add(thinkingMessage);
        ScrollToBottom();
        
        try
        {
            var startTime = DateTime.Now;
            
            // Use tool calling approach
            var response = await _ollamaClient.ChatWithToolsAsync(_currentModel, userMessage, _conversationHistory.Take(_conversationHistory.Count - 1).ToList(), tools);
            
            var responseMessage = new ChatMessage
            {
                Sender = "LLM",
                Message = "",
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                SenderColor = new SolidColorBrush(Color.FromRgb(255, 165, 0)),
                BackgroundColor = new SolidColorBrush(Color.FromRgb(25, 25, 35))
            };
            
            // Remove thinking message and add response message
            _messages.Remove(thinkingMessage);
            _messages.Add(responseMessage);
            
            var fullResponse = "";
            
            if (response?.Message != null)
            {
                // Check if there are tool calls
                if (response.Message.ToolCalls != null && response.Message.ToolCalls.Any())
                {
                    // Handle tool calls
                    var toolResults = new List<string>();
                    
                    foreach (var toolCall in response.Message.ToolCalls)
                    {
                        var toolResult = await ExecuteToolCall(toolCall);
                        toolResults.Add(toolResult);
                    }
                    
                    // Add tool results to conversation and get final response
                    _conversationHistory.Add(new wcode.ChatMessage { Role = "assistant", Content = response.Message.Content ?? "", ToolCalls = response.Message.ToolCalls });
                    
                    // Add tool results as user messages
                    for (int i = 0; i < toolResults.Count; i++)
                    {
                        _conversationHistory.Add(new wcode.ChatMessage { Role = "user", Content = $"Tool result: {toolResults[i]}" });
                    }
                    
                    // Directly use tool results as the response since tools executed successfully
                    if (toolResults.Count == 1)
                    {
                        fullResponse = $"Tool '{response.Message.ToolCalls[0].Function.Name}' executed successfully:\n\n{toolResults[0]}";
                    }
                    else
                    {
                        var resultText = "";
                        for (int i = 0; i < toolResults.Count; i++)
                        {
                            resultText += $"Tool '{response.Message.ToolCalls[i].Function.Name}' result:\n{toolResults[i]}\n\n";
                        }
                        fullResponse = resultText.TrimEnd();
                    }
                }
                else
                {
                    // No tool calls, just regular response
                    fullResponse = response.Message.Content ?? "No response received";
                }
            }
            else
            {
                fullResponse = "No response received";
            }
            
            await Dispatcher.InvokeAsync(() =>
            {
                responseMessage.Message = fullResponse;
                ScrollToBottom();
            });
            
            // Add assistant response to conversation history
            if (!string.IsNullOrEmpty(fullResponse))
            {
                _conversationHistory.Add(new wcode.ChatMessage { Role = "assistant", Content = fullResponse });
                
                // Log complete LLM response including tool calls
                var responseTime = (int)(DateTime.Now - startTime).TotalMilliseconds;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (_conversationLogger != null)
                        {
                            var completeResponse = new
                            {
                                message = new
                                {
                                    role = "assistant",
                                    content = response?.Message?.Content ?? "",
                                    tool_calls = response?.Message?.ToolCalls?.Select(tc => new
                                    {
                                        id = tc.Id,
                                        type = tc.Type,
                                        function = new
                                        {
                                            name = tc.Function.Name,
                                            arguments = tc.Function.Arguments
                                        }
                                    }).ToArray()
                                },
                                done = response?.Done ?? true,
                                final_response = fullResponse
                            };
                            
                            var responseJson = JsonSerializer.Serialize(completeResponse, new JsonSerializerOptions { WriteIndented = true });
                            await _conversationLogger.LogConversationAsync("Assistant", responseJson, _currentModel, 0, responseTime);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to log LLM response: {ex.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _messages.Remove(thinkingMessage);
            AddSystemMessage($"❌ Error communicating with Ollama: {ex.Message}");
        }
    }

    private void ScrollToBottom()
    {
        ChatScrollViewer.ScrollToBottom();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _messages.Clear();
        _conversationHistory.Clear();
        if (_isConnected)
        {
            AddSystemMessage($"Chat cleared. Using model: {_currentModel}");
        }
        else
        {
            AddSystemMessage("Chat cleared. Not connected to Ollama server.");
        }
    }
    
    private bool IsProjectQuery(string message)
    {
        var lowerMessage = message.ToLower();
        var queryKeywords = new[]
        {
            "read file", "show me", "content of", "list files", "what files",
            "search for", "find files", "project structure", "folder structure",
            "find function", "where is function", "file info", "details about",
            "longest file", "largest file", "biggest file", "file size", "how many files",
            "which file", "what file", "code in", "implementation of", "where is",
            "open file", "view file", "display file", "analyze file", "examine file"
        };
        
        return queryKeywords.Any(keyword => lowerMessage.Contains(keyword));
    }
    
    private List<Tool>? CreateProjectTools()
    {
        // Always provide basic tools, even if project query service is unavailable
        var tools = new List<Tool>();
        
        // Add project tools only if query service is available
        if (_queryService != null)
        {
            tools.AddRange(new List<Tool>
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
            }
            });
        }
        
        // Add a simple diagnostic tool that always works
        tools.Add(new Tool
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
        });
        
        return tools.Count > 0 ? tools : null;
    }
    
    private async Task<string> ExecuteToolCall(ToolCall toolCall)
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
                    var readResult = await _queryService!.ProcessQueryAsync($"read file {filename}");
                    result = readResult is { Success: true } ? readResult.Message : $"Error reading file: {readResult?.Message ?? "Unknown error"}";
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
                    
                case "get_system_info":
                    var sysInfo = $"System Information:\n" +
                                 $"- Tool calling: Available\n" +
                                 $"- Project query service: {(_queryService != null ? "Available" : "Not available")}\n" +
                                 $"- Current time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                 $"- Available tools: {(_queryService != null ? "read_file, list_files, search_files, get_project_structure, get_system_info" : "get_system_info only")}";
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
        
        // Log the tool execution
        _ = Task.Run(async () =>
        {
            try
            {
                if (_conversationLogger != null)
                {
                    var toolExecution = new
                    {
                        tool_call = new
                        {
                            id = toolCall.Id,
                            type = toolCall.Type,
                            function = new
                            {
                                name = functionName,
                                arguments = arguments
                            }
                        },
                        result = result
                    };
                    
                    var toolJson = JsonSerializer.Serialize(toolExecution, new JsonSerializerOptions { WriteIndented = true });
                    await _conversationLogger.LogConversationAsync("Tool", toolJson);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to log tool execution: {ex.Message}");
            }
        });
        
        return result;
    }
    
    private async Task HandleProjectQueryAsync(string userMessage)
    {
        if (_queryService == null) return;
        
        // Show "processing" indicator
        var processingMessage = new ChatMessage
        {
            Sender = "System",
            Message = "🔍 Processing project query...",
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            SenderColor = new SolidColorBrush(Color.FromRgb(0, 120, 204)),
            BackgroundColor = new SolidColorBrush(Color.FromRgb(45, 45, 48))
        };
        _messages.Add(processingMessage);
        ScrollToBottom();
        
        try
        {
            // Process user query directly
            var result = await _queryService.ProcessQueryAsync(userMessage);
            
            // Remove processing message
            _messages.Remove(processingMessage);
            
            // Add query result
            var resultMessage = new ChatMessage
            {
                Sender = result.Success ? "Query Result" : "Query Error",
                Message = result.Message,
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                SenderColor = new SolidColorBrush(result.Success ? Color.FromRgb(0, 255, 0) : Color.FromRgb(255, 0, 0)),
                BackgroundColor = new SolidColorBrush(Color.FromRgb(25, 25, 35))
            };
            _messages.Add(resultMessage);
            
            // If successful and we have an LLM connection, also send the result to the LLM for analysis
            if (result.Success && _isConnected && _ollamaClient != null)
            {
                var contextMessage = $"User asked: {userMessage}\n\nProject query result:\n{result.Message}\n\nPlease analyze this information and provide a helpful response.";
                
                
                // Add context to conversation history
                _conversationHistory.Add(new wcode.ChatMessage { Role = "user", Content = contextMessage });
                
                // Get LLM response
                await GetLLMResponseAsync(contextMessage);
            }
            
            // Log the query
            if (_conversationLogger != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _conversationLogger.LogConversationAsync("Query", userMessage);
                        await _conversationLogger.LogConversationAsync("QueryResult", result.Message);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to log query: {ex.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _messages.Remove(processingMessage);
            AddSystemMessage($"❌ Query failed: {ex.Message}");
        }
        
        ScrollToBottom();
    }
    
    private async Task GetLLMResponseAsync(string contextMessage)
    {
        if (_ollamaClient == null) return;
        
        var startTime = DateTime.Now;
        
        var responseMessage = new ChatMessage
        {
            Sender = "LLM",
            Message = "",
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            SenderColor = new SolidColorBrush(Color.FromRgb(255, 165, 0)),
            BackgroundColor = new SolidColorBrush(Color.FromRgb(25, 25, 35))
        };
        _messages.Add(responseMessage);
        
        try
        {
            var fullResponse = "";
            var hasReceivedData = false;
            
            await foreach (var chunk in _ollamaClient.ChatStreamAsync(_currentModel, contextMessage, _conversationHistory.Take(_conversationHistory.Count - 1).ToList()))
            {
                hasReceivedData = true;
                fullResponse += chunk;
                
                await Dispatcher.InvokeAsync(() =>
                {
                    responseMessage.Message = fullResponse;
                    ScrollToBottom();
                }, System.Windows.Threading.DispatcherPriority.Normal);
                
                await Task.Delay(50);
            }
            
            if (!hasReceivedData)
            {
                var regularResponse = await _ollamaClient.ChatAsync(_currentModel, contextMessage, _conversationHistory.Take(_conversationHistory.Count - 1).ToList());
                await Dispatcher.InvokeAsync(() =>
                {
                    responseMessage.Message = regularResponse;
                    ScrollToBottom();
                });
                fullResponse = regularResponse;
            }
            
            if (!string.IsNullOrEmpty(fullResponse))
            {
                _conversationHistory.Add(new wcode.ChatMessage { Role = "assistant", Content = fullResponse });
                
                // Log LLM response
                var responseTime = (int)(DateTime.Now - startTime).TotalMilliseconds;
                if (_conversationLogger != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _conversationLogger.LogConversationAsync("LLM", fullResponse, _currentModel, 0, responseTime);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to log LLM response: {ex.Message}");
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _messages.Remove(responseMessage);
            AddSystemMessage($"❌ Error getting LLM response: {ex.Message}");
        }
    }
}