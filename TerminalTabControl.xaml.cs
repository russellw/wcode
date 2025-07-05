using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.IO;

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

        // Log user message
        _ = Task.Run(async () =>
        {
            try
            {
                if (_conversationLogger != null)
                {
                    await _conversationLogger.LogConversationAsync("User", message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to log user message: {ex.Message}");
            }
        });

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
        
        // Check if this is a project query first
        if (_queryService != null && IsProjectQuery(userMessage))
        {
            await HandleProjectQueryAsync(userMessage);
            return;
        }
        
        // Add user message to conversation history
        _conversationHistory.Add(new wcode.ChatMessage { Role = "user", Content = userMessage });
        
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
            
            // Stream the response
            var fullResponse = "";
            var hasReceivedData = false;
            
            await foreach (var chunk in _ollamaClient.ChatStreamAsync(_currentModel, userMessage, _conversationHistory.Take(_conversationHistory.Count - 1).ToList()))
            {
                hasReceivedData = true;
                fullResponse += chunk;
                
                // Update on UI thread
                await Dispatcher.InvokeAsync(() =>
                {
                    responseMessage.Message = fullResponse;
                    ScrollToBottom();
                }, System.Windows.Threading.DispatcherPriority.Normal);
                
                // Small delay to make streaming visible
                await Task.Delay(50);
            }
            
            // If no streaming data was received, try regular chat
            if (!hasReceivedData)
            {
                var regularResponse = await _ollamaClient.ChatAsync(_currentModel, userMessage, _conversationHistory.Take(_conversationHistory.Count - 1).ToList());
                await Dispatcher.InvokeAsync(() =>
                {
                    responseMessage.Message = regularResponse;
                    ScrollToBottom();
                });
                fullResponse = regularResponse;
            }
            
            // Add assistant response to conversation history
            if (!string.IsNullOrEmpty(fullResponse))
            {
                _conversationHistory.Add(new wcode.ChatMessage { Role = "assistant", Content = fullResponse });
                
                // Log LLM response
                var responseTime = (int)(DateTime.Now - startTime).TotalMilliseconds;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (_conversationLogger != null)
                        {
                            await _conversationLogger.LogConversationAsync("LLM", fullResponse, _currentModel, 0, responseTime);
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
            "find function", "where is function", "file info", "details about"
        };
        
        return queryKeywords.Any(keyword => lowerMessage.Contains(keyword));
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
                
                // Add system context about project capabilities if this is the first project query
                if (_conversationHistory.Count == 0 || !_conversationHistory.Any(m => m.Content.Contains("You have access to project files")))
                {
                    var systemContext = "You have access to project files and can help users with code analysis, file exploration, and project understanding. The system automatically processes queries about files, folders, and code structure. You can help users understand the results of these queries and provide insights about their codebase.";
                    _conversationHistory.Add(new wcode.ChatMessage { Role = "system", Content = systemContext });
                }
                
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