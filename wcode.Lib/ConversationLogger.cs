using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace wcode.Lib;

public class ConversationLogger
{
    private readonly string _projectPath;
    private readonly string _markdownLogPath;
    private readonly string _csvLogPath;

    public ConversationLogger(string projectPath)
    {
        _projectPath = projectPath;
        _markdownLogPath = Path.Combine(projectPath, "llm_conversations.md");
        _csvLogPath = Path.Combine(projectPath, "llm_conversations.csv");
        
        InitializeLogs();
    }

    private void InitializeLogs()
    {
        // Initialize markdown log if it doesn't exist
        if (!File.Exists(_markdownLogPath))
        {
            File.WriteAllText(_markdownLogPath, "# LLM Conversations\n\n");
        }

        // Initialize CSV log if it doesn't exist
        if (!File.Exists(_csvLogPath))
        {
            var csvHeader = "timestamp,sessionId,sender,message,model,tokensUsed,responseTimeMs\n";
            File.WriteAllText(_csvLogPath, csvHeader);
        }
    }


    public async Task LogConversationAsync(string sender, string message, string model = "", int tokensUsed = 0, int responseTimeMs = 0)
    {
        var timestamp = DateTime.Now;
        var sessionId = GetCurrentSessionId();
        
        // Log to markdown (casual reading)
        await LogToMarkdownAsync(sender, message, timestamp);
        
        // Log to CSV (efficient structured format)
        await LogToCsvAsync(sender, message, timestamp, sessionId, model, tokensUsed, responseTimeMs);
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

    private async Task LogToCsvAsync(string sender, string message, DateTime timestamp, string sessionId, string model, int tokensUsed, int responseTimeMs)
    {
        try
        {
            // Escape CSV special characters and newlines
            var escapedMessage = EscapeCsvValue(message);
            var escapedSender = EscapeCsvValue(sender);
            var escapedModel = EscapeCsvValue(model);
            var escapedSessionId = EscapeCsvValue(sessionId);
            
            var csvLine = $"{timestamp:yyyy-MM-ddTHH:mm:ss.fffK},{escapedSessionId},{escapedSender},{escapedMessage},{escapedModel},{tokensUsed},{responseTimeMs}\n";
            
            await File.AppendAllTextAsync(_csvLogPath, csvLine);
        }
        catch (Exception ex)
        {
            // Fallback logging if CSV fails
            System.Diagnostics.Debug.WriteLine($"CSV logging failed: {ex.Message}");
        }
    }
    
    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
            
        // If the value contains comma, newline, or quote, wrap in quotes and escape internal quotes
        if (value.Contains(',') || value.Contains('\n') || value.Contains('\r') || value.Contains('"'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        
        return value;
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