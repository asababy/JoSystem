using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JoSystem.Services.Hosting
{
    public class WebSocketManager
    {
        private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _sendLocks = new();

        public event Func<string, Task> ClientMessageReceived;

        public async Task HandleConnectionAsync(HttpContext ctx)
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                return;
            }

            var socket = await ctx.WebSockets.AcceptWebSocketAsync();
            var id = Guid.NewGuid().ToString("N");

            _sockets[id] = socket;
            _sendLocks[id] = new SemaphoreSlim(1, 1);

            try
            {
                await ReceiveLoopAsync(id, socket);
            }
            finally
            {
                _sockets.TryRemove(id, out _);
                if (_sendLocks.TryRemove(id, out var l)) l.Dispose();
                TryClose(socket);
            }
        }

        private async Task ReceiveLoopAsync(string id, WebSocket socket)
        {
            var buffer = new byte[4096];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (ClientMessageReceived != null)
                    {
                        _ = Task.Run(() => ClientMessageReceived.Invoke(text));
                    }
                }
            }
        }

        public async Task SendToAllAsync(object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var data = Encoding.UTF8.GetBytes(json);
            var tasks = new List<Task>();

            foreach (var kv in _sockets)
            {
                tasks.Add(SendToSpecificClientAsync(kv.Key, kv.Value, data));
            }

            await Task.WhenAll(tasks);
        }

        private async Task SendToSpecificClientAsync(string id, WebSocket ws, byte[] data)
        {
            if (ws.State != WebSocketState.Open) return;

            if (!_sendLocks.TryGetValue(id, out var locker)) return;

            await locker.WaitAsync();
            try
            {
                if (ws.State == WebSocketState.Open)
                {
                    await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch
            {
            }
            finally
            {
                locker.Release();
            }
        }

        public void Cleanup()
        {
            foreach (var ws in _sockets.Values) TryClose(ws);
            _sockets.Clear();
            foreach (var l in _sendLocks.Values) l.Dispose();
            _sendLocks.Clear();
        }

        private static void TryClose(WebSocket ws)
        {
            try { ws.Abort(); } catch { }
        }
    }
}

