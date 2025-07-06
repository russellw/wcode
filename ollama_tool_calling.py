#!/usr/bin/env python3
"""
Ollama Tool Calling Demo Script

This script demonstrates how to use Ollama with tool calling functionality
via LAN connection. It defines a simple calculator function and persuades 
the model to use it.
"""

import requests
import json
import time
from typing import Dict, Any, Optional, Union
from datetime import datetime
import uuid


def calculate(operation: str, a: float, b: float) -> float:
    """
    Perform basic mathematical operations
    
    Args:
        operation: The operation to perform ('add', 'subtract', 'multiply', 'divide')
        a: The first number
        b: The second number
    
    Returns:
        float: The result of the calculation
    """
    if operation == 'add':
        return a + b
    elif operation == 'subtract':
        return a - b
    elif operation == 'multiply':
        return a * b
    elif operation == 'divide':
        if b == 0:
            raise ValueError("Cannot divide by zero")
        return a / b
    else:
        raise ValueError(f"Unknown operation: {operation}")


def get_weather(location: str) -> str:
    """
    Get weather information for a location (mock function)
    
    Args:
        location: The location to get weather for
    
    Returns:
        str: Weather information
    """
    return f"The weather in {location} is sunny with a temperature of 22°C"


class ConversationLogger:
    def __init__(self, log_file: str = "ollama_tool_calling_log.json"):
        self.log_file = log_file
        self.session_id = self._generate_session_id()
        self.conversation_log = []
    
    def _generate_session_id(self) -> str:
        """Generate a unique session ID"""
        date_component = datetime.now().strftime("%Y%m%d")
        random_component = str(uuid.uuid4())[:8]
        return f"{date_component}-{random_component}"
    
    def log_message(self, sender: str, message: str, model: str = "", tokens_used: int = 0, response_time_ms: int = 0):
        """Log a conversation message"""
        entry = {
            "timestamp": datetime.now().isoformat(),
            "sessionId": self.session_id,
            "sender": sender,
            "message": message,
            "model": model,
            "tokensUsed": tokens_used,
            "responseTimeMs": response_time_ms
        }
        
        self.conversation_log.append(entry)
        
        # Write to file (overwrite mode)
        try:
            with open(self.log_file, 'w', encoding='utf-8') as f:
                json.dump(self.conversation_log, f, indent=2, ensure_ascii=False)
        except Exception as e:
            print(f"Warning: Failed to write to log file: {e}")
    
    def log_tool_call(self, tool_name: str, arguments: dict, result: Any):
        """Log a tool call and its result"""
        tool_message = f"Tool Call: {tool_name}\nArguments: {json.dumps(arguments, indent=2)}\nResult: {result}"
        self.log_message("Tool", tool_message)


class OllamaClient:
    def __init__(self, host: str = "192.168.0.63", port: int = 11434, logger: Optional[ConversationLogger] = None):
        self.base_url = f"http://{host}:{port}"
        self.session = requests.Session()
        self.session.timeout = 3600  # 1 hour for CPU inference
        self.logger = logger
    
    def test_connection(self) -> bool:
        """Test if the Ollama server is reachable."""
        try:
            response = self.session.get(f"{self.base_url}/api/version")
            return response.status_code == 200
        except requests.exceptions.RequestException:
            return False
    
    def list_models(self) -> Optional[Dict[str, Any]]:
        """Get list of available models."""
        try:
            response = self.session.get(f"{self.base_url}/api/tags")
            if response.status_code == 200:
                return response.json()
            return None
        except requests.exceptions.RequestException:
            return None
    
    def chat_with_tools(self, model: str, message: str, tools: list) -> Optional[Dict[str, Any]]:
        """Chat with tool calling support."""
        start_time = time.time()
        
        # Convert functions to tool definitions
        tool_definitions = []
        for tool in tools:
            tool_def = {
                "type": "function",
                "function": {
                    "name": tool.__name__,
                    "description": tool.__doc__ or f"Function: {tool.__name__}",
                    "parameters": {
                        "type": "object",
                        "properties": {},
                        "required": []
                    }
                }
            }
            
            # Try to extract parameters from function annotations
            if hasattr(tool, '__annotations__'):
                for param_name, param_type in tool.__annotations__.items():
                    if param_name != 'return':
                        tool_def["function"]["parameters"]["properties"][param_name] = {
                            "type": self._get_json_type(param_type),
                            "description": f"Parameter {param_name}"
                        }
                        tool_def["function"]["parameters"]["required"].append(param_name)
            
            tool_definitions.append(tool_def)
        
        payload = {
            "model": model,
            "messages": [{"role": "user", "content": message}],
            "tools": tool_definitions,
            "stream": False
        }
        
        # Log the complete user request including tools
        if self.logger:
            user_message_with_tools = {
                "message": message,
                "tools": tool_definitions
            }
            self.logger.log_message("User", json.dumps(user_message_with_tools, indent=2), model)
        
        try:
            response = self.session.post(
                f"{self.base_url}/api/chat",
                json=payload,
                headers={"Content-Type": "application/json"}
            )
            
            response_time_ms = int((time.time() - start_time) * 1000)
            
            if response.status_code == 200:
                result = response.json()
                
                # Log the complete assistant response
                if self.logger:
                    # Log the full response structure, not just content
                    response_data = {
                        "message": result.get('message', {}),
                        "done": result.get('done', False),
                        "total_duration": result.get('total_duration'),
                        "load_duration": result.get('load_duration'),
                        "prompt_eval_count": result.get('prompt_eval_count'),
                        "prompt_eval_duration": result.get('prompt_eval_duration'),
                        "eval_count": result.get('eval_count'),
                        "eval_duration": result.get('eval_duration')
                    }
                    # Remove None values
                    response_data = {k: v for k, v in response_data.items() if v is not None}
                    
                    self.logger.log_message("Assistant", json.dumps(response_data, indent=2), model, 
                                          result.get('prompt_eval_count', 0) + result.get('eval_count', 0), 
                                          response_time_ms)
                
                return result
            else:
                error_msg = f"❌ Chat failed with status {response.status_code}: {response.text}"
                print(error_msg)
                if self.logger:
                    self.logger.log_message("System", error_msg, model, 0, response_time_ms)
                return None
                
        except requests.exceptions.RequestException as e:
            response_time_ms = int((time.time() - start_time) * 1000)
            error_msg = f"❌ Chat request failed: {e}"
            print(error_msg)
            if self.logger:
                self.logger.log_message("System", error_msg, model, 0, response_time_ms)
            return None
    
    def _get_json_type(self, python_type) -> str:
        """Convert Python type to JSON schema type."""
        if python_type == str:
            return "string"
        elif python_type == int:
            return "integer"
        elif python_type == float:
            return "number"
        elif python_type == bool:
            return "boolean"
        else:
            return "string"


def main():
    """Main function to demonstrate tool calling with Ollama via LAN"""
    
    # Define available functions
    available_functions = {
        'calculate': calculate,
        'get_weather': get_weather,
    }
    
    print("🦙 Ollama Tool Calling Demo (LAN)")
    print("=" * 40)
    
    # Initialize logger
    logger = ConversationLogger("ollama_tool_calling_log.json")
    print(f"📝 Logging to: {logger.log_file}")
    print(f"🔧 Session ID: {logger.session_id}")
    
    # Initialize client with logger
    client = OllamaClient(logger=logger)
    
    # Test connection
    if not client.test_connection():
        print("❌ Cannot connect to Ollama server at 192.168.0.63:11434")
        return
    
    print("✅ Connected to Ollama server")
    
    # Get available models
    models_data = client.list_models()
    if not models_data or not models_data.get('models'):
        print("❌ No models available")
        return
    
    model_name = models_data['models'][0]['name']
    print(f"📋 Using model: {model_name}")
    
    # Example 1: Persuade the model to use the calculator
    print("\n📊 Example 1: Mathematical calculation")
    print("Asking: 'What is 15 multiplied by 7? Please use the calculator tool.'")
    
    response = client.chat_with_tools(
        model=model_name,
        message="What is 15 multiplied by 7? Please use the calculator tool to get the exact result.",
        tools=[calculate, get_weather]
    )
    
    if response:
        print(f"\n🤖 Model response: {response.get('message', {}).get('content', 'No response')}")
        
        # Check for tool calls in structured format or content
        message = response.get('message', {})
        tool_calls_found = False
        
        # First check for structured tool calls
        if 'tool_calls' in message and message['tool_calls']:
            print("\n🔧 Tool calls detected (structured):")
            for tool_call in message['tool_calls']:
                func_name = tool_call.get('function', {}).get('name')
                func_args = tool_call.get('function', {}).get('arguments', {})
                
                print(f"   - Function: {func_name}")
                print(f"   - Arguments: {func_args}")
                
                function_to_call = available_functions.get(func_name)
                if function_to_call:
                    try:
                        result = function_to_call(**func_args)
                        print(f"   - Result: {result}")
                        # Log the tool call
                        logger.log_tool_call(func_name, func_args, result)
                    except Exception as e:
                        error_msg = f"Error: {e}"
                        print(f"   - {error_msg}")
                        logger.log_tool_call(func_name, func_args, error_msg)
                else:
                    error_msg = f"Function not found: {func_name}"
                    print(f"   - {error_msg}")
                    logger.log_tool_call(func_name, func_args, error_msg)
                tool_calls_found = True
        
        # Also check if tool call is in content as JSON
        content = message.get('content', '')
        if content and not tool_calls_found:
            try:
                # Try to parse content as JSON tool call
                tool_call_data = json.loads(content)
                if 'name' in tool_call_data and 'arguments' in tool_call_data:
                    print("\n🔧 Tool call detected (in content):")
                    func_name = tool_call_data['name']
                    func_args = tool_call_data['arguments']
                    
                    print(f"   - Function: {func_name}")
                    print(f"   - Arguments: {func_args}")
                    
                    function_to_call = available_functions.get(func_name)
                    if function_to_call:
                        try:
                            result = function_to_call(**func_args)
                            print(f"   - Result: {result}")
                        except Exception as e:
                            print(f"   - Error: {e}")
                    else:
                        print(f"   - Function not found: {func_name}")
                    tool_calls_found = True
            except json.JSONDecodeError:
                pass
        
        if not tool_calls_found:
            print("\n⚠️  No tool calls were made by the model")
            print("    The model may not support tool calling or needs different prompting")
    else:
        print("❌ No response received")
    
    # Example 2: Weather query
    print("\n" + "=" * 40)
    print("🌤️  Example 2: Weather query")
    print("Asking: 'What's the weather like in Paris? Use the weather tool.'")
    
    response = client.chat_with_tools(
        model=model_name,
        message="What's the weather like in Paris? Please use the weather tool to get current information.",
        tools=[calculate, get_weather]
    )
    
    if response:
        print(f"\n🤖 Model response: {response.get('message', {}).get('content', 'No response')}")
        
        message = response.get('message', {})
        tool_calls_found = False
        
        # First check for structured tool calls
        if 'tool_calls' in message and message['tool_calls']:
            print("\n🔧 Tool calls detected (structured):")
            for tool_call in message['tool_calls']:
                func_name = tool_call.get('function', {}).get('name')
                func_args = tool_call.get('function', {}).get('arguments', {})
                
                print(f"   - Function: {func_name}")
                print(f"   - Arguments: {func_args}")
                
                function_to_call = available_functions.get(func_name)
                if function_to_call:
                    try:
                        result = function_to_call(**func_args)
                        print(f"   - Result: {result}")
                        # Log the tool call
                        logger.log_tool_call(func_name, func_args, result)
                    except Exception as e:
                        error_msg = f"Error: {e}"
                        print(f"   - {error_msg}")
                        logger.log_tool_call(func_name, func_args, error_msg)
                else:
                    error_msg = f"Function not found: {func_name}"
                    print(f"   - {error_msg}")
                    logger.log_tool_call(func_name, func_args, error_msg)
                tool_calls_found = True
        
        # Also check if tool call is in content as JSON
        content = message.get('content', '')
        if content and not tool_calls_found:
            try:
                # Try to parse content as JSON tool call
                tool_call_data = json.loads(content)
                if 'name' in tool_call_data and 'arguments' in tool_call_data:
                    print("\n🔧 Tool call detected (in content):")
                    func_name = tool_call_data['name']
                    func_args = tool_call_data['arguments']
                    
                    print(f"   - Function: {func_name}")
                    print(f"   - Arguments: {func_args}")
                    
                    function_to_call = available_functions.get(func_name)
                    if function_to_call:
                        try:
                            result = function_to_call(**func_args)
                            print(f"   - Result: {result}")
                        except Exception as e:
                            print(f"   - Error: {e}")
                    else:
                        print(f"   - Function not found: {func_name}")
                    tool_calls_found = True
            except json.JSONDecodeError:
                pass
        
        if not tool_calls_found:
            print("\n⚠️  No tool calls were made by the model")
    else:
        print("❌ No response received")
    
    print("\n" + "=" * 40)
    print("✅ Demo completed!")


if __name__ == "__main__":
    main()