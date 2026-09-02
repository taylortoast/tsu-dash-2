using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.WebSockets;

public static class PublicBoardRealtimeHub
{
    private static readonly object Gate = new object();
    private static readonly List<WebSocket> Clients = new List<WebSocket>();
    private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

    public static void Add(WebSocket socket)
    {
        lock (Gate)
        {
            RemoveClosed_NoLock();
            Clients.Add(socket);
        }
    }

    public static void Remove(WebSocket socket)
    {
        lock (Gate)
        {
            Clients.Remove(socket);
            RemoveClosed_NoLock();
        }
    }

    public static void BroadcastBoardChanged(string reason)
    {
        Task.Run(async delegate
        {
            await BroadcastAsync(new Dictionary<string, object>
            {
                { "type", "public-board-changed" },
                { "reason", reason },
                { "sentUtc", DateTime.UtcNow.ToString("o") }
            });
        });
    }

    public static async Task SendConnectedAsync(WebSocket socket)
    {
        await SendAsync(socket, new Dictionary<string, object>
        {
            { "type", "connected" },
            { "sentUtc", DateTime.UtcNow.ToString("o") }
        });
    }

    private static async Task BroadcastAsync(object payload)
    {
        WebSocket[] snapshot;
        lock (Gate)
        {
            RemoveClosed_NoLock();
            snapshot = Clients.ToArray();
        }

        foreach (WebSocket socket in snapshot)
        {
            await SendAsync(socket, payload);
        }
    }

    private static async Task SendAsync(WebSocket socket, object payload)
    {
        if (socket == null || socket.State != WebSocketState.Open) return;

        string json = Serializer.Serialize(payload);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        try
        {
            await socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            // C# 5.0 compatible formatting
            System.Diagnostics.Trace.TraceError(string.Format("Failed to send message to socket: {0}", ex.Message));
            Remove(socket);
        }
    }

    private static void RemoveClosed_NoLock()
    {
        Clients.RemoveAll(delegate (WebSocket socket)
        {
            return socket == null || socket.State != WebSocketState.Open;
        });
    }
}

public class PublicBoardSocket : IHttpHandler
{
    public bool IsReusable
    {
        get { return false; }
    }

    public void ProcessRequest(HttpContext context)
    {
        if (!context.IsWebSocketRequest)
        {
            context.Response.ContentType = "application/json";
            context.Response.Write("{\"ok\":true,\"data\":{\"message\":\"Public board WebSocket handler loaded. Use a WebSocket client to connect.\"},\"errors\":[]}");
            return;
        }

        context.AcceptWebSocketRequest(HandleSocket);
    }

    private async Task HandleSocket(AspNetWebSocketContext context)
    {
        WebSocket socket = context.WebSocket;
        PublicBoardRealtimeHub.Add(socket);

        try
        {
            await PublicBoardRealtimeHub.SendConnectedAsync(socket);

            byte[] buffer = new byte[1024 * 4];
            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // Properly acknowledge the close
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    // ==========================================
                    // START: HEARTBEAT HANDLING
                    // ==========================================
                    // We received a text message (like our JS "ping").
                    // We don't need to respond or process it, just receiving it 
                    // resets the IIS idle timeout timer. We do nothing and let the loop continue.
                    // ==========================================
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            // This will show up in your Server Trace logs
            System.Diagnostics.Trace.TraceError("WebSocket Loop Crash: " + ex.Message);
            if (ex.InnerException != null)
            {
                System.Diagnostics.Trace.TraceError("Inner: " + ex.InnerException.Message);
            }
        }
        finally
        {
            PublicBoardRealtimeHub.Remove(socket);
        }
    }

}
