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


namespace TecanOpcUa
{
    /// <summary>
    /// Tecan Control class for communicating with Tecan devices
    /// </summary>
    public class TecanControl
    {
        // Properties to store Tecan state
        private bool _isConnected = false;
        private bool _isPlateIn = false;
        private string _serialNumber = "TECAN-SIM-12345";
        private double _temperature = 37.0;
        private MeasurementServer _measurementServer = null;


        // Constructor
        public TecanControl()
        {
            // Initialize any required resources
        }

        // Connect to the Tecan device - simple method with no parameters
        public int Connect()
        {
            try
            {
                if (_isConnected)
                    return 0; // Already connected

                // connect to the device directly
                Console.WriteLine("Connecting to Tecan device...");

                // Use the MeasurementServer to connect
                _measurementServer = new MeasurementServer();
                bool connected = _measurementServer.Connect(InstrumentConnectionMethod.Manually, "porttype=USB, type=reader, option=default, name=*");

                if (connected)
                {
                    _isConnected = true;
                    _serialNumber = _measurementServer.ConnectedReader.Information.GetInstrumentSerial();
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

        // Disconnect from the Tecan device
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

        // Status variables
        private BaseDataVariableState _isConnectedVariable;
        private BaseDataVariableState _isPlateInVariable;
        private BaseDataVariableState _temperatureVariable;
        private BaseDataVariableState _serialNumberVariable;
        private BaseDataVariableState _productNameVariable;
        private BaseDataVariableState _modelVariable;
        private BaseDataVariableState _firmwareVersionVariable;

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
                Console.WriteLine("TecanNodeManager.Initialize called - connecting to device");
                if (_tecan != null)
                {
                    // Automatically connect to the device when initialized
                    _tecan.Connect();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TecanNodeManager.Initialize: {ex.Message}");
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

                    // Create commands folder
                    _commandsFolder = CreateFolder(_tecanFolder, "Commands", "Commands");

                    // Create Connect method with no parameters
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

                    // Define empty output arguments for Connect
                    connectMethod.OutputArguments = new PropertyState<Argument[]>(connectMethod);
                    connectMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    connectMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    connectMethod.OutputArguments.DisplayName = connectMethod.OutputArguments.BrowseName.Name;
                    connectMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    connectMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    connectMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    connectMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;
                    connectMethod.OutputArguments.Value = new Argument[0]; // No output arguments

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
                    }

                    // Update serial number
                    if (_serialNumberVariable != null)
                    {
                        _serialNumberVariable.Value = "TECAN-SIM-12345"; // Hardcoded for now
                    }

                    // Update other device info with placeholder values
                    if (_productNameVariable != null)
                    {
                        _productNameVariable.Value = "Tecan Infinite";
                    }

                    if (_modelVariable != null)
                    {
                        _modelVariable.Value = "M200";
                    }

                    if (_firmwareVersionVariable != null)
                    {
                        _firmwareVersionVariable.Value = "1.0.0";
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
                    case "Connect":
                        return OnConnect(context, method, inputArguments, outputArguments);

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
