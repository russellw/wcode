using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace wcode;

public class ConversationLogger
{
    private readonly string _projectPath;
    private readonly string _markdownLogPath;
    private readonly string _jsonLogPath;

    public ConversationLogger(string projectPath)
    {
        _projectPath = projectPath;
        _markdownLogPath = Path.Combine(projectPath, "llm_conversations.md");
        _jsonLogPath = Path.Combine(projectPath, "llm_conversations.json");
        
        InitializeLogs();
    }

    private void InitializeLogs()
    {
        // Initialize markdown log if it doesn't exist
        if (!File.Exists(_markdownLogPath))
        {
            File.WriteAllText(_markdownLogPath, "# LLM Conversations\n\n");
        }

        // Initialize JSON log if it doesn't exist
        if (!File.Exists(_jsonLogPath))
        {
            var emptyLog = new List<ConversationEntry>();
            var json = JsonSerializer.Serialize(emptyLog, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_jsonLogPath, json);
        }

    }


    public async Task LogConversationAsync(string sender, string message, string model = "", int tokensUsed = 0, int responseTimeMs = 0)
    {
        var timestamp = DateTime.Now;
        var sessionId = GetCurrentSessionId();
        
        // Log to markdown (casual reading)
        await LogToMarkdownAsync(sender, message, timestamp);
        
        // Log to JSON (structured but human-readable)
        await LogToJsonAsync(sender, message, timestamp, sessionId, model, tokensUsed, responseTimeMs);
    }

    private async Task LogToMarkdownAsync(string sender, string message, DateTime timestamp)
    {
        var markdownEntry = $"""
            ## {sender} - {timestamp:yyyy-MM-dd HH:mm:ss}

            {message}

            ---

            """;
        
        await File.AppendAllTextAsync(_markdownLogPath, markdownEntry);
    }

    private async Task LogToJsonAsync(string sender, string message, DateTime timestamp, string sessionId, string model, int tokensUsed, int responseTimeMs)
    {
        List<ConversationEntry> log;
        
        try
        {
            var existingJson = await File.ReadAllTextAsync(_jsonLogPath);
            log = JsonSerializer.Deserialize<List<ConversationEntry>>(existingJson) ?? new List<ConversationEntry>();
        }
        catch
        {
            log = new List<ConversationEntry>();
        }

        log.Add(new ConversationEntry
        {
            Timestamp = timestamp,
            SessionId = sessionId,
            Sender = sender,
            Message = message,
            Model = model,
            TokensUsed = tokensUsed,
            ResponseTimeMs = responseTimeMs
        });

        var updatedJson = JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_jsonLogPath, updatedJson);
    }


    private string GetCurrentSessionId()
    {
        // Generate a session ID based on the current date and a random component
        var dateComponent = DateTime.Now.ToString("yyyyMMdd");
        var randomComponent = Guid.NewGuid().ToString()[..8];
        return $"{dateComponent}-{randomComponent}";
    }

    public class ConversationEntry
    {
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
        
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = "";
        
        [JsonPropertyName("sender")]
        public string Sender { get; set; } = "";
        
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
        
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";
        
        [JsonPropertyName("tokensUsed")]
        public int TokensUsed { get; set; }
        
        [JsonPropertyName("responseTimeMs")]
        public int ResponseTimeMs { get; set; }
    }
}