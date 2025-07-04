using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Data.SQLite;

namespace wcode;

public class ConversationLogger
{
    private readonly string _projectPath;
    private readonly string _markdownLogPath;
    private readonly string _jsonLogPath;
    private readonly string _sqliteLogPath;

    public ConversationLogger(string projectPath)
    {
        _projectPath = projectPath;
        _markdownLogPath = Path.Combine(projectPath, "llm_conversations.md");
        _jsonLogPath = Path.Combine(projectPath, "llm_conversations.json");
        _sqliteLogPath = Path.Combine(projectPath, "llm_conversations.db");
        
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
            var emptyLog = new ConversationLog { Conversations = new List<ConversationEntry>() };
            var json = JsonSerializer.Serialize(emptyLog, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_jsonLogPath, json);
        }

        // Initialize SQLite database if it doesn't exist
        if (!File.Exists(_sqliteLogPath))
        {
            InitializeSQLiteDatabase();
        }
    }

    private void InitializeSQLiteDatabase()
    {
        using var connection = new SQLiteConnection($"Data Source={_sqliteLogPath}");
        connection.Open();
        
        var createTableCommand = @"
            CREATE TABLE IF NOT EXISTS conversations (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                session_id TEXT NOT NULL,
                sender TEXT NOT NULL,
                message TEXT NOT NULL,
                model TEXT,
                tokens_used INTEGER,
                response_time_ms INTEGER
            );
            
            CREATE INDEX IF NOT EXISTS idx_timestamp ON conversations(timestamp);
            CREATE INDEX IF NOT EXISTS idx_session ON conversations(session_id);
            CREATE INDEX IF NOT EXISTS idx_sender ON conversations(sender);
        ";
        
        using var command = new SQLiteCommand(createTableCommand, connection);
        command.ExecuteNonQuery();
    }

    public async Task LogConversationAsync(string sender, string message, string model = "", int tokensUsed = 0, int responseTimeMs = 0)
    {
        var timestamp = DateTime.Now;
        var sessionId = GetCurrentSessionId();
        
        // Log to markdown (casual reading)
        await LogToMarkdownAsync(sender, message, timestamp);
        
        // Log to JSON (structured but human-readable)
        await LogToJsonAsync(sender, message, timestamp, sessionId, model, tokensUsed, responseTimeMs);
        
        // Log to SQLite (programmatic access)
        await LogToSQLiteAsync(sender, message, timestamp, sessionId, model, tokensUsed, responseTimeMs);
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
        ConversationLog log;
        
        try
        {
            var existingJson = await File.ReadAllTextAsync(_jsonLogPath);
            log = JsonSerializer.Deserialize<ConversationLog>(existingJson) ?? new ConversationLog();
        }
        catch
        {
            log = new ConversationLog();
        }

        log.Conversations.Add(new ConversationEntry
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

    private async Task LogToSQLiteAsync(string sender, string message, DateTime timestamp, string sessionId, string model, int tokensUsed, int responseTimeMs)
    {
        using var connection = new SQLiteConnection($"Data Source={_sqliteLogPath}");
        await connection.OpenAsync();
        
        var insertCommand = @"
            INSERT INTO conversations (timestamp, session_id, sender, message, model, tokens_used, response_time_ms)
            VALUES (@timestamp, @sessionId, @sender, @message, @model, @tokensUsed, @responseTimeMs)
        ";
        
        using var command = new SQLiteCommand(insertCommand, connection);
        command.Parameters.AddWithValue("@timestamp", timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        command.Parameters.AddWithValue("@sessionId", sessionId);
        command.Parameters.AddWithValue("@sender", sender);
        command.Parameters.AddWithValue("@message", message);
        command.Parameters.AddWithValue("@model", model ?? "");
        command.Parameters.AddWithValue("@tokensUsed", tokensUsed);
        command.Parameters.AddWithValue("@responseTimeMs", responseTimeMs);
        
        await command.ExecuteNonQueryAsync();
    }

    private string GetCurrentSessionId()
    {
        // Generate a session ID based on the current date and a random component
        var dateComponent = DateTime.Now.ToString("yyyyMMdd");
        var randomComponent = Guid.NewGuid().ToString()[..8];
        return $"{dateComponent}-{randomComponent}";
    }

    public class ConversationLog
    {
        [JsonPropertyName("conversations")]
        public List<ConversationEntry> Conversations { get; set; } = new();
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