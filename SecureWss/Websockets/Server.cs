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
using Crestron.SimplSharp;

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
            {"cer", "application/x-x509-ca-cert" },
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
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, "Restarting HTTP server...");
                    Stop(Constants.HttpPort);
                    Start();
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, "HTTP server restarted.");
                    break;
                case Constants.HttpsPort:
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, "Restarting HTTPS server...");
                    Stop(Constants.HttpsPort);
                    Start();
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, "HTTPS server restarted.");
                    break;
                default:
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, "Restarting HTTP and HTTPS servers...");
                    Stop();
                    Start();
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, "HTTP and HTTPS servers restarted.");
                    break;
            }
        }

        public void Start()
        {
            try
            {
                // Unsecure HTTP Server
                if (!HttpIsRunning)
                {
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, $"Creating HTTP Server from directory {RootPath}");
                    _httpServer = new HttpServer(HttpPort)
                    {
                        RootPath = RootPath
                    };
                    ConfigureServer(_httpServer, RootPath);

                    _httpServer.Start();
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, $"HTTP server ready on port {HttpPort}");
                }

                // Secure HTTPS Server
                if (!HttpsIsRunning)
                {
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, $"Creating HTTPS Server from directory {RootPath}");
                    _httpsServer = new HttpServer(HttpsPort, true)
                    {
                        RootPath = RootPath
                    };

                    if (Constants.EnableDebugging) Debug.Print($"RootPath = {_httpsServer.RootPath}");
                    if (!string.IsNullOrWhiteSpace(CertPath))
                    {
                        Debug.Print(DebugLevel.WebSocket, $"Assigning SSL Configuration with certificate: {CertPath}");
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
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, $"HTTPS server ready on port {HttpsPort}");
                }
            }
            catch (Exception ex)
            {
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.Error, "WebSocket Failed to start {0}", ex.Message);
                ErrorLog.Error("WebSocket Failed to start {0}", ex.Message);
            }
        }

        private void ConfigureServer(HttpServer server, string rootPath)
        {
            if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, $"RootPath = {server.RootPath}");
            if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, "Adding Echo Service");
            server.AddWebSocketService<UIService>("/echo");
            if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, "Assigning Log Info");
            server.Log.Level = LogLevel.Trace;
            server.Log.Output = delegate
            {
                // Logging output
            };

            server.OnGet += (sender, e) =>
            {
                if (e.Request.RawUrl == "/file/rootCert" || e.Request.RawUrl == "/file/rootCert.cer")
                {
                    if (Constants.EnableDebugging)
                    {
                        Debug.Print("Request for certificate received.");
                    }

                    string filePath = Path.Combine($@"\user\{Constants.RootCertName}.cer");

                    if (Constants.EnableDebugging)
                    {
                        Debug.Print($"Looking for file at {filePath}");
                    }

                    if (File.Exists(filePath))
                    {
                        if (Constants.EnableDebugging)
                        {
                            Debug.Print("Requested file found.");
                        }
                        byte[] fileContent = File.ReadAllBytes(filePath);
                        e.Response.StatusCode = (int)System.Net.HttpStatusCode.OK;
                        e.Response.ContentType = "application/x-x509-ca-cert";
                        e.Response.ContentLength64 = fileContent.Length;
                        e.Response.OutputStream.Write(fileContent, 0, fileContent.Length);
                        e.Response.Close();
                    }
                    else
                    {
                        if (Constants.EnableDebugging)
                        {
                            Debug.Print("Requested file NOT found.");
                        }
                        e.Response.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                        e.Response.Close();
                    }
                }
                else if(e.Request.RawUrl == "/file/system" || e.Request.RawUrl == "/file/system.json")
                {
                    if (Constants.EnableDebugging)
                    {
                        Debug.Print("Request for system config received.");
                    }

                    string filePath = Path.Combine($@"\user\system.json");

                    if (Constants.EnableDebugging)
                    {
                        Debug.Print($"Looking for file at {filePath}");
                    }

                    if (File.Exists(filePath))
                    {
                        if (Constants.EnableDebugging)
                        {
                            Debug.Print("Requested file found.");
                        }
                        byte[] fileContent = File.ReadAllBytes(filePath);
                        e.Response.StatusCode = (int)System.Net.HttpStatusCode.OK;
                        e.Response.ContentType = "application/x-x509-ca-cert";
                        e.Response.ContentLength64 = fileContent.Length;

                        // Add CORS headers
                        e.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                        e.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                        e.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                        e.Response.OutputStream.Write(fileContent, 0, fileContent.Length);
                        e.Response.Close();
                    }
                    else
                    {
                        if (Constants.EnableDebugging)
                        {
                            Debug.Print("Requested file NOT found.");
                        }
                        e.Response.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                        e.Response.Close();
                    }
                }
                else
                {
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.WebSocket, $"OnGet requesting {e.Request}");
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
                    else if (File.Exists(Path.Combine(rootPath, "index.html")))
                    {
                        contents = File.ReadAllBytes(Path.Combine(rootPath, "index.html"));
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
                }

            };
        }


        public void Stop(int port = 0)
        {
            switch (port)
            {
                case Constants.HttpPort:
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, "Stopping HTTP server...");
                    _httpServer?.Stop();
                    _httpServer = null;
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, "HTTP server stopped.");
                    break;

                case Constants.HttpsPort:
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, "Stopping HTTPS server...");
                    _httpsServer?.Stop();
                    _httpsServer = null;
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, "HTTPS server stopped.");
                    break;

                default:
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, "Stopping HTTP and HTTPS servers...");
                    _httpServer?.Stop();
                    _httpsServer?.Stop();
                    _httpServer = null;
                    _httpsServer = null;
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, "HTTP and HTTPS servers stopped.");
                    break;
            }
        }

    }

    // Crestron Websocket service for message transmission
    public class UIService : WebSocketBehavior
    {
        // ===================================
        // Static Members
        // ===================================

        // List of connected clients.
        public static List<UIService> Clients { get; } = new List<UIService>();

        // Locks
        private static readonly object _clientsLock = new object();
        private static readonly object _broadcastLock = new object();

        // ===================================
        // Instance Members
        // ===================================

        // Optional IP ID to associate with a user interface instance.
        public uint IpId { get; set; }

        // Gets whether there is a compatible registered user interface.
        private bool RegisteredWithInterface => IpId > 0 && ControlSystem.MySystem?.UserInterfaces?.Find(u => u.IpId == IpId) != null;

        // Gets the registered user interface that corresponds with this Crestron Service instance.
        public UserInterface RegisteredInterface => RegisteredWithInterface ? ControlSystem.MySystem?.UserInterfaces?.Find(u => u.IpId == IpId) : null;

        // ===================================
        // Constructors
        // ===================================

        // Initializes a new instance of the UIService class.
        public UIService()
        {
            try
            {
                if (Constants.EnableDebugging)
                {
                    Debug.Print(DebugLevel.WebSocket, "Crestron service created");
                }
            }
            catch (Exception ex)
            {
                if (Constants.EnableDebugging)
                {
                    Debug.Print(DebugLevel.Error, $"UIService.Constructor error {ex.Message}");
                }
                ErrorLog.Error($"UIService.Constructor error {ex.Message}");
            }
        }

        // ===================================
        // Static Methods
        // ===================================

        // Broadcasts data to all connected clients.
        public static void BroadcastData(string data)
        {
            lock (_broadcastLock)
            {
                try
                {
                    if (Constants.EnableDebugging)
                    {
                        Debug.Print(DebugLevel.WebSocket, "Broadcasting data to all clients.");
                    }
                    foreach (var client in Clients)
                    {
                        client.Send(data);
                    }
                }
                catch (Exception ex)
                {
                    if (Constants.EnableDebugging)
                    {
                        Debug.Print(DebugLevel.WebSocket, $"Error broadcasting to clients: {ex.Message}");
                    }
                    ErrorLog.Error($"Error broadcasting to clients: {ex.Message}");
                }
            }
        }

        // ===================================
        // Instance Methods
        // ===================================

        // Handles the WebSocket connection opening event.
        protected override void OnOpen()
        {
            try
            {
                base.OnOpen();
                if (Constants.EnableDebugging)
                {
                    Debug.Print(DebugLevel.WebSocket, $"New user interface with ID {ID} connected from IP address: {Context.UserEndPoint.Address}");
                }
                lock (_clientsLock)
                {
                    Clients.Add(this);
                }
                if (Constants.EnableDebugging)
                {
                    Debug.Print(DebugLevel.WebSocket, $"Client {ID} added.");
                }
                Send(JsonConvert.SerializeObject(ControlSystem.Database.State));
                foreach (var intersystem in IntersystemService.Intersystems)
                {
                    Send(JsonConvert.SerializeObject(intersystem.Database.State));
                }
            }
            catch (Exception ex)
            {
                if (Constants.EnableDebugging)
                {
                    Debug.Print(DebugLevel.Error, $"UIService.OnOpen error {ex.Message}");
                }
                ErrorLog.Error($"UIService.OnOpen error {ex.Message}");
            }
        }

        // Handles incoming messages from the WebSocket.
        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                if (Constants.EnableDebugging)
                {
                    Debug.Print(DebugLevel.WebSocket, $"UIService Received {e.Data}");
                }
                var data = JObject.Parse(e.Data);

                // Register the client with a user interface if an IP ID was sent
                if (IsRegistrationToken(data))
                {
                    IpId = Convert.ToUInt32(data["IpId"].ToString(), 16);
                    if (Constants.EnableDebugging)
                    {
                        Debug.Print(DebugLevel.WebSocket, $"New client registered with user interface on IP ID {IpId}");
                    }
                    return;
                }

                var cmdlets = Utility.ConvertObjectToCmdlets(data);
                foreach (var cmdlet in cmdlets)
                {
                    IntersystemService.BroadcastData(cmdlet);
                }
            }
            catch (Exception ex)
            {
                if (Constants.EnableDebugging)
                {
                    Debug.Print(DebugLevel.Error, $"WebSocket.OnMessage error {ex.Message}");
                }
                ErrorLog.Error($"WebSocket.OnMessage error {ex.Message}");
            }
        }

        // Handles the WebSocket connection closing event.
        protected override void OnClose(CloseEventArgs e)
        {
            try
            {
                base.OnClose(e);
                if (Constants.EnableDebugging)
                {
                    Debug.Print(DebugLevel.WebSocket, $"Client Disconnected: {ID}");
                }
                lock (_clientsLock)
                {
                    Clients.Remove(this);
                }
                if (Constants.EnableDebugging)
                {
                    Debug.Print(DebugLevel.WebSocket, $"Client {ID} removed from database");
                }
            }
            catch (Exception ex)
            {
                if (Constants.EnableDebugging)
                {
                    Debug.Print(DebugLevel.Error, $"WebSocket.OnClose error {ex.Message}");
                }
                ErrorLog.Error($"WebSocket.OnClose error {ex.Message}");
            }
        }

        // Handles WebSocket errors.
        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            if (Constants.EnableDebugging)
            {
                Debug.Print(DebugLevel.Error, $"WebSocket.OnError message {e.Message}");
            }
            ErrorLog.Error($"WebSocket.OnError message {e.Message}");
        }

        // Sends a message to the client.
        public void SendMessage(string message)
        {
            Send(message);
        }

        // Checks if the provided JSON object contains a registration token.
        private bool IsRegistrationToken(JObject json)
        {
            return json["IpId"] != null;
        }
    }
}
