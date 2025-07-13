# wcode - LLM-Assisted Development Tools

A suite of tools for LLM-assisted software development with Ollama integration, featuring secure Docker-based code execution and comprehensive tool calling support.

## Project Structure

This solution contains three projects:

### wcode.Lib - Core Library
- `OllamaClient` - HTTP client for Ollama API with enhanced tool calling support
- `ProjectQueryService` - File and project analysis capabilities  
- `ProjectToolExecutor` - Secure Docker-based command execution
- `ConversationLogger` - CSV and Markdown conversation logging
- `ProjectConfig` - Project configuration management

### wcode.Cli - Command Line Interface
A headless command-line tool for batch processing LLM instructions with full development capabilities.

**Usage:**
```bash
dotnet run --project wcode.Cli instructions.txt project_directory
dotnet run --project wcode.Cli example/instructions.txt example
```

**Features:**
- Batch processing of multiple prompts from instruction files
- Secure Docker-based code execution in isolated containers
- Full development workflow support (write code → compile → run → debug)
- Enhanced tool calling with shell command support
- Real-time program output display
- Automatic conversation logging to markdown and CSV

### wcode.Wpf - WPF GUI Application  
A Windows desktop application providing an interactive development interface.

**Features:**
- Interactive LLM chat with full tool calling support
- Project file browser and integrated editor
- Real-time conversation logging and history
- Project configuration management
- Visual development workflow

**Usage:**
```bash
dotnet run --project wcode.Wpf -- /path/to/project
```

## Core Features

### Secure Code Execution
- **Docker Integration**: All code runs in isolated Docker containers
- **Multi-language Support**: Python, C/C++, JavaScript/Node.js, Java, Go, Rust, Ruby, PHP
- **Security Measures**: Network isolation, memory limits (256MB), CPU limits (0.5 cores)
- **Shell Command Support**: Complex workflows with `&&`, `;`, `|`, `>`, `<` operators

### Enhanced Tool Calling
- **File Operations**: Create, read, edit, and search files
- **Command Execution**: Compile and run programs with shell command support
- **Project Management**: Directory structure analysis and file listing
- **System Integration**: Real-time output capture and error handling

### LLM Integration
- **Ollama Compatibility**: Works with any Ollama-compatible model
- **Advanced Parsing**: Handles both structured tool calls and JSON-in-content responses
- **Multi-turn Conversations**: Supports complex development workflows
- **Error Recovery**: Robust handling of compilation errors and runtime issues

## Dependencies

- .NET 9.0
- Docker (for secure code execution)
- Ollama server (configurable endpoint)
- AvalonEdit (WPF project only)

## Development

### Building the Solution
```bash
# Build all projects (Linux/macOS - excludes WPF)
dotnet build wcode.Lib wcode.Cli

# Build individual projects  
dotnet build wcode.Lib
dotnet build wcode.Cli
dotnet build wcode.Wpf  # Windows only
```

### Running Tests
```bash
dotnet test wcode.Tests
```

### Docker Setup
Ensure Docker is installed and running for code execution features:
```bash
# Verify Docker installation
docker --version

# Pre-pull common development images (optional)
docker pull python:3.11-slim
docker pull gcc:latest  
docker pull node:18-alpine
```

## Tool Reference

### File Operations
- `read_file` - Read file contents with syntax highlighting
- `write_file` - Create or overwrite files with content
- `list_files` - List files in project directory
- `search_files` - Search for content across project files

### Code Execution  
- `run_command` - Execute shell commands and programs in secure Docker containers
  - Supports compilation: `gcc file.c -o program && ./program`
  - Handles complex workflows: `make && make test && ./app`
  - Shell operators: `echo "code" > file.c; gcc file.c`

### Project Management
- `get_project_structure` - Display directory tree structure
- `get_system_info` - System status and available tools

## Example Workflows

### Python Development
```
User: "Write a Python script to calculate fibonacci numbers and run it"
LLM: 
1. Uses write_file to create fibonacci.py
2. Uses run_command: "python fibonacci.py" 
3. Shows output: "1, 1, 2, 3, 5, 8, 13..."
```

### C Development  
```
User: "Create a C program to sort an array"
LLM:
1. Uses write_file to create sort.c
2. Uses run_command: "gcc sort.c -o sort && ./sort"
3. Shows compilation output and program results
```

### Multi-file Projects
```
User: "Create a web server with HTML and JavaScript"
LLM:
1. Uses write_file to create index.html
2. Uses write_file to create server.js  
3. Uses run_command: "node server.js"
4. Shows server startup messages
```

## Security Notes

- All code execution happens in isolated Docker containers
- Network access is disabled for security (`--network=none`)
- Resource limits prevent runaway processes
- File access is restricted to the project directory
- Containers are automatically removed after execution
