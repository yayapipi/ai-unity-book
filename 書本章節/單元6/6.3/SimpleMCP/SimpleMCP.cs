// 文件名: SimpleMCP.cs
// 位置: Assets/SimpleMCP.cs
// 使用方法: 將此腳本附加到場景中的任意 GameObject 上

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class SimpleMCP : MonoBehaviour
{
    [Header("設置")] [SerializeField] private int port = 8765;
    [SerializeField] private Transform playerTransform; // 拖曳你的玩家到這裡

    private TcpListener listener;
    private Thread serverThread;
    private bool isRunning = false;
    private Queue<Action> mainThreadActions = new Queue<Action>();

    private void Start()
    {
        Application.runInBackground = true;
        // 如果沒有設置玩家，自動尋找
        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                Debug.LogWarning("[SimpleMCP] 找不到玩家！請在 Inspector 中設置 Player Transform");
            }
        }

        StartServer();
    }

    private void StartServer()
    {
        try
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            isRunning = true;

            serverThread = new Thread(AcceptClients);
            serverThread.IsBackground = true;
            serverThread.Start();

            Debug.Log($"[SimpleMCP] 伺服器已啟動在端口 {port}");
            Debug.Log("[SimpleMCP] 建議透過 MCP Bridge 連接（Cursor 將依 .cursor/mcp.json 自動啟動 Python Bridge）");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SimpleMCP] 啟動失敗: {e.Message}");
        }
    }

    private void AcceptClients()
    {
        while (isRunning)
        {
            try
            {
                TcpClient client = listener.AcceptTcpClient();
                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.IsBackground = true;
                clientThread.Start();
            }
            catch
            {
            }
        }
    }

    private void HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[4096];

        try
        {
            // WebSocket 握手
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (request.Contains("Upgrade: websocket"))
            {
                string response = PerformHandshake(request);
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                stream.Write(responseBytes, 0, responseBytes.Length);

                Debug.Log("[SimpleMCP] 客戶端已連接");

                // 處理消息
                while (isRunning && client.Connected)
                {
                    if (stream.DataAvailable)
                    {
                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead <= 0)
                        {
                            break; // 連線關閉
                        }
                        if (bytesRead > 0)
                        {
                            string message = DecodeFrame(buffer, bytesRead);
                            if (!string.IsNullOrEmpty(message))
                            {
                                ProcessMessage(message, stream);
                            }
                        }
                    }

                    Thread.Sleep(10);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SimpleMCP] 客戶端錯誤: {e.Message}");
        }
        finally
        {
            client.Close();
            Debug.Log("[SimpleMCP] 客戶端已斷開");
        }
    }

    private string PerformHandshake(string request)
    {
        string key = "";
        var lines = request.Split('\n');
        foreach (var line in lines)
        {
            var headerLine = line.Trim('\r');
            if (headerLine.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
            {
                key = headerLine.Substring(19).Trim();
                break;
            }
        }

        string acceptKey = Convert.ToBase64String(
            System.Security.Cryptography.SHA1.Create().ComputeHash(
                Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")
            )
        );

        return "HTTP/1.1 101 Switching Protocols\r\n" +
               "Upgrade: websocket\r\n" +
               "Connection: Upgrade\r\n" +
               $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";
    }

    private string DecodeFrame(byte[] buffer, int length)
    {
        if (length < 2) return null;

        byte firstByte = buffer[0];
        bool fin = (firstByte & 0x80) != 0;
        byte opcode = (byte)(firstByte & 0x0F);
        // 僅處理文本幀，忽略其他幀（如 ping/pong/binary/close/續傳）
        if (opcode != 0x1) return null;

        bool isMasked = (buffer[1] & 0x80) != 0;
        // 來自客戶端的幀應為遮罩，否則忽略
        if (!isMasked) return null;
        int payloadLength = buffer[1] & 0x7F;
        int offset = 2;

        if (payloadLength == 126)
        {
            if (length < 4) return null;
            payloadLength = (buffer[2] << 8) | buffer[3];
            offset = 4;
        }
        else if (payloadLength == 127)
        {
            // 不支援超大訊息，避免超範圍與阻塞
            return null;
        }

        byte[] mask = new byte[4];
        if (isMasked)
        {
            Array.Copy(buffer, offset, mask, 0, 4);
            offset += 4;
        }

        if (offset + payloadLength > length) return null;
        byte[] payload = new byte[payloadLength];
        for (int i = 0; i < payloadLength; i++)
        {
            payload[i] = isMasked ? (byte)(buffer[offset + i] ^ mask[i % 4]) : buffer[offset + i];
        }

        return Encoding.UTF8.GetString(payload);
    }

    private byte[] EncodeFrame(string message)
    {
        byte[] payload = Encoding.UTF8.GetBytes(message);
        int length = payload.Length;
        byte[] frame;

        if (length < 126)
        {
            frame = new byte[2 + length];
            frame[1] = (byte)length;
            Array.Copy(payload, 0, frame, 2, length);
        }
        else
        {
            frame = new byte[4 + length];
            frame[1] = 126;
            frame[2] = (byte)(length >> 8);
            frame[3] = (byte)(length & 0xFF);
            Array.Copy(payload, 0, frame, 4, length);
        }

        frame[0] = 0x81;
        return frame;
    }

    private void ProcessMessage(string message, NetworkStream stream)
    {
        lock (mainThreadActions)
        {
            mainThreadActions.Enqueue(() =>
            {
                try
                {
                    Debug.Log($"[SimpleMCP] 收到消息: {message}");

                    var request = JsonUtility.FromJson<MCPRequest>(message);

                    if (request == null || string.IsNullOrEmpty(request.method))
                    {
                        Debug.LogError("[SimpleMCP] 無效的請求格式");
                        SendResponse(stream, "{\"error\":\"Invalid request format\"}");
                        return;
                    }

                    string response = HandleRequest(request);
                    SendResponse(stream, response);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SimpleMCP] 處理消息錯誤: {e.Message}\n消息內容: {message}");
                    SendResponse(stream, "{\"error\":\"Internal server error\"}");
                }
            });
        }
    }

    // 新增的 HandleRequest 方法
    private string HandleRequest(MCPRequest request)
    {
        try
        {
            Debug.Log($"[SimpleMCP] 處理請求方法: {request.method}");

            switch (request.method.ToLower())
            {
                case "get_position":
                case "getposition":
                    return GetPlayerPosition();

                case "move_player":
                case "moveplayer":
                case "set_position":
                case "setposition":
                    return MovePlayer(request.x, request.y, request.z);

                case "ping":
                    return "{\"success\":true,\"message\":\"pong\"}";

                case "get_player_info":
                case "getplayerinfo":
                    return GetPlayerInfo();

                default:
                    Debug.LogWarning($"[SimpleMCP] 未知的請求方法: {request.method}");
                    return "{\"error\":\"Unknown method\",\"method\":\"" + request.method + "\"}";
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SimpleMCP] HandleRequest 錯誤: {e.Message}");
            return "{\"error\":\"Request handling failed\",\"details\":\"" + e.Message + "\"}";
        }
    }

    private void SendResponse(NetworkStream stream, string response)
    {
        try
        {
            Debug.Log($"[SimpleMCP] 發送回應: {response}");
            byte[] responseFrame = EncodeFrame(response);
            stream.Write(responseFrame, 0, responseFrame.Length);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SimpleMCP] 發送回應失敗: {e.Message}");
        }
    }

    private string GetPlayerPosition()
    {
        if (playerTransform == null)
            return "{\"error\":\"Player not found\"}";

        var pos = playerTransform.position;
        return $"{{\"success\":true,\"x\":{pos.x},\"y\":{pos.y},\"z\":{pos.z}}}";
    }

    private string MovePlayer(float x, float y, float z)
    {
        if (playerTransform == null)
            return "{\"error\":\"Player not found\"}";

        playerTransform.position = new Vector3(x, y, z);
        Debug.Log($"[SimpleMCP] 玩家移動到: ({x}, {y}, {z})");

        return $"{{\"success\":true,\"x\":{x},\"y\":{y},\"z\":{z}}}";
    }

    // 新增的 GetPlayerInfo 方法
    private string GetPlayerInfo()
    {
        if (playerTransform == null)
            return "{\"error\":\"Player not found\"}";

        var pos = playerTransform.position;
        var rot = playerTransform.rotation;
        var scale = playerTransform.localScale;

        return $"{{\"success\":true," +
               $"\"position\":{{\"x\":{pos.x},\"y\":{pos.y},\"z\":{pos.z}}}," +
               $"\"rotation\":{{\"x\":{rot.x},\"y\":{rot.y},\"z\":{rot.z},\"w\":{rot.w}}}," +
               $"\"scale\":{{\"x\":{scale.x},\"y\":{scale.y},\"z\":{scale.z}}}," +
               $"\"name\":\"{playerTransform.name}\"}}";
    }

    private void Update()
    {
        // 在主線程執行隊列中的動作
        lock (mainThreadActions)
        {
            while (mainThreadActions.Count > 0)
            {
                mainThreadActions.Dequeue().Invoke();
            }
        }
    }

    private void OnDestroy()
    {
        isRunning = false;
        listener?.Stop();
        serverThread?.Join(1000);
        Debug.Log("[SimpleMCP] 伺服器已停止");
    }

    private void OnApplicationQuit()
    {
        isRunning = false;
        listener?.Stop();
        serverThread?.Join(1000);
    }

    [Serializable]
    public class MCPRequest
    {
        public string method;
        public float x;
        public float y;
        public float z;
    }
}