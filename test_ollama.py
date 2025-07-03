#!/usr/bin/env python3
"""
Test script for Ollama LLM server at 192.168.0.63
Tests basic connectivity, model listing, and chat functionality.
"""

import requests
import json
import time
from typing import Dict, Any, Optional

class OllamaClient:
    def __init__(self, host: str = "192.168.0.63", port: int = 11434):
        self.base_url = f"http://{host}:{port}"
        self.session = requests.Session()
        self.session.timeout = 30
    
    def test_connection(self) -> bool:
        """Test if the Ollama server is reachable."""
        try:
            response = self.session.get(f"{self.base_url}/api/version")
            if response.status_code == 200:
                version_info = response.json()
                print(f"✅ Connected to Ollama server")
                print(f"   Version: {version_info.get('version', 'Unknown')}")
                return True
            else:
                print(f"❌ Server responded with status {response.status_code}")
                return False
        except requests.exceptions.RequestException as e:
            print(f"❌ Connection failed: {e}")
            return False
    
    def list_models(self) -> Optional[Dict[str, Any]]:
        """Get list of available models."""
        try:
            response = self.session.get(f"{self.base_url}/api/tags")
            if response.status_code == 200:
                models_data = response.json()
                models = models_data.get('models', [])
                print(f"📋 Available models ({len(models)}):")
                for model in models:
                    name = model.get('name', 'Unknown')
                    size = model.get('size', 0)
                    size_mb = size / (1024 * 1024) if size else 0
                    modified = model.get('modified_at', 'Unknown')
                    print(f"   • {name} ({size_mb:.1f} MB) - {modified}")
                return models_data
            else:
                print(f"❌ Failed to list models: {response.status_code}")
                return None
        except requests.exceptions.RequestException as e:
            print(f"❌ Error listing models: {e}")
            return None
    
    def test_chat(self, model: str, message: str) -> bool:
        """Test chat functionality with a specific model."""
        print(f"\n💬 Testing chat with model: {model}")
        print(f"   Prompt: {message}")
        
        payload = {
            "model": model,
            "messages": [
                {"role": "user", "content": message}
            ],
            "stream": False
        }
        
        try:
            start_time = time.time()
            response = self.session.post(
                f"{self.base_url}/api/chat",
                json=payload,
                headers={"Content-Type": "application/json"}
            )
            elapsed_time = time.time() - start_time
            
            if response.status_code == 200:
                result = response.json()
                assistant_message = result.get('message', {}).get('content', 'No response')
                usage = result.get('usage', {})
                
                print(f"✅ Response received ({elapsed_time:.2f}s):")
                print(f"   {assistant_message}")
                
                if usage:
                    prompt_tokens = usage.get('prompt_tokens', 0)
                    completion_tokens = usage.get('completion_tokens', 0)
                    total_tokens = usage.get('total_tokens', 0)
                    print(f"   📊 Tokens: {prompt_tokens} prompt + {completion_tokens} completion = {total_tokens} total")
                
                return True
            else:
                print(f"❌ Chat failed with status {response.status_code}")
                print(f"   Response: {response.text}")
                return False
                
        except requests.exceptions.RequestException as e:
            print(f"❌ Chat request failed: {e}")
            return False
    
    def test_streaming_chat(self, model: str, message: str) -> bool:
        """Test streaming chat functionality."""
        print(f"\n🌊 Testing streaming chat with model: {model}")
        print(f"   Prompt: {message}")
        
        payload = {
            "model": model,
            "messages": [
                {"role": "user", "content": message}
            ],
            "stream": True
        }
        
        try:
            start_time = time.time()
            response = self.session.post(
                f"{self.base_url}/api/chat",
                json=payload,
                headers={"Content-Type": "application/json"},
                stream=True
            )
            
            if response.status_code == 200:
                print("✅ Streaming response:")
                print("   ", end="", flush=True)
                
                full_response = ""
                for line in response.iter_lines():
                    if line:
                        try:
                            chunk = json.loads(line)
                            if 'message' in chunk and 'content' in chunk['message']:
                                content = chunk['message']['content']
                                print(content, end="", flush=True)
                                full_response += content
                            
                            if chunk.get('done', False):
                                elapsed_time = time.time() - start_time
                                print(f"\n   ⏱️ Completed in {elapsed_time:.2f}s")
                                break
                        except json.JSONDecodeError:
                            continue
                
                return True
            else:
                print(f"❌ Streaming chat failed with status {response.status_code}")
                return False
                
        except requests.exceptions.RequestException as e:
            print(f"❌ Streaming chat request failed: {e}")
            return False

def main():
    print("🚀 Testing Ollama LLM Server at 192.168.0.63")
    print("=" * 50)
    
    # Initialize client
    client = OllamaClient()
    
    # Test connection
    if not client.test_connection():
        print("\n❌ Cannot connect to Ollama server. Please check:")
        print("   • Server is running at 192.168.0.63:11434")
        print("   • Network connectivity")
        print("   • Firewall settings")
        return
    
    # List available models
    print("\n" + "=" * 50)
    models_data = client.list_models()
    
    if not models_data or not models_data.get('models'):
        print("\n❌ No models available. Please pull a model first:")
        print("   ollama pull llama2")
        return
    
    # Get the first available model for testing
    first_model = models_data['models'][0]['name']
    
    # Test basic chat
    print("\n" + "=" * 50)
    test_prompts = [
        "Hello! Can you introduce yourself?",
        "What is 2+2?",
        "Write a simple Python function to calculate factorial."
    ]
    
    for prompt in test_prompts:
        success = client.test_chat(first_model, prompt)
        if not success:
            break
        time.sleep(1)  # Brief pause between requests
    
    # Test streaming chat
    print("\n" + "=" * 50)
    client.test_streaming_chat(first_model, "Tell me a short story about AI in exactly 3 sentences.")
    
    print("\n" + "=" * 50)
    print("🎉 Ollama server testing completed!")

if __name__ == "__main__":
    main()