using System;
using System.Collections.Generic;
using System.Threading;
using Opc.Ua;
using Opc.Ua.Server;
using Tecan.At.Measurement.Server;
using Tecan.At.Instrument.Common;
using Tecan.At.Common.Settings;
using LabEquipmentOpcUa;
using Tecan.At.Measurement;
using System.Linq;
using System.Diagnostics;
using Tecan.At.Communication.Port;
using Tecan.At.Instrument;


namespace TecanOpcUa
{
    /// <summary>
    /// Represents a discovered Tecan device
    /// </summary>
    public class TecanDevice
    {
        /// <summary>
        /// The name of the device (e.g., "Infinite 200Pro")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of the device (e.g., "READER")
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The serial number of the device (e.g., "1812003347")
        /// </summary>
        public string Serial { get; set; }

        /// <summary>
        /// The port of the device (e.g., "USB/USB0")
        /// </summary>
        public string Port { get; set; }

        /// <summary>
        /// The driver information for the device
        /// </summary>
        public string Driver { get; set; }

        /// <summary>
        /// The connection string for the device
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Creates a string representation of the device
        /// </summary>
        public override string ToString()
        {
            return $"{Name} ({Type}) - {Serial} on {Port}";
        }
    }

    /// <summary>
    /// Tecan Control class for communicating with Tecan devices
    /// </summary>
    public class TecanControl
    {
        // Properties to store Tecan state
        private bool _isConnected = false;
        private bool _isPlateIn = false;
        private string _serialNumber = "";
        private double _temperature = 37.0;
        private MeasurementServer _measurementServer = null;

        // List of discovered devices
        private List<TecanDevice> _discoveredDevices = new List<TecanDevice>();

        // Currently connected device
        private TecanDevice _connectedDevice = null;


        // Constructor
        public TecanControl()
        {
            // Initialize any required resources
        }

        /// <summary>
        /// Discovers available Tecan devices using the InstrumentServer methods
        /// </summary>
        /// <returns>Number of devices discovered</returns>
        public int DiscoverDevices()
        {
            try
            {
                Console.WriteLine("Discovering Tecan devices...");

                // Clear previous discoveries
                _discoveredDevices.Clear();

                try
                {
                    Console.WriteLine("Creating InstrumentServer instance...");
                    // Create instrument server
                    InstrumentServer instrumentServer = new InstrumentServer();
                    Console.WriteLine("InstrumentServer created successfully");

                    // We'll work with InstrumentOnPort objects directly instead of DeviceOnPort
                    List<InstrumentOnPort> instruments = null;

                    // Try multiple approaches to discover instruments
                    try
                    {
                        // Approach 1: Try using the Instruments property (which calls GetInstruments(""))
                        Console.WriteLine("Trying Instruments property...");
                        instruments = instrumentServer.Instruments;
                        Console.WriteLine($"Found {(instruments != null ? instruments.Count : 0)} instruments using Instruments property");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error using Instruments property: {ex.Message}");
                        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    }

                    // If that failed, try GetInstruments with specific filter strings
                    if (instruments == null || instruments.Count == 0)
                    {
                        try
                        {
                            // Try with USB READER filter
                            Console.WriteLine("Trying GetInstruments with USB READER filter...");
                            string connectionString = "PORTTYPE=USB, TYPE=READER";
                            Console.WriteLine($"Using connection string: {connectionString}");
                            instruments = instrumentServer.GetInstruments(connectionString);
                            Console.WriteLine($"Found {(instruments != null ? instruments.Count : 0)} instruments using GetInstruments with USB READER filter");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error using GetInstruments with USB READER filter: {ex.Message}");
                            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                        }
                    }

                    // If that failed, try with just USB filter
                    if (instruments == null || instruments.Count == 0)
                    {
                        try
                        {
                            Console.WriteLine("Trying GetInstruments with USB filter only...");
                            string connectionString = "PORTTYPE=USB";
                            Console.WriteLine($"Using connection string: {connectionString}");
                            instruments = instrumentServer.GetInstruments(connectionString);
                            Console.WriteLine($"Found {(instruments != null ? instruments.Count : 0)} instruments using GetInstruments with USB filter");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error using GetInstruments with USB filter: {ex.Message}");
                            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                        }
                    }

                    // If that failed, try with empty filter (should be same as Instruments property)
                    if (instruments == null || instruments.Count == 0)
                    {
                        try
                        {
                            Console.WriteLine("Trying GetInstruments with empty filter...");
                            instruments = instrumentServer.GetInstruments("");
                            Console.WriteLine($"Found {(instruments != null ? instruments.Count : 0)} instruments using GetInstruments with empty filter");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error using GetInstruments with empty filter: {ex.Message}");
                            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                        }
                    }

                    Console.WriteLine("Instrument discovery attempts completed");

                    if (instruments != null && instruments.Count > 0)
                    {
                        Console.WriteLine($"Found {instruments.Count} Tecan instruments");

                        // Convert InstrumentOnPort objects to our TecanDevice format
                        foreach (InstrumentOnPort instrument in instruments)
                        {
                            // Get the device information from the instrument
                            DeviceOnPort device = instrument.Device;

                            Console.WriteLine($"Processing instrument: {instrument.Instrument.InstrumentName} - {device.m_sInstrumentSerial}");

                            var tecanDevice = new TecanDevice
                            {
                                Name = instrument.Instrument.InstrumentName,
                                Type = device.m_sInstrumentType,
                                Serial = device.m_sInstrumentSerial,
                                Port = $"{device.m_sPortType}/{device.m_sPort}",
                                Driver = device.m_sDriver,
                                ConnectionString = BuildConnectionString(device)
                            };

                            _discoveredDevices.Add(tecanDevice);
                            Console.WriteLine($"  - {tecanDevice}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("No Tecan instruments found (instruments list is null or empty)");
                    }
                }
                catch (ArgumentNullException anex)
                {
                    Console.WriteLine($"ArgumentNullException in DetectDevices:");
                    Console.WriteLine($"Parameter: {anex.ParamName}");
                    Console.WriteLine($"Message: {anex.Message}");
                    Console.WriteLine($"Stack Trace: {anex.StackTrace}");
                    if (anex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {anex.InnerException}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error using InstrumentServer:");
                    Console.WriteLine($"Exception Type: {ex.GetType().FullName}");
                    Console.WriteLine($"Message: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {ex.InnerException}");
                    }
                }

                return _discoveredDevices.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error discovering devices: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Builds a connection string for a device
        /// </summary>
        private string BuildConnectionString(DeviceOnPort device)
        {
            return $"porttype={device.m_sPortType}, type={device.m_sInstrumentType.ToLower()}, option=default, name={device.m_sInstrumentName}";
        }

        /// <summary>
        /// Gets the list of discovered devices
        /// </summary>
        public List<TecanDevice> GetDiscoveredDevices()
        {
            return _discoveredDevices;
        }

        /// <summary>
        /// Gets the currently connected device
        /// </summary>
        public TecanDevice GetConnectedDevice()
        {
            return _connectedDevice;
        }

        /// <summary>
        /// Connect to a Tecan device by serial number
        /// </summary>
        /// <param name="deviceSerial">The serial number of the device to connect to</param>
        /// <returns>0 for success, -1 for failure, -2 for device not found</returns>
        public int ConnectBySerial(string deviceSerial)
        {
            try
            {
                if (_isConnected)
                {
                    Console.WriteLine("Already connected to a device. Disconnecting first...");
                    Disconnect();
                }

                Console.WriteLine($"Connecting to Tecan device with serial: {deviceSerial}");

                // Find the device in the discovered devices list
                var device = _discoveredDevices.FirstOrDefault(d => d.Serial == deviceSerial);

                if (device == null)
                {
                    Console.WriteLine($"Device with serial {deviceSerial} not found in discovered devices");
                    return -2; // Device not found
                }

                // Use the MeasurementServer to connect
                if (_measurementServer == null)
                {
                    _measurementServer = new MeasurementServer();
                }

                bool connected = _measurementServer.Connect(InstrumentConnectionMethod.Manually, device.ConnectionString);

                if (connected)
                {
                    _isConnected = true;
                    _connectedDevice = device;
                    _serialNumber = device.Serial;
                    Console.WriteLine($"Connected to Tecan device: {device}");
                    return 0; // Success
                }

                Console.WriteLine($"Failed to connect to Tecan device: {device}");
                return -1; // Failed
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to Tecan by serial: {ex.Message}");
                return -1; // Error
            }
        }

        /// <summary>
        /// Connect to the Tecan device - simple method with no parameters
        /// Uses the first discovered device or a default connection string
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

                // If we have discovered devices, use the first one
                if (_discoveredDevices.Count > 0)
                {
                    return ConnectBySerial(_discoveredDevices[0].Serial);
                }

                // Otherwise, use a default connection string
                Console.WriteLine("Connecting to Tecan device with default parameters...");

                // Use the MeasurementServer to connect
                if (_measurementServer == null)
                {
                    _measurementServer = new MeasurementServer();
                }

                bool connected = _measurementServer.Connect(InstrumentConnectionMethod.Manually, "porttype=USB, type=reader, option=default, name=*");

                if (connected)
                {
                    _isConnected = true;
                    _serialNumber = _measurementServer.ConnectedReader.Information.GetInstrumentSerial();

                    // Create a device object for the connected device
                    _connectedDevice = new TecanDevice
                    {
                        Name = _measurementServer.ConnectedReader.Information.GetProductName(),
                        Type = "READER",
                        Serial = _serialNumber,
                        Port = "USB/USB0",
                        Driver = "Unknown",
                        ConnectionString = "porttype=USB, type=reader, option=default, name=*"
                    };

                    Console.WriteLine($"Connected to Tecan device: {_serialNumber}");
                    return 0; // Success
                }

                Console.WriteLine("Failed to connect to Tecan device");
                return -1; // Failed
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to Tecan: {ex.Message}");
                return -1; // Error
            }
        }

        /// <summary>
        /// Disconnect from the Tecan device
        /// </summary>
        public int Disconnect()
        {
            try
            {
                if (!_isConnected)
                    return 0; // Already disconnected

                Console.WriteLine("Disconnecting from Tecan device...");

                // Use the MeasurementServer to disconnect
                if (_measurementServer != null)
                {
                    _measurementServer.Disconnect();
                    _measurementServer = null;
                }

                _isConnected = false;
                _connectedDevice = null;
                Console.WriteLine("Disconnected from Tecan device");
                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disconnecting from Tecan: {ex.Message}");
                return -1; // Error
            }
        }

        // Move plate in
        public int MovePlateIn()
        {
            if (!_isConnected)
                return -1;

            try
            {
                Console.WriteLine("Moving plate into reader...");
                // In a real implementation, this would control the actual device
                if (_measurementServer != null && _measurementServer.ConnectedReader != null)
                {
                    _measurementServer.ConnectedReader.Movement.PlateIn();
                }
                _isPlateIn = true;
                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error moving plate in: {ex.Message}");
                return -1; // Error
            }
        }

        // Move plate out
        public int MovePlateOut()
        {
            if (!_isConnected)
                return -1;

            try
            {
                Console.WriteLine("Moving plate out of reader...");
                // In a real implementation, this would control the actual device
                if (_measurementServer != null && _measurementServer.ConnectedReader != null)
                {
                    _measurementServer.ConnectedReader.Movement.PlateOut();
                }
                _isPlateIn = false;
                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error moving plate out: {ex.Message}");
                return -1; // Error
            }
        }

        // Get plate position
        public bool IsPlateIn()
        {
            if (!_isConnected)
                return false;

            try
            {
                // In a real implementation, this would query the actual device
                return _isPlateIn;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking plate position: {ex.Message}");
                return false;
            }
        }

        // Get temperature
        public double GetTemperature()
        {
            if (!_isConnected)
                return 0.0;

            try
            {
                // In a real implementation, this would query the actual device
                return _temperature;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting temperature: {ex.Message}");
                return 0.0;
            }
        }

        // Check if device is connected
        public bool IsConnected()
        {
            return _isConnected;
        }

        // Set temperature
        public int SetTemperature(double temperature)
        {
            if (!_isConnected)
                return -1;

            try
            {
                Console.WriteLine($"Setting temperature to {temperature}°C...");
                // In a real implementation, this would control the actual device
                _temperature = temperature;
                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting temperature: {ex.Message}");
                return -1; // Error
            }
        }
    }

    /// <summary>
    /// Tecan Node Manager for OPC UA Server
    /// </summary>
    public class TecanNodeManager : CustomNodeManager2, IEquipmentNodeManager, INodeManagerFactory
    {
        private const int UPDATE_INTERVAL_MS = 1000;

        // Private fields
        private TecanControl _tecan;
        private ushort _namespaceIndex;
        private uint _lastUsedId;
        private Timer _updateTimer;

        // Folders
        private FolderState _tecanFolder;
        private FolderState _statusFolder;
        private FolderState _commandsFolder;
        private FolderState _discoveredDevicesFolder;

        // Status variables
        private BaseDataVariableState _isConnectedVariable;
        private BaseDataVariableState _isPlateInVariable;
        private BaseDataVariableState _temperatureVariable;
        private BaseDataVariableState _serialNumberVariable;
        private BaseDataVariableState _productNameVariable;
        private BaseDataVariableState _modelVariable;
        private BaseDataVariableState _firmwareVersionVariable;
        private BaseDataVariableState _connectedDeviceSerialVariable;
        private BaseDataVariableState _deviceCountVariable;

        // List to keep track of device variables
        private List<FolderState> _deviceFolders = new List<FolderState>();

        // Constructor
        public TecanNodeManager(
            IServerInternal server,
            ApplicationConfiguration configuration,
            TecanControl tecan)
        : base(server, configuration, new string[] { "http://persist.com/Tecan" })
        {
            try
            {
                Console.WriteLine("TecanNodeManager constructor called");

                // Store the Tecan control
                _tecan = tecan;
                _lastUsedId = 0;

                // Start a timer to update the Tecan status
                _updateTimer = new Timer(UpdateTecanStatus, null, UPDATE_INTERVAL_MS, UPDATE_INTERVAL_MS);

                Console.WriteLine("TecanNodeManager constructor completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TecanNodeManager constructor: {ex.Message}");
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
            return new TecanNodeManager(server, configuration, _tecan);
        }

        // IEquipmentNodeManager implementation
        public void Initialize()
        {
            try
            {
                Console.WriteLine("TecanNodeManager.Initialize called - discovering devices");
                if (_tecan != null)
                {
                    // Discover devices first
                    _tecan.DiscoverDevices();

                    // Update the device folders in the address space
                    UpdateDeviceFolders();

                    // Don't automatically connect - let the user choose which device to connect to
                    Console.WriteLine("Tecan devices discovered. Ready for connection.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TecanNodeManager.Initialize: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the device folders in the address space based on discovered devices
        /// </summary>
        private void UpdateDeviceFolders()
        {
            try
            {
                if (_discoveredDevicesFolder == null || _tecan == null)
                    return;

                // Get the list of discovered devices
                var devices = _tecan.GetDiscoveredDevices();

                // Update the device count
                if (_deviceCountVariable != null)
                {
                    _deviceCountVariable.Value = devices.Count;
                }

                // Clear existing device folders
                foreach (var folder in _deviceFolders)
                {
                    _discoveredDevicesFolder.RemoveChild(folder);
                }
                _deviceFolders.Clear();

                // Create a folder for each device
                foreach (var device in devices)
                {
                    // Create a folder for the device
                    FolderState deviceFolder = CreateFolder(_discoveredDevicesFolder, $"Device_{device.Serial}", device.Name);
                    _deviceFolders.Add(deviceFolder);

                    // Add variables for device properties
                    CreateVariable(deviceFolder, "Name", "Name", DataTypeIds.String, ValueRanks.Scalar).Value = device.Name;
                    CreateVariable(deviceFolder, "Type", "Type", DataTypeIds.String, ValueRanks.Scalar).Value = device.Type;
                    CreateVariable(deviceFolder, "Serial", "Serial", DataTypeIds.String, ValueRanks.Scalar).Value = device.Serial;
                    CreateVariable(deviceFolder, "Port", "Port", DataTypeIds.String, ValueRanks.Scalar).Value = device.Port;
                    CreateVariable(deviceFolder, "Driver", "Driver", DataTypeIds.String, ValueRanks.Scalar).Value = device.Driver;

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
                }
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
                Console.WriteLine("TecanNodeManager.Shutdown called - disconnecting from device");

                // Disconnect from the device
                if (_tecan != null)
                {
                    _tecan.Disconnect();
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
                Console.WriteLine($"Error in TecanNodeManager.Shutdown: {ex.Message}");
            }
        }

        // Overridden methods
        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            try
            {
                Console.WriteLine("TecanNodeManager.CreateAddressSpace - Starting address space creation");

                lock (Lock)
                {
                    // Get the namespace index
                    _namespaceIndex = Server.NamespaceUris.GetIndexOrAppend(NamespaceUris.First());

                    // Create the Tecan folder
                    _tecanFolder = CreateFolder(null, "Tecan", "Tecan");

                    // Add the root folder to the Objects folder
                    IList<IReference> references = null;
                    if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out references))
                    {
                        externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
                    }

                    // Add references
                    references.Add(new NodeStateReference(ReferenceTypes.Organizes, false, _tecanFolder.NodeId));
                    _tecanFolder.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);

                    // Create status folder
                    _statusFolder = CreateFolder(_tecanFolder, "Status", "Status");

                    _isConnectedVariable = CreateVariable(_statusFolder, "IsConnected", "IsConnected", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _isConnectedVariable.Value = false;

                    _isPlateInVariable = CreateVariable(_statusFolder, "IsPlateIn", "IsPlateIn", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _isPlateInVariable.Value = false;

                    _temperatureVariable = CreateVariable(_statusFolder, "Temperature", "Temperature", DataTypeIds.Double, ValueRanks.Scalar);
                    _temperatureVariable.Value = 0.0;

                    _connectedDeviceSerialVariable = CreateVariable(_statusFolder, "ConnectedDeviceSerial", "Connected Device Serial", DataTypeIds.String, ValueRanks.Scalar);
                    _connectedDeviceSerialVariable.Value = string.Empty;

                    // Create device info variables
                    FolderState deviceInfoFolder = CreateFolder(_statusFolder, "DeviceInfo", "DeviceInfo");

                    _serialNumberVariable = CreateVariable(deviceInfoFolder, "SerialNumber", "SerialNumber", DataTypeIds.String, ValueRanks.Scalar);
                    _serialNumberVariable.Value = string.Empty;

                    _productNameVariable = CreateVariable(deviceInfoFolder, "ProductName", "ProductName", DataTypeIds.String, ValueRanks.Scalar);
                    _productNameVariable.Value = string.Empty;

                    _modelVariable = CreateVariable(deviceInfoFolder, "Model", "Model", DataTypeIds.String, ValueRanks.Scalar);
                    _modelVariable.Value = string.Empty;

                    _firmwareVersionVariable = CreateVariable(deviceInfoFolder, "FirmwareVersion", "FirmwareVersion", DataTypeIds.String, ValueRanks.Scalar);
                    _firmwareVersionVariable.Value = string.Empty;

                    // Create discovered devices folder
                    _discoveredDevicesFolder = CreateFolder(_statusFolder, "DiscoveredDevices", "Discovered Devices");

                    // Add device count variable
                    _deviceCountVariable = CreateVariable(_discoveredDevicesFolder, "DeviceCount", "Device Count", DataTypeIds.Int32, ValueRanks.Scalar);
                    _deviceCountVariable.Value = 0;

                    // Create commands folder
                    _commandsFolder = CreateFolder(_tecanFolder, "Commands", "Commands");

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

                    // Create ConnectBySerial method with device serial parameter
                    MethodState connectBySerialMethod = CreateMethod(_commandsFolder, "ConnectBySerial", "Connect By Serial");

                    // Define input arguments for ConnectBySerial
                    connectBySerialMethod.InputArguments = new PropertyState<Argument[]>(connectBySerialMethod);
                    connectBySerialMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    connectBySerialMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    connectBySerialMethod.InputArguments.DisplayName = connectBySerialMethod.InputArguments.BrowseName.Name;
                    connectBySerialMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    connectBySerialMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    connectBySerialMethod.InputArguments.DataType = DataTypeIds.Argument;
                    connectBySerialMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument deviceSerialArgument = new Argument();
                    deviceSerialArgument.Name = "DeviceSerial";
                    deviceSerialArgument.Description = new LocalizedText("Serial number of the device to connect to");
                    deviceSerialArgument.DataType = DataTypeIds.String;
                    deviceSerialArgument.ValueRank = ValueRanks.Scalar;

                    connectBySerialMethod.InputArguments.Value = new Argument[] { deviceSerialArgument };

                    // Define output arguments for ConnectBySerial (returns result code)
                    connectBySerialMethod.OutputArguments = new PropertyState<Argument[]>(connectBySerialMethod);
                    connectBySerialMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    connectBySerialMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    connectBySerialMethod.OutputArguments.DisplayName = connectBySerialMethod.OutputArguments.BrowseName.Name;
                    connectBySerialMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    connectBySerialMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    connectBySerialMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    connectBySerialMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument connectResultArgument = new Argument();
                    connectResultArgument.Name = "Result";
                    connectResultArgument.Description = new LocalizedText("Result code: 0=success, -1=failure, -2=device not found");
                    connectResultArgument.DataType = DataTypeIds.Int32;
                    connectResultArgument.ValueRank = ValueRanks.Scalar;

                    connectBySerialMethod.OutputArguments.Value = new Argument[] { connectResultArgument };

                    // Create Connect method with no parameters (connects to first available device)
                    MethodState connectMethod = CreateMethod(_commandsFolder, "Connect", "Connect");

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

                    Argument defaultConnectResultArgument = new Argument();
                    defaultConnectResultArgument.Name = "Result";
                    defaultConnectResultArgument.Description = new LocalizedText("Result code: 0=success, -1=failure");
                    defaultConnectResultArgument.DataType = DataTypeIds.Int32;
                    defaultConnectResultArgument.ValueRank = ValueRanks.Scalar;

                    connectMethod.OutputArguments.Value = new Argument[] { defaultConnectResultArgument };

                    // Create Disconnect method with no parameters
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

                    // Define empty output arguments for Disconnect
                    disconnectMethod.OutputArguments = new PropertyState<Argument[]>(disconnectMethod);
                    disconnectMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    disconnectMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    disconnectMethod.OutputArguments.DisplayName = disconnectMethod.OutputArguments.BrowseName.Name;
                    disconnectMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    disconnectMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    disconnectMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    disconnectMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;
                    disconnectMethod.OutputArguments.Value = new Argument[0]; // No output arguments

                    // Create MovePlateIn method with arguments
                    MethodState movePlateInMethod = CreateMethod(_commandsFolder, "MovePlateIn", "MovePlateIn");

                    // Define empty input arguments for MovePlateIn
                    movePlateInMethod.InputArguments = new PropertyState<Argument[]>(movePlateInMethod);
                    movePlateInMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    movePlateInMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    movePlateInMethod.InputArguments.DisplayName = movePlateInMethod.InputArguments.BrowseName.Name;
                    movePlateInMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    movePlateInMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    movePlateInMethod.InputArguments.DataType = DataTypeIds.Argument;
                    movePlateInMethod.InputArguments.ValueRank = ValueRanks.OneDimension;
                    movePlateInMethod.InputArguments.Value = new Argument[0];

                    // Define empty output arguments for MovePlateIn
                    movePlateInMethod.OutputArguments = new PropertyState<Argument[]>(movePlateInMethod);
                    movePlateInMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    movePlateInMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    movePlateInMethod.OutputArguments.DisplayName = movePlateInMethod.OutputArguments.BrowseName.Name;
                    movePlateInMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    movePlateInMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    movePlateInMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    movePlateInMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;
                    movePlateInMethod.OutputArguments.Value = new Argument[0];

                    // Create MovePlateOut method with arguments
                    MethodState movePlateOutMethod = CreateMethod(_commandsFolder, "MovePlateOut", "MovePlateOut");

                    // Define empty input arguments for MovePlateOut
                    movePlateOutMethod.InputArguments = new PropertyState<Argument[]>(movePlateOutMethod);
                    movePlateOutMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    movePlateOutMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    movePlateOutMethod.InputArguments.DisplayName = movePlateOutMethod.InputArguments.BrowseName.Name;
                    movePlateOutMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    movePlateOutMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    movePlateOutMethod.InputArguments.DataType = DataTypeIds.Argument;
                    movePlateOutMethod.InputArguments.ValueRank = ValueRanks.OneDimension;
                    movePlateOutMethod.InputArguments.Value = new Argument[0];

                    // Define empty output arguments for MovePlateOut
                    movePlateOutMethod.OutputArguments = new PropertyState<Argument[]>(movePlateOutMethod);
                    movePlateOutMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    movePlateOutMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    movePlateOutMethod.OutputArguments.DisplayName = movePlateOutMethod.OutputArguments.BrowseName.Name;
                    movePlateOutMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    movePlateOutMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    movePlateOutMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    movePlateOutMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;
                    movePlateOutMethod.OutputArguments.Value = new Argument[0];

                    // Create SetTemperature method with arguments
                    MethodState setTemperatureMethod = CreateMethod(_commandsFolder, "SetTemperature", "SetTemperature");

                    // Define input arguments for SetTemperature
                    setTemperatureMethod.InputArguments = new PropertyState<Argument[]>(setTemperatureMethod);
                    setTemperatureMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    setTemperatureMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    setTemperatureMethod.InputArguments.DisplayName = setTemperatureMethod.InputArguments.BrowseName.Name;
                    setTemperatureMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    setTemperatureMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    setTemperatureMethod.InputArguments.DataType = DataTypeIds.Argument;
                    setTemperatureMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                    // Define the temperature argument
                    Argument temperatureArgument = new Argument();
                    temperatureArgument.Name = "Temperature";
                    temperatureArgument.Description = new LocalizedText("The temperature to set in degrees Celsius");
                    temperatureArgument.DataType = DataTypeIds.Double;
                    temperatureArgument.ValueRank = ValueRanks.Scalar;

                    setTemperatureMethod.InputArguments.Value = new Argument[] { temperatureArgument };

                    // Define output arguments (empty for this method)
                    setTemperatureMethod.OutputArguments = new PropertyState<Argument[]>(setTemperatureMethod);
                    setTemperatureMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    setTemperatureMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    setTemperatureMethod.OutputArguments.DisplayName = setTemperatureMethod.OutputArguments.BrowseName.Name;
                    setTemperatureMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    setTemperatureMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    setTemperatureMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    setTemperatureMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;
                    setTemperatureMethod.OutputArguments.Value = new Argument[0];

                    // Register all nodes with the address space
                    AddPredefinedNode(SystemContext, _tecanFolder);

                    Console.WriteLine("Address space creation completed successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreateAddressSpace: {ex.Message}");
                throw;
            }
        }

        public override void DeleteAddressSpace()
        {
            lock (Lock)
            {
                // Stop the update timer
                if (_updateTimer != null)
                {
                    _updateTimer.Dispose();
                    _updateTimer = null;
                }

                base.DeleteAddressSpace();
            }
        }

        // Method to update Tecan status
        private void UpdateTecanStatus(object state)
        {
            try
            {
                // Only update variables if we have a reference to the Tecan control
                if (_tecan != null)
                {
                    // Update connection status
                    if (_isConnectedVariable != null)
                    {
                        _isConnectedVariable.Value = _tecan.IsConnected();
                    }

                    // Get the connected device
                    var connectedDevice = _tecan.GetConnectedDevice();

                    // Update connected device serial
                    if (_connectedDeviceSerialVariable != null)
                    {
                        _connectedDeviceSerialVariable.Value = connectedDevice != null ? connectedDevice.Serial : string.Empty;
                    }

                    // Only update other variables if the device is connected
                    if (_tecan.IsConnected())
                    {
                        // Update plate position
                        if (_isPlateInVariable != null)
                        {
                            _isPlateInVariable.Value = _tecan.IsPlateIn();
                        }

                        // Update temperature
                        if (_temperatureVariable != null)
                        {
                            _temperatureVariable.Value = _tecan.GetTemperature();
                        }

                        // Update device info from the connected device
                        if (connectedDevice != null)
                        {
                            if (_serialNumberVariable != null)
                            {
                                _serialNumberVariable.Value = connectedDevice.Serial;
                            }

                            if (_productNameVariable != null)
                            {
                                _productNameVariable.Value = connectedDevice.Name;
                            }

                            if (_modelVariable != null)
                            {
                                _modelVariable.Value = connectedDevice.Type;
                            }
                        }
                    }
                    else
                    {
                        // Clear device info when not connected
                        if (_serialNumberVariable != null)
                        {
                            _serialNumberVariable.Value = string.Empty;
                        }

                        if (_productNameVariable != null)
                        {
                            _productNameVariable.Value = string.Empty;
                        }

                        if (_modelVariable != null)
                        {
                            _modelVariable.Value = string.Empty;
                        }

                        if (_firmwareVersionVariable != null)
                        {
                            _firmwareVersionVariable.Value = string.Empty;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateTecanStatus: {ex.Message}");
            }
        }

        // Helper methods for creating nodes
        private FolderState CreateFolder(NodeState parent, string name, string displayName)
        {
            FolderState folder = new FolderState(parent);

            folder.SymbolicName = name;
            folder.ReferenceTypeId = ReferenceTypes.Organizes;
            folder.TypeDefinitionId = ObjectTypeIds.FolderType;

            // Increment the ID and create a NodeId
            _lastUsedId++;
            folder.NodeId = new NodeId((uint)_lastUsedId, _namespaceIndex);

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

        private BaseDataVariableState CreateVariable(NodeState parent, string name, string displayName, NodeId dataType, int valueRank)
        {
            BaseDataVariableState variable = new BaseDataVariableState(parent);

            variable.SymbolicName = name;
            variable.ReferenceTypeId = ReferenceTypes.Organizes;
            variable.TypeDefinitionId = VariableTypeIds.BaseDataVariableType;

            // Increment the ID and create a NodeId
            _lastUsedId++;
            variable.NodeId = new NodeId((uint)_lastUsedId, _namespaceIndex);

            variable.BrowseName = new QualifiedName(name, _namespaceIndex);
            variable.DisplayName = new LocalizedText("en", displayName);
            variable.WriteMask = AttributeWriteMask.None;
            variable.UserWriteMask = AttributeWriteMask.None;
            variable.DataType = dataType;
            variable.ValueRank = valueRank;
            variable.AccessLevel = AccessLevels.CurrentRead;
            variable.UserAccessLevel = AccessLevels.CurrentRead;
            variable.Historizing = false;
            variable.Value = GetDefaultValue(dataType, valueRank);
            variable.StatusCode = StatusCodes.Good;
            variable.Timestamp = DateTime.UtcNow;

            if (parent != null)
            {
                parent.AddChild(variable);
            }

            return variable;
        }

        // Get default value for a data type
        private object GetDefaultValue(NodeId dataType, int valueRank)
        {
            if (valueRank != ValueRanks.Scalar)
            {
                return null;
            }

            if (dataType == DataTypeIds.Boolean)
            {
                return false;
            }

            if (dataType == DataTypeIds.String)
            {
                return string.Empty;
            }

            if (dataType == DataTypeIds.Double)
            {
                return 0.0;
            }

            // Add more data types as needed

            return null;
        }

        // Create a method node
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

        // Method call handler
        private ServiceResult OnCallMethod(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Handle method calls based on the method name
                switch (method.BrowseName.Name)
                {
                    case "DiscoverDevices":
                        return OnDiscoverDevices(context, method, inputArguments, outputArguments);

                    case "ConnectBySerial":
                        return OnConnectBySerial(context, method, inputArguments, outputArguments);

                    case "Connect":
                        // Check if this is a device-specific Connect method
                        if (method.Parent != null && method.Parent.BrowseName.Name.StartsWith("Device_"))
                        {
                            return OnDeviceConnect(context, method, inputArguments, outputArguments);
                        }
                        else
                        {
                            return OnConnect(context, method, inputArguments, outputArguments);
                        }

                    case "Disconnect":
                        return OnDisconnect(context, method, inputArguments, outputArguments);

                    case "MovePlateIn":
                        return OnMovePlateIn(context, method, inputArguments, outputArguments);

                    case "MovePlateOut":
                        return OnMovePlateOut(context, method, inputArguments, outputArguments);

                    case "SetTemperature":
                        return OnSetTemperature(context, method, inputArguments, outputArguments);

                    default:
                        return StatusCodes.BadMethodInvalid;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnCallMethod: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // DiscoverDevices method implementation
        private ServiceResult OnDiscoverDevices(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Call the Tecan control method
                int deviceCount = _tecan.DiscoverDevices();

                // Update the device folders in the address space
                UpdateDeviceFolders();

                // Set the output argument (device count)
                outputArguments[0] = deviceCount;

                return StatusCodes.Good;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnDiscoverDevices: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // ConnectBySerial method implementation
        private ServiceResult OnConnectBySerial(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Check if we have the right number of input arguments
                if (inputArguments.Count != 1)
                {
                    return StatusCodes.BadArgumentsMissing;
                }

                // Get the device serial
                string deviceSerial;
                try
                {
                    deviceSerial = inputArguments[0].ToString();
                }
                catch
                {
                    return StatusCodes.BadTypeMismatch;
                }

                // Call the Tecan control method
                int result = _tecan.ConnectBySerial(deviceSerial);

                // Set the output argument (result code)
                outputArguments[0] = result;

                if (result == 0)
                {
                    return StatusCodes.Good;
                }
                else if (result == -2)
                {
                    return StatusCodes.BadNodeIdUnknown;
                }
                else
                {
                    return StatusCodes.BadUnexpectedError;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnConnectBySerial: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // Device-specific Connect method implementation
        private ServiceResult OnDeviceConnect(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Get the device serial from the parent folder name
                string folderName = method.Parent.BrowseName.Name;
                string deviceSerial = folderName.Substring("Device_".Length);

                // Call the Tecan control method
                int result = _tecan.ConnectBySerial(deviceSerial);

                // Set the output argument (result code)
                outputArguments[0] = result;

                if (result == 0)
                {
                    return StatusCodes.Good;
                }
                else if (result == -2)
                {
                    return StatusCodes.BadNodeIdUnknown;
                }
                else
                {
                    return StatusCodes.BadUnexpectedError;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnDeviceConnect: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // Connect method implementation
        private ServiceResult OnConnect(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Call the Tecan control method
                int result = _tecan.Connect();

                if (result == 0)
                {
                    return StatusCodes.Good;
                }
                else
                {
                    return StatusCodes.BadUnexpectedError;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnConnect: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // Disconnect method implementation
        private ServiceResult OnDisconnect(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Call the Tecan control method
                int result = _tecan.Disconnect();

                if (result == 0)
                {
                    return StatusCodes.Good;
                }
                else
                {
                    return StatusCodes.BadUnexpectedError;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnDisconnect: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // MovePlateIn method implementation
        private ServiceResult OnMovePlateIn(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Call the Tecan control method
                int result = _tecan.MovePlateIn();

                if (result == 0)
                {
                    return StatusCodes.Good;
                }
                else
                {
                    return StatusCodes.BadUnexpectedError;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnMovePlateIn: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // MovePlateOut method implementation
        private ServiceResult OnMovePlateOut(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Call the Tecan control method
                int result = _tecan.MovePlateOut();

                if (result == 0)
                {
                    return StatusCodes.Good;
                }
                else
                {
                    return StatusCodes.BadUnexpectedError;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnMovePlateOut: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // SetTemperature method implementation
        private ServiceResult OnSetTemperature(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Check if we have the right number of input arguments
                if (inputArguments.Count != 1)
                {
                    return StatusCodes.BadArgumentsMissing;
                }

                // Get the temperature value
                double temperature;
                try
                {
                    temperature = Convert.ToDouble(inputArguments[0]);
                }
                catch
                {
                    return StatusCodes.BadTypeMismatch;
                }

                // Call the Tecan control method
                int result = _tecan.SetTemperature(temperature);

                if (result == 0)
                {
                    return StatusCodes.Good;
                }
                else
                {
                    return StatusCodes.BadUnexpectedError;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnSetTemperature: {ex.Message}");
                return new ServiceResult(ex);
            }
        }
    }
}
