using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using C5Debugger;
using Crestron.SimplSharpPro.UI;
using Newtonsoft.Json;
using Crestron.SimplSharpPro;
using Crestron.SimplSharp;
using SecureWss.Websockets;
using Crestron.SimplSharpPro.Lighting.Din;

namespace SecureWss
{
    public class SystemConfig
    {
        public List<UserInterface> UserInterfaces { get; set; }
        public List<Area> Areas { get; set; }

        public Din1DimU4 din1DimU4 = new Din1DimU4(3, ControlSystem.ThisControlSystem);
        

        // Parameterless constructor
        public SystemConfig()
        {
            Debug.Print(DebugLevel.Debug, "System config constructor called");
            ErrorLog.Notice("DEBUG: System config constructor called");
        }
    }

    public class Area
    {
        public string ID { get; set; }
        public string Label { get; set; }

        public Lighting Lighting { get; set; }
        public Shading Shading { get; set; }

        // Parameterless constructor
        public Area()
        {
            Debug.Print(DebugLevel.Debug, "Area constructor called");
        }
    }


    /// <summary>
    /// Area category classes
    /// </summary>
    public class AreaCategory
    {
        public List<Scene> Scenes { get; set; }
    }

    public class Lighting : AreaCategory
    {
        public List<Load> Loads { get; set; }
    }

    public class Shading : AreaCategory
    {

    }

    /// <summary>
    /// Components
    /// </summary>
    public class Scene
    {
        public string[] Actions { get; set; }
        public string Label { get; set; }
        public void SetScene()
        {
            Debug.Print(DebugLevel.Debug, $"{Label} called");
        }
    }
    public class Load
    {
        public string Label { get; set; }
        public bool Dimmable { get; set; }
        public bool Switched { get; set; }
        public void SetLevel(int level)
        {
            Debug.Print(DebugLevel.Debug, $"{Label} set to {level}");
        }
    }

    /// <summary>
    /// User interface class to utilise the touch panel's native SIP functionality
    /// </summary>
    public class UserInterface
    {
        public string Type { get; }
        public string Label { get; }
        public string Id { get; }
        public uint IpId { get; }
        public object Instance { get; set; }

        // Bool value to get whether there is a compatible registered WebSocket instance
        public bool RegisteredWithWebSocketInstance => IpId > 0 && UIService.Clients?.Find(c => c.IpId == IpId) != null;

        // Gets the registered user WebSocket service that corresponds with this User Interface instance
        public UIService RegisteredWebSocketInstance => RegisteredWithWebSocketInstance ? UIService.Clients?.Find(c => c.IpId == IpId) : null;

        [JsonConstructor]
        public UserInterface(string Type, string Label, string IpId)
        {
            this.Type = Type;
            this.Label = Label;
            this.IpId = !String.IsNullOrEmpty(IpId) ? Convert.ToUInt32(IpId, 16) : 0;

            ErrorLog.Notice("DEBUG: User interface instantiated!");
            

            if (this.IpId > 2)
            {
                switch (this.Type)
                {
                    case "Tsw770":
                        Debug.Print(DebugLevel.Debug, $"Instantiating TSW-770 on ID: {this.IpId}");
                        this.Instance = new Tsw770(this.IpId, ControlSystem.ThisControlSystem);
                        break;
                    case "Tsw1070":
                        Debug.Print(DebugLevel.Debug, $"Instantiating TSW-1070 on ID: {this.IpId}");
                        this.Instance = new Tsw1070(this.IpId, ControlSystem.ThisControlSystem);
                        break;
                    case "Tsw760":
                        Debug.Print(DebugLevel.Debug, $"Instantiating TSW-760 on ID: {this.IpId}");
                        this.Instance = new Tsw760(this.IpId, ControlSystem.ThisControlSystem);
                        break;
                    case "Tsw1060":
                        Debug.Print(DebugLevel.Debug, $"Instantiating TSW-1060 on ID: {this.IpId}");
                        this.Instance = new Tsw1060(this.IpId, ControlSystem.ThisControlSystem);
                        break;
                    case "Tsw560":
                        Debug.Print(DebugLevel.Debug, $"Instantiating TSW-560 on ID: {this.IpId}");
                        this.Instance = new Tsw560(this.IpId, ControlSystem.ThisControlSystem);
                        break;
                    case "Tsw570":
                        Debug.Print(DebugLevel.Debug, $"Instantiating TSW-570 on ID: {this.IpId}");
                        this.Instance = new Tsw570(this.IpId, ControlSystem.ThisControlSystem);
                        break;
                    case "Xpanel":
                        Debug.Print(DebugLevel.Debug, $"Instantiating Xpanel on ID: {this.IpId}");
                        this.Instance = new XpanelForHtml5(this.IpId, ControlSystem.ThisControlSystem);
                        break;                    
                }
                if (this.Instance != null)
                {
                    try
                    {                   
                        if(this.Type != "Xpanel")
                        {
                            // Set up event handler for VOIP extenders
                            ReflectionHelper.SetupEventHandler(
                                this,
                                Instance.GetType().GetProperties().First(p => p.Name == "ExtenderVoipReservedSigs")?.GetValue(Instance),
                                "DeviceExtenderSigChange",
                                typeof(UserInterface),
                                "ExtenderVoipReservedSigs_DeviceExtenderSigChange"
                            );
                        }

                        ReflectionHelper.SetupEventHandler(
                            this,
                            Instance,
                            "OnlineStatusChange",          
                            typeof(UserInterface),             
                            "OnlineStatusChange"
                        );


                        // Register user interface
                        ReflectionHelper.InvokeMethod(this.Instance, "Register");

                        Debug.Print(DebugLevel.Debug, $"Touch panel registered.");


                    }
                    catch (Exception ex)
                    {
                        Debug.Print(DebugLevel.Error, $"Error: {ex.Message}");
                        throw ex;
                    }
                }
            }
        }

        // Event handler to respond to when the user interface connects
        public void OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            Debug.Print(DebugLevel.Debug, $"Ethernet reserved join triggered on touch panel with IP ID {this.IpId}");
            Debug.Print(DebugLevel.Debug, $"args.DeviceOnLine: {args.DeviceOnLine}. Type: {args.DeviceOnLine.GetType()}");

            if (args.DeviceOnLine)
            {
                var stringInputPropertyInfo = Instance.GetType().GetProperty("StringInput");
                Debug.Print(DebugLevel.Debug, $"stringInputPropertyInfo: {stringInputPropertyInfo}");
                var stringInputProperty = stringInputPropertyInfo.GetValue(Instance);
                Debug.Print(DebugLevel.Debug, $"stringInputProperty: {stringInputProperty}");
                // Get the type of the collection
                var arrayType = stringInputProperty.GetType();
                Debug.Print(DebugLevel.Debug, $"arrayType: {arrayType}");

                // Get the "Item" property (indexer)
                PropertyInfo itemProperty = arrayType.GetProperty("Item", new[] { typeof(uint) }); // Try with `uint` or the appropriate type for the index
                Debug.Print(DebugLevel.Debug, $"itemProperty: {itemProperty}");
                if (itemProperty != null)
                {
                    // Access an element, for example at index 0
                    uint index = 1; // Adjust based on the type and index you need
                    var element = itemProperty.GetValue(stringInputProperty, new object[] { index });
                    Debug.Print(DebugLevel.Debug, $"Element at index {index}: {element}");

                    ReflectionHelper.SetPropertyValue(element, "StringValue", "192.168.0.49");

                }
                else
                {
                    Debug.Print(DebugLevel.Debug, "Indexer (Item) not found.");
                }

                /*var methods = arrayType.GetMethods();

                Debug.Print(DebugLevel.Debug, $"methods: {methods}");                
                
                var myMethod = arrayType.GetMember("get_Item");

                Debug.Print(DebugLevel.Debug, $"my method: {myMethod}");


                
                Type type = typeof(Crestron.SimplSharpPro.DeviceStringInputCollection);

                // List all public members to identify how elements are accessed
                foreach (var member in type.GetMembers())
                {
                    Debug.Print(DebugLevel.Debug, $"{member.MemberType}: {member.Name}");
                }*/


            }

            //XpanelForHtml5 xpanelForHtml5 = new XpanelForHtml5(this.IpId, ControlSystem.ThisControlSystem);
            //xpanelForHtml5.StringInput[1].StringValue = "Chicken";

        }

        public void ExtenderVoipReservedSigs_DeviceExtenderSigChange(DeviceExtender currentDeviceExtender, SigEventArgs args)
        {
            //Debug.Print(DebugLevel.Debug, $"VOIP event invoked. Type: {args.Sig.Type}. Number: {args.Sig.Number}");
            switch (args.Sig.Type)
            {
                case eSigType.Bool:
                    switch (args.Sig.Number)
                    {
                        case (uint)eVoipReservedJoins.Incoming: // Call incoming
                            Debug.Print(DebugLevel.Debug, $"Call incoming on panel: {IpId}");
                            RegisteredWebSocketInstance?.SendMessage("Call incoming");
                            break;
                        case (uint)eVoipReservedJoins.CallActive: // Call active
                            Debug.Print(DebugLevel.Debug, $"Call active on panel: {IpId}");
                            RegisteredWebSocketInstance?.SendMessage("Call active");
                            break;
                        case (uint)eVoipReservedJoins.Terminated: // Terminated
                            Debug.Print(DebugLevel.Debug, $"Call terminated on panel: {IpId}");
                            RegisteredWebSocketInstance?.SendMessage("Call terminated");
                            break;
                        case (uint)eVoipReservedJoins.Ringing: // Ringing
                            Debug.Print(DebugLevel.Debug, $"Panel {IpId} ringing");
                            RegisteredWebSocketInstance?.SendMessage("Call ringing");
                            break;
                        case (uint)eVoipReservedJoins.Dialing: // Dialing
                            Debug.Print(DebugLevel.Debug, $"Panel {IpId} dialing");
                            RegisteredWebSocketInstance?.SendMessage("Call dialing");
                            break;
                    }
                    break;

                case eSigType.String:
                    break;
                case eSigType.UShort:
                    break;
            }
        }

        public void Answer()
        {
            try
            {
                Debug.Print(DebugLevel.Debug, $"Panel {IpId} answer.");
                var voipExtenders = ReflectionHelper.GetPropertyValue(this.Instance, "ExtenderVoipReservedSigs");
                ReflectionHelper.InvokeMethod(voipExtenders, "Answer");
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Debug, $"{ex.Message}");
                throw ex;
            }
        }
        public void Hangup()
        {
            try
            {
                Debug.Print(DebugLevel.Debug, $"Panel {IpId} hangup.");
                var voipExtenders = ReflectionHelper.GetPropertyValue(this.Instance, "ExtenderVoipReservedSigs");
                ReflectionHelper.InvokeMethod(voipExtenders, "Hangup");
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Debug, $"{ex.Message}");
                throw ex;
            }
        }
        public void DialString(string str)
        {
            try
            {
                Debug.Print(DebugLevel.Debug, $"Panel {IpId} dial string {str}.");
                var voipExtenders = ReflectionHelper.GetPropertyValue(this.Instance, "ExtenderVoipReservedSigs");
                ReflectionHelper.SetPropertyValue(ReflectionHelper.GetPropertyValue(voipExtenders, "DialString"), "StringValue", str);
            }
            catch(Exception ex)
            {
                Debug.Print(DebugLevel.Debug, $"{ex.Message}");
                throw ex;
            }
        }
    }
    enum eVoipReservedJoins : uint
    {
        Incoming = 27226,
        CallActive = 27224,
        Terminated = 27238,
        Ringing = 27225,
        Dialing = 27222
    }
}
