#!/usr/bin/env python3
"""
Unity MCP Bridge with Debug Logging
"""

import asyncio
import websockets
import json
import sys
import os
import logging

# 設置日誌文件
log_file = os.path.join(os.path.dirname(__file__), "unity_mcp_debug.log")
logging.basicConfig(
    level=logging.DEBUG,
    format='%(asctime)s [%(levelname)s] %(message)s',
    handlers=[
        logging.FileHandler(log_file, encoding='utf-8'),
        logging.StreamHandler(sys.stderr)
    ]
)

logger = logging.getLogger(__name__)

# 從環境變數讀取配置
UNITY_HOST = os.getenv("UNITY_HOST", "localhost")
UNITY_PORT = os.getenv("UNITY_PORT", "8765")
UNITY_URI = f"ws://{UNITY_HOST}:{UNITY_PORT}"

ws_connection = None

def log(message):
    """輸出日誌"""
    logger.info(message)

async def connect_to_unity():
    """連接到 Unity WebSocket 伺服器"""
    global ws_connection
    
    log(f"Attempting to connect to Unity at {UNITY_URI}")
    
    max_retries = 5
    retry_delay = 2
    
    for attempt in range(max_retries):
        try:
            ws_connection = await asyncio.wait_for(
                websockets.connect(UNITY_URI),
                timeout=5.0
            )
            log(f"✓ Connected to Unity at {UNITY_URI}")
            return True
        except asyncio.TimeoutError:
            log(f"✗ Connection timeout (attempt {attempt + 1}/{max_retries})")
        except ConnectionRefusedError:
            log(f"✗ Connection refused - Is Unity running? (attempt {attempt + 1}/{max_retries})")
        except Exception as e:
            log(f"✗ Connection error: {type(e).__name__} - {e} (attempt {attempt + 1}/{max_retries})")
        
        if attempt < max_retries - 1:
            log(f"Retrying in {retry_delay} seconds...")
            await asyncio.sleep(retry_delay)
    
    log("✗ Failed to connect to Unity after all retries")
    log("Please ensure:")
    log("  1. Unity is running")
    log("  2. SimpleMCP script is attached to a GameObject")
    log("  3. The game is in Play mode")
    log(f"  4. Port {UNITY_PORT} is not blocked by firewall")
    return False

async def send_unity_request(method, **kwargs):
    """發送請求到 Unity"""
    global ws_connection
    
    if ws_connection is None:
        log("WebSocket not connected, attempting to connect...")
        if not await connect_to_unity():
            return {"error": "Not connected to Unity. Is the game running?"}
    
    try:
        request = {"method": method, **kwargs}
        log(f"Sending to Unity: {json.dumps(request)}")
        
        await ws_connection.send(json.dumps(request))
        response = await asyncio.wait_for(ws_connection.recv(), timeout=5.0)
        
        log(f"Received from Unity: {response}")
        return json.loads(response)
        
    except asyncio.TimeoutError:
        log("✗ Unity request timeout")
        return {"error": "Unity request timeout"}
    except websockets.exceptions.ConnectionClosed as e:
        log(f"✗ Connection to Unity lost: {e}")
        ws_connection = None
        return {"error": "Connection to Unity lost"}
    except Exception as e:
        log(f"✗ Error sending request: {type(e).__name__} - {e}")
        return {"error": str(e)}

async def handle_mcp_request(method, params=None):
    """處理來自 Claude 的 MCP 請求"""
    log(f"Handling MCP request: {method} with params: {params}")
    
    if params is None:
        params = {}
    
    # MCP 初始化握手
    if method == "initialize":
        log("Handling initialize request")
        return {
            "protocolVersion": "2024-11-05",
            "capabilities": {
                "tools": {}
            },
            "serverInfo": {
                "name": "unity-game-mcp",
                "version": "1.0.0"
            }
        }
    
    # 初始化完成通知（不需要返回結果）
    elif method == "notifications/initialized":
        log("Received initialized notification")
        return None  # 通知不需要返回結果
    
    elif method == "tools/list":
        return {
            "tools": [
                {
                    "name": "get_player_position",
                    "description": "獲取玩家在遊戲中的當前位置座標",
                    "inputSchema": {
                        "type": "object",
                        "properties": {},
                        "required": []
                    }
                },
                {
                    "name": "move_player",
                    "description": "將玩家移動到指定的座標位置",
                    "inputSchema": {
                        "type": "object",
                        "properties": {
                            "x": {"type": "number", "description": "X 座標"},
                            "y": {"type": "number", "description": "Y 座標（高度）"},
                            "z": {"type": "number", "description": "Z 座標"}
                        },
                        "required": ["x", "y", "z"]
                    }
                }
            ]
        }
    
    elif method == "tools/call":
        tool_name = params.get("name")
        arguments = params.get("arguments", {})
        
        log(f"Calling tool: {tool_name} with arguments: {arguments}")
        
        if tool_name == "get_player_position":
            result = await send_unity_request("get_position")
            if "error" in result:
                return {"error": result["error"]}
            return {
                "content": [
                    {
                        "type": "text",
                        "text": f"玩家當前位置：\nX: {result.get('x', 0):.2f}\nY: {result.get('y', 0):.2f}\nZ: {result.get('z', 0):.2f}"
                    }
                ]
            }
        
        elif tool_name == "move_player":
            x = arguments.get("x")
            y = arguments.get("y")
            z = arguments.get("z")
            
            if x is None or y is None or z is None:
                return {"error": "Missing required parameters: x, y, z"}
            
            result = await send_unity_request("move_player", x=x, y=y, z=z)
            if "error" in result:
                return {"error": result["error"]}
            
            return {
                "content": [
                    {
                        "type": "text",
                        "text": f"✓ 玩家已移動到：\nX: {result.get('x', 0):.2f}\nY: {result.get('y', 0):.2f}\nZ: {result.get('z', 0):.2f}"
                    }
                ]
            }
        
        else:
            return {"error": f"Unknown tool: {tool_name}"}
    
    else:
        return {"error": f"Unknown method: {method}"}

async def main():
    """主循環"""
    log("=" * 60)
    log("Unity MCP Bridge Starting")
    log("=" * 60)
    log(f"Target Unity Server: {UNITY_URI}")
    log(f"Log file: {log_file}")
    log(f"Python version: {sys.version}")
    log(f"Working directory: {os.getcwd()}")
    log("=" * 60)
    
    # 預先連接到 Unity
    if not await connect_to_unity():
        log("CRITICAL: Cannot connect to Unity. Bridge will still start but may not work.")
    
    log("Entering main loop, waiting for MCP requests...")
    
    while True:
        try:
            # 從 stdin 讀取請求
            line = await asyncio.get_event_loop().run_in_executor(
                None, sys.stdin.readline
            )
            
            if not line:
                log("EOF received, exiting")
                break
            
            line = line.strip()
            if not line:
                continue
            
            log(f"Received MCP request: {line}")
            
            try:
                request = json.loads(line)
            except json.JSONDecodeError as e:
                log(f"JSON parse error: {e}")
                # MCP 協議不允許 id 為 null，跳過無效請求
                log("Skipping invalid request")
                continue
            
            request_id = request.get("id")
            method = request.get("method")
            params = request.get("params", {})
            
            try:
                result = await handle_mcp_request(method, params)
                
                # 如果是通知（沒有 id），不需要返回響應
                if request_id is None:
                    log("Notification processed, no response needed")
                    continue
                
                # 如果結果是 None（通知的返回值），不發送響應
                if result is None:
                    log("Method returned None, no response needed")
                    continue
                
                if "error" in result:
                    response = {
                        "jsonrpc": "2.0",
                        "id": request_id,
                        "error": {"code": -32000, "message": result["error"]}
                    }
                else:
                    response = {
                        "jsonrpc": "2.0",
                        "id": request_id,
                        "result": result
                    }
                
                response_str = json.dumps(response)
                log(f"Sending MCP response: {response_str}")
                print(response_str, flush=True)
                
            except Exception as e:
                log(f"Error handling request: {type(e).__name__} - {e}")
                import traceback
                log(traceback.format_exc())
                
                error_response = {
                    "jsonrpc": "2.0",
                    "id": request_id,
                    "error": {"code": -32603, "message": "Internal error", "data": str(e)}
                }
                print(json.dumps(error_response), flush=True)
        
        except KeyboardInterrupt:
            log("Keyboard interrupt received, shutting down")
            break
        except Exception as e:
            log(f"Main loop error: {type(e).__name__} - {e}")
            import traceback
            log(traceback.format_exc())
    
    if ws_connection:
        await ws_connection.close()
    log("Unity MCP Bridge stopped")

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        pass
    except Exception as e:
        logger.error(f"Fatal error: {e}")
        import traceback
        logger.error(traceback.format_exc())