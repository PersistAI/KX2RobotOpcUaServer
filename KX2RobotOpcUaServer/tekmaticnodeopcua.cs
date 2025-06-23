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
        // Properties to store Tekmatic state for each slot (1-6)
        private Dictionary<int, bool> _isShaking = new Dictionary<int, bool>();
        private Dictionary<int, double> _temperature = new Dictionary<int, double>();
        private Dictionary<int, double> _targetTemperature = new Dictionary<int, double>();
        private Dictionary<int, int> _shakingRpm = new Dictionary<int, int>();
        private Dictionary<int, int> _targetShakingRpm = new Dictionary<int, int>();
        private bool _isClamped = false;

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

            // Initialize dictionaries for each slot (1-6)
            for (int slot = 1; slot <= 6; slot++)
            {
                _isShaking[slot] = false;
                _temperature[slot] = 0.0;
                _targetTemperature[slot] = 0.0;
                _shakingRpm[slot] = 0;
                _targetShakingRpm[slot] = 0;
            }
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
                Thread.Sleep(100);

                // Read the response
                string response = _inhecoController.ReadSync();
                Console.WriteLine($"Received response: {response}");

                // Wait before allowing another command to be sent
                Thread.Sleep(100);

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
                Thread.Sleep(100);

                // Read the response (no logging)
                string response = _inhecoController.ReadSync();

                // Wait before allowing another command to be sent
                Thread.Sleep(100);

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
        /// Get the current temperature for the specified slot
        /// </summary>
        /// <param name="slotId">The slot ID (1-6) to get the temperature for</param>
        /// <returns>The current temperature in degrees Celsius</returns>
        public double GetTemperature(int slotId)
        {
            if (slotId < 1 || slotId > 6)
                return 0.0;

            return GetTemperatureForSlot(slotId);
        }

        /// <summary>
        /// Get the current temperature for a specific slot
        /// </summary>
        public double GetTemperatureForSlot(int slotId)
        {
            try
            {
                if (slotId < 1 || slotId > 6)
                    return 0.0;

                // Get the current temperature
                string response = SendCommandSilent($"{slotId}RAT");
                if (!string.IsNullOrEmpty(response) && response.Length > 5)
                {
                    double temp;
                    if (double.TryParse(response.Substring(5), out temp))
                    {
                        // Temperature is reported in 1/10 °C, e.g: 345 = 34.5 °C
                        double temperature = temp / 10.0;

                        // Update the cached temperature for this slot
                        _temperature[slotId] = temperature;

                        return temperature;
                    }
                }

                // If we couldn't get the temperature, return the last known value
                return _temperature[slotId];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting temperature for slot {slotId}: {ex.Message}");

                // Return the last known value
                return _temperature[slotId];
            }
        }

        /// <summary>
        /// Get the target temperature for the specified slot
        /// </summary>
        /// <param name="slotId">The slot ID (1-6) to get the target temperature for</param>
        /// <returns>The target temperature in degrees Celsius</returns>
        public double GetTargetTemperature(int slotId)
        {
            if (slotId < 1 || slotId > 6)
                return 0.0;

            return GetTargetTemperatureForSlot(slotId);
        }

        /// <summary>
        /// Get the target temperature for a specific slot
        /// </summary>
        public double GetTargetTemperatureForSlot(int slotId)
        {
            try
            {
                if (slotId < 1 || slotId > 6)
                    return 0.0;

                // Get the target temperature
                string response = SendCommandSilent($"{slotId}RTT");
                if (!string.IsNullOrEmpty(response) && response.Length > 5)
                {
                    double temp;
                    if (double.TryParse(response.Substring(5), out temp))
                    {
                        // Temperature is reported in 1/10 °C, e.g: 345 = 34.5 °C
                        double targetTemperature = temp / 10.0;

                        // Update the cached target temperature for this slot
                        _targetTemperature[slotId] = targetTemperature;

                        return targetTemperature;
                    }
                }

                // If we couldn't get the target temperature, return the last known value
                return _targetTemperature[slotId];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting target temperature for slot {slotId}: {ex.Message}");

                // Return the last known value
                return _targetTemperature[slotId];
            }
        }

        /// <summary>
        /// Set the target temperature for the specified slot
        /// </summary>
        /// <param name="slotId">The slot ID (1-6) to set the target temperature for</param>
        /// <param name="temperature">The target temperature in degrees Celsius</param>
        /// <returns>0 for success, -1 for failure</returns>
        public int SetTargetTemperature(int slotId, double temperature)
        {
            if (slotId < 1 || slotId > 6)
                return -1;

            return SetTargetTemperatureForSlot(slotId, temperature);
        }

        /// <summary>
        /// Set the target temperature for a specific slot
        /// </summary>
        public int SetTargetTemperatureForSlot(int slotId, double temperature)
        {
            try
            {
                if (slotId < 1 || slotId > 6)
                    return -1;

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

                // Update the cached target temperature for this slot
                _targetTemperature[slotId] = temperature;

                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting target temperature for slot {slotId}: {ex.Message}");
                return -1; // Error
            }
        }

        /// <summary>
        /// Enable or disable temperature control for the specified slot
        /// </summary>
        /// <param name="slotId">The slot ID (1-6) to enable/disable temperature control for</param>
        /// <param name="enable">True to enable temperature control, false to disable</param>
        /// <returns>0 for success, -1 for failure</returns>
        public int EnableTemperature(int slotId, bool enable)
        {
            if (slotId < 1 || slotId > 6)
                return -1;

            return EnableTemperatureForSlot(slotId, enable);
        }

        /// <summary>
        /// Enable or disable temperature control for a specific slot
        /// </summary>
        public int EnableTemperatureForSlot(int slotId, bool enable)
        {
            try
            {
                if (slotId < 1 || slotId > 6)
                    return -1;

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
        /// Get the current shaking RPM for the specified slot
        /// </summary>
        /// <param name="slotId">The slot ID (1-6) to get the shaking RPM for</param>
        /// <returns>The current shaking RPM</returns>
        public int GetShakingRpm(int slotId)
        {
            if (slotId < 1 || slotId > 6)
                return 0;

            return GetShakingRpmForSlot(slotId);
        }

        /// <summary>
        /// Get the current shaking RPM for a specific slot
        /// </summary>
        public int GetShakingRpmForSlot(int slotId)
        {
            try
            {
                if (slotId < 1 || slotId > 6)
                    return 0;

                // Get the current RPM
                string response = SendCommandSilent($"{slotId}RSR");
                if (!string.IsNullOrEmpty(response) && response.Length > 5)
                {
                    int rpm;
                    if (int.TryParse(response.Substring(5), out rpm))
                    {
                        // Update the cached RPM for this slot
                        _shakingRpm[slotId] = rpm;

                        return rpm;
                    }
                }

                // If we couldn't get the RPM, return the last known value
                return _shakingRpm[slotId];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting shaking RPM for slot {slotId}: {ex.Message}");

                // Return the last known value
                return _shakingRpm[slotId];
            }
        }

        /// <summary>
        /// Set the target shaking RPM for the specified slot
        /// </summary>
        /// <param name="slotId">The slot ID (1-6) to set the shaking RPM for</param>
        /// <param name="rpm">The target shaking RPM</param>
        /// <returns>0 for success, -1 for failure</returns>
        public int SetShakingRpm(int slotId, int rpm)
        {
            if (slotId < 1 || slotId > 6)
                return -1;

            return SetShakingRpmForSlot(slotId, rpm);
        }

        /// <summary>
        /// Set the target shaking RPM for a specific slot
        /// </summary>
        public int SetShakingRpmForSlot(int slotId, int rpm)
        {
            try
            {
                if (slotId < 1 || slotId > 6)
                    return -1;

                Console.WriteLine($"Setting shaking RPM for slot {slotId} to {rpm}...");

                // Set the target RPM
                string response = SendCommand($"{slotId}SSR{rpm}");
                if (string.IsNullOrEmpty(response) || !response.StartsWith($"{slotId}ssr"))
                {
                    Console.WriteLine($"Failed to set shaking RPM for slot {slotId}: {response}");
                    return -1;
                }

                // Update the cached target RPM for this slot
                _targetShakingRpm[slotId] = rpm;

                // If shaking is enabled, update the current RPM
                if (_isShaking.ContainsKey(slotId) && _isShaking[slotId])
                {
                    _shakingRpm[slotId] = rpm;
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
        /// Enable or disable shaking for the specified slot
        /// </summary>
        /// <param name="slotId">The slot ID (1-6) to enable/disable shaking for</param>
        /// <param name="enable">True to enable shaking, false to disable</param>
        /// <returns>0 for success, -1 for failure</returns>
        public int EnableShaking(int slotId, bool enable)
        {
            if (slotId < 1 || slotId > 6)
                return -1;

            return EnableShakingForSlot(slotId, enable);
        }

        /// <summary>
        /// Enable or disable shaking for a specific slot
        /// </summary>
        public int EnableShakingForSlot(int slotId, bool enable)
        {
            try
            {
                if (slotId < 1 || slotId > 6)
                    return -1;

                Console.WriteLine($"{(enable ? "Enabling" : "Disabling")} shaking for slot {slotId}...");

                // Enable or disable shaking
                string response = SendCommand($"{slotId}ASE{(enable ? "1" : "0")}");
                if (string.IsNullOrEmpty(response) || !response.StartsWith($"{slotId}ase"))
                {
                    Console.WriteLine($"Failed to {(enable ? "enable" : "disable")} shaking for slot {slotId}: {response}");
                    return -1;
                }

                // Update the cached shaking state for this slot
                _isShaking[slotId] = enable;

                // Update the current RPM based on the shaking state
                if (enable)
                {
                    _shakingRpm[slotId] = _targetShakingRpm[slotId];
                }
                else
                {
                    _shakingRpm[slotId] = 0;
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
            try
            {
                // Get the clamp status
                string response = SendCommandSilent("0RCS");
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
        /// Check if a device is shaking in the specified slot
        /// </summary>
        /// <param name="slotId">The slot ID (1-6) to check</param>
        /// <returns>True if the device is shaking, false otherwise</returns>
        public bool IsShaking(int slotId)
        {
            if (slotId < 1 || slotId > 6)
                return false;

            return _isShaking.ContainsKey(slotId) && _isShaking[slotId];
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
                string response = SendCommandSilent("0RFV0");
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
        private FolderState _discoveredDevicesFolder;

        // Status variables
        private BaseDataVariableState _isConnectedVariable;
        private BaseDataVariableState _temperatureVariable;
        private BaseDataVariableState _targetTemperatureVariable;
        private BaseDataVariableState _shakingRpmVariable;
        private BaseDataVariableState _targetShakingRpmVariable;
        private BaseDataVariableState _isShakingVariable;
        private BaseDataVariableState _isClampClosedVariable;
        private BaseDataVariableState _deviceCountVariable;
        private BaseDataVariableState _serialNumberVariable;
        private BaseDataVariableState _deviceNameVariable;
        private BaseDataVariableState _deviceTypeVariable;
        private BaseDataVariableState _connectedDeviceSerialVariable;

        // List to keep track of device variables
        private List<FolderState> _deviceFolders = new List<FolderState>();

        // Dictionary to keep track of slot folders
        private Dictionary<int, FolderState> _slotFolders = new Dictionary<int, FolderState>();

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
                _updateTimer = new Timer(UpdateTekmaticStatus, null, NORMAL_INTERVAL_MS, NORMAL_INTERVAL_MS);

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

                    // Create a folder for each slot (1-6)
                    FolderState slotsFolder = CreateFolder(_statusFolder, "Slots", "Slots");
                    for (int slot = 1; slot <= 6; slot++)
                    {
                        // Create a folder for this slot
                        FolderState slotFolder = CreateFolder(slotsFolder, $"Slot{slot}", $"Slot {slot}");
                        _slotFolders[slot] = slotFolder;

                        // Add device info variables
                        BaseDataVariableState hasDeviceVar = CreateVariable(slotFolder, "HasDevice", "Has Device", DataTypeIds.Boolean, ValueRanks.Scalar);
                        hasDeviceVar.Value = false;

                        BaseDataVariableState deviceNameVar = CreateVariable(slotFolder, "DeviceName", "Device Name", DataTypeIds.String, ValueRanks.Scalar);
                        deviceNameVar.Value = "No Device";

                        BaseDataVariableState deviceTypeVar = CreateVariable(slotFolder, "DeviceType", "Device Type", DataTypeIds.String, ValueRanks.Scalar);
                        deviceTypeVar.Value = string.Empty;

                        BaseDataVariableState deviceSerialVar = CreateVariable(slotFolder, "DeviceSerial", "Device Serial", DataTypeIds.String, ValueRanks.Scalar);
                        deviceSerialVar.Value = string.Empty;

                        // Add status variables
                        BaseDataVariableState tempVar = CreateVariable(slotFolder, "Temperature", "Temperature", DataTypeIds.Double, ValueRanks.Scalar);
                        tempVar.Value = 0.0;

                        BaseDataVariableState targetTempVar = CreateVariable(slotFolder, "TargetTemperature", "Target Temperature", DataTypeIds.Double, ValueRanks.Scalar);
                        targetTempVar.Value = 0.0;

                        BaseDataVariableState rpmVar = CreateVariable(slotFolder, "ShakingRpm", "Shaking RPM", DataTypeIds.Int32, ValueRanks.Scalar);
                        rpmVar.Value = 0;

                        BaseDataVariableState isShakingVar = CreateVariable(slotFolder, "IsShaking", "Is Shaking", DataTypeIds.Boolean, ValueRanks.Scalar);
                        isShakingVar.Value = false;
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

                    Argument slotArgument = new Argument();
                    slotArgument.Name = "SlotId";
                    slotArgument.Description = new LocalizedText("Slot ID (1-6)");
                    slotArgument.DataType = DataTypeIds.Int32;
                    slotArgument.ValueRank = ValueRanks.Scalar;

                    Argument temperatureArgument = new Argument();
                    temperatureArgument.Name = "Temperature";
                    temperatureArgument.Description = new LocalizedText("Target temperature in degrees Celsius");
                    temperatureArgument.DataType = DataTypeIds.Double;
                    temperatureArgument.ValueRank = ValueRanks.Scalar;

                    setTemperatureMethod.InputArguments.Value = new Argument[] { slotArgument, temperatureArgument };

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

                    Argument slotArgument2 = new Argument();
                    slotArgument2.Name = "SlotId";
                    slotArgument2.Description = new LocalizedText("Slot ID (1-6)");
                    slotArgument2.DataType = DataTypeIds.Int32;
                    slotArgument2.ValueRank = ValueRanks.Scalar;

                    Argument enableTempArgument = new Argument();
                    enableTempArgument.Name = "Enable";
                    enableTempArgument.Description = new LocalizedText("True to enable temperature control, false to disable");
                    enableTempArgument.DataType = DataTypeIds.Boolean;
                    enableTempArgument.ValueRank = ValueRanks.Scalar;

                    enableTemperatureMethod.InputArguments.Value = new Argument[] { slotArgument2, enableTempArgument };

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

                    setRpmMethod.InputArguments.Value = new Argument[] { slotArgument3, rpmArgument };

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

                    Argument slotArgument4 = new Argument();
                    slotArgument4.Name = "SlotId";
                    slotArgument4.Description = new LocalizedText("Slot ID (1-6)");
                    slotArgument4.DataType = DataTypeIds.Int32;
                    slotArgument4.ValueRank = ValueRanks.Scalar;

                    Argument enableArgument = new Argument();
                    enableArgument.Name = "Enable";
                    enableArgument.Description = new LocalizedText("True to enable shaking, false to disable");
                    enableArgument.DataType = DataTypeIds.Boolean;
                    enableArgument.ValueRank = ValueRanks.Scalar;

                    enableShakingMethod.InputArguments.Value = new Argument[] { slotArgument4, enableArgument };

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
        /// Updates the device status variables
        /// </summary>
        private void UpdateDeviceStatus()
        {
            try
            {
                // Update the clamp status
                if (_isClampClosedVariable != null)
                {
                    _isClampClosedVariable.Value = _tekmatic.IsClampClosed();
                    _isClampClosedVariable.ClearChangeMasks(SystemContext, false);
                }

                // Update status for each slot
                for (int slot = 1; slot <= 6; slot++)
                {
                    // Get the slot folder
                    FolderState slotFolder = _slotFolders.ContainsKey(slot) ? _slotFolders[slot] : null;
                    if (slotFolder == null)
                        continue;

                    // Get the device in this slot
                    var device = _tekmatic.GetDeviceBySlot(slot);
                    bool hasDevice = device != null && device.HasDevice;

                    // Update device info variables
                    UpdateVariable(slotFolder, "HasDevice", hasDevice);
                    UpdateVariable(slotFolder, "DeviceName", hasDevice ? device.Name : "No Device");
                    UpdateVariable(slotFolder, "DeviceType", hasDevice ? device.Type : "");
                    UpdateVariable(slotFolder, "DeviceSerial", hasDevice ? device.Serial : "");

                    // Only update status variables if the device exists in this slot
                    if (hasDevice)
                    {
                        // Update status variables
                        UpdateVariable(slotFolder, "Temperature", _tekmatic.GetTemperature(slot));
                        UpdateVariable(slotFolder, "TargetTemperature", _tekmatic.GetTargetTemperature(slot));
                        UpdateVariable(slotFolder, "ShakingRpm", _tekmatic.GetShakingRpm(slot));
                        UpdateVariable(slotFolder, "IsShaking", _tekmatic.IsShaking(slot));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating device status: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the Tekmatic status variables with backoff strategy
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

                    // Only proceed with other updates if the device is connected
                    UpdateDeviceStatus();
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
        /// Helper method to update a variable in a folder
        /// </summary>
        private void UpdateVariable(FolderState folder, string name, object value)
        {
            try
            {
                // Find the variable by name
                BaseDataVariableState variable = null;

                // Create a list to hold the children
                List<BaseInstanceState> children = new List<BaseInstanceState>();

                // Get the children using the proper method with context
                folder.GetChildren(SystemContext, children);

                // Now iterate through the list
                foreach (var child in children)
                {
                    if (child is BaseDataVariableState && child.BrowseName.Name == name)
                    {
                        variable = (BaseDataVariableState)child;
                        break;
                    }
                }

                // Update the variable if found
                if (variable != null)
                {
                    variable.Value = value;
                    variable.ClearChangeMasks(SystemContext, false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating variable {name}: {ex.Message}");
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

                    case "SetTargetTemperature":
                        if (inputArguments.Count > 1)
                        {
                            int slotId = Convert.ToInt32(inputArguments[0]);
                            double temperature = Convert.ToDouble(inputArguments[1]);
                            int result = _tekmatic.SetTargetTemperature(slotId, temperature);
                            outputArguments[0] = result;
                            return ServiceResult.Good;
                        }
                        return ServiceResult.Create(StatusCodes.BadInvalidArgument, "Invalid slot ID or temperature");

                    case "EnableTemperature":
                        if (inputArguments.Count > 1)
                        {
                            int slotId = Convert.ToInt32(inputArguments[0]);
                            bool enable = Convert.ToBoolean(inputArguments[1]);
                            int result = _tekmatic.EnableTemperature(slotId, enable);
                            outputArguments[0] = result;
                            return ServiceResult.Good;
                        }
                        return ServiceResult.Create(StatusCodes.BadInvalidArgument, "Invalid slot ID or enable value");

                    case "SetShakingRpm":
                        if (inputArguments.Count > 1)
                        {
                            int slotId = Convert.ToInt32(inputArguments[0]);
                            int rpm = Convert.ToInt32(inputArguments[1]);
                            int result = _tekmatic.SetShakingRpm(slotId, rpm);
                            outputArguments[0] = result;
                            return ServiceResult.Good;
                        }
                        return ServiceResult.Create(StatusCodes.BadInvalidArgument, "Invalid slot ID or RPM");

                    case "EnableShaking":
                        if (inputArguments.Count > 1)
                        {
                            int slotId = Convert.ToInt32(inputArguments[0]);
                            bool enable = Convert.ToBoolean(inputArguments[1]);
                            int result = _tekmatic.EnableShaking(slotId, enable);
                            outputArguments[0] = result;
                            return ServiceResult.Good;
                        }
                        return ServiceResult.Create(StatusCodes.BadInvalidArgument, "Invalid slot ID or enable value");

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
