# Ollama Tool Calling Demo (C#)

This demo shows how to use Ollama's tool calling functionality with C#. It demonstrates:

- Connecting to a remote Ollama server via LAN
- Defining tools with proper JSON schemas
- Persuading the model to use tools
- Handling tool calls in both structured format and content-based JSON
- Executing tool functions and displaying results

## Features

- **Calculator Tool**: Performs basic mathematical operations (add, subtract, multiply, divide)
- **Weather Tool**: Mock weather information retrieval
- **Robust Parsing**: Handles tool calls returned as JSON text in message content
- **Long Timeout**: 1-hour timeout for CPU inference

## Requirements

- .NET 8.0 or later
- Ollama server running on 192.168.0.63:11434
- Available models on the Ollama server

## Usage

1. Build the project:
   ```bash
   dotnet build
   ```

2. Run the demo:
   ```bash
   dotnet run
   ```

The demo will:
1. Connect to the Ollama server
2. List available models and select the first one
3. Ask the model to multiply 15 by 7 using the calculator tool
4. Ask the model for weather information about Paris using the weather tool
5. Display the results and tool call execution

## Example Output

```
🦙 Ollama Tool Calling Demo (C#)
==========================================
✅ Connected to Ollama server
📋 Using model: qwen2.5-coder:14b

📊 Example 1: Mathematical calculation
Asking: 'What is 15 multiplied by 7? Please use the calculator tool.'

🤖 Model response: {
  "name": "calculate",
  "arguments": {
    "operation": "multiply",
    "a": 15,
    "b": 7
  }
}

🔧 Tool calls detected:
   - Function: calculate
   - Arguments: {"operation":"multiply","a":15,"b":7}
   - Result: 105
```

## Architecture

- **OllamaClient.cs**: HTTP client for communicating with Ollama API
- **Program.cs**: Main demo application with tool definitions and execution
- **Tool Parsing**: Handles both structured tool calls and JSON content parsing
- **Error Handling**: Graceful handling of connection and parsing errors