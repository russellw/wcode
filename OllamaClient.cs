using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace wcode;

public class OllamaClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private bool _disposed = false;

    public OllamaClient(string host = "192.168.0.63", int port = 11434)
    {
        _baseUrl = $"http://{host}:{port}";
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromHours(1) // Allow longer timeouts for CPU inference
        };
    }

    public async Task<bool> IsServerAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/version");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<OllamaModel>> GetModelsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
            if (!response.IsSuccessStatusCode) return new List<OllamaModel>();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ModelsResponse>(json);
            return result?.Models ?? new List<OllamaModel>();
        }
        catch
        {
            return new List<OllamaModel>();
        }
    }

    public async Task<string> ChatAsync(string model, string message, List<ChatMessage>? previousMessages = null, List<Tool>? tools = null)
    {
        try
        {
            var messages = new List<ChatMessage>();
            
            // Add previous messages if provided
            if (previousMessages != null)
            {
                messages.AddRange(previousMessages);
            }
            
            // Add current user message
            messages.Add(new ChatMessage { Role = "user", Content = message });

            var request = new ChatRequest
            {
                Model = model,
                Messages = messages,
                Stream = false,
                Tools = tools
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/chat", content);
            
            if (!response.IsSuccessStatusCode)
            {
                return $"Error: Server returned {response.StatusCode}";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseJson);
            
            return chatResponse?.Message?.Content ?? "No response received";
        }
        catch (HttpRequestException ex)
        {
            return $"Network error: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            return "Request timed out. The model might be too slow or the server is overloaded.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<ChatResponse?> ChatWithToolsAsync(string model, string message, List<ChatMessage>? previousMessages = null, List<Tool>? tools = null)
    {
        try
        {
            var messages = new List<ChatMessage>();
            
            // Add previous messages if provided
            if (previousMessages != null)
            {
                messages.AddRange(previousMessages);
            }
            
            // Add current user message
            messages.Add(new ChatMessage { Role = "user", Content = message });

            var request = new ChatRequest
            {
                Model = model,
                Messages = messages,
                Stream = false,
                Tools = tools
            };

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
            
            // Debug: Log the actual JSON being sent to Ollama
            System.Diagnostics.Debug.WriteLine($"Sending to Ollama: {json}");
            
            // Also write to debug file for analysis
            try
            {
                await File.WriteAllTextAsync("ollama_debug_request.json", json);
            }
            catch
            {
                // Ignore file write errors
            }
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/chat", content);
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseJson);
            
            // Check if tool calls are embedded in content as JSON text
            if (chatResponse?.Message != null && 
                (chatResponse.Message.ToolCalls == null || chatResponse.Message.ToolCalls.Count == 0) &&
                !string.IsNullOrEmpty(chatResponse.Message.Content))
            {
                try
                {
                    // Try to parse content as a tool call JSON
                    var toolCallData = JsonSerializer.Deserialize<JsonElement>(chatResponse.Message.Content);
                    if (toolCallData.TryGetProperty("name", out var nameElement) && 
                        toolCallData.TryGetProperty("arguments", out var argsElement))
                    {
                        // Create a tool call from the content
                        var toolCall = new ToolCall
                        {
                            Id = Guid.NewGuid().ToString(),
                            Type = "function",
                            Function = new ToolCallFunction
                            {
                                Name = nameElement.GetString() ?? string.Empty,
                                Arguments = argsElement
                            }
                        };
                        
                        chatResponse.Message.ToolCalls = new List<ToolCall> { toolCall };
                    }
                }
                catch (JsonException)
                {
                    // Content is not JSON, leave as is
                }
            }
            
            return chatResponse;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(string model, string message, List<ChatMessage>? previousMessages = null, List<Tool>? tools = null)
    {
        var messages = new List<ChatMessage>();
        
        // Add previous messages if provided
        if (previousMessages != null)
        {
            messages.AddRange(previousMessages);
        }
        
        // Add current user message
        messages.Add(new ChatMessage { Role = "user", Content = message });

        var request = new ChatRequest
        {
            Model = model,
            Messages = messages,
            Stream = true,
            Tools = tools
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage? response = null;
        Stream? stream = null;
        StreamReader? reader = null;
        
        try
        {
            response = await _httpClient.PostAsync($"{_baseUrl}/api/chat", content);
            
            if (!response.IsSuccessStatusCode)
            {
                yield return $"Error: Server returned {response.StatusCode}";
                yield break;
            }

            stream = await response.Content.ReadAsStreamAsync();
            reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                ChatStreamResponse? streamResponse = null;
                try
                {
                    streamResponse = JsonSerializer.Deserialize<ChatStreamResponse>(line);
                }
                catch (JsonException)
                {
                    // Skip malformed JSON lines
                    continue;
                }

                if (streamResponse?.Message?.Content != null)
                {
                    yield return streamResponse.Message.Content;
                }
                
                if (streamResponse?.Done == true)
                {
                    break;
                }
            }
        }
        finally
        {
            reader?.Dispose();
            stream?.Dispose();
            response?.Dispose();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

// Data models for Ollama API
public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    
    [JsonPropertyName("tool_calls")]
    public List<ToolCall>? ToolCalls { get; set; }
}

public class ChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();
    
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
    
    [JsonPropertyName("tools")]
    public List<Tool>? Tools { get; set; }
}

public class ChatResponse
{
    [JsonPropertyName("message")]
    public ChatMessage? Message { get; set; }
    
    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

public class ChatStreamResponse
{
    [JsonPropertyName("message")]
    public ChatMessage? Message { get; set; }
    
    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

public class OllamaModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("size")]
    public long Size { get; set; }
    
    [JsonPropertyName("modified_at")]
    public string ModifiedAt { get; set; } = string.Empty;
}

public class ModelsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModel> Models { get; set; } = new();
}

public class Tool
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";
    
    [JsonPropertyName("function")]
    public ToolFunction Function { get; set; } = new();
}

public class ToolFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("parameters")]
    public JsonElement Parameters { get; set; }
}

public class ToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";
    
    [JsonPropertyName("function")]
    public ToolCallFunction Function { get; set; } = new();
}

public class ToolCallFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("arguments")]
    public JsonElement Arguments { get; set; }
}