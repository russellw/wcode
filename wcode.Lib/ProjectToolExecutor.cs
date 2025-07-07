using System.Text.Json;

namespace wcode.Lib;

public class ProjectToolExecutor
{
    private readonly ProjectQueryService? _queryService;
    
    public ProjectToolExecutor(ProjectQueryService? queryService = null)
    {
        _queryService = queryService;
    }
    
    public async Task<string> ExecuteToolCallAsync(ToolCall toolCall)
    {
        var functionName = toolCall.Function.Name;
        var arguments = toolCall.Function.Arguments;
        string result;
        
        if (_queryService == null && functionName != "get_system_info")
        {
            result = "Project query service not available";
        }
        else
        {
            try
            {
                switch (functionName)
                {
                    case "read_file":
                        var filename = arguments.GetProperty("filename").GetString();
                        var readResult = await _queryService!.ProcessQueryAsync($"read file {filename}");
                        result = readResult is { Success: true } ? readResult.Message : $"Error reading file: {readResult?.Message ?? "Unknown error"}";
                        break;
                        
                    case "write_file":
                        var writeFilename = arguments.GetProperty("filename").GetString();
                        var content = arguments.GetProperty("content").GetString();
                        var writeResult = await _queryService!.ProcessQueryAsync($"write file {writeFilename} content: {content}");
                        result = writeResult is { Success: true } ? writeResult.Message : $"Error writing file: {writeResult?.Message ?? "Unknown error"}";
                        break;
                        
                    case "list_files":
                        var listResult = await _queryService!.ProcessQueryAsync("list files");
                        result = listResult is { Success: true } ? listResult.Message : $"Error listing files: {listResult?.Message ?? "Unknown error"}";
                        break;
                        
                    case "search_files":
                        var query = arguments.GetProperty("query").GetString();
                        var searchResult = await _queryService!.ProcessQueryAsync($"search for {query}");
                        result = searchResult is { Success: true } ? searchResult.Message : $"Error searching files: {searchResult?.Message ?? "Unknown error"}";
                        break;
                        
                    case "get_project_structure":
                        var structureResult = await _queryService!.ProcessQueryAsync("project structure");
                        result = structureResult is { Success: true } ? structureResult.Message : $"Error getting project structure: {structureResult?.Message ?? "Unknown error"}";
                        break;
                        
                    case "get_system_info":
                        var sysInfo = $"System Information:\n" +
                                     $"- Tool calling: Available\n" +
                                     $"- Project query service: {(_queryService != null ? "Available" : "Not available")}\n" +
                                     $"- Current time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                     $"- Available tools: {(_queryService != null ? "read_file, write_file, list_files, search_files, get_project_structure, get_system_info" : "get_system_info only")}";
                        result = sysInfo;
                        break;
                        
                    default:
                        result = $"Unknown tool function: {functionName}";
                        break;
                }
            }
            catch (Exception ex)
            {
                result = $"Error executing tool call: {ex.Message}";
            }
        }
        
        return result;
    }
}