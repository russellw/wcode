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
            Timeout = TimeSpan.FromMinutes(5) // Allow longer timeouts for LLM responses
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

    public async Task<string> ChatAsync(string model, string message, List<ChatMessage>? previousMessages = null)
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
                Stream = false
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

    public async IAsyncEnumerable<string> ChatStreamAsync(string model, string message, List<ChatMessage>? previousMessages = null)
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
            Stream = true
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
}

public class ChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();
    
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
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