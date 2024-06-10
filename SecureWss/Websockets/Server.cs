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
        public bool IsRunning { get => _httpsServer?.IsListening ?? false; }//_wsServer?.IsListening ?? false; }

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
                    this._httpServer.Stop();
                    this._httpServer.Start();
                    Debug.Print(DebugLevel.Debug, "HTTP server restarted.");
                    break;
                case Constants.HttpsPort:
                    Debug.Print(DebugLevel.Debug, "Restarting HTTPS server...");
                    this._httpsServer.Stop();
                    this._httpsServer.Start();
                    Debug.Print(DebugLevel.Debug, "HTTPS server restarted.");
                    break;
                default:
                    Debug.Print(DebugLevel.Debug, "Restarting HTTP and HTTPS servers...");
                    this._httpServer.Stop();
                    this._httpsServer.Stop();
                    this._httpServer.Start();
                    this._httpsServer.Start();
                    Debug.Print(DebugLevel.Debug, "HTTP and HTTPS servers restarted.");
                    break;
            }
        }

        public void Start(int httpPort, int httpsPort, string certPath = "", string certPassword = "", string rootPath = @"\html")
        {
            try
            {
                // Unsecure HTTP Server
                Debug.Print(DebugLevel.WebSocket, $"Creating HTTP Server from directory {rootPath}");
                _httpServer = new HttpServer(httpPort)
                {
                    RootPath = rootPath
                };
                ConfigureServer(_httpServer, rootPath);

                // Secure HTTPS Server
                Debug.Print(DebugLevel.WebSocket, $"Creating HTTPS Server from directory {rootPath}");
                _httpsServer = new HttpServer(httpsPort, true)
                {
                    RootPath = rootPath
                };

                Debug.Print($"RootPath = {_httpsServer.RootPath}");
                if (!string.IsNullOrWhiteSpace(certPath)) 
                {
                    Debug.Print(DebugLevel.WebSocket, "Assigning SSL Configuration");
                    _httpsServer.SslConfiguration = new ServerSslConfiguration(new X509Certificate2(certPath, certPassword))
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
                ConfigureServer(_httpsServer, rootPath);

                // Start both servers
                _httpServer.Start();
                _httpsServer.Start();

                Debug.Print(DebugLevel.WebSocket, $"HTTP server ready at ws://localhost:{httpPort}/echo");
                Debug.Print(DebugLevel.WebSocket, $"HTTPS server ready at wss://localhost:{httpsPort}/echo");

             
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
            server.AddWebSocketService<EchoService>("/echo");
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


        public void Stop()
        {
            Debug.Print(DebugLevel.Debug, "Stopping web servers...");
            _httpServer?.Stop();
            _httpsServer?.Stop();

            _httpServer = null;
            _httpsServer = null;
            Debug.Print(DebugLevel.Debug, "Web servers stopped.");
        }

    }
    /// <summary>
    /// Basic echo service
    /// </summary>
    public class EchoService : WebSocketBehavior
    {
        public EchoService()
        {
            try
            {
                Debug.Print(DebugLevel.WebSocket, "Echo Service Created");
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, "Websocket.Constructor error {0}", ex.Message);
            }
        }

        protected override void OnOpen()
        {
            try
            {
                base.OnOpen();
                Debug.Print(DebugLevel.WebSocket, $"New Client Connected: {ID}");
                
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, "Websocket.OnOpen error {0}", ex.Message);
            }
        }
        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {                
                var received = e.Data;
                Debug.Print(DebugLevel.WebSocket, $"EchoService Received {received} and echoing back");
                Send(received);
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, "WebSocket.OnMessage error {0}", ex.Message);
            }
        }
        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            Debug.Print(DebugLevel.Error, "WebSocket.OnError message {0}", e.Message);
        }
    }

    /// <summary>
    /// Crestron Websocket service for message transmission
    /// </summary>
    public class CrestronService : WebSocketBehavior
    {
        public CrestronService()
        {
            try
            {
                Debug.Print(DebugLevel.WebSocket, "Crestron Websocket Service Created");
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, "Websocket.Constructor error {0}", ex.Message);
            }
        }

        protected override void OnOpen()
        {
            try
            {
                base.OnOpen();
                Debug.Print(DebugLevel.WebSocket, $"New Client Connected: {ID}");

            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, "Websocket.OnOpen error {0}", ex.Message);
            }
        }
        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                var received = e.Data;
                Debug.Print(DebugLevel.WebSocket, $"EchoService Received {received} and echoing back");
                Send(received);
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, "WebSocket.OnMessage error {0}", ex.Message);
            }
        }
        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            Debug.Print(DebugLevel.Error, "WebSocket.OnError message {0}", e.Message);
        }
    }
}
