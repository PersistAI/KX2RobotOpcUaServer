using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Opc.Ua;
using Opc.Ua.Server;
using Opc.Ua.Configuration;
using Newtonsoft.Json;
using KX2RobotControlNamespace;
using LabEquipmentOpcUa;

namespace KX2RobotOpcUa
{
    /// <summary>
    /// KX2 Robot Node Manager for OPC UA Server    
    /// </summary>
    public class KX2RobotNodeManager : CustomNodeManager2, IEquipmentNodeManager, INodeManagerFactory
    {
        /// <summary>
        /// The interval in milliseconds for updating robot status.
        /// Change this value to adjust how frequently the robot status is updated.
        /// </summary>
        private const int UPDATE_INTERVAL_MS = 1000;
        #region INodeManagerFactory Implementation
        /// <summary>
        /// Gets the namespace URIs for the node manager.
        /// </summary>
        Opc.Ua.StringCollection INodeManagerFactory.NamespacesUris
        {
            get
            {
                // Convert string[] to Opc.Ua.StringCollection
                Opc.Ua.StringCollection namespaces = new Opc.Ua.StringCollection();
                foreach (string uri in base.NamespaceUris)
                {
                    namespaces.Add(uri);
                }
                return namespaces;
            }
        }

        /// <summary>
        /// Creates a new node manager.
        /// </summary>
        public INodeManager Create(IServerInternal server, ApplicationConfiguration configuration)
        {
            // Create a new instance of the node manager with the server and configuration
            return new KX2RobotNodeManager(server, configuration, _kx2Robot);
        }
        #endregion

        #region Private Fields
        private KX2RobotControl _kx2Robot;
        private ushort _namespaceIndex;
        private uint _lastUsedId;  // Changed from long to uint for NodeId compatibility
        private Timer _updateTimer;
        private FolderState _robotFolder;
        private FolderState _statusFolder;
        private FolderState _commandsFolder;
        private FolderState _teachPointsFolder;
        private FolderState _sequencesFolder;
        private FolderState _positionsFolder;
        private BaseDataVariableState _isInitializedVariable;
        private BaseDataVariableState _isMovingVariable;
        private BaseDataVariableState _isRobotOnRailVariable;
        private BaseDataVariableState _isScriptRunningVariable;
        private BaseDataVariableState _errorCodeVariable;
        private BaseDataVariableState _errorMessageVariable;
        private BaseDataVariableState _axis1PositionVariable; // Shoulder
        private BaseDataVariableState _axis2PositionVariable; // Z-Axis
        private BaseDataVariableState _axis3PositionVariable; // Elbow
        private BaseDataVariableState _axis4PositionVariable; // Wrist
        private BaseDataVariableState _axis5PositionVariable; // Rail (if present)
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the KX2RobotNodeManager class.
        /// </summary>
        public KX2RobotNodeManager(
            IServerInternal server,
            ApplicationConfiguration configuration,
            KX2RobotControl kx2Robot)
        : base(server, configuration, new string[] { "http://persist.com/KX2Robot" })
        {
            try
            {
                Console.WriteLine("KX2RobotNodeManager constructor called with IServerInternal");

                // Store the robot control
                _kx2Robot = kx2Robot;
                _lastUsedId = 0;

                // Start a timer to update the robot status
                _updateTimer = new Timer(UpdateRobotStatus, null, UPDATE_INTERVAL_MS, UPDATE_INTERVAL_MS);

                Console.WriteLine("KX2RobotNodeManager constructor completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in KX2RobotNodeManager constructor: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        #endregion

        #region IEquipmentNodeManager Implementation
        /// <summary>
        /// Initializes the KX2 robot.
        /// </summary>
        public void Initialize()
        {
            try
            {
                // Initialize the robot
                Console.WriteLine("Initializing KX2 robot...");
                int result = _kx2Robot.Initialize();
                if (result == 0)
                    Console.WriteLine("KX2 robot initialized successfully.");
                else
                    Console.WriteLine($"Failed to initialize KX2 robot: Error {result}");

                // Note: Teach points and sequence files are loaded by the factory
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing KX2 robot: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Shuts down the KX2 robot.
        /// </summary>
        public void Shutdown()
        {
            try
            {
                // Shutdown the robot
                Console.WriteLine("Shutting down KX2 robot...");
                _kx2Robot.ShutDown();
                Console.WriteLine("KX2 robot shut down successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error shutting down KX2 robot: {ex.Message}");
            }
        }

        #endregion

        #region Overridden Methods
        /// <summary>
        /// Creates the custom address space.
        /// </summary>
        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            try
            {
                Console.WriteLine("KX2RobotNodeManager.CreateAddressSpace - Starting address space creation");

                lock (Lock)
                {
                    try
                    {
                        // Get the namespace index for our namespace
                        Console.WriteLine("Getting namespace index...");
                        _namespaceIndex = Server.NamespaceUris.GetIndexOrAppend(NamespaceUris.First());
                        Console.WriteLine($"Namespace index: {_namespaceIndex}");

                        // Create a simplified address space for testing
                        Console.WriteLine("Creating root folder...");
                        _robotFolder = CreateFolder(null, "KX2Robot", "KX2Robot");

                        // IMPORTANT: Add the root folder directly to the Objects folder
                        Console.WriteLine("Adding root folder to Objects folder...");
                        IList<IReference> references = null;
                        if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out references))
                        {
                            Console.WriteLine("Creating new reference list for Objects folder");
                            externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
                        }

                        // Add forward reference (Objects folder organizes our node)
                        Console.WriteLine("Adding forward reference (Objects folder organizes our node)");
                        references.Add(new NodeStateReference(ReferenceTypes.Organizes, false, _robotFolder.NodeId));

                        // Add backward reference (our node is organized by Objects folder)
                        Console.WriteLine("Adding backward reference (our node is organized by Objects folder)");
                        _robotFolder.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);

                        Console.WriteLine($"Root folder added to Objects folder with NodeId: {_robotFolder.NodeId}");

                        // Create a single status folder with minimal variables
                        Console.WriteLine("Creating status folder...");
                        _statusFolder = CreateFolder(_robotFolder, "Status", "Status");

                        // Create status variables
                        Console.WriteLine("Creating status variables...");
                        _isInitializedVariable = CreateVariable(_statusFolder, "IsInitialized", "IsInitialized", DataTypeIds.Boolean, ValueRanks.Scalar);
                        _isInitializedVariable.Value = false;

                        _isMovingVariable = CreateVariable(_statusFolder, "IsMoving", "IsMoving", DataTypeIds.Boolean, ValueRanks.Scalar);
                        _isMovingVariable.Value = false;
                        _isMovingVariable.Description = new LocalizedText("en", "Indicates whether any axis of the robot is currently moving");

                        _isRobotOnRailVariable = CreateVariable(_statusFolder, "IsRobotOnRail", "IsRobotOnRail", DataTypeIds.Boolean, ValueRanks.Scalar);
                        _isRobotOnRailVariable.Value = false;

                        _isScriptRunningVariable = CreateVariable(_statusFolder, "IsScriptRunning", "IsScriptRunning", DataTypeIds.Boolean, ValueRanks.Scalar);
                        _isScriptRunningVariable.Value = false;
                        _isScriptRunningVariable.Description = new LocalizedText("en", "Indicates whether a script is currently running");

                        // Create axis position variables
                        Console.WriteLine("Creating axis position variables...");
                        _axis1PositionVariable = CreateVariable(_statusFolder, "Axis1Position", "Shoulder Position", DataTypeIds.Double, ValueRanks.Scalar);
                        _axis1PositionVariable.Value = 0.0;
                        _axis1PositionVariable.Description = new LocalizedText("en", "Current position of Axis 1 (Shoulder) in degrees");

                        _axis2PositionVariable = CreateVariable(_statusFolder, "Axis2Position", "Z-Axis Position", DataTypeIds.Double, ValueRanks.Scalar);
                        _axis2PositionVariable.Value = 0.0;
                        _axis2PositionVariable.Description = new LocalizedText("en", "Current position of Axis 2 (Z-Axis) in millimeters");

                        _axis3PositionVariable = CreateVariable(_statusFolder, "Axis3Position", "Elbow Position", DataTypeIds.Double, ValueRanks.Scalar);
                        _axis3PositionVariable.Value = 0.0;
                        _axis3PositionVariable.Description = new LocalizedText("en", "Current position of Axis 3 (Elbow) in degrees");

                        _axis4PositionVariable = CreateVariable(_statusFolder, "Axis4Position", "Wrist Position", DataTypeIds.Double, ValueRanks.Scalar);
                        _axis4PositionVariable.Value = 0.0;
                        _axis4PositionVariable.Description = new LocalizedText("en", "Current position of Axis 4 (Wrist) in degrees");

                        _axis5PositionVariable = CreateVariable(_statusFolder, "Axis5Position", "Rail Position", DataTypeIds.Double, ValueRanks.Scalar);
                        _axis5PositionVariable.Value = 0.0;
                        _axis5PositionVariable.Description = new LocalizedText("en", "Current position of Axis 5 (Rail) in millimeters");

                        // Create commands folder for methods
                        Console.WriteLine("Creating commands folder...");
                        _commandsFolder = CreateFolder(_robotFolder, "Commands", "Commands");

                        // Create method nodes and connect them to handlers
                        Console.WriteLine("Creating method nodes...");
                        CreateMethod(_commandsFolder, "Initialize", "Initialize", OnInitialize);
                        CreateMethod(_commandsFolder, "Shutdown", "Shutdown", OnShutdown);
                        CreateMethod(_commandsFolder, "MoveAbsolute", "MoveAbsolute", OnMoveAbsolute);
                        CreateMethod(_commandsFolder, "MoveRelative", "MoveRelative", OnMoveRelative);
                        CreateMethod(_commandsFolder, "LoadTeachPoints", "LoadTeachPoints", OnLoadTeachPoints);
                        CreateMethod(_commandsFolder, "MoveToTeachPoint", "MoveToTeachPoint", OnMoveToTeachPoint);
                        CreateMethod(_commandsFolder, "ExecuteSequence", "ExecuteSequence", OnExecuteSequence);
                        CreateMethod(_commandsFolder, "UpdateVariable", "UpdateVariable", OnUpdateVariable);

                        // Create teach points folder and nodes
                        Console.WriteLine("Creating teach points folder...");
                        _teachPointsFolder = CreateFolder(_robotFolder, "TeachPoints", "TeachPoints");
                        CreateTeachPointsNodes();

                        // Create sequences folder and nodes
                        Console.WriteLine("Creating sequences folder...");
                        _sequencesFolder = CreateFolder(_robotFolder, "Sequences", "Sequences");
                        CreateSequencesNodes();

                        // IMPORTANT: Register all nodes with the address space
                        Console.WriteLine("Registering nodes with the address space...");

                        // Add all nodes to the NodeManager
                        AddPredefinedNode(SystemContext, _robotFolder);

                        Console.WriteLine("Nodes registered successfully");
                        Console.WriteLine("Address space creation completed successfully");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in CreateAddressSpace (inner): {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        }
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreateAddressSpace (outer): {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        /// <summary>
        /// Frees any resources allocated for the address space.
        /// </summary>
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
        #endregion

        #region Private Methods
        /// <summary>
        /// Creates a folder node.
        /// </summary>
        private FolderState CreateFolder(NodeState parent, string name, string displayName)
        {
            try
            {
                Console.WriteLine($"Creating folder: {name}");

                FolderState folder = new FolderState(parent);

                folder.SymbolicName = name;
                folder.ReferenceTypeId = ReferenceTypes.Organizes;
                folder.TypeDefinitionId = ObjectTypeIds.FolderType;

                // Increment the ID and create a NodeId with uint (not long)
                _lastUsedId++;
                Console.WriteLine($"Creating NodeId with numeric ID: {_lastUsedId}, namespace index: {_namespaceIndex}");
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

                Console.WriteLine($"Folder {name} created successfully with NodeId: {folder.NodeId}");
                return folder;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating folder {name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        /// <summary>
        /// Creates a variable node.
        /// </summary>
        private BaseDataVariableState CreateVariable(NodeState parent, string name, string displayName, NodeId dataType, int valueRank)
        {
            try
            {
                Console.WriteLine($"Creating variable: {name}");

                BaseDataVariableState variable = new BaseDataVariableState(parent);

                variable.SymbolicName = name;
                variable.ReferenceTypeId = ReferenceTypes.Organizes;
                variable.TypeDefinitionId = VariableTypeIds.BaseDataVariableType;

                // Increment the ID and create a NodeId with uint (not long)
                _lastUsedId++;
                Console.WriteLine($"Creating NodeId with numeric ID: {_lastUsedId}, namespace index: {_namespaceIndex}");
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

                Console.WriteLine($"Variable {name} created successfully with NodeId: {variable.NodeId}");
                return variable;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating variable {name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        /// <summary>
        /// Creates a method node.
        /// </summary>
        private MethodState CreateMethod(NodeState parent, string name, string displayName, GenericMethodCalledEventHandler onCalled)
        {
            try
            {
                Console.WriteLine($"Creating method: {name}");

                MethodState method = new MethodState(parent);

                method.SymbolicName = name;
                method.ReferenceTypeId = ReferenceTypes.HasComponent;

                // Increment the ID and create a NodeId with uint (not long)
                _lastUsedId++;
                Console.WriteLine($"Creating NodeId with numeric ID: {_lastUsedId}, namespace index: {_namespaceIndex}");
                method.NodeId = new NodeId((uint)_lastUsedId, _namespaceIndex);

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

                Console.WriteLine($"Method {name} created successfully with NodeId: {method.NodeId}");

                // Set up method arguments based on the method name
                switch (name)
                {
                    case "Initialize":
                        method.OnCallMethod = onCalled;
                        break;

                    case "Shutdown":
                        method.OnCallMethod = onCalled;
                        break;

                    case "MoveAbsolute":
                        method.InputArguments = new PropertyState<Argument[]>(method);
                        _lastUsedId++;
                        method.InputArguments.NodeId = new NodeId((uint)_lastUsedId, _namespaceIndex);
                        method.InputArguments.BrowseName = BrowseNames.InputArguments;
                        method.InputArguments.DisplayName = method.InputArguments.BrowseName.Name;
                        method.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                        method.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                        method.InputArguments.DataType = DataTypeIds.Argument;
                        method.InputArguments.ValueRank = ValueRanks.OneDimension;

                        method.InputArguments.Value = new Argument[]
                        {
                        new Argument { Name = "Axis1", Description = "Position for Axis 1", DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar },
                        new Argument { Name = "Axis2", Description = "Position for Axis 2", DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar },
                        new Argument { Name = "Axis3", Description = "Position for Axis 3", DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar },
                        new Argument { Name = "Axis4", Description = "Position for Axis 4", DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar },
                        new Argument { Name = "Velocity", Description = "Velocity percentage", DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar },
                        new Argument { Name = "Acceleration", Description = "Acceleration percentage", DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar }
                        };

                        method.OutputArguments = new PropertyState<Argument[]>(method);
                        _lastUsedId++;
                        method.OutputArguments.NodeId = new NodeId((uint)_lastUsedId, _namespaceIndex);
                        method.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                        method.OutputArguments.DisplayName = method.OutputArguments.BrowseName.Name;
                        method.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                        method.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                        method.OutputArguments.DataType = DataTypeIds.Argument;
                        method.OutputArguments.ValueRank = ValueRanks.OneDimension;

                        method.OutputArguments.Value = new Argument[]
                        {
                        new Argument { Name = "Result", Description = "Result code (0 = success)", DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar }
                        };

                        method.OnCallMethod = onCalled;
                        break;

                    case "MoveRelative":
                        method.InputArguments = new PropertyState<Argument[]>(method);
                        method.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                        method.InputArguments.BrowseName = BrowseNames.InputArguments;
                        method.InputArguments.DisplayName = method.InputArguments.BrowseName.Name;
                        method.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                        method.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                        method.InputArguments.DataType = DataTypeIds.Argument;
                        method.InputArguments.ValueRank = ValueRanks.OneDimension;

                        method.InputArguments.Value = new Argument[]
                        {
                        new Argument { Name = "AxisNumber", Description = "Axis number (1-5)", DataType = DataTypeIds.Int16, ValueRank = ValueRanks.Scalar },
                        new Argument { Name = "Distance", Description = "Distance to move", DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar },
                        new Argument { Name = "Velocity", Description = "Velocity percentage", DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar },
                        new Argument { Name = "Acceleration", Description = "Acceleration percentage", DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar }
                        };

                        method.OutputArguments = new PropertyState<Argument[]>(method);
                        method.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                        method.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                        method.OutputArguments.DisplayName = method.OutputArguments.BrowseName.Name;
                        method.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                        method.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                        method.OutputArguments.DataType = DataTypeIds.Argument;
                        method.OutputArguments.ValueRank = ValueRanks.OneDimension;

                        method.OutputArguments.Value = new Argument[]
                        {
                        new Argument { Name = "Result", Description = "Result code (0 = success)", DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar }
                        };

                        method.OnCallMethod = onCalled;
                        break;

                    case "LoadTeachPoints":
                        method.InputArguments = new PropertyState<Argument[]>(method);
                        method.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                        method.InputArguments.BrowseName = BrowseNames.InputArguments;
                        method.InputArguments.DisplayName = method.InputArguments.BrowseName.Name;
                        method.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                        method.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                        method.InputArguments.DataType = DataTypeIds.Argument;
                        method.InputArguments.ValueRank = ValueRanks.OneDimension;

                        method.InputArguments.Value = new Argument[]
                        {
                        new Argument { Name = "FilePath", Description = "Path to teach points file", DataType = DataTypeIds.String, ValueRank = ValueRanks.Scalar }
                        };

                        method.OutputArguments = new PropertyState<Argument[]>(method);
                        method.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                        method.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                        method.OutputArguments.DisplayName = method.OutputArguments.BrowseName.Name;
                        method.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                        method.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                        method.OutputArguments.DataType = DataTypeIds.Argument;
                        method.OutputArguments.ValueRank = ValueRanks.OneDimension;

                        method.OutputArguments.Value = new Argument[]
                        {
                        new Argument { Name = "Result", Description = "Result code (0 = success)", DataType = DataTypeIds.Int16, ValueRank = ValueRanks.Scalar }
                        };

                        method.OnCallMethod = onCalled;
                        break;

                    case "MoveToTeachPoint":
                        method.InputArguments = new PropertyState<Argument[]>(method);
                        method.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                        method.InputArguments.BrowseName = BrowseNames.InputArguments;
                        method.InputArguments.DisplayName = method.InputArguments.BrowseName.Name;
                        method.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                        method.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                        method.InputArguments.DataType = DataTypeIds.Argument;
                        method.InputArguments.ValueRank = ValueRanks.OneDimension;

                        method.InputArguments.Value = new Argument[]
                        {
                        new Argument { Name = "TeachPointName", Description = "Name of the teach point", DataType = DataTypeIds.String, ValueRank = ValueRanks.Scalar },
                        new Argument { Name = "Velocity", Description = "Velocity percentage", DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar },
                        new Argument { Name = "Acceleration", Description = "Acceleration percentage", DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar }
                        };

                        method.OutputArguments = new PropertyState<Argument[]>(method);
                        method.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                        method.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                        method.OutputArguments.DisplayName = method.OutputArguments.BrowseName.Name;
                        method.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                        method.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                        method.OutputArguments.DataType = DataTypeIds.Argument;
                        method.OutputArguments.ValueRank = ValueRanks.OneDimension;

                        method.OutputArguments.Value = new Argument[]
                        {
                        new Argument { Name = "Result", Description = "Result code (0 = success)", DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar }
                        };

                        method.OnCallMethod = onCalled;
                        break;

                    case "ExecuteSequence":
                        method.InputArguments = new PropertyState<Argument[]>(method);
                        method.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                        method.InputArguments.BrowseName = BrowseNames.InputArguments;
                        method.InputArguments.DisplayName = method.InputArguments.BrowseName.Name;
                        method.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                        method.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                        method.InputArguments.DataType = DataTypeIds.Argument;
                        method.InputArguments.ValueRank = ValueRanks.OneDimension;

                        method.InputArguments.Value = new Argument[]
                        {
                        new Argument { Name = "SequenceName", Description = "Name of the sequence", DataType = DataTypeIds.String, ValueRank = ValueRanks.Scalar }
                        };

                        method.OutputArguments = new PropertyState<Argument[]>(method);
                        method.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                        method.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                        method.OutputArguments.DisplayName = method.OutputArguments.BrowseName.Name;
                        method.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                        method.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                        method.OutputArguments.DataType = DataTypeIds.Argument;
                        method.OutputArguments.ValueRank = ValueRanks.OneDimension;

                        method.OutputArguments.Value = new Argument[]
                        {
                        new Argument { Name = "Result", Description = "Result code (0 = success)", DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar }
                        };

                        method.OnCallMethod = onCalled;
                        break;

                    case "UpdateVariable":
                        method.InputArguments = new PropertyState<Argument[]>(method);
                        method.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                        method.InputArguments.BrowseName = BrowseNames.InputArguments;
                        method.InputArguments.DisplayName = method.InputArguments.BrowseName.Name;
                        method.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                        method.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                        method.InputArguments.DataType = DataTypeIds.Argument;
                        method.InputArguments.ValueRank = ValueRanks.OneDimension;

                        method.InputArguments.Value = new Argument[]
                        {
                        new Argument { Name = "VariableName", Description = "Name of the variable", DataType = DataTypeIds.String, ValueRank = ValueRanks.Scalar },
                        new Argument { Name = "Value", Description = "Value to set", DataType = DataTypeIds.String, ValueRank = ValueRanks.Scalar }
                        };

                        method.OutputArguments = new PropertyState<Argument[]>(method);
                        method.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                        method.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                        method.OutputArguments.DisplayName = method.OutputArguments.BrowseName.Name;
                        method.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                        method.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                        method.OutputArguments.DataType = DataTypeIds.Argument;
                        method.OutputArguments.ValueRank = ValueRanks.OneDimension;

                        method.OutputArguments.Value = new Argument[]
                        {
                        new Argument { Name = "Result", Description = "Result code (0 = success)", DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar }
                        };

                        method.OnCallMethod = onCalled;
                        break;
                }

                return method;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating method {name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }


        /// <summary>
        /// Gets the default value for a data type.
        /// </summary>
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

            if (dataType == DataTypeIds.SByte)
            {
                return (sbyte)0;
            }

            if (dataType == DataTypeIds.Byte)
            {
                return (byte)0;
            }

            if (dataType == DataTypeIds.Int16)
            {
                return (short)0;
            }

            if (dataType == DataTypeIds.UInt16)
            {
                return (ushort)0;
            }

            if (dataType == DataTypeIds.Int32)
            {
                return 0;
            }

            if (dataType == DataTypeIds.UInt32)
            {
                return (uint)0;
            }

            if (dataType == DataTypeIds.Int64)
            {
                return (long)0;
            }

            if (dataType == DataTypeIds.UInt64)
            {
                return (ulong)0;
            }

            if (dataType == DataTypeIds.Float)
            {
                return (float)0;
            }

            if (dataType == DataTypeIds.Double)
            {
                return (double)0;
            }

            if (dataType == DataTypeIds.String)
            {
                return string.Empty;
            }

            if (dataType == DataTypeIds.DateTime)
            {
                return DateTime.MinValue;
            }

            if (dataType == DataTypeIds.Guid)
            {
                return Guid.Empty;
            }

            if (dataType == DataTypeIds.ByteString)
            {
                return new byte[0];
            }

            if (dataType == DataTypeIds.XmlElement)
            {
                return null;
            }

            if (dataType == DataTypeIds.NodeId)
            {
                return NodeId.Null;
            }

            if (dataType == DataTypeIds.ExpandedNodeId)
            {
                return ExpandedNodeId.Null;
            }

            if (dataType == DataTypeIds.QualifiedName)
            {
                return QualifiedName.Null;
            }

            if (dataType == DataTypeIds.LocalizedText)
            {
                return LocalizedText.Null;
            }

            if (dataType == DataTypeIds.StatusCode)
            {
                return StatusCodes.Good;
            }

            // if (dataType == DataTypeIds.BaseDataType)
            // {
            //     return Variant.Null;
            // }

            return null;
        }

        /// <summary>
        /// Adds an external reference to the root node.
        /// </summary>
        private void AddExternalReferenceToRoot(NodeState node, IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            if (node != null)
            {
                Console.WriteLine($"Adding external reference for node {node.NodeId} with BrowseName {node.BrowseName}");

                try
                {
                    // Add reference to Objects folder
                    Console.WriteLine($"Adding reference to Objects folder (NodeId: {ObjectIds.ObjectsFolder})");
                    IList<IReference> references = null;
                    if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out references))
                    {
                        Console.WriteLine("Creating new reference list for Objects folder");
                        externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
                    }

                    // Add forward reference (Objects folder organizes our node)
                    Console.WriteLine("Adding forward reference (Objects folder organizes our node)");
                    references.Add(new NodeStateReference(ReferenceTypes.Organizes, false, node.NodeId));

                    // Add backward reference (our node is organized by Objects folder)
                    Console.WriteLine("Adding backward reference (our node is organized by Objects folder)");
                    node.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);

                    // Make sure the node is visible in the address space
                    Console.WriteLine("Ensuring node is visible in address space");

                    Console.WriteLine($"External reference added successfully for {node.NodeId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error adding external reference: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                    throw;
                }
            }
            else
            {
                Console.WriteLine("Cannot add external reference: node is null");
            }
        }

        /// <summary>
        /// Creates the teach points nodes.
        /// </summary>
        private void CreateTeachPointsNodes()
        {
            try
            {
                // Get the number of teach points
                int teachPointCount = 0;
                short result = _kx2Robot.TeachPointsGetCount(ref teachPointCount);

                if (result != 0)
                {
                    return;
                }

                // Create a node for each teach point
                for (short i = 0; i < teachPointCount; i++)
                {
                    string teachPointName = "";
                    _kx2Robot.TeachPointGetName(i, ref teachPointName);

                    if (!string.IsNullOrEmpty(teachPointName))
                    {
                        // Create a folder for the teach point
                        FolderState teachPointFolder = CreateFolder(_teachPointsFolder, teachPointName, teachPointName);

                        // Get the teach point values
                        double[] positionValues = new double[5];
                        _kx2Robot.TeachPointGetValue(i, ref positionValues);

                        // Create variables for each axis
                        CreateVariable(teachPointFolder, "Shoulder", "Shoulder", DataTypeIds.Double, ValueRanks.Scalar).Value = positionValues[1];
                        CreateVariable(teachPointFolder, "Z_Axis", "Z_Axis", DataTypeIds.Double, ValueRanks.Scalar).Value = positionValues[2];
                        CreateVariable(teachPointFolder, "Elbow", "Elbow", DataTypeIds.Double, ValueRanks.Scalar).Value = positionValues[3];
                        CreateVariable(teachPointFolder, "Wrist", "Wrist", DataTypeIds.Double, ValueRanks.Scalar).Value = positionValues[4];
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Trace(ex, "Error creating teach points nodes: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Creates the sequences nodes.
        /// </summary>
        private void CreateSequencesNodes()
        {
            try
            {
                // Get the sequence file path
                string sequenceFilePath = _kx2Robot.GetDefaultSequenceFile();
                if (string.IsNullOrEmpty(sequenceFilePath) || !File.Exists(sequenceFilePath))
                {
                    return;
                }

                // Get sequence names
                string[] scriptNames = new string[10];
                _kx2Robot.ScriptsGetNames(sequenceFilePath, ref scriptNames);

                // Create a node for each sequence
                foreach (string scriptName in scriptNames.Where(s => !string.IsNullOrEmpty(s)))
                {
                    // Create a folder for the sequence
                    FolderState sequenceFolder = CreateFolder(_sequencesFolder, scriptName, scriptName);

                    // Get the sequence operations
                    string[] operations = new string[100];
                    _kx2Robot.ScriptGetOperations(sequenceFilePath, scriptName, ref operations);

                    // Create a variable for the operations
                    BaseDataVariableState operationsVariable = CreateVariable(sequenceFolder, "Operations", "Operations", DataTypeIds.String, ValueRanks.OneDimension);
                    operationsVariable.Value = operations.Where(op => !string.IsNullOrEmpty(op)).ToArray();
                }
            }
            catch (Exception ex)
            {
                Utils.Trace(ex, "Error creating sequences nodes: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Updates the robot status.
        /// </summary>
        private void UpdateRobotStatus(object state)
        {
            try
            {
                // Update IsInitialized variable
                if (_isInitializedVariable != null)
                {
                    try
                    {
                        _isInitializedVariable.Value = _kx2Robot.IsInitialized();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error updating IsInitialized variable: {ex.Message}");
                        // Set to false if there's an error
                        _isInitializedVariable.Value = false;
                    }
                }

                // Update IsMoving variable
                if (_isMovingVariable != null)
                {
                    try
                    {
                        bool isAnyAxisMoving = false;

                        // Check each axis
                        for (short axis = 1; axis <= 5; axis++)
                        {
                            // Skip axis 5 if robot is not on rail
                            if (axis == 5 && _isRobotOnRailVariable != null && !(bool)_isRobotOnRailVariable.Value)
                                continue;

                            bool isDone = true;
                            short result = _kx2Robot.MotorCheckIfMoveDone(axis, ref isDone);

                            if (result == 0 && !isDone)
                            {
                                isAnyAxisMoving = true;
                                break; // No need to check other axes if one is moving
                            }
                        }

                        _isMovingVariable.Value = isAnyAxisMoving;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error updating IsMoving variable: {ex.Message}");
                        // Set to false if there's an error
                        _isMovingVariable.Value = false;
                    }
                }

                // Update IsRobotOnRail variable
                if (_isRobotOnRailVariable != null)
                {
                    try
                    {
                        _isRobotOnRailVariable.Value = _kx2Robot.IsRobotOnRail();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error updating IsRobotOnRail variable: {ex.Message}");
                        // Set to false if there's an error
                        _isRobotOnRailVariable.Value = false;
                    }
                }

                // Update IsScriptRunning variable
                if (_isScriptRunningVariable != null)
                {
                    try
                    {
                        _isScriptRunningVariable.Value = _kx2Robot.IsScriptRunning();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error updating IsScriptRunning variable: {ex.Message}");
                        // Set to false if there's an error
                        _isScriptRunningVariable.Value = false;
                    }
                }

                // Update axis position variables
                try
                {
                    // Update Axis 1 (Shoulder) position
                    if (_axis1PositionVariable != null)
                    {
                        double position = 0.0;
                        short result = _kx2Robot.MotorGetCurrentPosition(1, ref position);
                        if (result == 0)
                        {
                            _axis1PositionVariable.Value = position;
                        }
                    }

                    // Update Axis 2 (Z-Axis) position
                    if (_axis2PositionVariable != null)
                    {
                        double position = 0.0;
                        short result = _kx2Robot.MotorGetCurrentPosition(2, ref position);
                        if (result == 0)
                        {
                            _axis2PositionVariable.Value = position;
                        }
                    }

                    // Update Axis 3 (Elbow) position
                    if (_axis3PositionVariable != null)
                    {
                        double position = 0.0;
                        short result = _kx2Robot.MotorGetCurrentPosition(3, ref position);
                        if (result == 0)
                        {
                            _axis3PositionVariable.Value = position;
                        }
                    }

                    // Update Axis 4 (Wrist) position
                    if (_axis4PositionVariable != null)
                    {
                        double position = 0.0;
                        short result = _kx2Robot.MotorGetCurrentPosition(4, ref position);
                        if (result == 0)
                        {
                            _axis4PositionVariable.Value = position;
                        }
                    }

                    // Update Axis 5 (Rail) position if robot is on rail
                    if (_axis5PositionVariable != null && _isRobotOnRailVariable != null && (bool)_isRobotOnRailVariable.Value)
                    {
                        double position = 0.0;
                        short result = _kx2Robot.MotorGetCurrentPosition(5, ref position);
                        if (result == 0)
                        {
                            _axis5PositionVariable.Value = position;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating axis position variables: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateRobotStatus: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        /// Handles the Initialize method call.
        /// </summary>
        private ServiceResult OnInitialize(
            ISystemContext context,
            MethodState method,
            IList<object> inputArguments,
            IList<object> outputArguments)
        {
            try
            {
                // Initialize the robot
                int result = _kx2Robot.Initialize();

                // Return the result
                outputArguments.Add(result);

                return ServiceResult.Good;
            }
            catch (Exception ex)
            {
                return new ServiceResult(ex);
            }
        }

        /// <summary>
        /// Handles the Shutdown method call.
        /// </summary>
        private ServiceResult OnShutdown(
            ISystemContext context,
            MethodState method,
            IList<object> inputArguments,
            IList<object> outputArguments)
        {
            try
            {
                // Shutdown the robot
                _kx2Robot.ShutDown();

                return ServiceResult.Good;
            }
            catch (Exception ex)
            {
                return new ServiceResult(ex);
            }
        }

        /// <summary>
        /// Handles the MoveAbsolute method call.
        /// </summary>
        private ServiceResult OnMoveAbsolute(
            ISystemContext context,
            MethodState method,
            IList<object> inputArguments,
            IList<object> outputArguments)
        {
            try
            {
                // Get the input arguments
                double axis1 = (double)inputArguments[0];
                double axis2 = (double)inputArguments[1];
                double axis3 = (double)inputArguments[2];
                double axis4 = (double)inputArguments[3];
                double velocity = (double)inputArguments[4];
                double acceleration = (double)inputArguments[5];

                // Create the positions array
                double[] positions = { axis1, axis2, axis3, axis4 };

                // Move the robot
                int calculatedTime = 0;
                byte index = 0;
                int result = _kx2Robot.MoveAbsoluteAllAxes(positions, velocity, acceleration, true, ref calculatedTime, false, ref index);

                // Return the result
                outputArguments.Add(result);

                return ServiceResult.Good;
            }
            catch (Exception ex)
            {
                return new ServiceResult(ex);
            }
        }

        /// <summary>
        /// Handles the MoveRelative method call.
        /// </summary>
        private ServiceResult OnMoveRelative(
            ISystemContext context,
            MethodState method,
            IList<object> inputArguments,
            IList<object> outputArguments)
        {
            try
            {
                // Get the input arguments
                short axisNumber = (short)inputArguments[0];
                double distance = (double)inputArguments[1];
                double velocity = (double)inputArguments[2];
                double acceleration = (double)inputArguments[3];

                // Move the robot
                int calculatedTime = 0;
                byte index = 0;
                int result = _kx2Robot.MoveRelativeSingleAxis(axisNumber, distance, velocity, acceleration, true, ref calculatedTime, false, ref index);

                // Return the result
                outputArguments.Add(result);

                return ServiceResult.Good;
            }
            catch (Exception ex)
            {
                return new ServiceResult(ex);
            }
        }

        /// <summary>
        /// Handles the LoadTeachPoints method call.
        /// </summary>
        private ServiceResult OnLoadTeachPoints(
            ISystemContext context,
            MethodState method,
            IList<object> inputArguments,
            IList<object> outputArguments)
        {
            try
            {
                // Get the input arguments
                string filePath = (string)inputArguments[0];

                // Load the teach points
                short fileNumAxes = 0;
                short result = _kx2Robot.TeachPointsLoad(filePath, true, ref fileNumAxes);

                // Return the result
                outputArguments.Add(result);

                return ServiceResult.Good;
            }
            catch (Exception ex)
            {
                return new ServiceResult(ex);
            }
        }

        /// <summary>
        /// Handles the MoveToTeachPoint method call.
        /// </summary>
        private ServiceResult OnMoveToTeachPoint(
            ISystemContext context,
            MethodState method,
            IList<object> inputArguments,
            IList<object> outputArguments)
        {
            try
            {
                // Get the input arguments
                string teachPointName = (string)inputArguments[0];
                double velocity = (double)inputArguments[1];
                double acceleration = (double)inputArguments[2];

                // Move to the teach point
                int timeoutMsec = 0;
                byte index = 0;
                int result = _kx2Robot.TeachPointMoveTo(teachPointName, velocity, acceleration, true, ref timeoutMsec, false, ref index);

                // Return the result
                outputArguments.Add(result);

                return ServiceResult.Good;
            }
            catch (Exception ex)
            {
                return new ServiceResult(ex);
            }
        }

        /// <summary>
        /// Handles the ExecuteSequence method call.
        /// </summary>
        private ServiceResult OnExecuteSequence(
            ISystemContext context,
            MethodState method,
            IList<object> inputArguments,
            IList<object> outputArguments)
        {
            try
            {
                // Get the input arguments
                string sequenceName = (string)inputArguments[0];

                // Get the sequence file path
                string sequenceFilePath = _kx2Robot.GetDefaultSequenceFile();
                if (string.IsNullOrEmpty(sequenceFilePath))
                {
                    outputArguments.Add(-1); // Error: No sequence file loaded
                    return ServiceResult.Good;
                }

                // Execute the sequence
                int result = _kx2Robot.ScriptRun(sequenceFilePath, sequenceName, true);

                // Return the result
                outputArguments.Add(result);

                return ServiceResult.Good;
            }
            catch (Exception ex)
            {
                return new ServiceResult(ex);
            }
        }

        /// <summary>
        /// Handles the UpdateVariable method call.
        /// </summary>
        private ServiceResult OnUpdateVariable(
            ISystemContext context,
            MethodState method,
            IList<object> inputArguments,
            IList<object> outputArguments)
        {
            try
            {
                // Get the input arguments
                string variableName = (string)inputArguments[0];
                string value = (string)inputArguments[1];

                // Ensure the variable name starts with $
                if (!variableName.StartsWith("$"))
                {
                    variableName = "$" + variableName;
                }

                // Get the sequence file path
                string sequenceFilePath = _kx2Robot.GetDefaultSequenceFile();
                if (string.IsNullOrEmpty(sequenceFilePath) || !File.Exists(sequenceFilePath))
                {
                    outputArguments.Add(-1); // Error: No sequence file loaded or file doesn't exist
                    return ServiceResult.Good;
                }

                // Update the variable
                int result = UpdateVariable(variableName, value);

                // Return the result
                outputArguments.Add(result);

                return ServiceResult.Good;
            }
            catch (Exception ex)
            {
                return new ServiceResult(ex);
            }
        }

        /// <summary>
        /// Updates a variable in the sequence file.
        /// </summary>
        private int UpdateVariable(string variableName, string value)
        {
            try
            {
                string sequenceFilePath = _kx2Robot.GetDefaultSequenceFile();
                if (string.IsNullOrEmpty(sequenceFilePath) || !File.Exists(sequenceFilePath))
                    return -1; // No sequence file loaded or file doesn't exist

                string[] lines = File.ReadAllLines(sequenceFilePath);
                bool inVariablesSection = false;
                bool found = false;
                StringBuilder updatedFile = new StringBuilder();

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();

                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        if (inVariablesSection && !found)
                        {
                            // If we reach a new section and the variable wasn't found, add it
                            updatedFile.AppendLine($"{variableName}={value}");
                            found = true;
                        }
                        inVariablesSection = trimmedLine.Equals("[variables]", StringComparison.OrdinalIgnoreCase);
                    }

                    if (inVariablesSection && trimmedLine.StartsWith(variableName + "=", StringComparison.OrdinalIgnoreCase))
                    {
                        updatedFile.AppendLine($"{variableName}={value}"); // Update value
                        found = true;
                    }
                    else
                    {
                        updatedFile.AppendLine(line); // Keep other lines unchanged
                    }
                }

                if (inVariablesSection && !found)
                {
                    // If the file ends while inside [variables], add the new key
                    updatedFile.AppendLine($"{variableName}={value}");
                }

                File.WriteAllText(sequenceFilePath, updatedFile.ToString());
                return 0; // Success
            }
            catch (Exception ex)
            {
                Utils.Trace(ex, "Error updating variable {0}: {1}", variableName, ex.Message);
                return -1; // Error
            }
        }
        #endregion
    }
}
