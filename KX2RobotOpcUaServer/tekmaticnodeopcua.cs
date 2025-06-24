using System;
using System.Collections.Generic;
using System.Threading;
using Opc.Ua;
using Opc.Ua.Server;
using LabEquipmentOpcUa;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Text;
using InhecoMTCdll;

namespace TekmaticOpcUa
{
    /// <summary>
    /// Represents a discovered Tekmatic device
    /// </summary>
    public class TekmaticDevice
    {
        /// <summary>
        /// The name of the device (e.g., "Teleshake95 AC")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The slot ID (1-6) where the device is connected
        /// </summary>
        public int SlotId { get; set; }

        /// <summary>
        /// Whether a device is present in this slot
        /// </summary>
        public bool HasDevice { get; set; }

        /// <summary>
        /// Creates a string representation of the device
        /// </summary>
        public override string ToString()
        {
            if (!HasDevice)
                return $"Slot {SlotId}: No Device";

            return $"Slot {SlotId}: {Name}";
        }
    }

    /// <summary>
    /// Tekmatic Control class for communicating with Tekmatic devices using InhecoMTCdll
    /// </summary>
    public class TekmaticControl
    {
        // List of discovered devices (one per slot)
        private List<TekmaticDevice> _discoveredDevices = new List<TekmaticDevice>();

        // Inheco MTC controller
        private GlobCom _inhecoController;

        // DIP switch ID for the device (default to 0)
        private int _deviceId = 0;

        // Constructor
        public TekmaticControl()
        {
            // Initialize the Inheco controller
            _inhecoController = new GlobCom();

            // Initialize any required resources
            InitializeSettings();
        }

        /// <summary>
        /// Initializes the settings
        /// </summary>
        private void InitializeSettings()
        {
            try
            {
                // Create a configuration file path where settings can be stored
                string configPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                configPath = Path.Combine(configPath, "TekmaticOpcUa");

                if (!Directory.Exists(configPath))
                {
                    Directory.CreateDirectory(configPath);
                }

                string configFile = Path.Combine(configPath, "TekmaticOpcUa_Config.xml");

                if (!File.Exists(configFile))
                {
                    // Create an empty file
                    using (FileStream fs = File.Create(configFile))
                    {
                        // Just create the file
                    }
                }

                Console.WriteLine($"Using configuration file: {configFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a command to the device and returns the response
        /// </summary>
        private string SendCommand(string command)
        {
            try
            {
                // Check if controller is initialized before trying to send a command
                if (_inhecoController == null)
                {
                    Console.WriteLine($"Cannot send command '{command}': Controller not initialized");
                    return string.Empty;
                }

                // Send the command
                Console.WriteLine($"Sending command: {command}");
                _inhecoController.WriteOnly(command);

                // Wait for the device to process the command
                Thread.Sleep(1000);

                // Read the response
                string response = _inhecoController.ReadSync();
                Console.WriteLine($"Received response: {response}");

                // Wait before allowing another command to be sent
                Thread.Sleep(1000);

                return response;
            }
            catch (IndexOutOfRangeException ex)
            {
                Console.WriteLine($"Error sending command '{command}': Index was outside the bounds of the array");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending command '{command}': {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Sends a command to the device silently (without logging) and returns the response
        /// Used primarily for background refresh operations
        /// </summary>
        private string SendCommandSilent(string command)
        {
            try
            {
                // Check if controller is initialized before trying to send a command
                if (_inhecoController == null)
                {
                    return string.Empty;
                }

                // Send the command (no logging)
                _inhecoController.WriteOnly(command);

                // Wait for the device to process the command
                Thread.Sleep(1000);

                // Read the response (no logging)
                string response = _inhecoController.ReadSync();

                // Wait before allowing another command to be sent
                Thread.Sleep(1000);

                return response;
            }
            catch (Exception ex)
            {
                // Silent exception handling
                return string.Empty;
            }
        }

        /// <summary>
        /// Initializes the Tekmatic control
        /// </summary>
        /// <returns>True if initialization was successful, false otherwise</returns>
        public bool Initialize()
        {
            try
            {
                Console.WriteLine("Initializing Tekmatic control...");

                // Find the device with the specified DIP switch ID
                int result = _inhecoController.FindTheUniversalControl(_deviceId);
                if (result == 1)
                {
                    Console.WriteLine($"Tekmatic device found with ID: {_deviceId}");

                    // Discover devices
                    DiscoverDevices();

                    Console.WriteLine("Tekmatic control initialized successfully.");
                    return true;
                }
                else
                {
                    Console.WriteLine($"Failed to find Tekmatic device with ID: {_deviceId}");
                    // Instead of throwing an exception, return false to indicate initialization failed
                    // but allow the server to continue running
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Tekmatic control: {ex.Message}");
                // Return false instead of re-throwing the exception
                return false;
            }
        }

        /// <summary>
        /// Shuts down the Tekmatic control
        /// </summary>
        public void Shutdown()
        {
            try
            {
                Console.WriteLine("Shutting down Tekmatic control...");

                // No explicit shutdown method in the DLL, but we can set variables to null
                _inhecoController = null;

                Console.WriteLine("Tekmatic control shut down successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error shutting down Tekmatic control: {ex.Message}");
            }
        }

        /// <summary>
        /// Discovers available Tekmatic devices
        /// </summary>
        /// <returns>Number of devices discovered</returns>
        public int DiscoverDevices()
        {
            try
            {
                Console.WriteLine("Discovering Tekmatic devices...");

                // Clear previous discoveries
                _discoveredDevices.Clear();

                // Get firmware version to check communication
                string response = SendCommand("0RFV1");
                if (string.IsNullOrEmpty(response))
                {
                    Console.WriteLine("Failed to communicate with the device");
                    return 0;
                }

                Console.WriteLine($"Device firmware: {response}");

                // Create a device object for each slot (1-6)
                for (int slot = 1; slot <= 6; slot++)
                {
                    var device = new TekmaticDevice
                    {
                        SlotId = slot,
                        HasDevice = false,
                        Name = "No Device"
                    };

                    // Check if a device is present in this slot
                    string typeResponse = SendCommand($"{slot}RTD");
                    if (!string.IsNullOrEmpty(typeResponse) && typeResponse.Length > 5)
                    {
                        int typeCode = 0;
                        if (int.TryParse(typeResponse.Substring(5), out typeCode))
                        {
                            // Device found in this slot
                            device.HasDevice = true;
                            device.Name = GetDeviceTypeName(typeCode);

                            Console.WriteLine($"  - Slot {slot}: {device.Name}");
                        }
                        else
                        {
                            Console.WriteLine($"  - Slot {slot}: No Device");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  - Slot {slot}: No Slot Module");
                        device.Name = "No Slot Module";
                    }

                    _discoveredDevices.Add(device);
                }

                return _discoveredDevices.Count(d => d.HasDevice);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error discovering devices: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Helper method to convert device type code to name
        /// </summary>
        private string GetDeviceTypeName(int typeCode)
        {
            switch (typeCode)
            {
                case 0: return "Thermoshake";
                case 1: return "CPAC";
                case 2: return "Teleshake";
                case 12: return "Thermoshake AC";
                case 13: return "Teleshake AC";
                case 14: return "Teleshake95 AC";
                default: return $"Unknown ({typeCode})";
            }
        }

        /// <summary>
        /// Gets the list of discovered devices
        /// </summary>
        public List<TekmaticDevice> GetDiscoveredDevices()
        {
            return _discoveredDevices;
        }

        /// <summary>
        /// Gets a device by slot ID
        /// </summary>
        /// <param name="slotId">The slot ID (1-6) to get the device for</param>
        /// <returns>The device in the specified slot, or null if no device is present</returns>
        public TekmaticDevice GetDeviceBySlot(int slotId)
        {
            if (slotId < 1 || slotId > 6)
                return null;

            return _discoveredDevices.FirstOrDefault(d => d.SlotId == slotId);
        }

        /// <summary>
        /// Checks if a device is present in the specified slot
        /// </summary>
        /// <param name="slotId">The slot ID (1-6) to check</param>
        /// <returns>True if a device is present, false otherwise</returns>
        public bool HasDeviceInSlot(int slotId)
        {
            if (slotId < 1 || slotId > 6)
                return false;

            var device = _discoveredDevices.FirstOrDefault(d => d.SlotId == slotId);
            return device != null && device.HasDevice;
        }

        /// <summary>
        /// Gets the device in the specified slot
        /// </summary>
        /// <param name="slotId">The slot ID (1-6) to get the device for</param>
        /// <returns>The device in the specified slot, or null if no device is present</returns>
        public TekmaticDevice GetDeviceInSlot(int slotId)
        {
            if (slotId < 1 || slotId > 6)
                return null;

            return _discoveredDevices.FirstOrDefault(d => d.SlotId == slotId);
        }



        /// <summary>
        /// Controls the temperature for the specified slot by setting the target temperature and enabling/disabling temperature control
        /// </summary>
        /// <param name="slotId">The slot ID (1-6) to control</param>
        /// <param name="temperature">The target temperature in degrees Celsius</param>
        /// <param name="enable">True to enable temperature control, false to disable</param>
        /// <returns>0 for success, -1 for failure</returns>
        public int ControlTemperature(int slotId, double temperature, bool enable)
        {
            try
            {
                if (slotId < 1 || slotId > 6)
                    return -1;

                Console.WriteLine($"Controlling temperature for slot {slotId}: {temperature}°C, {(enable ? "enabled" : "disabled")}");

                // Always set the target temperature
                // Convert to 1/10 °C for the command
                int tempValue = (int)(temperature * 10);
                string tempCommand = $"{slotId}STT{tempValue}";

                // Check if controller is initialized
                if (_inhecoController == null)
                {
                    Console.WriteLine($"Cannot send command '{tempCommand}': Controller not initialized");
                    return -1;
                }

                // Send the temperature command
                Console.WriteLine($"Sending command: {tempCommand}");
                _inhecoController.WriteOnly(tempCommand);

                // Wait for the device to process the command
                Thread.Sleep(1000);

                // Read the response
                string tempResponse = _inhecoController.ReadSync();
                Console.WriteLine($"Received response: {tempResponse}");

                // Wait before allowing another command to be sent
                Thread.Sleep(1000);

                if (string.IsNullOrEmpty(tempResponse) || !tempResponse.StartsWith($"{slotId}stt"))
                {
                    Console.WriteLine($"Failed to set target temperature for slot {slotId}: {tempResponse}");
                    return -1;
                }

                // Then enable/disable temperature control
                string enableCommand = $"{slotId}ATE{(enable ? "1" : "0")}";

                // Check if controller is initialized (again, in case it became null between commands)
                if (_inhecoController == null)
                {
                    Console.WriteLine($"Cannot send command '{enableCommand}': Controller not initialized");
                    return -1;
                }

                // Send the enable/disable command
                Console.WriteLine($"Sending command: {enableCommand}");
                _inhecoController.WriteOnly(enableCommand);

                // Wait for the device to process the command
                Thread.Sleep(1000);

                // Read the response
                string enableResponse = _inhecoController.ReadSync();
                Console.WriteLine($"Received response: {enableResponse}");

                // Wait before allowing another command to be sent
                Thread.Sleep(1000);

                if (string.IsNullOrEmpty(enableResponse) || !enableResponse.StartsWith($"{slotId}ate"))
                {
                    Console.WriteLine($"Failed to {(enable ? "enable" : "disable")} temperature control for slot {slotId}: {enableResponse}");
                    return -1;
                }

                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error controlling temperature for slot {slotId}: {ex.Message}");
                return -1; // Error
            }
        }


        /// <summary>
        /// Controls shaking for the specified slot by setting the RPM and enabling/disabling shaking
        /// </summary>
        /// <param name="slotId">The slot ID (1-6) to control</param>
        /// <param name="rpm">The target shaking RPM</param>
        /// <param name="enable">True to enable shaking, false to disable</param>
        /// <returns>0 for success, -1 for failure</returns>
        public int ControlShaking(int slotId, int rpm, bool enable)
        {
            try
            {
                if (slotId < 1 || slotId > 6)
                    return -1;

                Console.WriteLine($"Controlling shaking for slot {slotId}: {rpm} RPM, {(enable ? "enabled" : "disabled")}");

                // Always set the target RPM
                string rpmCommand = $"{slotId}SSR{rpm}";

                // Check if controller is initialized
                if (_inhecoController == null)
                {
                    Console.WriteLine($"Cannot send command '{rpmCommand}': Controller not initialized");
                    return -1;
                }

                // Send the RPM command
                Console.WriteLine($"Sending command: {rpmCommand}");
                _inhecoController.WriteOnly(rpmCommand);

                // Wait for the device to process the command
                Thread.Sleep(1000);

                // Read the response
                string rpmResponse = _inhecoController.ReadSync();
                Console.WriteLine($"Received response: {rpmResponse}");

                // Wait before allowing another command to be sent
                Thread.Sleep(1000);

                if (string.IsNullOrEmpty(rpmResponse) || !rpmResponse.StartsWith($"{slotId}ssr"))
                {
                    Console.WriteLine($"Failed to set shaking RPM for slot {slotId}: {rpmResponse}");
                    return -1;
                }

                // Then enable/disable shaking
                string enableCommand = $"{slotId}ASE{(enable ? "1" : "0")}";

                // Check if controller is initialized (again, in case it became null between commands)
                if (_inhecoController == null)
                {
                    Console.WriteLine($"Cannot send command '{enableCommand}': Controller not initialized");
                    return -1;
                }

                // Send the enable/disable command
                Console.WriteLine($"Sending command: {enableCommand}");
                _inhecoController.WriteOnly(enableCommand);

                // Wait for the device to process the command
                Thread.Sleep(1000);

                // Read the response
                string enableResponse = _inhecoController.ReadSync();
                Console.WriteLine($"Received response: {enableResponse}");

                // Wait before allowing another command to be sent
                Thread.Sleep(1000);

                if (string.IsNullOrEmpty(enableResponse) || !enableResponse.StartsWith($"{slotId}ase"))
                {
                    Console.WriteLine($"Failed to {(enable ? "enable" : "disable")} shaking for slot {slotId}: {enableResponse}");
                    return -1;
                }


                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error controlling shaking for slot {slotId}: {ex.Message}");
                return -1; // Error
            }
        }




        /// <summary>
        /// Check if we can communicate with the device
        /// </summary>
        /// <returns>True if we can communicate with the device, false otherwise</returns>
        public bool IsConnected()
        {
            try
            {
                // Check if controller is initialized
                if (_inhecoController == null)
                    return false;

                // Try to get firmware version to check communication
                string response = SendCommandSilent("0RFV1");
                return !string.IsNullOrEmpty(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking connection status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get error codes from the device
        /// </summary>
        public string GetErrorCodes()
        {
            try
            {
                // Get the error codes
                string response = SendCommand("0REC");
                if (!string.IsNullOrEmpty(response) && response.Length > 5)
                {
                    return response.Substring(5);
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting error codes: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Clear error codes on the device
        /// </summary>
        public int ClearErrorCodes()
        {
            try
            {
                Console.WriteLine("Clearing error codes...");

                // Clear the error codes
                string response = SendCommand("0CEC");
                if (string.IsNullOrEmpty(response) || !response.StartsWith("0cec"))
                {
                    Console.WriteLine($"Failed to clear error codes: {response}");
                    return -1;
                }

                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing error codes: {ex.Message}");
                return -1; // Error
            }
        }

        /// <summary>
        /// Executes a raw command directly on the device
        /// </summary>
        /// <param name="command">The full command string including slot ID prefix (e.g., '1SSR300')</param>
        /// <returns>The response from the device</returns>
        public string ExecuteRawCommand(string command)
        {
            try
            {
                Console.WriteLine($"Executing raw command: {command}");

                // Send the command directly to the device
                return SendCommand(command);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing raw command '{command}': {ex.Message}");
                return $"Error: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Tekmatic Node Manager for OPC UA Server
    /// </summary>
    public class TekmaticNodeManager : CustomNodeManager2, IEquipmentNodeManager, INodeManagerFactory
    {
        private const int NORMAL_INTERVAL_MS = 1000;
        private const int BACKOFF_INTERVAL_MS = 30000; // 30 seconds
        private const int MAX_CONSECUTIVE_FAILURES = 3;

        // Private fields
        private TekmaticControl _tekmatic;
        private ushort _namespaceIndex;
        private uint _lastUsedId;
        private Timer _updateTimer;

        // Connection state tracking
        private int _connectionFailureCount = 0;
        private DateTime _lastConnectionAttemptTime = DateTime.MinValue;
        private bool _isInBackoffMode = false;
        private bool _wasConnected = false; // To track connection state changes

        // Folders
        private FolderState _tekmaticFolder;
        private FolderState _statusFolder;
        private FolderState _commandsFolder;

        // Status variables
        private BaseDataVariableState _isConnectedVariable;
        private BaseDataVariableState _deviceCountVariable;

        // Dictionary to keep track of slot device variables
        private Dictionary<int, BaseDataVariableState> _slotDeviceVariables = new Dictionary<int, BaseDataVariableState>();

        // Constructor
        public TekmaticNodeManager(
            IServerInternal server,
            ApplicationConfiguration configuration,
            TekmaticControl tekmatic)
        : base(server, configuration, new string[] { "http://persist.com/Tekmatic" })
        {
            try
            {
                Console.WriteLine("TekmaticNodeManager constructor called");

                // Store the Tekmatic control
                _tekmatic = tekmatic;
                _lastUsedId = 0;

                // Start a timer to update the Tekmatic status with reduced frequency (5 seconds)
                _updateTimer = new Timer(UpdateTekmaticStatus, null, 5000, 5000);

                Console.WriteLine("TekmaticNodeManager constructor completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TekmaticNodeManager constructor: {ex.Message}");
                throw;
            }
        }

        // INodeManagerFactory implementation
        public StringCollection NamespacesUris
        {
            get
            {
                StringCollection namespaces = new StringCollection();
                foreach (string uri in base.NamespaceUris)
                {
                    namespaces.Add(uri);
                }
                return namespaces;
            }
        }

        public INodeManager Create(IServerInternal server, ApplicationConfiguration configuration)
        {
            return new TekmaticNodeManager(server, configuration, _tekmatic);
        }

        // IEquipmentNodeManager implementation
        public void Initialize()
        {
            try
            {
                Console.WriteLine("TekmaticNodeManager.Initialize called - discovering devices");
                if (_tekmatic != null)
                {
                    // Discover devices first
                    _tekmatic.DiscoverDevices();

                    // Update the slot device info
                    UpdateSlotFolderDeviceInfo();

                    // Don't automatically connect - let the user choose which device to connect to
                    Console.WriteLine("Tekmatic devices discovered. Ready for connection.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TekmaticNodeManager.Initialize: {ex.Message}");
            }
        }


        public void Shutdown()
        {
            try
            {
                Console.WriteLine("TekmaticNodeManager.Shutdown called");

                // Stop the update timer
                if (_updateTimer != null)
                {
                    _updateTimer.Dispose();
                    _updateTimer = null;
                }

                // Shutdown the Tekmatic control
                if (_tekmatic != null)
                {
                    _tekmatic.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TekmaticNodeManager.Shutdown: {ex.Message}");
            }
        }

        // Overridden methods
        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            try
            {
                Console.WriteLine("TekmaticNodeManager.CreateAddressSpace - Starting address space creation");

                lock (Lock)
                {
                    // Get the namespace index
                    _namespaceIndex = Server.NamespaceUris.GetIndexOrAppend(NamespaceUris.First());

                    // Create the Tekmatic folder
                    _tekmaticFolder = CreateFolder(null, "Tekmatic", "Tekmatic");

                    // Add the root folder to the Objects folder
                    IList<IReference> references = null;
                    if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out references))
                    {
                        externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
                    }

                    // Add references
                    references.Add(new NodeStateReference(ReferenceTypes.Organizes, false, _tekmaticFolder.NodeId));
                    _tekmaticFolder.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);

                    // Create status folder
                    _statusFolder = CreateFolder(_tekmaticFolder, "Status", "Status");

                    // Keep only the IsConnected variable
                    _isConnectedVariable = CreateVariable(_statusFolder, "IsConnected", "IsConnected", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _isConnectedVariable.Value = false;


                    // Create device count variable
                    _deviceCountVariable = CreateVariable(_statusFolder, "DeviceCount", "Device Count", DataTypeIds.Int32, ValueRanks.Scalar);
                    _deviceCountVariable.Value = 0;

                    // Create slot device variables directly in the status folder
                    for (int slot = 1; slot <= 6; slot++)
                    {
                        // Create a variable for each slot's device name
                        BaseDataVariableState slotDeviceVar = CreateVariable(_statusFolder, $"Slot{slot}Device", $"Slot {slot} Device", DataTypeIds.String, ValueRanks.Scalar);
                        slotDeviceVar.Value = "No Device";
                        _slotDeviceVariables[slot] = slotDeviceVar;
                    }


                    // Create commands folder
                    _commandsFolder = CreateFolder(_tekmaticFolder, "Commands", "Commands");

                    // Create DiscoverDevices method
                    MethodState discoverDevicesMethod = CreateMethod(_commandsFolder, "DiscoverDevices", "Discover Devices");

                    // Define empty input arguments for DiscoverDevices
                    discoverDevicesMethod.InputArguments = new PropertyState<Argument[]>(discoverDevicesMethod);
                    discoverDevicesMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    discoverDevicesMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    discoverDevicesMethod.InputArguments.DisplayName = discoverDevicesMethod.InputArguments.BrowseName.Name;
                    discoverDevicesMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    discoverDevicesMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    discoverDevicesMethod.InputArguments.DataType = DataTypeIds.Argument;
                    discoverDevicesMethod.InputArguments.ValueRank = ValueRanks.OneDimension;
                    discoverDevicesMethod.InputArguments.Value = new Argument[0]; // No input arguments

                    // Define output arguments for DiscoverDevices (returns device count)
                    discoverDevicesMethod.OutputArguments = new PropertyState<Argument[]>(discoverDevicesMethod);
                    discoverDevicesMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    discoverDevicesMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    discoverDevicesMethod.OutputArguments.DisplayName = discoverDevicesMethod.OutputArguments.BrowseName.Name;
                    discoverDevicesMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    discoverDevicesMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    discoverDevicesMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    discoverDevicesMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument deviceCountArgument = new Argument();
                    deviceCountArgument.Name = "DeviceCount";
                    deviceCountArgument.Description = new LocalizedText("Number of devices discovered");
                    deviceCountArgument.DataType = DataTypeIds.Int32;
                    deviceCountArgument.ValueRank = ValueRanks.Scalar;

                    discoverDevicesMethod.OutputArguments.Value = new Argument[] { deviceCountArgument };



                    // Create ControlTemperature method
                    MethodState controlTemperatureMethod = CreateMethod(_commandsFolder, "ControlTemperature", "Control Temperature");

                    // Define input arguments for ControlTemperature
                    controlTemperatureMethod.InputArguments = new PropertyState<Argument[]>(controlTemperatureMethod);
                    controlTemperatureMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    controlTemperatureMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    controlTemperatureMethod.InputArguments.DisplayName = controlTemperatureMethod.InputArguments.BrowseName.Name;
                    controlTemperatureMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    controlTemperatureMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    controlTemperatureMethod.InputArguments.DataType = DataTypeIds.Argument;
                    controlTemperatureMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument slotArgument5 = new Argument();
                    slotArgument5.Name = "SlotId";
                    slotArgument5.Description = new LocalizedText("Slot ID (1-6)");
                    slotArgument5.DataType = DataTypeIds.Int32;
                    slotArgument5.ValueRank = ValueRanks.Scalar;

                    Argument temperatureArgument2 = new Argument();
                    temperatureArgument2.Name = "Temperature";
                    temperatureArgument2.Description = new LocalizedText("Target temperature in degrees Celsius");
                    temperatureArgument2.DataType = DataTypeIds.Double;
                    temperatureArgument2.ValueRank = ValueRanks.Scalar;

                    Argument enableArgument2 = new Argument();
                    enableArgument2.Name = "Enable";
                    enableArgument2.Description = new LocalizedText("True to enable temperature control, false to disable");
                    enableArgument2.DataType = DataTypeIds.Boolean;
                    enableArgument2.ValueRank = ValueRanks.Scalar;

                    controlTemperatureMethod.InputArguments.Value = new Argument[] { slotArgument5, temperatureArgument2, enableArgument2 };

                    // Define output arguments for ControlTemperature (result code)
                    controlTemperatureMethod.OutputArguments = new PropertyState<Argument[]>(controlTemperatureMethod);
                    controlTemperatureMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    controlTemperatureMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    controlTemperatureMethod.OutputArguments.DisplayName = controlTemperatureMethod.OutputArguments.BrowseName.Name;
                    controlTemperatureMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    controlTemperatureMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    controlTemperatureMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    controlTemperatureMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument controlTemperatureResultArgument = new Argument();
                    controlTemperatureResultArgument.Name = "Result";
                    controlTemperatureResultArgument.Description = new LocalizedText("Result code: 0=success, -1=failure");
                    controlTemperatureResultArgument.DataType = DataTypeIds.Int32;
                    controlTemperatureResultArgument.ValueRank = ValueRanks.Scalar;

                    controlTemperatureMethod.OutputArguments.Value = new Argument[] { controlTemperatureResultArgument };

                    // Create ControlShaking method
                    MethodState controlShakingMethod = CreateMethod(_commandsFolder, "ControlShaking", "Control Shaking");

                    // Define input arguments for ControlShaking
                    controlShakingMethod.InputArguments = new PropertyState<Argument[]>(controlShakingMethod);
                    controlShakingMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    controlShakingMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    controlShakingMethod.InputArguments.DisplayName = controlShakingMethod.InputArguments.BrowseName.Name;
                    controlShakingMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    controlShakingMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    controlShakingMethod.InputArguments.DataType = DataTypeIds.Argument;
                    controlShakingMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument slotArgument3 = new Argument();
                    slotArgument3.Name = "SlotId";
                    slotArgument3.Description = new LocalizedText("Slot ID (1-6)");
                    slotArgument3.DataType = DataTypeIds.Int32;
                    slotArgument3.ValueRank = ValueRanks.Scalar;

                    Argument rpmArgument = new Argument();
                    rpmArgument.Name = "Rpm";
                    rpmArgument.Description = new LocalizedText("Target shaking RPM");
                    rpmArgument.DataType = DataTypeIds.Int32;
                    rpmArgument.ValueRank = ValueRanks.Scalar;

                    Argument enableArgument = new Argument();
                    enableArgument.Name = "Enable";
                    enableArgument.Description = new LocalizedText("True to enable shaking, false to disable");
                    enableArgument.DataType = DataTypeIds.Boolean;
                    enableArgument.ValueRank = ValueRanks.Scalar;

                    controlShakingMethod.InputArguments.Value = new Argument[] { slotArgument3, rpmArgument, enableArgument };

                    // Define output arguments for ControlShaking (result code)
                    controlShakingMethod.OutputArguments = new PropertyState<Argument[]>(controlShakingMethod);
                    controlShakingMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    controlShakingMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    controlShakingMethod.OutputArguments.DisplayName = controlShakingMethod.OutputArguments.BrowseName.Name;
                    controlShakingMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    controlShakingMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    controlShakingMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    controlShakingMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument controlShakingResultArgument = new Argument();
                    controlShakingResultArgument.Name = "Result";
                    controlShakingResultArgument.Description = new LocalizedText("Result code: 0=success, -1=failure");
                    controlShakingResultArgument.DataType = DataTypeIds.Int32;
                    controlShakingResultArgument.ValueRank = ValueRanks.Scalar;

                    controlShakingMethod.OutputArguments.Value = new Argument[] { controlShakingResultArgument };


                    // Create ClearErrorCodes method
                    MethodState clearErrorCodesMethod = CreateMethod(_commandsFolder, "ClearErrorCodes", "Clear Error Codes");

                    // Define empty input arguments for ClearErrorCodes
                    clearErrorCodesMethod.InputArguments = new PropertyState<Argument[]>(clearErrorCodesMethod);
                    clearErrorCodesMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    clearErrorCodesMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    clearErrorCodesMethod.InputArguments.DisplayName = clearErrorCodesMethod.InputArguments.BrowseName.Name;
                    clearErrorCodesMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    clearErrorCodesMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    clearErrorCodesMethod.InputArguments.DataType = DataTypeIds.Argument;
                    clearErrorCodesMethod.InputArguments.ValueRank = ValueRanks.OneDimension;
                    clearErrorCodesMethod.InputArguments.Value = new Argument[0]; // No input arguments

                    // Create ExecuteRawCommand method
                    MethodState executeRawCommandMethod = CreateMethod(_commandsFolder, "ExecuteRawCommand", "Execute Raw Command");

                    // Define input arguments for ExecuteRawCommand
                    executeRawCommandMethod.InputArguments = new PropertyState<Argument[]>(executeRawCommandMethod);
                    executeRawCommandMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    executeRawCommandMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    executeRawCommandMethod.InputArguments.DisplayName = executeRawCommandMethod.InputArguments.BrowseName.Name;
                    executeRawCommandMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    executeRawCommandMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    executeRawCommandMethod.InputArguments.DataType = DataTypeIds.Argument;
                    executeRawCommandMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument commandArgument = new Argument();
                    commandArgument.Name = "Command";
                    commandArgument.Description = new LocalizedText("Full command string including slot ID prefix (e.g., '1SSR300')");
                    commandArgument.DataType = DataTypeIds.String;
                    commandArgument.ValueRank = ValueRanks.Scalar;

                    executeRawCommandMethod.InputArguments.Value = new Argument[] { commandArgument };

                    // Define output arguments for ExecuteRawCommand (response string)
                    executeRawCommandMethod.OutputArguments = new PropertyState<Argument[]>(executeRawCommandMethod);
                    executeRawCommandMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    executeRawCommandMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    executeRawCommandMethod.OutputArguments.DisplayName = executeRawCommandMethod.OutputArguments.BrowseName.Name;
                    executeRawCommandMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    executeRawCommandMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    executeRawCommandMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    executeRawCommandMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument responseArgument = new Argument();
                    responseArgument.Name = "Response";
                    responseArgument.Description = new LocalizedText("Response from the device");
                    responseArgument.DataType = DataTypeIds.String;
                    responseArgument.ValueRank = ValueRanks.Scalar;

                    executeRawCommandMethod.OutputArguments.Value = new Argument[] { responseArgument };

                    // Define output arguments for ClearErrorCodes (result code)
                    clearErrorCodesMethod.OutputArguments = new PropertyState<Argument[]>(clearErrorCodesMethod);
                    clearErrorCodesMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    clearErrorCodesMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    clearErrorCodesMethod.OutputArguments.DisplayName = clearErrorCodesMethod.OutputArguments.BrowseName.Name;
                    clearErrorCodesMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    clearErrorCodesMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    clearErrorCodesMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    clearErrorCodesMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument clearErrorCodesResultArgument = new Argument();
                    clearErrorCodesResultArgument.Name = "Result";
                    clearErrorCodesResultArgument.Description = new LocalizedText("Result code: 0=success, -1=failure");
                    clearErrorCodesResultArgument.DataType = DataTypeIds.Int32;
                    clearErrorCodesResultArgument.ValueRank = ValueRanks.Scalar;

                    clearErrorCodesMethod.OutputArguments.Value = new Argument[] { clearErrorCodesResultArgument };

                    // Add the nodes to the address space
                    AddPredefinedNode(SystemContext, _tekmaticFolder);

                    Console.WriteLine("TekmaticNodeManager.CreateAddressSpace - Address space created successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating address space: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates a folder node
        /// </summary>
        private FolderState CreateFolder(NodeState parent, string name, string displayName)
        {
            FolderState folder = new FolderState(parent);

            folder.SymbolicName = name;
            folder.ReferenceTypeId = ReferenceTypes.Organizes;
            folder.TypeDefinitionId = ObjectTypeIds.FolderType;
            folder.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
            folder.BrowseName = new QualifiedName(name, _namespaceIndex);
            folder.DisplayName = new LocalizedText("en", displayName);
            folder.WriteMask = AttributeWriteMask.None;
            folder.UserWriteMask = AttributeWriteMask.None;
            folder.EventNotifier = EventNotifiers.None;

            if (parent != null)
            {
                parent.AddChild(folder);
            }

            return folder;
        }

        /// <summary>
        /// Creates a variable node
        /// </summary>
        private BaseDataVariableState CreateVariable(NodeState parent, string name, string displayName, NodeId dataType, int valueRank)
        {
            BaseDataVariableState variable = new BaseDataVariableState(parent);

            variable.SymbolicName = name;
            variable.ReferenceTypeId = ReferenceTypes.Organizes;
            variable.TypeDefinitionId = VariableTypeIds.BaseDataVariableType;
            variable.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
            variable.BrowseName = new QualifiedName(name, _namespaceIndex);
            variable.DisplayName = new LocalizedText("en", displayName);
            variable.WriteMask = AttributeWriteMask.None;
            variable.UserWriteMask = AttributeWriteMask.None;
            variable.DataType = dataType;
            variable.ValueRank = valueRank;
            variable.AccessLevel = AccessLevels.CurrentRead;
            variable.UserAccessLevel = AccessLevels.CurrentRead;
            variable.Historizing = false;
            variable.StatusCode = StatusCodes.Good;
            variable.Timestamp = DateTime.UtcNow;

            if (parent != null)
            {
                parent.AddChild(variable);
            }

            return variable;
        }

        /// <summary>
        /// Creates a method node
        /// </summary>
        private MethodState CreateMethod(NodeState parent, string name, string displayName)
        {
            MethodState method = new MethodState(parent);

            method.SymbolicName = name;
            method.ReferenceTypeId = ReferenceTypes.HasComponent;
            method.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
            method.BrowseName = new QualifiedName(name, _namespaceIndex);
            method.DisplayName = new LocalizedText("en", displayName);
            method.WriteMask = AttributeWriteMask.None;
            method.UserWriteMask = AttributeWriteMask.None;
            method.Executable = true;
            method.UserExecutable = true;

            if (parent != null)
            {
                parent.AddChild(method);
            }

            // Set up method callbacks
            method.OnCallMethod = OnCallMethod;

            return method;
        }

        /// <summary>
        /// Adjusts the update timer interval based on connection status
        /// </summary>
        private void AdjustUpdateInterval(bool useBackoff)
        {
            if (_updateTimer != null)
            {
                _updateTimer.Change(0, useBackoff ? BACKOFF_INTERVAL_MS : NORMAL_INTERVAL_MS);

                if (useBackoff && !_isInBackoffMode)
                {
                    Console.WriteLine($"Switching to reduced polling frequency ({BACKOFF_INTERVAL_MS / 1000} seconds) due to connection failures");
                    _isInBackoffMode = true;
                }
                else if (!useBackoff && _isInBackoffMode)
                {
                    Console.WriteLine($"Resuming normal polling frequency ({NORMAL_INTERVAL_MS} ms)");
                    _isInBackoffMode = false;
                }
            }
        }

        /// <summary>
        /// Updates the slot device information
        /// </summary>
        private void UpdateSlotFolderDeviceInfo()
        {
            try
            {
                // Update status for each slot
                for (int slot = 1; slot <= 6; slot++)
                {
                    // Get the slot device variable
                    if (!_slotDeviceVariables.ContainsKey(slot))
                        continue;

                    // Get the device in this slot
                    var device = _tekmatic.GetDeviceBySlot(slot);
                    bool hasDevice = device != null && device.HasDevice;

                    // Update device name
                    string deviceName = hasDevice ? device.Name : (device != null ? device.Name : "No Device");
                    _slotDeviceVariables[slot].Value = deviceName;
                    _slotDeviceVariables[slot].ClearChangeMasks(SystemContext, false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating slot device info: {ex.Message}");
            }
        }


        /// <summary>
        /// Updates only the connection status with backoff strategy
        /// </summary>
        private void UpdateTekmaticStatus(object state)
        {
            try
            {
                if (_tekmatic == null)
                    return;

                // Record the time of this connection attempt
                _lastConnectionAttemptTime = DateTime.Now;

                // Check if the device is connected
                bool isConnected = _tekmatic.IsConnected();

                // Update the connection status variable
                if (_isConnectedVariable != null)
                {
                    _isConnectedVariable.Value = isConnected;
                    _isConnectedVariable.ClearChangeMasks(SystemContext, false);
                }

                // Handle connection state changes and backoff logic
                if (isConnected)
                {
                    // Reset failure count on successful connection
                    if (_connectionFailureCount > 0)
                    {
                        _connectionFailureCount = 0;

                        // If we were in backoff mode, switch back to normal polling
                        if (_isInBackoffMode)
                        {
                            AdjustUpdateInterval(false);
                        }
                    }

                    // Log connection recovery if state changed
                    if (!_wasConnected)
                    {
                        Console.WriteLine("Connection to Tekmatic device established");
                        _wasConnected = true;
                    }

                    // No longer updating device status - only checking connection
                }
                else
                {
                    // Increment failure count
                    _connectionFailureCount++;

                    // Log disconnection if state changed
                    if (_wasConnected)
                    {
                        Console.WriteLine("Connection to Tekmatic device lost");
                        _wasConnected = false;
                    }

                    // Check if we need to enter backoff mode
                    if (_connectionFailureCount >= MAX_CONSECUTIVE_FAILURES && !_isInBackoffMode)
                    {
                        AdjustUpdateInterval(true);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating Tekmatic status: {ex.Message}");
            }
        }


        /// <summary>
        /// Method call handler
        /// </summary>
        private ServiceResult OnCallMethod(ISystemContext context, MethodState methodState, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Handle method calls based on the method name
                switch (methodState.BrowseName.Name)
                {
                    case "DiscoverDevices":
                        int deviceCount = _tekmatic.DiscoverDevices();
                        // Update the slot device information
                        UpdateSlotFolderDeviceInfo();
                        outputArguments[0] = deviceCount;
                        return ServiceResult.Good;



                    case "ControlTemperature":
                        if (inputArguments.Count > 2)
                        {
                            int slotId = Convert.ToInt32(inputArguments[0]);
                            double temperature = Convert.ToDouble(inputArguments[1]);
                            bool enable = Convert.ToBoolean(inputArguments[2]);
                            int result = _tekmatic.ControlTemperature(slotId, temperature, enable);
                            outputArguments[0] = result;
                            return ServiceResult.Good;
                        }
                        return ServiceResult.Create(StatusCodes.BadInvalidArgument, "Invalid arguments");

                    case "ControlShaking":
                        if (inputArguments.Count > 2)
                        {
                            int slotId = Convert.ToInt32(inputArguments[0]);
                            int rpm = Convert.ToInt32(inputArguments[1]);
                            bool enable = Convert.ToBoolean(inputArguments[2]);
                            int result = _tekmatic.ControlShaking(slotId, rpm, enable);
                            outputArguments[0] = result;
                            return ServiceResult.Good;
                        }
                        return ServiceResult.Create(StatusCodes.BadInvalidArgument, "Invalid arguments");


                    case "ClearErrorCodes":
                        int clearResult = _tekmatic.ClearErrorCodes();
                        outputArguments[0] = clearResult;
                        return ServiceResult.Good;

                    case "ExecuteRawCommand":
                        if (inputArguments.Count > 0)
                        {
                            string command = Convert.ToString(inputArguments[0]);
                            Console.WriteLine($"Executing raw command via OPC UA: {command}");
                            string response = _tekmatic.ExecuteRawCommand(command);
                            outputArguments[0] = response;
                            return ServiceResult.Good;
                        }
                        return ServiceResult.Create(StatusCodes.BadInvalidArgument, "Missing command argument");

                    default:
                        return ServiceResult.Create(StatusCodes.BadMethodInvalid, "Method not found");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling method: {ex.Message}");
                return ServiceResult.Create(StatusCodes.BadInternalError, ex.Message);
            }
        }
    }
}
