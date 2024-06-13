using C5Debugger;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace SecureWss.Websockets
{
    internal class Server
    {
        private int HttpPort { get; set; }
        private int HttpsPort { get; set; }
        private string CertPath { get; set; }
        private string CertPassword { get; set; }
        private string RootPath { get; set; }

        private HttpServer _httpServer;
        private HttpServer _httpsServer;

        private static Dictionary<string, string> _contentTypes = new Dictionary<string, string>
        {
            { "htm", "text/html" },
            { "html", "text/html" },
            { "js", "application/javascript" },
            { "json", "application/json" },
            { "css", "text/css" },
            { "webp", "image/webp" },
            { "png", "image/png" },
            { "jsonld", "application/ld+json" },
            { "mid", "audio/midi" },
            { "midi", "audio/x-midi" },
            { "mjs", "text/javascript" },
            { "mp3", "audio/mpeg" },
            { "mp4", "video/mp4" },
            { "mpeg", "video/mpeg" },
            { "mpkg", "application/vnd.apple.installer+xml" },
            { "odp", "application/vnd.oasis.opendocument.presentation" },
            { "ods", "application/vnd.oasis.opendocument.spreadsheet" },
            { "odt", "application/vnd.oasis.opendocument.text" },
            { "oga", "audio/ogg" },
            { "ogv", "video/ogg" },
            { "ogx", "application/ogg" },
            { "opus", "audio/opus" },
            { "otf", "font/otf" },
            { "pdf", "application/pdf" },
            { "php", "application/x-httpd-php" },
            { "ppt", "application/vnd.ms-powerpoint" },
            { "pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
            { "rar", "application/vnd.rar" },
            { "rtf", "application/rtf" },
            { "sh", "application/x-sh" },
            { "svg", "image/svg+xml" },
            { "swf", "application/x-shockwave-flash" },
            { "tar", "application/x-tar" },
            { "tif", "image/tiff" },
            { "tiff", "image/tiff" },
            { "ts", "video/mp2t" },
            { "ttf", "font/ttf" },
            { "txt", "text/plain" },
            { "vsd", "application/vnd.visio" },
            { "wav", "audio/wav" },
            { "weba", "audio/webm" },
            { "webm", "video/webm" },
            { "woff", "font/woff" },
            { "woff2", "font/woff2" },
            { "xhtml", "application/xhtml+xml" },
            { "xls", "application/vnd.ms-excel" },
            { "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
            { "xml", "application/xml" },
            { "xul", "application/vnd.mozilla.xul+xml" },
            { "zip", "application/zip" },
            { "7z", "application/x-7z-compressed" },
            { "collection", "font/collection" },
            { "sfnt", "font/sfnt" },
            { "ico", "image/vnd.microsoft.icon" }
        };
        public bool HttpIsRunning { get => _httpServer?.IsListening ?? false; }//_wsServer?.IsListening ?? false; }
        public bool HttpsIsRunning { get => _httpsServer?.IsListening ?? false; }//_wsServer?.IsListening ?? false; }

        // Constructor for secure server
        public Server(int httpPort, int httpsPort, string certPath = "", string certPassword = "", string rootPath = @"\html")
        {
            this.HttpPort = httpPort;
            this.HttpsPort = httpsPort;
            this.CertPath = certPath;
            this.CertPassword = certPassword;
            this.RootPath = rootPath;
        }

        public void Restart(int port = 0)
        {
            switch (port)
            {
                case Constants.HttpPort:
                    Debug.Print(DebugLevel.Debug, "Restarting HTTP server...");
                    Stop(Constants.HttpPort);
                    Start();
                    Debug.Print(DebugLevel.Debug, "HTTP server restarted.");
                    break;
                case Constants.HttpsPort:
                    Debug.Print(DebugLevel.Debug, "Restarting HTTPS server...");
                    Stop(Constants.HttpsPort);
                    Start();
                    Debug.Print(DebugLevel.Debug, "HTTPS server restarted.");
                    break;
                default:
                    Debug.Print(DebugLevel.Debug, "Restarting HTTP and HTTPS servers...");
                    Stop();
                    Start();
                    Debug.Print(DebugLevel.Debug, "HTTP and HTTPS servers restarted.");
                    break;
            }
        }

        //public void Start(int httpPort, int httpsPort, string certPath = "", string certPassword = "", string rootPath = @"\html")
        public void Start()
        {
            try
            {
                // Unsecure HTTP Server
                if (!HttpIsRunning)
                {
                    Debug.Print(DebugLevel.WebSocket, $"Creating HTTP Server from directory {RootPath}");
                    _httpServer = new HttpServer(HttpPort)
                    {
                        RootPath = RootPath
                    };
                    ConfigureServer(_httpServer, RootPath);

                    _httpServer.Start();
                    Debug.Print(DebugLevel.WebSocket, $"HTTP server ready on port {HttpPort}");
                }

                // Secure HTTPS Server
                if (!HttpsIsRunning)
                {
                    Debug.Print(DebugLevel.WebSocket, $"Creating HTTPS Server from directory {RootPath}");
                    _httpsServer = new HttpServer(HttpsPort, true)
                    {
                        RootPath = RootPath
                    };

                    Debug.Print($"RootPath = {_httpsServer.RootPath}");
                    if (!string.IsNullOrWhiteSpace(CertPath))
                    {
                        Debug.Print(DebugLevel.WebSocket, "Assigning SSL Configuration");
                        _httpsServer.SslConfiguration = new ServerSslConfiguration(new X509Certificate2(CertPath, CertPassword))
                        {
                            ClientCertificateRequired = false,
                            CheckCertificateRevocation = false,
                            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls,
                            //this is just to test, you might want to actually validate
                            ClientCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                            {
                                Debug.Print(DebugLevel.WebSocket, "HTTPS ClientCerticateValidation Callback triggered");
                                return true;
                            }
                        };
                    }
                    ConfigureServer(_httpsServer, RootPath);
                    _httpsServer.Start();
                    Debug.Print(DebugLevel.WebSocket, $"HTTPS server ready on port {HttpsPort}");
                }
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, "WebSocket Failed to start {0}", ex.Message);
            }
        }

        private void ConfigureServer(HttpServer server, string rootPath)
        {
            Debug.Print(DebugLevel.WebSocket, $"RootPath = {server.RootPath}");
            Debug.Print(DebugLevel.WebSocket, "Adding Echo Service");
            server.AddWebSocketService<UIService>("/echo");
            Debug.Print(DebugLevel.WebSocket, "Assigning Log Info");
            server.Log.Level = LogLevel.Trace;
            server.Log.Output = delegate
            {
                // Logging output
            };

            server.OnGet += (sender, e) =>
            {
                Debug.Print(DebugLevel.WebSocket, $"OnGet requesting {e.Request}");
                var req = e.Request;
                var res = e.Response;

                res.Headers.Add("Access-Control-Allow-Origin", "*");
                res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                res.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

                var reqPath = req.RawUrl;
                if (reqPath == "/")
                    reqPath += "index.html";

                string localPath = Path.Combine(rootPath, reqPath.Substring(1));
                byte[] contents;
                if (File.Exists(localPath))
                {
                    contents = File.ReadAllBytes(localPath);
                }
                else
                {
                    e.Response.StatusCode = 404;
                    contents = Encoding.UTF8.GetBytes("Path not found " + e.Request.RawUrl);
                }

                var extension = Path.GetExtension(reqPath).Replace(".", "");
                if (!_contentTypes.TryGetValue(extension, out var contentType))
                {
                    contentType = "text/html";
                }

                res.ContentLength64 = contents.LongLength;
                res.ContentType = contentType;

                res.Close(contents, true);
            };
        }


        public void Stop(int port = 0)
        {
            switch (port)
            {
                case Constants.HttpPort:
                    Debug.Print(DebugLevel.Debug, "Stopping HTTP server...");
                    _httpServer?.Stop();
                    _httpServer = null;
                    Debug.Print(DebugLevel.Debug, "HTTP server stopped.");
                    break;

                case Constants.HttpsPort:
                    Debug.Print(DebugLevel.Debug, "Stopping HTTPS server...");
                    _httpsServer?.Stop();
                    _httpsServer = null;
                    Debug.Print(DebugLevel.Debug, "HTTPS server stopped.");
                    break;

                default:
                    Debug.Print(DebugLevel.Debug, "Stopping HTTP and HTTPS servers...");
                    _httpServer?.Stop();
                    _httpsServer?.Stop();
                    _httpServer = null;
                    _httpsServer = null;
                    Debug.Print(DebugLevel.Debug, "HTTP and HTTPS servers stopped.");
                    break;
            }
        }

    }

    /// <summary>
    /// Crestron Websocket service for message transmission
    /// </summary>
    public class UIService : WebSocketBehavior
    {
        public uint IpId { get; set; }

        // Bool value to get whether there is a compatible registered user interface 
        private bool RegisteredWithInterface => IpId > 0 && ControlSystem.MySystem?.UserInterfaces?.Find(u => u.IpId == IpId) != null;

        // Gets the registered user interface that corresponds with this Crestron Service instance
        public UserInterface RegisteredInterface => RegisteredWithInterface ? ControlSystem.MySystem?.UserInterfaces?.Find(u => u.IpId == IpId) : null;

        public static List<UIService> Clients = new List<UIService>();

        public static void BroadcastData(string data)
        {
            try
            {
                Debug.Print(DebugLevel.WebSocket, "Broadcasting data to all clients.");
                foreach (var client in Clients)
                {
                    client.Send(data);
                }
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.WebSocket, $"Error broadcasting to clients: {ex.Message}");
            }
        }

        public UIService()
        {
            try
            {
                Debug.Print(DebugLevel.WebSocket, "Crestron service created");
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, "UIService.Constructor error {0}", ex.Message);
            }
        }

        protected override void OnOpen()
        {
            try
            {
                base.OnOpen();
                Debug.Print(DebugLevel.WebSocket, $"New user interface with ID {ID} connected from IP address: {Context.UserEndPoint.Address}");
                Clients.Add(this);
                Debug.Print(DebugLevel.WebSocket, $"Client {ID} added.");
                Send(JsonConvert.SerializeObject(ControlSystem.State));
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, "UIService.OnOpen error {0}", ex.Message);
            }
        }
        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                Debug.Print(DebugLevel.WebSocket, $"UIService Received {e.Data}");
                JObject data = JObject.Parse(e.Data);

                // Register the client with a user interface if an IP ID was sent
                RegistrationToken registrationToken = JsonConvert.DeserializeObject<RegistrationToken>(e.Data);
                if (IsRegistrationToken(data))
                {
                    Debug.Print(DebugLevel.WebSocket, $"Client registration token received.");
                    IpId = registrationToken.IpId;
                    Debug.Print(DebugLevel.WebSocket, $"New client registered with user interface on IP ID {IpId}");
                    return;
                }

                List<Cmdlet> cmdlets = Utility.ConvertObjectToCmdlets(data);
                foreach(Cmdlet cmdlet in cmdlets)
                {
                    IntersystemService.SendData(cmdlet);
                }
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, "WebSocket.OnMessage error {0}", ex.Message);
            }
        }
        protected override void OnClose(CloseEventArgs e)
        {
            try
            {
                base.OnClose(e);
                Debug.Print(DebugLevel.WebSocket, $"Client Disconnected: {ID}");
                Clients.Remove(this);
                Debug.Print(DebugLevel.WebSocket, $"Client {ID} removed from database");
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, "WebSocket.OnClose error {0}", ex.Message);
            }
        }
        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            Debug.Print(DebugLevel.Error, "WebSocket.OnError message {0}", e.Message);
        }

        // Messages to Client
        public void SendMessage(string message)
        {
            Send(message);
        }

        private class RegistrationToken
        {
            public uint IpId;

            public RegistrationToken(string ipId)
            {
                IpId = Convert.ToUInt32(ipId, 16);
            }
        }

        // Methods for check for special data types
        private bool IsRegistrationToken(JObject json)
        {
            // Example of a simple check
            return json["IpId"] != null;
        }
    }

    public class WebSocketMethod
    {
        public string Method { get; set; }
        public object[] Parameters { get; set; }
    }


}
