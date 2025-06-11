using System;
using System.Collections.Generic;
using System.Threading;
using Opc.Ua;
using Opc.Ua.Server;
using LabEquipmentOpcUa;
using System.Linq;


namespace TecanOpcUa
{
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
        private BaseDataVariableState _isInitializedVariable;
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
                // Initialize the Tecan
                Console.WriteLine("Initializing Tecan...");
                int result = _tecan.Initialize();
                if (result == 0)
                    Console.WriteLine("Tecan initialized successfully.");
                else
                    Console.WriteLine($"Failed to initialize Tecan: Error {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Tecan: {ex.Message}");
                throw;
            }
        }

        public void Shutdown()
        {
            try
            {
                // Shutdown the Tecan
                Console.WriteLine("Shutting down Tecan...");
                _tecan.ShutDown();
                Console.WriteLine("Tecan shut down successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error shutting down Tecan: {ex.Message}");
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
                    
                    // Create status variables
                    _isInitializedVariable = CreateVariable(_statusFolder, "IsInitialized", "IsInitialized", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _isInitializedVariable.Value = false;
                    
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
                // Update IsInitialized variable
                if (_isInitializedVariable != null)
                {
                    _isInitializedVariable.Value = _tecan.IsInitialized();
                }
                
                // Only update other variables if the device is initialized
                if (_tecan.IsInitialized())
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
                    
                    // Update device info
                    Dictionary<string, string> deviceInfo = _tecan.GetDeviceInfo();
                    
                    if (_serialNumberVariable != null && deviceInfo.ContainsKey("SerialNumber"))
                    {
                        _serialNumberVariable.Value = deviceInfo["SerialNumber"];
                    }
                    
                    if (_productNameVariable != null && deviceInfo.ContainsKey("ProductName"))
                    {
                        _productNameVariable.Value = deviceInfo["ProductName"];
                    }
                    
                    if (_modelVariable != null && deviceInfo.ContainsKey("Model"))
                    {
                        _modelVariable.Value = deviceInfo["Model"];
                    }
                    
                    if (_firmwareVersionVariable != null && deviceInfo.ContainsKey("FirmwareVersion"))
                    {
                        _firmwareVersionVariable.Value = deviceInfo["FirmwareVersion"];
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
