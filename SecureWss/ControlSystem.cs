using C5Debugger;

using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.UI;

using Org.BouncyCastle.Asn1.X509;
using SecureWss.Websockets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace SecureWss
{

    public static class Constants
    {
        public const int HttpPort = 42080;
        public const int HttpsPort = 42081;
        public const string RootCertName = "rootCert";
        public const bool EnableDebugging = true;
    }

    public class ConsoleCommand
    {
        public string Help { get; set; }
        public Action<string[]> Action { get; set; }
    }

    public class ControlSystem : CrestronControlSystem
    {
        private Dictionary<string, ConsoleCommand> _consoleCommands;
        private Server _websocketServer;
        private Intersystem _intersystemWebsocketServer;
        private const string _certificateName = "clientCert";
        private const string _certificatePassword = "cres12345";
        public static ControlSystem ThisControlSystem;
        public static SystemConfig MySystem;
        public string IpAddress;
        /// <summary>
        /// Database for this program
        /// </summary>
        public static Database Database = new Database();

        /// <summary>
        /// ControlSystem Constructor. Starting point for the SIMPL#Pro program.
        /// Use the constructor to:
        /// * Initialize the maximum number of threads (max = 400)
        /// * Register devices
        /// * Register event handlers
        /// * Add Console Commands
        /// 
        /// Please be aware that the constructor needs to exit quickly; if it doesn't
        /// exit in time, the SIMPL#Pro program will exit.
        /// 
        /// You cannot send / receive data in the constructor
        /// </summary>
        public ControlSystem() : base()
        {
            try
            {
                Thread.MaxNumberOfUserThreads = 20;

                //Subscribe to the controller events (System, Program, and Ethernet)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(_ControllerProgramEventHandler);
                CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(_ControllerEthernetEventHandler);

            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }

        /// <summary>
        /// InitializeSystem - this method gets called after the constructor 
        /// has finished. 
        /// 
        /// Use InitializeSystem to:
        /// * Start threads
        /// * Configure ports, such as serial and verisports
        /// * Start and initialize socket connections
        /// Send initial device configurations
        /// 
        /// Please be aware that InitializeSystem needs to exit quickly also; 
        /// if it doesn't exit in time, the SIMPL#Pro program will exit.
        /// </summary>
        public override void InitializeSystem()
        {
            try
            {
                var ipAddress = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, 0);
                IpAddress = ipAddress;
                // Create a secure WebSocket server on port 42081 and unsecure WebSocket server on port 42080
                _websocketServer = new Server(Constants.HttpPort, Constants.HttpsPort, $"\\user\\{_certificateName}.pfx", _certificatePassword, @"\user\html");
                _intersystemWebsocketServer = new Intersystem(42089);

                ControlSystem.ThisControlSystem = this;
                Debug.Name = "WebSocket Secure Test";
                Debug.DebugMessage += CrestronConsole.PrintLine;
                //Debug.ErrorMessage += (message) => { };  //Only use this if you want to send a message to the UI's
                Debug.Enabled = true;
                Debug.Levels.Insert(0, DebugLevel.All);

                ErrorLog.Notice($"DEBUG: Initialize system...");

                _consoleCommands = new Dictionary<string, ConsoleCommand>()
                {
                    { "getcommands", new ConsoleCommand() { Action = ConsoleGetCommands,    Help = "Lists all commands used by the system" } },
                    { "debug", new ConsoleCommand()       { Action = ConsoleDebug,          Help = "Toggles debug actions, blank to report current debug types." } },
                    { "createcert", new ConsoleCommand()  { Action = CreateCert,            Help = "Creates a SelfSigned Certificate." } },
                    { "startserver", new ConsoleCommand() { Action = StartServer,           Help = "Lists devices in the specified collection" } },
                    { "x509list", new ConsoleCommand()    { Action = ConsoleX509List,       Help = "Lists all X509 Certificates on the system giving the ID (1-8)...hopefully.\rExample wss:[id] X509List 1" } },
                };
                CrestronConsole.AddNewConsoleCommand(ConsoleCommandProcessor, "wss", "", ConsoleAccessLevelEnum.AccessOperator);

                // Task for HTTP and WebSocket setup
                Task.Run(() =>
                {
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, "Creating certificate...");
                    CreateCert(null);
                    _websocketServer.Start();
                    _intersystemWebsocketServer.Start();
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, "Certificate and websocket task complete");

                });

                // Task for JSON config deserialization
                Task.Run(() =>
                {
                    Debug.Print(DebugLevel.Debug, $"Deserializing JSON...");
                    var json = File.ReadAllText(@"\user/system.json");
                    if (json == null)
                    {
                        Debug.Print(DebugLevel.Error, $"No config.json file found");
                        ErrorLog.Notice($"DEBUG: No config.json file found");
                        throw new Exception("No config.json file found");
                    }

                    //var system = JsonSerializer.Deserialize<System>(json);
                    ErrorLog.Notice($"DEBUG: Instantiate SystemConfig object...");
                    ControlSystem.MySystem = JsonConvert.DeserializeObject<SystemConfig>(json);
                    Debug.Print(DebugLevel.Debug, $"Deserialization complete.");
                    if (ControlSystem.MySystem != null)
                    {
                        Debug.Print(DebugLevel.Debug, $"User Interfaces:");
                        foreach (UserInterface userInterface in ControlSystem.MySystem.UserInterfaces)
                        {
                            Debug.Print(DebugLevel.Debug, $"{userInterface.Label}");
                        }
                        Debug.Print(DebugLevel.Debug, $"Areas:");
                        foreach (Area area in ControlSystem.MySystem.Areas)
                        {
                            Debug.Print(DebugLevel.Debug, $"{area.Label}");
                        }
                    }
                });

                Database.OnStateChange += Database_OnStateChange;
            }
            catch (Exception e)
            {
                ErrorLog.Error($"Error in InitializeSystem: {e.Message}");
            }
        }

        /// <summary>
        /// Event handler to broadcast data to the user interfaces when this system's database state changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Database_OnStateChange(object sender, StateChangeEventArgs e)
        {
            UIService.BroadcastData(JsonConvert.SerializeObject(e.Data));
        }

        private void ConsoleCommandProcessor(string arg)
        {
            Task.Run(() =>
            {
                try
                {
                    string[] args = arg.Split(' ');
                    if (args.Length > 0)
                    {
                        if (_consoleCommands.TryGetValue(args[0].ToLower(), out ConsoleCommand command))
                            command.Action.Invoke(args.Skip(1).ToArray());
                        else
                            if(Constants.EnableDebugging) Debug.Print(DebugLevel.Error, $"No such command: {args[0]} found. Use wss GetCommands to see a list of supported commands.");
                    }
                    else
                        if (Constants.EnableDebugging)  Debug.Print(DebugLevel.Debug, $"Please supply a command");
                }
                catch (Exception ex)
                {
                    if (Constants.EnableDebugging)  Debug.Print(DebugLevel.Error, $"CommandProcessor Unknown Error: {ex.Message}");
                }
            });
        }

        private void ConsoleGetCommands(string[] args)
        {
            foreach (string key in _consoleCommands.Keys)
            {
                if (key != "getcommands")
                    Debug.Print(DebugLevel.Console, $"{key}: {_consoleCommands[key].Help}\r");
            }
        }
        private void ConsoleDebug(string[] args)
        {
            string debugLevelReport = string.Empty;
            if (args != null)
            {
                foreach (string command in args)
                {
                    switch (command.ToLower())
                    {
                        case "on":
                        case "enable":
                            Debug.Enabled = true;
                            Debug.Print(DebugLevel.Console, "{0}", Debug.Enabled ? "Enabled" : "Disabled");
                            break;
                        case "off":
                        case "disable":
                            Debug.Levels.Clear();
                            Debug.Enabled = false;
                            break;
                        default:
                            if (Enum.TryParse(command, true, out DebugLevel level))
                            {
                                if (Debug.Levels.Contains(level))
                                    Debug.Levels.Remove(level);
                                else
                                {
                                    if (level == DebugLevel.All)
                                        Debug.Levels.Insert(0, level);
                                    else
                                        Debug.Levels.Add(level);
                                }
                            }
                            break;
                    }
                }
            }
            if (Debug.Levels.Count > 0)
            {
                debugLevelReport = Debug.Levels[0].ToString();
                for (int i = 1; i < Debug.Levels.Count; i++)
                    debugLevelReport = $"{debugLevelReport}, {Debug.Levels[i]}";
            }
            Debug.Print(DebugLevel.Console, "Status: {0}", Debug.Enabled ? debugLevelReport : "Disabled");
        }

        private void CreateCert(string[] args)
        {
            try
            {
                var ipAddress = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, 0);
                var hostName = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_HOSTNAME, 0);
                var domainName = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_DOMAIN_NAME, 0);

                Debug.Print($"DomainName: {domainName} | HostName: {hostName} | {hostName}.{domainName}@{ipAddress}");
                Debug.Print($"CreateCert Creating Utility");
                var utility = new BouncyCertificate($"CN={hostName}.{domainName}", new[] { $"{hostName}.{domainName}", ipAddress }, @"\user\", _certificateName, _websocketServer);

                utility.CertificatePassword = _certificatePassword;
                utility.CreateAndWriteCertificates($"CN={hostName}.{domainName}", new[] { $"{hostName}.{domainName}", ipAddress }, @"\user\", _certificateName, _websocketServer);
                utility.CheckCertificates();
                Debug.Print($"CreateCert Ending CreateCert");

            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Debug, $"WSS CreateCert Failed\r\n{ex.Message}\r\n{ex.StackTrace}");
                ErrorLog.Error($"WSS CreateCert Failed\r\n{ex.Message}\r\n{ex.StackTrace}");
            }
        }

        private void StartServer(string[] args)
        {
            if (_websocketServer.HttpsIsRunning) _websocketServer.Stop(Constants.HttpsPort);
            if(_websocketServer.HttpIsRunning) _websocketServer.Stop(Constants.HttpPort);

            if (args.Length > 0 && args[0].Equals("secure", StringComparison.OrdinalIgnoreCase))
                _websocketServer.Start();
            else
                _websocketServer.Start();
        }
        private void ConsoleX509List(string[] args)
        {
            if (args.Length < 1)
            {
                Debug.Print(DebugLevel.Error, "Invalid Argument count, please use the following help information\r{0}", _consoleCommands["x509list"].Help);
                return;
            }
            if (!int.TryParse(args[0], out int storeId))
            {
                Debug.Print(DebugLevel.Error, "Invalid Argument, please use the following help information\r{0}", _consoleCommands["x509list"].Help);
                return;
            }
            if (storeId < 1 || storeId > 8)
            {
                Debug.Print(DebugLevel.Error, "Invalid Argument, please use the following help information\r{0}", _consoleCommands["x509list"].Help);
                return;
            }
            X509Store store = null;
            try
            {
                Debug.Print(DebugLevel.Debug, "Creating Store");
                store = new X509Store((StoreName)storeId, StoreLocation.CurrentUser);
                Debug.Print(DebugLevel.Debug, "Opening Store");
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                Debug.Print(DebugLevel.Debug, "Getting Collection");
                X509Certificate2Collection collection = store.Certificates;
                Debug.Print(DebugLevel.Debug, "Narrowing Collection of {0:D}", collection.Count);
                X509Certificate2Collection fCollection = collection.Find(X509FindType.FindByTimeValid, DateTime.Now, false);
                //X509Certificate2Collection sCollection = X509Certificate2UI.SelectFromCollection(fCollection, "Test Certificate Select", "Select a certificate from the following list to get information on that certificate", X509SelectionFlag.MultiSelection);
                Debug.Print(DebugLevel.Debug, "Number of certificates: {0}", fCollection.Count);

                foreach (X509Certificate2 x509 in fCollection)
                {
                    try
                    {
                        byte[] rawData = x509.RawData;
                        Debug.Print(DebugLevel.Debug, "Content Type: {0}", X509Certificate2.GetCertContentType(rawData));
                        Debug.Print(DebugLevel.Debug, "Friendly Name: {0}", x509.FriendlyName);
                        Debug.Print(DebugLevel.Debug, "Certificate Verified?: {0}", x509.Verify());
                        Debug.Print(DebugLevel.Debug, "Simple Name: {0}", x509.GetNameInfo(X509NameType.SimpleName, true));
                        Debug.Print(DebugLevel.Debug, "Signature Algorithm: {0}", x509.SignatureAlgorithm.FriendlyName);
                        Debug.Print(DebugLevel.Debug, "Public Key: {0}", x509.PublicKey.Key.ToXmlString(false));
                        Debug.Print(DebugLevel.Debug, "Certificate Archived?: {0}", x509.Archived);
                        Debug.Print(DebugLevel.Debug, "Length of Raw Data: {0}", x509.RawData.Length);
                        //X509Certificate2UI.DisplayCertificate(x509);
                        x509.Reset();
                    }
                    catch (CryptographicException)
                    {
                        Debug.Print(DebugLevel.Debug, "Information could not be written out for this certificate.");
                    }
                    catch (Exception ex)
                    {
                        Debug.Print(DebugLevel.Error, "X509List Collection error: {0}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, "X509List error: {0}", ex.Message);
            }

            if (store != null)
            {
                store.Close();
                store.Dispose();
            }
        }

        /// <summary>
        /// Event Handler for Ethernet events: Link Up and Link Down. 
        /// Use these events to close / re-open sockets, etc. 
        /// </summary>
        /// <param name="ethernetEventArgs">This parameter holds the values 
        /// such as whether it's a Link Up or Link Down event. It will also indicate 
        /// wich Ethernet adapter this event belongs to.
        /// </param>
        void _ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            switch (ethernetEventArgs.EthernetEventType)
            {//Determine the event type Link Up or Link Down
                case (eEthernetEventType.LinkDown):
                    //Next need to determine which adapter the event is for. 
                    //LAN is the adapter is the port connected to external networks.
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                        //
                    }
                    break;
                case (eEthernetEventType.LinkUp):
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {

                    }
                    break;
            }
        }

        /// <summary>
        /// Event Handler for Programmatic events: Stop, Pause, Resume.
        /// Use this event to clean up when a program is stopping, pausing, and resuming.
        /// This event only applies to this SIMPL#Pro program, it doesn't receive events
        /// for other programs stopping
        /// </summary>
        /// <param name="programStatusEventType"></param>
        void _ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
        {
            switch (programStatusEventType)
            {
                case (eProgramStatusEventType.Paused):
                    //The program has been paused.  Pause all user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Resumed):
                    //The program has been resumed. Resume all the user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Stopping):
                    //The program has been stopped.
                    //Close all threads. 
                    //Shutdown all Client/Servers in the system.
                    //General cleanup.
                    //Unsubscribe to all System Monitor events
                    break;
            }
        }

        /// <summary>
        /// Event Handler for system events, Disk Inserted/Ejected, and Reboot
        /// Use this event to clean up when someone types in reboot, or when your SD /USB
        /// removable media is ejected / re-inserted.
        /// </summary>
        /// <param name="systemEventType"></param>
        void _ControllerSystemEventHandler(eSystemEventType systemEventType)
        {
            switch (systemEventType)
            {
                case (eSystemEventType.DiskInserted):
                    //Removable media was detected on the system
                    break;
                case (eSystemEventType.DiskRemoved):
                    //Removable media was detached from the system
                    break;
                case (eSystemEventType.Rebooting):
                    //The system is rebooting. 
                    //Very limited time to preform clean up and save any settings to disk.
                    break;
            }

        }
    }
}