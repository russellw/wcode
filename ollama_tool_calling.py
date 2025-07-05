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


class OllamaClient:
    def __init__(self, host: str = "192.168.0.63", port: int = 11434):
        self.base_url = f"http://{host}:{port}"
        self.session = requests.Session()
        self.session.timeout = 30
    
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
        
        try:
            response = self.session.post(
                f"{self.base_url}/api/chat",
                json=payload,
                headers={"Content-Type": "application/json"}
            )
            
            if response.status_code == 200:
                return response.json()
            else:
                print(f"❌ Chat failed with status {response.status_code}")
                print(f"   Response: {response.text}")
                return None
                
        except requests.exceptions.RequestException as e:
            print(f"❌ Chat request failed: {e}")
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
    
    # Initialize client
    client = OllamaClient()
    
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
        
        # Check for tool calls
        message = response.get('message', {})
        if 'tool_calls' in message and message['tool_calls']:
            print("\n🔧 Tool calls detected:")
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
                    except Exception as e:
                        print(f"   - Error: {e}")
                else:
                    print(f"   - Function not found: {func_name}")
        else:
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
        if 'tool_calls' in message and message['tool_calls']:
            print("\n🔧 Tool calls detected:")
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
                    except Exception as e:
                        print(f"   - Error: {e}")
                else:
                    print(f"   - Function not found: {func_name}")
        else:
            print("\n⚠️  No tool calls were made by the model")
    else:
        print("❌ No response received")
    
    print("\n" + "=" * 40)
    print("✅ Demo completed!")


if __name__ == "__main__":
    main()