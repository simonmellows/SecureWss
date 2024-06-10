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


namespace SecureWss
{
    public class SystemConfig
    {
        public List<UserInterface> UserInterfaces { get; set; }
        public List<Area> Areas { get; set; }

        // Parameterless constructor
        public SystemConfig()
        {
            Debug.Print(DebugLevel.Debug, "System config constructor called");
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


        [JsonConstructor]
        public UserInterface(string Type, string Label, string IpId)
        {
            this.Type = Type;
            this.Label = Label;
            this.IpId = Convert.ToUInt32(IpId, 16);

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
                }
                if (this.Instance != null)
                {
                    try
                    {
                        Debug.Print(DebugLevel.Debug, $"Creating delegate for instance: {this.Instance.GetType()}");
                        // Get VOIP extender property info
                        var voipExtenders = this.Instance
                            .GetType()
                            .GetProperties().First(p => p.Name == "ExtenderVoipReservedSigs")
                            .GetValue(this.Instance);
                        // Use VOIP Extenders
                        ReflectionHelper.InvokeMethod(voipExtenders, "Use");

                        // Get event info
                        EventInfo e = voipExtenders
                            .GetType()
                            .GetEvent("DeviceExtenderSigChange");
                        Debug.Print(DebugLevel.Debug, $"VOIP extender event info: {e}");

                        // Get method info
                        MethodInfo m = typeof(UserInterface)
                            .GetMethod("ExtenderVoipReservedSigs_DeviceExtenderSigChange");
                        Debug.Print(DebugLevel.Debug, $"VOIP extender method info: {m}");

                        // Create delegate
                        Delegate d = Delegate.CreateDelegate(e.EventHandlerType, this, m);
                        Debug.Print(DebugLevel.Debug, $"Delegate created for: {d.Target} with method: {d.Method}");

                        // Attach event handler to event
                        e.AddEventHandler(voipExtenders, d);
                        Debug.Print(DebugLevel.Debug, $"Event handler set up for VOIP on panel ID: {this.Id}");

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
                            break;
                        case (uint)eVoipReservedJoins.CallActive: // Call active
                            Debug.Print(DebugLevel.Debug, $"Call active on panel: {IpId}");
                            break;
                        case (uint)eVoipReservedJoins.Terminated: // Terminated
                            Debug.Print(DebugLevel.Debug, $"Call terminated on panel: {IpId}");
                            break;
                        case (uint)eVoipReservedJoins.Ringing: // Ringing
                            Debug.Print(DebugLevel.Debug, $"Panel {IpId} ringing");
                            break;
                        case (uint)eVoipReservedJoins.Dialing: // Dialing
                            Debug.Print(DebugLevel.Debug, $"Panel {IpId} dialing");
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
            var voipExtenders = ReflectionHelper.GetPropertyValue(this.Instance, "ExtenderVoipReservedSigs");
            ReflectionHelper.InvokeMethod(voipExtenders, "Answer");
        }
        public void DialString(string str)
        {
            var voipExtenders = ReflectionHelper.GetPropertyValue(this.Instance, "ExtenderVoipReservedSigs");
            ReflectionHelper.SetPropertyValue(ReflectionHelper.GetPropertyValue(voipExtenders, "DialString"), "StringValue", str);
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
