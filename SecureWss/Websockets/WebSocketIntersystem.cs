using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;
using Crestron.SimplSharp;
using C5Debugger;

namespace SecureWss.Websockets
{
    internal class WebSocketIntersystem
    {
        private int Port { get; set; }
        private readonly WebSocketServer _webSocketServer;

        public WebSocketIntersystem(int port)
        {
            Port = port;
            _webSocketServer = new WebSocketServer(Port);
            _webSocketServer.AddWebSocketService<IntersystemService>("/intersystem");
            Debug.Print(DebugLevel.Debug, "Intersystems WebSocket server created."); 
        }

        public void Start()
        {
            _webSocketServer.Start();
            Debug.Print(DebugLevel.Debug, "Intersystems WebSocket server started.");
        }
        public void Stop()
        {
            _webSocketServer.Stop();
            Debug.Print(DebugLevel.Debug, "Intersystems WebSocket server stopped.");
        }
    }

    public class IntersystemService : WebSocketBehavior
    {
        public static List<IntersystemService> Clients = new List<IntersystemService>();
        public static void BroadcastData(string data)
        {
            try
            {
                Debug.Print(DebugLevel.WebSocket, "Broadcasting data to all WebSocket intersystem nodes.");
                foreach (var client in Clients)
                {
                    client.Send(data);
                }
            }
            catch(Exception ex)
            {
                Debug.Print(DebugLevel.WebSocket, $"Error broadcasting to WebSocket intersystem nodes: {ex.Message}");
            }
        }
        public IntersystemService()
        {
            try
            {
                Debug.Print(DebugLevel.WebSocket, "Intersystem Service Created");
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, "IntersystemService.Constructor error {0}", ex.Message);
            }
        }
        protected override void OnOpen()
        {
            try
            {
                base.OnOpen();
                Debug.Print(DebugLevel.WebSocket, $"New intersystem client connected. IP address: {Context.UserEndPoint.Address}");
                Clients.Add(this);
                Send("Welcome to the intersystem server");
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, "IntersystemService.OnOpen error {0}", ex.Message);
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                base.OnMessage(e);
                Debug.Print(DebugLevel.WebSocket, $"Intersystem message receieved {e.Data}");
                Send($"Echo: {e.Data}");
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, "IntersystemService.OnMessage error {0}", ex.Message);
            }
        }
        protected override void OnClose(CloseEventArgs e)
        {
        
            try
            {
                base.OnClose(e);
                Debug.Print(DebugLevel.WebSocket, $"Intersystem client disconnected. IP address: {Context.UserEndPoint.Address}");
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, "IntersystemService.OnClose error {0}", ex.Message);
            }
        }
        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            Debug.Print(DebugLevel.Error, "IntersystemService.OnError message {0}", e.Message);
        }
    }
}
