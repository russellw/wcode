using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace wcode;

public partial class TerminalTabControl : UserControl
{
    public class ChatMessage
    {
        public string Sender { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public Brush SenderColor { get; set; } = Brushes.White;
        public Brush BackgroundColor { get; set; } = Brushes.Transparent;
    }

    private ObservableCollection<ChatMessage> _messages = new();

    public TerminalTabControl()
    {
        InitializeComponent();
        ChatMessagesControl.ItemsSource = _messages;
        
        AddWelcomeMessage();
    }

    private void AddWelcomeMessage()
    {
        _messages.Add(new ChatMessage
        {
            Sender = "System",
            Message = "Welcome to wcode LLM Chat Terminal!\n\nThis is a placeholder for LLM integration. You can type messages below and they will appear in the chat.\n\nTo integrate with an actual LLM:\n1. Add HTTP client for API calls\n2. Implement authentication\n3. Add streaming response handling\n4. Add code syntax highlighting for responses",
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            SenderColor = new SolidColorBrush(Color.FromRgb(0, 120, 204)),
            BackgroundColor = new SolidColorBrush(Color.FromRgb(45, 45, 48))
        });
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

        MessageInput.Clear();
        
        ScrollToBottom();
        
        SimulateLLMResponse(message);
    }

    private void SimulateLLMResponse(string userMessage)
    {
        Task.Delay(1000).ContinueWith(_ =>
        {
            Dispatcher.Invoke(() =>
            {
                var response = GenerateSimulatedResponse(userMessage);
                
                _messages.Add(new ChatMessage
                {
                    Sender = "LLM",
                    Message = response,
                    Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                    SenderColor = new SolidColorBrush(Color.FromRgb(255, 165, 0)),
                    BackgroundColor = new SolidColorBrush(Color.FromRgb(25, 25, 35))
                });
                
                ScrollToBottom();
            });
        });
    }

    private string GenerateSimulatedResponse(string userMessage)
    {
        var responses = new[]
        {
            "I understand you're asking about: " + userMessage + "\n\nThis is a simulated response. To get real LLM responses, you would need to integrate with an API like OpenAI, Anthropic, or run a local model.",
            "That's an interesting question about: " + userMessage + "\n\nIn a real implementation, this would connect to an LLM service and provide detailed, contextual responses.",
            "Regarding your message: " + userMessage + "\n\nThis terminal is ready for LLM integration. You could add features like:\n- Code analysis\n- Documentation generation\n- Debugging assistance\n- Architecture suggestions"
        };

        return responses[new Random().Next(responses.Length)];
    }

    private void ScrollToBottom()
    {
        ChatScrollViewer.ScrollToBottom();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _messages.Clear();
        AddWelcomeMessage();
    }
}