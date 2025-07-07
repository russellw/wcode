# wcode - LLM-Assisted Development Tools

A suite of tools for LLM-assisted software development with Ollama integration.

## Project Structure

This solution contains three projects:

### wcode.Lib - Core Library
- `OllamaClient` - HTTP client for Ollama API with tool calling support
- `ProjectQueryService` - File and project analysis capabilities
- `ConversationLogger` - CSV-based conversation logging
- `ProjectConfig` - Project configuration management

### wcode.Cli - Command Line Interface
A headless command-line tool for batch processing LLM instructions.

**Usage:**
```bash
dotnet run --project wcode.Cli -- --batch instructions.txt
dotnet run --project wcode.Cli -- --batch=batch.txt --project-dir=/path/to/project
```

**Features:**
- Batch processing of multiple prompts
- Full tool calling support (read_file, list_files, search_files, get_project_structure)
- Automatic Ollama server connection and model selection
- Progress reporting and error handling

### wcode.Wpf - WPF GUI Application
A Windows desktop application providing an interactive interface.

**Features:**
- LLM chat interface with tool calling
- Project file browser and editor
- Real-time conversation logging
- Project configuration management

**Usage:**
```bash
dotnet run --project wcode.Wpf -- /path/to/project
```

## Dependencies

- .NET 9.0
- Ollama server running at 192.168.0.63:11434
- AvalonEdit (WPF project only)

## Development

To build all projects:
```bash
dotnet build  # Note: WPF project requires Windows
```

To build individual projects:
```bash
dotnet build wcode.Lib
dotnet build wcode.Cli
dotnet build wcode.Wpf  # Windows only
```

## Tool Calling Support

Both CLI and WPF applications support the following tools:
- `read_file` - Read file contents
- `list_files` - List project files
- `search_files` - Search content in files
- `get_project_structure` - Get directory structure
- `get_system_info` - System information
