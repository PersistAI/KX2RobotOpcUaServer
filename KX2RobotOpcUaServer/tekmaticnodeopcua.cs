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
        /// The type of the device
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The serial number of the device
        /// </summary>
        public string Serial { get; set; }

        /// <summary>
        /// The slot ID (1-6) where the device is connected
        /// </summary>
        public int SlotId { get; set; }

        /// <summary>
        /// Whether the device is connected
        /// </summary>
        public bool IsConnected { get; set; }

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
        // Properties to store Tekmatic state
        private bool _isConnected = false;
        private bool _isShaking = false;
        private double _temperature = 37.0;
        private double _targetTemperature = 37.0;
        private int _shakingRpm = 0;
        private int _targetShakingRpm = 0;
        private bool _isClamped = false;

        // List of discovered devices (one per slot)
        private List<TekmaticDevice> _discoveredDevices = new List<TekmaticDevice>();

        // Currently connected slot
        private int _connectedSlot = 0;

        // Currently connected device
        private TekmaticDevice _connectedDevice = null;

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
                _inhecoController.WriteOnly(command);
                return _inhecoController.ReadSync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending command '{command}': {ex.Message}");
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

                // Disconnect from any connected device
                if (_isConnected)
                {
                    Disconnect();
                }

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
                string response = SendCommand("0RFV0");
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
                        Name = "No Device",
                        Type = "None",
                        Serial = string.Empty,
                        IsConnected = false
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
                            device.Type = GetDeviceTypeName(typeCode);
                            device.Name = device.Type;

                            // Get serial number if available
                            string serialResponse = SendCommand($"{slot}RSN1");
                            if (!string.IsNullOrEmpty(serialResponse) && serialResponse.Length > 5)
                            {
                                device.Serial = serialResponse.Substring(5);
                            }

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
        /// Gets the currently connected device
        /// </summary>
        public TekmaticDevice GetConnectedDevice()
        {
            return _connectedDevice;
        }

        /// <summary>
        /// Connect to a Tekmatic device in the specified slot
        /// </summary>
        /// <param name="slotId">The slot ID (1-6) to connect to</param>
        /// <returns>0 for success, -1 for failure, -2 for device not found</returns>
        public int ConnectToSlot(int slotId)
        {
            try
            {
                if (_connectedSlot == slotId && _isConnected)
                    return 0; // Already connected to this slot

                // Disconnect from any currently connected slot
                if (_isConnected)
                {
                    Disconnect();
                }

                Console.WriteLine($"Connecting to device in slot {slotId}...");

                // Find the device in the discovered devices list
                var device = _discoveredDevices.FirstOrDefault(d => d.SlotId == slotId);

                if (device == null || !device.HasDevice)
                {
                    Console.WriteLine($"No device found in slot {slotId}");
                    return -2; // No device in this slot
                }

                // For Tekmatic devices, we're already connected via USB, so we just need to
                // set the current slot and initialize it

                // Enable temperature control for this slot
                string response = SendCommand($"{slotId}ATE1");
                if (string.IsNullOrEmpty(response) || !response.StartsWith($"{slotId}ate"))
                {
                    Console.WriteLine($"Failed to initialize device in slot {slotId}: {response}");
                    return -1;
                }

                _isConnected = true;
                _connectedSlot = slotId;
                _connectedDevice = device;
                device.IsConnected = true;

                Console.WriteLine($"Connected to device in slot {slotId}: {device.Name}");
                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to device: {ex.Message}");
                return -1; // Error
            }
        }

        /// <summary>
        /// Connect to the first available Tekmatic device
        /// </summary>
        public int Connect()
        {
            try
            {
                if (_isConnected)
                    return 0; // Already connected

                // If we have no discovered devices, discover them now
                if (_discoveredDevices.Count == 0)
                {
                    DiscoverDevices();
                }

                // Find the first slot with a device
                var deviceSlot = _discoveredDevices.FirstOrDefault(d => d.HasDevice);
                if (deviceSlot != null)
                {
                    return ConnectToSlot(deviceSlot.SlotId);
                }

                Console.WriteLine("No Tekmatic devices found to connect to");
                return -1; // Failed
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to Tekmatic: {ex.Message}");
                return -1; // Error
            }
        }

        /// <summary>
        /// Disconnect from the Tekmatic device
        /// </summary>
        public int Disconnect()
        {
            try
            {
                if (!_isConnected)
                    return 0; // Already disconnected

                Console.WriteLine($"Disconnecting from Tekmatic device in slot {_connectedSlot}...");

                // Disable temperature control
                string response = SendCommand($"{_connectedSlot}ATE0");
                if (string.IsNullOrEmpty(response) || !response.StartsWith($"{_connectedSlot}ate"))
                {
                    Console.WriteLine($"Warning: Failed to disable temperature control: {response}");
                }

                // Disable shaking if active
                if (_isShaking)
                {
                    response = SendCommand($"{_connectedSlot}ASE0");
                    if (string.IsNullOrEmpty(response) || !response.StartsWith($"{_connectedSlot}ase"))
                    {
                        Console.WriteLine($"Warning: Failed to disable shaking: {response}");
                    }
                }

                // Update device status
                var device = _discoveredDevices.FirstOrDefault(d => d.SlotId == _connectedSlot);
                if (device != null)
                {
                    device.IsConnected = false;
                }

                _isConnected = false;
                _connectedDevice = null;
                _connectedSlot = 0;

                Console.WriteLine("Disconnected from Tekmatic device");
                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disconnecting from Tekmatic: {ex.Message}");
                return -1; // Error
            }
        }

        /// <summary>
        /// Get the current temperature for the connected slot
        /// </summary>
        public double GetTemperature()
        {
            if (!_isConnected || _connectedSlot == 0)
                return 0.0;

            return GetTemperatureForSlot(_connectedSlot);
        }

        /// <summary>
        /// Get the current temperature for a specific slot
        /// </summary>
        public double GetTemperatureForSlot(int slotId)
        {
            try
            {
                // Get the current temperature
                string response = SendCommand($"{slotId}RAT");
                if (!string.IsNullOrEmpty(response) && response.Length > 5)
                {
                    double temp;
                    if (double.TryParse(response.Substring(5), out temp))
                    {
                        // Temperature is reported in 1/10 °C, e.g: 345 = 34.5 °C
                        double temperature = temp / 10.0;

                        // Update the temperature if this is the connected slot
                        if (slotId == _connectedSlot)
                        {
                            _temperature = temperature;
                        }

                        return temperature;
                    }
                }

                // If we couldn't get the temperature, return the last known value if this is the connected slot
                if (slotId == _connectedSlot)
                {
                    return _temperature;
                }

                return 0.0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting temperature for slot {slotId}: {ex.Message}");

                // Return the last known value if this is the connected slot
                if (slotId == _connectedSlot)
                {
                    return _temperature;
                }

                return 0.0;
            }
        }

        /// <summary>
        /// Get the target temperature for the connected slot
        /// </summary>
        public double GetTargetTemperature()
        {
            if (!_isConnected || _connectedSlot == 0)
                return 0.0;

            return GetTargetTemperatureForSlot(_connectedSlot);
        }

        /// <summary>
        /// Get the target temperature for a specific slot
        /// </summary>
        public double GetTargetTemperatureForSlot(int slotId)
        {
            try
            {
                // Get the target temperature
                string response = SendCommand($"{slotId}RTT");
                if (!string.IsNullOrEmpty(response) && response.Length > 5)
                {
                    double temp;
                    if (double.TryParse(response.Substring(5), out temp))
                    {
                        // Temperature is reported in 1/10 °C, e.g: 345 = 34.5 °C
                        double targetTemperature = temp / 10.0;

                        // Update the target temperature if this is the connected slot
                        if (slotId == _connectedSlot)
                        {
                            _targetTemperature = targetTemperature;
                        }

                        return targetTemperature;
                    }
                }

                // If we couldn't get the target temperature, return the last known value if this is the connected slot
                if (slotId == _connectedSlot)
                {
                    return _targetTemperature;
                }

                return 0.0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting target temperature for slot {slotId}: {ex.Message}");

                // Return the last known value if this is the connected slot
                if (slotId == _connectedSlot)
                {
                    return _targetTemperature;
                }

                return 0.0;
            }
        }

        /// <summary>
        /// Set the target temperature for the connected slot
        /// </summary>
        public int SetTargetTemperature(double temperature)
        {
            if (!_isConnected || _connectedSlot == 0)
                return -1;

            return SetTargetTemperatureForSlot(_connectedSlot, temperature);
        }

        /// <summary>
        /// Set the target temperature for a specific slot
        /// </summary>
        public int SetTargetTemperatureForSlot(int slotId, double temperature)
        {
            try
            {
                Console.WriteLine($"Setting target temperature for slot {slotId} to {temperature}°C...");

                // Convert to 1/10 °C for the command
                int tempValue = (int)(temperature * 10);

                // Set the target temperature
                string response = SendCommand($"{slotId}STT{tempValue}");
                if (string.IsNullOrEmpty(response) || !response.StartsWith($"{slotId}stt"))
                {
                    Console.WriteLine($"Failed to set target temperature for slot {slotId}: {response}");
                    return -1;
                }

                // Update the target temperature if this is the connected slot
                if (slotId == _connectedSlot)
                {
                    _targetTemperature = temperature;
                }

                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting target temperature for slot {slotId}: {ex.Message}");
                return -1; // Error
            }
        }

        /// <summary>
        /// Enable or disable temperature control for the connected slot
        /// </summary>
        public int EnableTemperature(bool enable)
        {
            if (!_isConnected || _connectedSlot == 0)
                return -1;

            return EnableTemperatureForSlot(_connectedSlot, enable);
        }

        /// <summary>
        /// Enable or disable temperature control for a specific slot
        /// </summary>
        public int EnableTemperatureForSlot(int slotId, bool enable)
        {
            try
            {
                Console.WriteLine($"{(enable ? "Enabling" : "Disabling")} temperature control for slot {slotId}...");

                // Enable or disable temperature control
                string response = SendCommand($"{slotId}ATE{(enable ? "1" : "0")}");
                if (string.IsNullOrEmpty(response) || !response.StartsWith($"{slotId}ate"))
                {
                    Console.WriteLine($"Failed to {(enable ? "enable" : "disable")} temperature control for slot {slotId}: {response}");
                    return -1;
                }

                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error {(enable ? "enabling" : "disabling")} temperature control for slot {slotId}: {ex.Message}");
                return -1; // Error
            }
        }

        /// <summary>
        /// Get the current shaking RPM for the connected slot
        /// </summary>
        public int GetShakingRpm()
        {
            if (!_isConnected || _connectedSlot == 0)
                return 0;

            return GetShakingRpmForSlot(_connectedSlot);
        }

        /// <summary>
        /// Get the current shaking RPM for a specific slot
        /// </summary>
        public int GetShakingRpmForSlot(int slotId)
        {
            try
            {
                // Get the current RPM
                string response = SendCommand($"{slotId}RSR");
                if (!string.IsNullOrEmpty(response) && response.Length > 5)
                {
                    int rpm;
                    if (int.TryParse(response.Substring(5), out rpm))
                    {
                        // Update the RPM if this is the connected slot
                        if (slotId == _connectedSlot)
                        {
                            _shakingRpm = rpm;
                        }

                        return rpm;
                    }
                }

                // If we couldn't get the RPM, return the last known value if this is the connected slot
                if (slotId == _connectedSlot)
                {
                    return _shakingRpm;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting shaking RPM for slot {slotId}: {ex.Message}");

                // Return the last known value if this is the connected slot
                if (slotId == _connectedSlot)
                {
                    return _shakingRpm;
                }

                return 0;
            }
        }

        /// <summary>
        /// Set the target shaking RPM for the connected slot
        /// </summary>
        public int SetShakingRpm(int rpm)
        {
            if (!_isConnected || _connectedSlot == 0)
                return -1;

            return SetShakingRpmForSlot(_connectedSlot, rpm);
        }

        /// <summary>
        /// Set the target shaking RPM for a specific slot
        /// </summary>
        public int SetShakingRpmForSlot(int slotId, int rpm)
        {
            try
            {
                Console.WriteLine($"Setting shaking RPM for slot {slotId} to {rpm}...");

                // Set the target RPM
                string response = SendCommand($"{slotId}SSR{rpm}");
                if (string.IsNullOrEmpty(response) || !response.StartsWith($"{slotId}ssr"))
                {
                    Console.WriteLine($"Failed to set shaking RPM for slot {slotId}: {response}");
                    return -1;
                }

                // Update the target RPM if this is the connected slot
                if (slotId == _connectedSlot)
                {
                    _targetShakingRpm = rpm;

                    // If shaking is enabled, update the current RPM
                    if (_isShaking)
                    {
                        _shakingRpm = rpm;
                    }
                }

                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting shaking RPM for slot {slotId}: {ex.Message}");
                return -1; // Error
            }
        }

        /// <summary>
        /// Enable or disable shaking for the connected slot
        /// </summary>
        public int EnableShaking(bool enable)
        {
            if (!_isConnected || _connectedSlot == 0)
                return -1;

            return EnableShakingForSlot(_connectedSlot, enable);
        }

        /// <summary>
        /// Enable or disable shaking for a specific slot
        /// </summary>
        public int EnableShakingForSlot(int slotId, bool enable)
        {
            try
            {
                Console.WriteLine($"{(enable ? "Enabling" : "Disabling")} shaking for slot {slotId}...");

                // Enable or disable shaking
                string response = SendCommand($"{slotId}ASE{(enable ? "1" : "0")}");
                if (string.IsNullOrEmpty(response) || !response.StartsWith($"{slotId}ase"))
                {
                    Console.WriteLine($"Failed to {(enable ? "enable" : "disable")} shaking for slot {slotId}: {response}");
                    return -1;
                }

                // Update the shaking state if this is the connected slot
                if (slotId == _connectedSlot)
                {
                    _isShaking = enable;

                    // Update the current RPM based on the shaking state
                    if (_isShaking)
                    {
                        _shakingRpm = _targetShakingRpm;
                    }
                    else
                    {
                        _shakingRpm = 0;
                    }
                }

                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error {(enable ? "enabling" : "disabling")} shaking for slot {slotId}: {ex.Message}");
                return -1; // Error
            }
        }

        /// <summary>
        /// Get the clamp status
        /// </summary>
        public bool IsClampClosed()
        {
            if (!_isConnected)
                return false;

            try
            {
                // Get the clamp status
                string response = SendCommand("0RCS");
                if (!string.IsNullOrEmpty(response) && response.Length > 5)
                {
                    int status;
                    if (int.TryParse(response.Substring(5), out status))
                    {
                        _isClamped = (status == 2); // 2 means clamps are closed
                        return _isClamped;
                    }
                }

                // If we couldn't get the status, return the last known value
                return _isClamped;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting clamp status: {ex.Message}");
                return _isClamped;
            }
        }

        /// <summary>
        /// Open or close the clamps
        /// </summary>
        public int SetClampState(bool closed)
        {
            if (!_isConnected)
                return -1;

            try
            {
                Console.WriteLine($"{(closed ? "Closing" : "Opening")} clamps...");

                // Set the clamp state
                string response = SendCommand($"0SCS{(closed ? "2" : "1")}");
                if (string.IsNullOrEmpty(response) || !response.StartsWith("0scs"))
                {
                    Console.WriteLine($"Failed to {(closed ? "close" : "open")} clamps: {response}");
                    return -1;
                }

                _isClamped = closed;

                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error {(closed ? "closing" : "opening")} clamps: {ex.Message}");
                return -1; // Error
            }
        }

        /// <summary>
        /// Check if device is connected
        /// </summary>
        public bool IsConnected()
        {
            return _isConnected;
        }

        /// <summary>
        /// Check if device is shaking
        /// </summary>
        public bool IsShaking()
        {
            return _isShaking;
        }

        /// <summary>
        /// Get error codes from the device
        /// </summary>
        public string GetErrorCodes()
        {
            if (!_isConnected)
                return string.Empty;

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
            if (!_isConnected)
                return -1;

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
    }

    /// <summary>
    /// Tekmatic Node Manager for OPC UA Server
    /// </summary>
    public class TekmaticNodeManager : CustomNodeManager2, IEquipmentNodeManager, INodeManagerFactory
    {
        private const int UPDATE_INTERVAL_MS = 1000;

        // Private fields
        private TekmaticControl _tekmatic;
        private ushort _namespaceIndex;
        private uint _lastUsedId;
        private Timer _updateTimer;

        // Folders
        private FolderState _tekmaticFolder;
        private FolderState _statusFolder;
        private FolderState _commandsFolder;
        private FolderState _discoveredDevicesFolder;

        // Status variables
        private BaseDataVariableState _isConnectedVariable;
        private BaseDataVariableState _temperatureVariable;
        private BaseDataVariableState _targetTemperatureVariable;
        private BaseDataVariableState _shakingRpmVariable;
        private BaseDataVariableState _targetShakingRpmVariable;
        private BaseDataVariableState _isShakingVariable;
        private BaseDataVariableState _isClampClosedVariable;
        private BaseDataVariableState _serialNumberVariable;
        private BaseDataVariableState _deviceNameVariable;
        private BaseDataVariableState _deviceTypeVariable;
        private BaseDataVariableState _connectedDeviceSerialVariable;
        private BaseDataVariableState _deviceCountVariable;

        // List to keep track of device variables
        private List<FolderState> _deviceFolders = new List<FolderState>();

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

                // Start a timer to update the Tekmatic status
                _updateTimer = new Timer(UpdateTekmaticStatus, null, UPDATE_INTERVAL_MS, UPDATE_INTERVAL_MS);

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

                    // Update the device folders in the address space
                    UpdateDeviceFolders();

                    // Don't automatically connect - let the user choose which device to connect to
                    Console.WriteLine("Tekmatic devices discovered. Ready for connection.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TekmaticNodeManager.Initialize: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the device folders in the address space based on discovered devices
        /// </summary>
        private void UpdateDeviceFolders()
        {
            try
            {
                if (_discoveredDevicesFolder == null || _tekmatic == null)
                    return;

                // Get the list of discovered devices
                var devices = _tekmatic.GetDiscoveredDevices();

                // Update the device count
                if (_deviceCountVariable != null)
                {
                    _deviceCountVariable.Value = devices.Count;
                    _deviceCountVariable.ClearChangeMasks(SystemContext, false);
                }

                // Clear existing device folders
                foreach (var folder in _deviceFolders)
                {
                    _discoveredDevicesFolder.RemoveChild(folder);
                }
                _deviceFolders.Clear();

                Console.WriteLine($"Creating folders for {devices.Count} discovered devices");

                // Create a folder for each device
                foreach (var device in devices)
                {
                    try
                    {
                        // Create a folder for the device with a clear name
                        string folderName = $"Device_{device.Serial}";
                        string displayName = device.Name;

                        Console.WriteLine($"Creating device folder: {folderName} ({displayName})");

                        FolderState deviceFolder = CreateFolder(_discoveredDevicesFolder, folderName, displayName);
                        _deviceFolders.Add(deviceFolder);

                        // Add device information variables
                        BaseDataVariableState nameVar = CreateVariable(deviceFolder, "Name", "Name", DataTypeIds.String, ValueRanks.Scalar);
                        nameVar.Value = device.Name;
                        nameVar.ClearChangeMasks(SystemContext, false);

                        BaseDataVariableState serialVar = CreateVariable(deviceFolder, "Serial", "Serial", DataTypeIds.String, ValueRanks.Scalar);
                        serialVar.Value = device.Serial;
                        serialVar.ClearChangeMasks(SystemContext, false);

                        BaseDataVariableState typeVar = CreateVariable(deviceFolder, "Type", "Type", DataTypeIds.String, ValueRanks.Scalar);
                        typeVar.Value = device.Type;
                        typeVar.ClearChangeMasks(SystemContext, false);

                        // Add a method to connect to this device
                        MethodState connectMethod = CreateMethod(deviceFolder, "Connect", "Connect");

                        // Define empty input arguments for Connect
                        connectMethod.InputArguments = new PropertyState<Argument[]>(connectMethod);
                        connectMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                        connectMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                        connectMethod.InputArguments.DisplayName = connectMethod.InputArguments.BrowseName.Name;
                        connectMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                        connectMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                        connectMethod.InputArguments.DataType = DataTypeIds.Argument;
                        connectMethod.InputArguments.ValueRank = ValueRanks.OneDimension;
                        connectMethod.InputArguments.Value = new Argument[0]; // No input arguments

                        // Define output arguments for Connect (returns result code)
                        connectMethod.OutputArguments = new PropertyState<Argument[]>(connectMethod);
                        connectMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                        connectMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                        connectMethod.OutputArguments.DisplayName = connectMethod.OutputArguments.BrowseName.Name;
                        connectMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                        connectMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                        connectMethod.OutputArguments.DataType = DataTypeIds.Argument;
                        connectMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                        Argument resultArgument = new Argument();
                        resultArgument.Name = "Result";
                        resultArgument.Description = new LocalizedText("Result code: 0=success, -1=failure");
                        resultArgument.DataType = DataTypeIds.Int32;
                        resultArgument.ValueRank = ValueRanks.Scalar;

                        connectMethod.OutputArguments.Value = new Argument[] { resultArgument };

                        // Make sure the folder and its children are properly registered with the address space
                        AddPredefinedNode(SystemContext, deviceFolder);

                        Console.WriteLine($"Successfully created device folder for {device.Name} ({device.Serial})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating device folder for {device.Name}: {ex.Message}");
                    }
                }

                // Make sure the discovered devices folder is updated in the address space
                _discoveredDevicesFolder.ClearChangeMasks(SystemContext, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating device folders: {ex.Message}");
            }
        }

        public void Shutdown()
        {
            try
            {
                Console.WriteLine("TekmaticNodeManager.Shutdown called - disconnecting from device");

                // Disconnect from the device
                if (_tekmatic != null)
                {
                    _tekmatic.Disconnect();
                }

                // Stop the update timer
                if (_updateTimer != null)
                {
                    _updateTimer.Dispose();
                    _updateTimer = null;
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

                    _isConnectedVariable = CreateVariable(_statusFolder, "IsConnected", "IsConnected", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _isConnectedVariable.Value = false;

                    _temperatureVariable = CreateVariable(_statusFolder, "Temperature", "Temperature", DataTypeIds.Double, ValueRanks.Scalar);
                    _temperatureVariable.Value = 0.0;

                    _targetTemperatureVariable = CreateVariable(_statusFolder, "TargetTemperature", "Target Temperature", DataTypeIds.Double, ValueRanks.Scalar);
                    _targetTemperatureVariable.Value = 0.0;

                    _shakingRpmVariable = CreateVariable(_statusFolder, "ShakingRpm", "Shaking RPM", DataTypeIds.Int32, ValueRanks.Scalar);
                    _shakingRpmVariable.Value = 0;

                    _targetShakingRpmVariable = CreateVariable(_statusFolder, "TargetShakingRpm", "Target Shaking RPM", DataTypeIds.Int32, ValueRanks.Scalar);
                    _targetShakingRpmVariable.Value = 0;

                    _isShakingVariable = CreateVariable(_statusFolder, "IsShaking", "Is Shaking", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _isShakingVariable.Value = false;

                    _isClampClosedVariable = CreateVariable(_statusFolder, "IsClampClosed", "Is Clamp Closed", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _isClampClosedVariable.Value = false;

                    _connectedDeviceSerialVariable = CreateVariable(_statusFolder, "ConnectedDeviceSerial", "Connected Device Serial", DataTypeIds.String, ValueRanks.Scalar);
                    _connectedDeviceSerialVariable.Value = string.Empty;

                    // Create device info variables
                    FolderState deviceInfoFolder = CreateFolder(_statusFolder, "DeviceInfo", "DeviceInfo");

                    _serialNumberVariable = CreateVariable(deviceInfoFolder, "SerialNumber", "SerialNumber", DataTypeIds.String, ValueRanks.Scalar);
                    _serialNumberVariable.Value = string.Empty;
                    _serialNumberVariable.Description = new LocalizedText("en", "The serial number of the connected device");

                    _deviceNameVariable = CreateVariable(deviceInfoFolder, "DeviceName", "DeviceName", DataTypeIds.String, ValueRanks.Scalar);
                    _deviceNameVariable.Value = string.Empty;
                    _deviceNameVariable.Description = new LocalizedText("en", "The name of the connected device");

                    _deviceTypeVariable = CreateVariable(deviceInfoFolder, "DeviceType", "DeviceType", DataTypeIds.String, ValueRanks.Scalar);
                    _deviceTypeVariable.Value = string.Empty;
                    _deviceTypeVariable.Description = new LocalizedText("en", "The type of the connected device");

                    // Create discovered devices folder
                    _discoveredDevicesFolder = CreateFolder(_statusFolder, "DiscoveredDevices", "Discovered Devices");

                    // Add device count variable
                    _deviceCountVariable = CreateVariable(_discoveredDevicesFolder, "DeviceCount", "Device Count", DataTypeIds.Int32, ValueRanks.Scalar);
                    _deviceCountVariable.Value = 0;

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

                    // Create Connect method
                    MethodState connectMethod = CreateMethod(_commandsFolder, "Connect", "Connect");

                    // Define input arguments for Connect (device serial)
                    connectMethod.InputArguments = new PropertyState<Argument[]>(connectMethod);
                    connectMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    connectMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    connectMethod.InputArguments.DisplayName = connectMethod.InputArguments.BrowseName.Name;
                    connectMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    connectMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    connectMethod.InputArguments.DataType = DataTypeIds.Argument;
                    connectMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument serialArgument = new Argument();
                    serialArgument.Name = "DeviceSerial";
                    serialArgument.Description = new LocalizedText("Serial number of the device to connect to");
                    serialArgument.DataType = DataTypeIds.String;
                    serialArgument.ValueRank = ValueRanks.Scalar;

                    connectMethod.InputArguments.Value = new Argument[] { serialArgument };

                    // Define output arguments for Connect (result code)
                    connectMethod.OutputArguments = new PropertyState<Argument[]>(connectMethod);
                    connectMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    connectMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    connectMethod.OutputArguments.DisplayName = connectMethod.OutputArguments.BrowseName.Name;
                    connectMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    connectMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    connectMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    connectMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument connectResultArgument = new Argument();
                    connectResultArgument.Name = "Result";
                    connectResultArgument.Description = new LocalizedText("Result code: 0=success, -1=failure, -2=device not found");
                    connectResultArgument.DataType = DataTypeIds.Int32;
                    connectResultArgument.ValueRank = ValueRanks.Scalar;

                    connectMethod.OutputArguments.Value = new Argument[] { connectResultArgument };

                    // Create Disconnect method
                    MethodState disconnectMethod = CreateMethod(_commandsFolder, "Disconnect", "Disconnect");

                    // Define empty input arguments for Disconnect
                    disconnectMethod.InputArguments = new PropertyState<Argument[]>(disconnectMethod);
                    disconnectMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    disconnectMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    disconnectMethod.InputArguments.DisplayName = disconnectMethod.InputArguments.BrowseName.Name;
                    disconnectMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    disconnectMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    disconnectMethod.InputArguments.DataType = DataTypeIds.Argument;
                    disconnectMethod.InputArguments.ValueRank = ValueRanks.OneDimension;
                    disconnectMethod.InputArguments.Value = new Argument[0]; // No input arguments

                    // Define output arguments for Disconnect (result code)
                    disconnectMethod.OutputArguments = new PropertyState<Argument[]>(disconnectMethod);
                    disconnectMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    disconnectMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    disconnectMethod.OutputArguments.DisplayName = disconnectMethod.OutputArguments.BrowseName.Name;
                    disconnectMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    disconnectMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    disconnectMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    disconnectMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument disconnectResultArgument = new Argument();
                    disconnectResultArgument.Name = "Result";
                    disconnectResultArgument.Description = new LocalizedText("Result code: 0=success, -1=failure");
                    disconnectResultArgument.DataType = DataTypeIds.Int32;
                    disconnectResultArgument.ValueRank = ValueRanks.Scalar;

                    disconnectMethod.OutputArguments.Value = new Argument[] { disconnectResultArgument };

                    // Create SetTargetTemperature method
                    MethodState setTemperatureMethod = CreateMethod(_commandsFolder, "SetTargetTemperature", "Set Target Temperature");

                    // Define input arguments for SetTargetTemperature
                    setTemperatureMethod.InputArguments = new PropertyState<Argument[]>(setTemperatureMethod);
                    setTemperatureMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    setTemperatureMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    setTemperatureMethod.InputArguments.DisplayName = setTemperatureMethod.InputArguments.BrowseName.Name;
                    setTemperatureMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    setTemperatureMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    setTemperatureMethod.InputArguments.DataType = DataTypeIds.Argument;
                    setTemperatureMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument temperatureArgument = new Argument();
                    temperatureArgument.Name = "Temperature";
                    temperatureArgument.Description = new LocalizedText("Target temperature in degrees Celsius");
                    temperatureArgument.DataType = DataTypeIds.Double;
                    temperatureArgument.ValueRank = ValueRanks.Scalar;

                    setTemperatureMethod.InputArguments.Value = new Argument[] { temperatureArgument };

                    // Define output arguments for SetTargetTemperature (result code)
                    setTemperatureMethod.OutputArguments = new PropertyState<Argument[]>(setTemperatureMethod);
                    setTemperatureMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    setTemperatureMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    setTemperatureMethod.OutputArguments.DisplayName = setTemperatureMethod.OutputArguments.BrowseName.Name;
                    setTemperatureMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    setTemperatureMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    setTemperatureMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    setTemperatureMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument setTemperatureResultArgument = new Argument();
                    setTemperatureResultArgument.Name = "Result";
                    setTemperatureResultArgument.Description = new LocalizedText("Result code: 0=success, -1=failure");
                    setTemperatureResultArgument.DataType = DataTypeIds.Int32;
                    setTemperatureResultArgument.ValueRank = ValueRanks.Scalar;

                    setTemperatureMethod.OutputArguments.Value = new Argument[] { setTemperatureResultArgument };

                    // Create EnableTemperature method
                    MethodState enableTemperatureMethod = CreateMethod(_commandsFolder, "EnableTemperature", "Enable Temperature Control");

                    // Define input arguments for EnableTemperature
                    enableTemperatureMethod.InputArguments = new PropertyState<Argument[]>(enableTemperatureMethod);
                    enableTemperatureMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    enableTemperatureMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    enableTemperatureMethod.InputArguments.DisplayName = enableTemperatureMethod.InputArguments.BrowseName.Name;
                    enableTemperatureMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    enableTemperatureMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    enableTemperatureMethod.InputArguments.DataType = DataTypeIds.Argument;
                    enableTemperatureMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument enableTempArgument = new Argument();
                    enableTempArgument.Name = "Enable";
                    enableTempArgument.Description = new LocalizedText("True to enable temperature control, false to disable");
                    enableTempArgument.DataType = DataTypeIds.Boolean;
                    enableTempArgument.ValueRank = ValueRanks.Scalar;

                    enableTemperatureMethod.InputArguments.Value = new Argument[] { enableTempArgument };

                    // Define output arguments for EnableTemperature (result code)
                    enableTemperatureMethod.OutputArguments = new PropertyState<Argument[]>(enableTemperatureMethod);
                    enableTemperatureMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    enableTemperatureMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    enableTemperatureMethod.OutputArguments.DisplayName = enableTemperatureMethod.OutputArguments.BrowseName.Name;
                    enableTemperatureMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    enableTemperatureMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    enableTemperatureMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    enableTemperatureMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument enableTemperatureResultArgument = new Argument();
                    enableTemperatureResultArgument.Name = "Result";
                    enableTemperatureResultArgument.Description = new LocalizedText("Result code: 0=success, -1=failure");
                    enableTemperatureResultArgument.DataType = DataTypeIds.Int32;
                    enableTemperatureResultArgument.ValueRank = ValueRanks.Scalar;

                    enableTemperatureMethod.OutputArguments.Value = new Argument[] { enableTemperatureResultArgument };

                    // Create SetShakingRpm method
                    MethodState setRpmMethod = CreateMethod(_commandsFolder, "SetShakingRpm", "Set Shaking RPM");

                    // Define input arguments for SetShakingRpm
                    setRpmMethod.InputArguments = new PropertyState<Argument[]>(setRpmMethod);
                    setRpmMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    setRpmMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    setRpmMethod.InputArguments.DisplayName = setRpmMethod.InputArguments.BrowseName.Name;
                    setRpmMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    setRpmMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    setRpmMethod.InputArguments.DataType = DataTypeIds.Argument;
                    setRpmMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument rpmArgument = new Argument();
                    rpmArgument.Name = "Rpm";
                    rpmArgument.Description = new LocalizedText("Target shaking RPM");
                    rpmArgument.DataType = DataTypeIds.Int32;
                    rpmArgument.ValueRank = ValueRanks.Scalar;

                    setRpmMethod.InputArguments.Value = new Argument[] { rpmArgument };

                    // Define output arguments for SetShakingRpm (result code)
                    setRpmMethod.OutputArguments = new PropertyState<Argument[]>(setRpmMethod);
                    setRpmMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    setRpmMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    setRpmMethod.OutputArguments.DisplayName = setRpmMethod.OutputArguments.BrowseName.Name;
                    setRpmMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    setRpmMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    setRpmMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    setRpmMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument setRpmResultArgument = new Argument();
                    setRpmResultArgument.Name = "Result";
                    setRpmResultArgument.Description = new LocalizedText("Result code: 0=success, -1=failure");
                    setRpmResultArgument.DataType = DataTypeIds.Int32;
                    setRpmResultArgument.ValueRank = ValueRanks.Scalar;

                    setRpmMethod.OutputArguments.Value = new Argument[] { setRpmResultArgument };

                    // Create EnableShaking method
                    MethodState enableShakingMethod = CreateMethod(_commandsFolder, "EnableShaking", "Enable Shaking");

                    // Define input arguments for EnableShaking
                    enableShakingMethod.InputArguments = new PropertyState<Argument[]>(enableShakingMethod);
                    enableShakingMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    enableShakingMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    enableShakingMethod.InputArguments.DisplayName = enableShakingMethod.InputArguments.BrowseName.Name;
                    enableShakingMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    enableShakingMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    enableShakingMethod.InputArguments.DataType = DataTypeIds.Argument;
                    enableShakingMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument enableArgument = new Argument();
                    enableArgument.Name = "Enable";
                    enableArgument.Description = new LocalizedText("True to enable shaking, false to disable");
                    enableArgument.DataType = DataTypeIds.Boolean;
                    enableArgument.ValueRank = ValueRanks.Scalar;

                    enableShakingMethod.InputArguments.Value = new Argument[] { enableArgument };

                    // Define output arguments for EnableShaking (result code)
                    enableShakingMethod.OutputArguments = new PropertyState<Argument[]>(enableShakingMethod);
                    enableShakingMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    enableShakingMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    enableShakingMethod.OutputArguments.DisplayName = enableShakingMethod.OutputArguments.BrowseName.Name;
                    enableShakingMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    enableShakingMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    enableShakingMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    enableShakingMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument enableShakingResultArgument = new Argument();
                    enableShakingResultArgument.Name = "Result";
                    enableShakingResultArgument.Description = new LocalizedText("Result code: 0=success, -1=failure");
                    enableShakingResultArgument.DataType = DataTypeIds.Int32;
                    enableShakingResultArgument.ValueRank = ValueRanks.Scalar;

                    enableShakingMethod.OutputArguments.Value = new Argument[] { enableShakingResultArgument };

                    // Create SetClampState method
                    MethodState setClampStateMethod = CreateMethod(_commandsFolder, "SetClampState", "Set Clamp State");

                    // Define input arguments for SetClampState
                    setClampStateMethod.InputArguments = new PropertyState<Argument[]>(setClampStateMethod);
                    setClampStateMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    setClampStateMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    setClampStateMethod.InputArguments.DisplayName = setClampStateMethod.InputArguments.BrowseName.Name;
                    setClampStateMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    setClampStateMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    setClampStateMethod.InputArguments.DataType = DataTypeIds.Argument;
                    setClampStateMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument closedArgument = new Argument();
                    closedArgument.Name = "Closed";
                    closedArgument.Description = new LocalizedText("True to close clamps, false to open");
                    closedArgument.DataType = DataTypeIds.Boolean;
                    closedArgument.ValueRank = ValueRanks.Scalar;

                    setClampStateMethod.InputArguments.Value = new Argument[] { closedArgument };

                    // Define output arguments for SetClampState (result code)
                    setClampStateMethod.OutputArguments = new PropertyState<Argument[]>(setClampStateMethod);
                    setClampStateMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    setClampStateMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    setClampStateMethod.OutputArguments.DisplayName = setClampStateMethod.OutputArguments.BrowseName.Name;
                    setClampStateMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    setClampStateMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    setClampStateMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    setClampStateMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument setClampStateResultArgument = new Argument();
                    setClampStateResultArgument.Name = "Result";
                    setClampStateResultArgument.Description = new LocalizedText("Result code: 0=success, -1=failure");
                    setClampStateResultArgument.DataType = DataTypeIds.Int32;
                    setClampStateResultArgument.ValueRank = ValueRanks.Scalar;

                    setClampStateMethod.OutputArguments.Value = new Argument[] { setClampStateResultArgument };

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
        /// Updates the Tekmatic status variables
        /// </summary>
        private void UpdateTekmaticStatus(object state)
        {
            try
            {
                if (_tekmatic == null)
                    return;

                // Update the status variables
                if (_isConnectedVariable != null)
                {
                    _isConnectedVariable.Value = _tekmatic.IsConnected();
                    _isConnectedVariable.ClearChangeMasks(SystemContext, false);
                }

                // Only update the other variables if connected
                if (_tekmatic.IsConnected())
                {
                    if (_temperatureVariable != null)
                    {
                        _temperatureVariable.Value = _tekmatic.GetTemperature();
                        _temperatureVariable.ClearChangeMasks(SystemContext, false);
                    }

                    if (_targetTemperatureVariable != null)
                    {
                        _targetTemperatureVariable.Value = _tekmatic.GetTargetTemperature();
                        _targetTemperatureVariable.ClearChangeMasks(SystemContext, false);
                    }

                    if (_shakingRpmVariable != null)
                    {
                        _shakingRpmVariable.Value = _tekmatic.GetShakingRpm();
                        _shakingRpmVariable.ClearChangeMasks(SystemContext, false);
                    }

                    if (_isShakingVariable != null)
                    {
                        _isShakingVariable.Value = _tekmatic.IsShaking();
                        _isShakingVariable.ClearChangeMasks(SystemContext, false);
                    }

                    if (_isClampClosedVariable != null)
                    {
                        _isClampClosedVariable.Value = _tekmatic.IsClampClosed();
                        _isClampClosedVariable.ClearChangeMasks(SystemContext, false);
                    }

                    // Update device info
                    var device = _tekmatic.GetConnectedDevice();
                    if (device != null)
                    {
                        if (_serialNumberVariable != null)
                        {
                            _serialNumberVariable.Value = device.Serial;
                            _serialNumberVariable.ClearChangeMasks(SystemContext, false);
                        }

                        if (_deviceNameVariable != null)
                        {
                            _deviceNameVariable.Value = device.Name;
                            _deviceNameVariable.ClearChangeMasks(SystemContext, false);
                        }

                        if (_deviceTypeVariable != null)
                        {
                            _deviceTypeVariable.Value = device.Type;
                            _deviceTypeVariable.ClearChangeMasks(SystemContext, false);
                        }

                        if (_connectedDeviceSerialVariable != null)
                        {
                            _connectedDeviceSerialVariable.Value = device.Serial;
                            _connectedDeviceSerialVariable.ClearChangeMasks(SystemContext, false);
                        }
                    }
                }
                else
                {
                    // Clear device info if not connected
                    if (_serialNumberVariable != null)
                    {
                        _serialNumberVariable.Value = string.Empty;
                        _serialNumberVariable.ClearChangeMasks(SystemContext, false);
                    }

                    if (_deviceNameVariable != null)
                    {
                        _deviceNameVariable.Value = string.Empty;
                        _deviceNameVariable.ClearChangeMasks(SystemContext, false);
                    }

                    if (_deviceTypeVariable != null)
                    {
                        _deviceTypeVariable.Value = string.Empty;
                        _deviceTypeVariable.ClearChangeMasks(SystemContext, false);
                    }

                    if (_connectedDeviceSerialVariable != null)
                    {
                        _connectedDeviceSerialVariable.Value = string.Empty;
                        _connectedDeviceSerialVariable.ClearChangeMasks(SystemContext, false);
                    }

                    if (_temperatureVariable != null)
                    {
                        _temperatureVariable.Value = 0.0;
                        _temperatureVariable.ClearChangeMasks(SystemContext, false);
                    }

                    if (_targetTemperatureVariable != null)
                    {
                        _targetTemperatureVariable.Value = 0.0;
                        _targetTemperatureVariable.ClearChangeMasks(SystemContext, false);
                    }

                    if (_shakingRpmVariable != null)
                    {
                        _shakingRpmVariable.Value = 0;
                        _shakingRpmVariable.ClearChangeMasks(SystemContext, false);
                    }

                    if (_isShakingVariable != null)
                    {
                        _isShakingVariable.Value = false;
                        _isShakingVariable.ClearChangeMasks(SystemContext, false);
                    }

                    if (_isClampClosedVariable != null)
                    {
                        _isClampClosedVariable.Value = false;
                        _isClampClosedVariable.ClearChangeMasks(SystemContext, false);
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
                        UpdateDeviceFolders();
                        outputArguments[0] = deviceCount;
                        return ServiceResult.Good;

                    case "Connect":
                        // Check if this is a device-specific Connect method
                        if (methodState.Parent != null && methodState.Parent.BrowseName.Name.StartsWith("Device_"))
                        {
                            // Get the device from the folder name
                            string folderName = methodState.Parent.BrowseName.Name;
                            string deviceSerial = folderName.Replace("Device_", "");

                            // Find the device in the discovered devices list
                            var devices = _tekmatic.GetDiscoveredDevices();
                            var device = devices.FirstOrDefault(d => d.HasDevice && d.Serial == deviceSerial);

                            if (device != null)
                            {
                                // Connect to the device slot
                                int result = _tekmatic.ConnectToSlot(device.SlotId);
                                outputArguments[0] = result;
                                return ServiceResult.Good;
                            }
                            else
                            {
                                return ServiceResult.Create(StatusCodes.BadInvalidArgument, "Device not found");
                            }
                        }
                        else if (inputArguments.Count > 0)
                        {
                            // This is the general Connect method with a slot ID parameter
                            int slotId;
                            if (int.TryParse(inputArguments[0].ToString(), out slotId))
                            {
                                int result = _tekmatic.ConnectToSlot(slotId);
                                outputArguments[0] = result;
                                return ServiceResult.Good;
                            }
                            else
                            {
                                // Try to connect by serial number
                                string deviceSerial = inputArguments[0].ToString();
                                var devices = _tekmatic.GetDiscoveredDevices();
                                var device = devices.FirstOrDefault(d => d.HasDevice && d.Serial == deviceSerial);

                                if (device != null)
                                {
                                    int result = _tekmatic.ConnectToSlot(device.SlotId);
                                    outputArguments[0] = result;
                                    return ServiceResult.Good;
                                }
                                else
                                {
                                    return ServiceResult.Create(StatusCodes.BadInvalidArgument, "Device not found");
                                }
                            }
                        }
                        return ServiceResult.Create(StatusCodes.BadInvalidArgument, "Invalid slot ID or serial");

                    case "Disconnect":
                        int disconnectResult = _tekmatic.Disconnect();
                        outputArguments[0] = disconnectResult;
                        return ServiceResult.Good;

                    case "SetTargetTemperature":
                        if (inputArguments.Count > 0)
                        {
                            double temperature = Convert.ToDouble(inputArguments[0]);
                            int result = _tekmatic.SetTargetTemperature(temperature);
                            outputArguments[0] = result;
                            return ServiceResult.Good;
                        }
                        return ServiceResult.Create(StatusCodes.BadInvalidArgument, "Invalid temperature");

                    case "EnableTemperature":
                        if (inputArguments.Count > 0)
                        {
                            bool enable = Convert.ToBoolean(inputArguments[0]);
                            int result = _tekmatic.EnableTemperature(enable);
                            outputArguments[0] = result;
                            return ServiceResult.Good;
                        }
                        return ServiceResult.Create(StatusCodes.BadInvalidArgument, "Invalid enable value");

                    case "SetShakingRpm":
                        if (inputArguments.Count > 0)
                        {
                            int rpm = Convert.ToInt32(inputArguments[0]);
                            int result = _tekmatic.SetShakingRpm(rpm);
                            outputArguments[0] = result;
                            return ServiceResult.Good;
                        }
                        return ServiceResult.Create(StatusCodes.BadInvalidArgument, "Invalid RPM");

                    case "EnableShaking":
                        if (inputArguments.Count > 0)
                        {
                            bool enable = Convert.ToBoolean(inputArguments[0]);
                            int result = _tekmatic.EnableShaking(enable);
                            outputArguments[0] = result;
                            return ServiceResult.Good;
                        }
                        return ServiceResult.Create(StatusCodes.BadInvalidArgument, "Invalid enable value");

                    case "SetClampState":
                        if (inputArguments.Count > 0)
                        {
                            bool closed = Convert.ToBoolean(inputArguments[0]);
                            int result = _tekmatic.SetClampState(closed);
                            outputArguments[0] = result;
                            return ServiceResult.Good;
                        }
                        return ServiceResult.Create(StatusCodes.BadInvalidArgument, "Invalid closed value");

                    case "ClearErrorCodes":
                        int clearResult = _tekmatic.ClearErrorCodes();
                        outputArguments[0] = clearResult;
                        return ServiceResult.Good;

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
