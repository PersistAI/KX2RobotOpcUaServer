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
        private BaseDataVariableState[] _axisPositionVariables;
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
                _updateTimer = new Timer(UpdateRobotStatus, null, 1000, 1000);

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

        /// <summary>
        /// Initializes a new instance of the KX2RobotNodeManager class for use with the OpcUaServer.
        /// </summary>
        public KX2RobotNodeManager(OpcUaServer server)
            : base(null, server.Configuration, new string[] { "http://persist.com/KX2Robot" })
        {
            // Store references to server and configuration
            _opcServer = server;

            // Create a single instance of KX2RobotControl
            _kx2Robot = new KX2RobotControl();
            _lastUsedId = 0;

            // Start a timer to update the robot status
            _updateTimer = new Timer(UpdateRobotStatus, null, 1000, 1000);
        }

        // Reference to the OPC UA server
        private OpcUaServer _opcServer;
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

                // Load default teach points and sequence files
                LoadDefaultTeachPoints();
                LoadDefaultSequenceFile();
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

        /// <summary>
        /// Loads the default teach points.
        /// </summary>
        private void LoadDefaultTeachPoints()
        {
            try
            {
                // Get the default teach points file path
                string teachPointsFilePath = GetDefaultTeachPointFile();

                // Check if the default file exists
                if (!File.Exists(teachPointsFilePath))
                {
                    Console.WriteLine($"Default Teach Points file not found at: {teachPointsFilePath}");
                    return;
                }

                // Load the default teach points
                short fileNumAxes = 0;
                short loadResult = _kx2Robot.TeachPointsLoad(teachPointsFilePath, true, ref fileNumAxes);

                if (loadResult == 0)
                {
                    Console.WriteLine("Default Teach Points loaded successfully.");
                }
                else
                {
                    Console.WriteLine($"Error loading default Teach Points: Code {loadResult}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the default teach points file path.
        /// </summary>
        private string GetDefaultTeachPointFile()
        {
            try
            {
                // First try the application directory (bin/Debug or bin/Release)
                string appDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TeachPoints.ini");
                if (File.Exists(appDirPath))
                {
                    Console.WriteLine($"Found TeachPoints.ini in application directory: {appDirPath}");
                    return appDirPath;
                }

                // If not found, check one level up (bin folder)
                string binDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\TeachPoints.ini");
                if (File.Exists(binDirPath))
                {
                    string fullPath = Path.GetFullPath(binDirPath);
                    Console.WriteLine($"Found TeachPoints.ini one level up: {fullPath}");
                    return fullPath;
                }

                // If still not found, check two levels up (project root directory)
                string projectDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\TeachPoints.ini");
                if (File.Exists(projectDirPath))
                {
                    string fullPath = Path.GetFullPath(projectDirPath);
                    Console.WriteLine($"Found TeachPoints.ini in project directory: {fullPath}");
                    return fullPath;
                }

                // Return the application directory path anyway, even if it doesn't exist yet
                Console.WriteLine($"TeachPoints.ini not found. Will use default path: {appDirPath}");
                return appDirPath;
            }
            catch (Exception ex)
            {
                // If any error occurs, return a path in the application directory
                string defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TeachPoints.ini");
                Console.WriteLine($"Error finding TeachPoints.ini: {ex.Message}. Will use default path: {defaultPath}");
                return defaultPath;
            }
        }

        /// <summary>
        /// Loads the default sequence file.
        /// </summary>
        private void LoadDefaultSequenceFile()
        {
            try
            {
                // Get the default sequence file path
                string sequenceFilePath = GetDefaultSequenceFile();

                // Check if the default file exists
                if (!File.Exists(sequenceFilePath))
                {
                    Console.WriteLine($"Default Sequence file not found at: {sequenceFilePath}");
                    return;
                }

                // Set the default sequence file
                _kx2Robot.SetDefaultSequenceFile(sequenceFilePath);
                Console.WriteLine($"Default Sequence file set to: {sequenceFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred while loading sequence file: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the default sequence file path.
        /// </summary>
        private string GetDefaultSequenceFile()
        {
            try
            {
                // First try the application directory (bin/Debug or bin/Release)
                string appDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sequences.ini");
                if (File.Exists(appDirPath))
                {
                    Console.WriteLine($"Found Sequences.ini in application directory: {appDirPath}");
                    return appDirPath;
                }

                // If not found, check one level up (bin folder)
                string binDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\Sequences.ini");
                if (File.Exists(binDirPath))
                {
                    string fullPath = Path.GetFullPath(binDirPath);
                    Console.WriteLine($"Found Sequences.ini one level up: {fullPath}");
                    return fullPath;
                }

                // If still not found, check two levels up (project root directory)
                string projectDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\Sequences.ini");
                if (File.Exists(projectDirPath))
                {
                    string fullPath = Path.GetFullPath(projectDirPath);
                    Console.WriteLine($"Found Sequences.ini in project directory: {fullPath}");
                    return fullPath;
                }

                // Return the application directory path anyway, even if it doesn't exist yet
                Console.WriteLine($"Sequences.ini not found. Will use default path: {appDirPath}");
                return appDirPath;
            }
            catch (Exception ex)
            {
                // If any error occurs, return a path in the application directory
                string defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sequences.ini");
                Console.WriteLine($"Error finding Sequences.ini: {ex.Message}. Will use default path: {defaultPath}");
                return defaultPath;
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
                        AddExternalReferenceToRoot(_robotFolder, externalReferences);

                        // Create a single status folder with minimal variables
                        Console.WriteLine("Creating status folder...");
                        _statusFolder = CreateFolder(_robotFolder, "Status", "Status");

                        // Create just one variable for testing
                        Console.WriteLine("Creating test variable...");
                        _isInitializedVariable = CreateVariable(_statusFolder, "IsInitialized", "IsInitialized", DataTypeIds.Boolean, ValueRanks.Scalar);
                        _isInitializedVariable.Value = false;

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
                        new Argument { Name = "AxisNumber", Description = "Axis number (1-4)", DataType = DataTypeIds.Int16, ValueRank = ValueRanks.Scalar },
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
                IList<IReference> references = null;

                if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out references))
                {
                    externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
                }

                references.Add(new NodeStateReference(ReferenceTypes.Organizes, false, node.NodeId));
                node.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);
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
                // In our simplified address space, we only have one variable to update
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

                // Skip updating other variables since they're not in our simplified address space
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
