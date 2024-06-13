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
    internal class WebSocketIntersystem
    {
        private int Port { get; set; }
        private readonly WebSocketServer _webSocketServer;

        public WebSocketIntersystem(int port)
        {
            Port = port;
            _webSocketServer = new WebSocketServer(Port);
            _webSocketServer.AddWebSocketService<IntersystemService>("/intersystem");
            CrestronConsole.PrintLine("Intersystems WebSocket server created."); 
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
        public string NodeID { get; set; }
        public Type NodeValueType { get; set; }
        public static void BroadcastData(string data)
        {
            try
            {
                Debug.Print(DebugLevel.WebSocket, "Broadcasting data to all WebSocket intersystem nodes.");
                foreach (var client in Nodes)
                {
                    client.Send(data);
                }
            }
            catch(Exception ex)
            {
                Debug.Print(DebugLevel.WebSocket, $"Error broadcasting to WebSocket intersystem nodes: {ex.Message}");
            }
        }
        public static void SendData(Cmdlet cmdlet)
        {
            try
            {
                Debug.Print(DebugLevel.WebSocket, $"Cmdlet received: {cmdlet}");
                Debug.Print(DebugLevel.WebSocket, $"Sending data to node: {cmdlet?.Command}, value: {cmdlet?.Value}, type: {cmdlet?.Value?.GetType()?.Name}");
                
                //var client = Nodes.First(c => cmdlet.Command.StartsWith(c.NodeID) && c.NodeValueType == cmdlet.CrestronValueType());
                Debug.Print($"Number of nodes: {Nodes.Count}");
                Type vType;

                if (cmdlet.Value == null)
                    vType = typeof(bool);
                else
                    vType = cmdlet.Value.GetType();

                var client = Nodes.FirstOrDefault(c => cmdlet.Command.StartsWith(c?.NodeID) && c?.NodeValueType?.Name == vType.Name);
                if(client != null)
                {
                    Debug.Print(DebugLevel.WebSocket, $"Client found: {client.NodeID}");
                    client.Send(JsonConvert.SerializeObject(cmdlet));
                    if (cmdlet.Value == null)
                    {
                        Debug.Print(DebugLevel.WebSocket, $"Value type = null");
                    }
                    else
                    {
                        Debug.Print(DebugLevel.WebSocket, $"Value type = {cmdlet.Value.GetType()}");
                    }
                }
                else
                {
                    Debug.Print(DebugLevel.WebSocket, $"No client found");
                }
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.WebSocket, $"Error sending data to WebSocket intersystem node ID {cmdlet.Command}: {ex.Message}");
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
                Nodes.Add(this);
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
                JObject data = JObject.Parse(e.Data);

                if (IsRegistrationToken(data))
                {
                    RegistrationToken registrationToken = JsonConvert.DeserializeObject<RegistrationToken>(e.Data);
                    Debug.Print(DebugLevel.WebSocket, $"Registration token received.");
                    NodeID = registrationToken.NodeID;
                    NodeValueType = registrationToken.NodeValueType;
                    Debug.Print(DebugLevel.WebSocket, $"New node registered: {NodeID}, type: {NodeValueType}");
                    return;
                }

                // Broadcast data to all user interfaces
                UIService.BroadcastData(e.Data);

                Debug.Print(DebugLevel.WebSocket, $"Merging received data with state.");
                Utility.MergeJsonObjects(ControlSystem.State, data);
                //Debug.Print(DebugLevel.WebSocket, $"Current state: {ControlSystem.State}");

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
                Debug.Print(DebugLevel.WebSocket, $"Intersystem client disconnected. Node ID: {NodeID}");
                Nodes.Remove(this);
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

        private class RegistrationToken
        {
            public string NodeID;
            public Type NodeValueType;
            public RegistrationToken(string nodeId, Type nodeValueType)
            {
                NodeID = nodeId;
                NodeValueType = nodeValueType;
            }
        }

        // Methods for check for special data types
        private bool IsRegistrationToken(JObject json)
        {
            // Example of a simple check
            return json["NodeID"] != null && json["NodeValueType"] != null;
        }
    }
}
