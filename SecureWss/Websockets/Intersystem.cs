using C5Debugger;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
            if (Constants.EnableDebugging)
            {
                Debug.Print(DebugLevel.Debug, "Intersystems WebSocket server created.");
            }
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
        // Static list of all connected intersystem programs (intersystems)
        public static List<IntersystemService> Intersystems = new List<IntersystemService>();

        // Database containing the state of this intersystem instance
        public Database Database { get; set; }

        // Locks
        private readonly static object _intersystemsLock = new object();

        public static void BroadcastData(Cmdlet cmdlet)
        {
            lock (_intersystemsLock)
            {
                try
                {
                    if (Constants.EnableDebugging)
                    {
                        Debug.Print(DebugLevel.WebSocket, "Broadcasting data to all intersystem websockets");
                    }

                    foreach (var intersystem in Intersystems)
                    {
                        intersystem.Send(JsonConvert.SerializeObject(cmdlet));
                    }
                }
                catch (Exception ex)
                {
                    if (Constants.EnableDebugging)
                    {
                        Debug.Print(DebugLevel.WebSocket, $"Error broadcasting to intersystem websockets: {ex.Message}");
                    }
                    ErrorLog.Error($"Error broadcasting to WebSocket intersystems: {ex.Message}");
                }
            }
        }
       
        /// <summary>
        /// Parameterless constructor for the intersystem service
        /// </summary>
        public IntersystemService()
        {
            try
            {
                if (Constants.EnableDebugging) 
                {
                    Debug.Print(DebugLevel.WebSocket, "Intersystem service created");
                }
            }
            catch (Exception ex)
            {
                if (Constants.EnableDebugging)
                {
                    Debug.Print(DebugLevel.Error, "IntersystemService.Constructor error {0}", ex.Message);
                }
                ErrorLog.Error("IntersystemService.Constructor error {0}", ex.Message);
            }
        }
        protected override void OnOpen()
        {
            try
            {
                base.OnOpen();
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, $"New intersystem connected. IP address: {Context.UserEndPoint.Address}");
                // Add the new connection instance to the static list of connected systems
                Intersystems.Add(this);
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, $"Added intersystem from instances.");
                // Create new database for this intersystem
                Database = new Database();
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

                if (Constants.EnableDebugging)
                {
                    Debug.Print(DebugLevel.WebSocket, $"Intersystem message receieved {e.Data}");
                }

                JObject data = JObject.Parse(e.Data);

                if (Utility.IsCmdlet(data))
                {
                    if (Constants.EnableDebugging)
                    {
                        Debug.Print(DebugLevel.WebSocket, $"Data received is a cmdlet {data}");
                    }
                }
                else
                {
                    if (Constants.EnableDebugging)
                    {
                        Debug.Print(DebugLevel.WebSocket, $"Merging received data with state.");
                    }

                    UIService.BroadcastData(JsonConvert.SerializeObject(e.Data));
                    Database?.SubmitData(data);
                }
            }
            catch (Exception ex)
            {
                if (Constants.EnableDebugging)
                {
                    Debug.Print(DebugLevel.Error, "IntersystemService.OnMessage error {0}", ex.Message);
                }
                ErrorLog.Error("IntersystemService.OnMessage error {0}", ex.Message);
            }
        }
        protected override void OnClose(CloseEventArgs e)
        {
            try
            {
                base.OnClose(e);
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, $"Intersystem client disconnected.");
                // Remove this instance from the static list of systems
                Intersystems.Remove(this);
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, $"Removed intersystem from instances.");
                // Remove database
                Database = null;
            }
            catch (Exception ex)
            {
                if (Constants.EnableDebugging) 
                {
                    Debug.Print(DebugLevel.Error, "IntersystemService.OnClose error {0}", ex.Message);
                }

                ErrorLog.Error("IntersystemService.OnClose error {0}", ex.Message);
            }
        }
        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            if (Constants.EnableDebugging)
            {
                Debug.Print(DebugLevel.Error, "IntersystemService.OnError message {0}", e.Message);
            }
            ErrorLog.Error("IntersystemService.OnError message {0}", e.Message);
        }
    }
}
