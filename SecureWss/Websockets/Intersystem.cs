using C5Debugger;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;
using Crestron.SimplSharp;

namespace SecureWss.Websockets
{
    internal class Intersystem
    {
        private int Port { get; set; }
        private readonly WebSocketServer _webSocketServer;

        public Intersystem(int port)
        {
            Port = port;
            _webSocketServer = new WebSocketServer(Port);
            _webSocketServer.AddWebSocketService<IntersystemService>("/intersystem");
            if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, "Intersystems WebSocket server created."); 
        }

        public void Start()
        {
            _webSocketServer.Start();
            Debug.Print(DebugLevel.Debug, $"Intersystems WebSocket server started at ws://{_webSocketServer.Address}:{_webSocketServer.Port}.");
        }
        public void Stop()
        {
            _webSocketServer.Stop();
            Debug.Print(DebugLevel.Debug, "Intersystems WebSocket server stopped.");
        }
    }

    public class IntersystemService : WebSocketBehavior
    {
        public static List<IntersystemService> Nodes = new List<IntersystemService>();

        public static void BroadcastData(Cmdlet cmdlet)
        {
            try
            {
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, "Broadcasting data to all WebSocket intersystem nodes.");

                foreach (var node in Nodes)
                {
                    node.Send(JsonConvert.SerializeObject(cmdlet));
                }
            }
            catch(Exception ex)
            {
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, $"Error broadcasting to WebSocket intersystem nodes: {ex.Message}");
                ErrorLog.Error($"Error broadcasting to WebSocket intersystem nodes: {ex.Message}");
            }
        }
       
        public IntersystemService()
        {
            try
            {
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, "Intersystem Service Created");
            }
            catch (Exception ex)
            {
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.Error, "IntersystemService.Constructor error {0}", ex.Message);
                ErrorLog.Error("IntersystemService.Constructor error {0}", ex.Message);
            }
        }
        protected override void OnOpen()
        {
            try
            {
                base.OnOpen();
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, $"New intersystem client connected. IP address: {Context.UserEndPoint.Address}");
                Nodes.Add(this);
            }
            catch (Exception ex)
            {
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.Error, "IntersystemService.OnOpen error {0}", ex.Message);
                ErrorLog.Error("IntersystemService.OnOpen error {0}", ex.Message);
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                base.OnMessage(e);

                if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, $"Intersystem message receieved {e.Data}");
                JObject data = JObject.Parse(e.Data);

                if (Utility.IsCmdlet(data))
                {
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, $"Data received is a cmdlet {data}");
                }

                // Broadcast data to all user interfaces
                UIService.BroadcastData(e.Data);

                if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, $"Merging received data with state.");
                ControlSystem.Database.SubmitData(data);
            }
            catch (Exception ex)
            {
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.Error, "IntersystemService.OnMessage error {0}", ex.Message);
                ErrorLog.Error("IntersystemService.OnMessage error {0}", ex.Message);
            }
        }
        protected override void OnClose(CloseEventArgs e)
        {
        
            try
            {
                base.OnClose(e);
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, $"Intersystem client disconnected.");
                Nodes.Remove(this);
            }
            catch (Exception ex)
            {
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.Error, "IntersystemService.OnClose error {0}", ex.Message);
                ErrorLog.Error("IntersystemService.OnClose error {0}", ex.Message);
            }
        }
        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            if (Constants.EnableDebugging) Debug.Print(DebugLevel.Error, "IntersystemService.OnError message {0}", e.Message);
            ErrorLog.Error("IntersystemService.OnError message {0}", e.Message);
        }

    }
}
