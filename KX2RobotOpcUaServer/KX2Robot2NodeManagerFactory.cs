using System;
using Opc.Ua;
using Opc.Ua.Server;
using LabEquipmentOpcUa;

namespace KX2Robot2OpcUa
{
    /// <summary>
    /// Factory class for creating KX2Robot2NodeManager instances
    /// </summary>
    public class KX2Robot2NodeManagerFactory : IEquipmentNodeManager
    {
        private readonly OpcUaServer _opcServer;

        /// <summary>
        /// Initializes a new instance of the KX2Robot2NodeManagerFactory class.
        /// </summary>
        public KX2Robot2NodeManagerFactory(OpcUaServer opcServer)
        {
            _opcServer = opcServer;
        }

        /// <summary>
        /// Gets the namespace URIs for the node manager.
        /// </summary>
        public StringCollection NamespacesUris
        {
            get
            {
                StringCollection namespaces = new StringCollection();
                namespaces.Add("http://persist.com/KX2Robot2");
                return namespaces;
            }
        }

        /// <summary>
        /// Creates a new node manager.
        /// </summary>
        public INodeManager Create(IServerInternal server, ApplicationConfiguration configuration)
        {
            try
            {
                Console.WriteLine("Creating KX2Robot2NodeManager...");

                // Create a new KX2RobotControl if it doesn't exist
                if (_kx2Robot2 == null)
                {
                    Console.WriteLine("Creating new KX2RobotControl instance for Robot 2...");
                    _kx2Robot2 = new KX2RobotControlNamespace.KX2RobotControl();
                }

                // Create the node manager with the server and configuration
                Console.WriteLine("Creating KX2Robot2NodeManager with server and configuration...");
                return new KX2Robot2NodeManager(server, configuration, _kx2Robot2);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating Robot 2 node manager: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        /// <summary>
        /// Initializes the equipment.
        /// </summary>
        public void Initialize()
        {
            try
            {
                // Configure PCAN device for Robot 2
                Console.WriteLine("Configuring Robot 2 PCAN device...");
                string robot2PCANDevice = "PCAN_USB 2 (52h)";
                _kx2Robot2.SetCANDevice(robot2PCANDevice);
                Console.WriteLine($"Robot 2 PCAN device configured to: {robot2PCANDevice}");

                // Initialize Robot 2
                Console.WriteLine("Initializing KX2 Robot 2...");
                int result = _kx2Robot2.Initialize();
                if (result == 0)
                    Console.WriteLine("KX2 Robot 2 initialized successfully.");
                else
                    Console.WriteLine($"Failed to initialize KX2 Robot 2: Error {result}");

                // Load default teach points and sequence files for Robot 2
                LoadDefaultTeachPoints();
                LoadDefaultSequenceFile();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing KX2 Robot 2: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Shuts down the equipment.
        /// </summary>
        public void Shutdown()
        {
            try
            {
                // Shutdown Robot 2
                Console.WriteLine("Shutting down KX2 Robot 2...");
                _kx2Robot2.ShutDown();
                Console.WriteLine("KX2 Robot 2 shut down successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error shutting down KX2 Robot 2: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the default teach points for Robot 2.
        /// </summary>
        private void LoadDefaultTeachPoints()
        {
            try
            {
                // Get the default teach points file path from registry using DLL method
                string teachPointsFilePath = _kx2Robot2.GetDefaultTeachPointFile();

                // Check if a path was returned and file exists
                if (string.IsNullOrEmpty(teachPointsFilePath))
                {
                    Console.WriteLine("No default Teach Points file configured in registry for Robot 2.");
                    return;
                }

                if (!System.IO.File.Exists(teachPointsFilePath))
                {
                    Console.WriteLine($"Default Teach Points file for Robot 2 not found at: {teachPointsFilePath}");
                    return;
                }

                // Load the default teach points
                short fileNumAxes = 0;
                short loadResult = _kx2Robot2.TeachPointsLoad(teachPointsFilePath, true, ref fileNumAxes);

                if (loadResult == 0)
                {
                    Console.WriteLine($"Default Teach Points for Robot 2 loaded successfully from: {teachPointsFilePath}");
                }
                else
                {
                    Console.WriteLine($"Error loading default Teach Points for Robot 2: Code {loadResult}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred loading Robot 2 teach points: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the default sequence file for Robot 2.
        /// </summary>
        private void LoadDefaultSequenceFile()
        {
            try
            {
                // Get the default sequence file path from registry using DLL method
                string sequenceFilePath = _kx2Robot2.GetDefaultSequenceFile();

                // Check if a path was returned and file exists
                if (string.IsNullOrEmpty(sequenceFilePath))
                {
                    Console.WriteLine("No default Sequence file configured in registry for Robot 2.");
                    return;
                }

                if (!System.IO.File.Exists(sequenceFilePath))
                {
                    Console.WriteLine($"Default Sequence file for Robot 2 not found at: {sequenceFilePath}");
                    return;
                }

                // Set the default sequence file
                _kx2Robot2.SetDefaultSequenceFile(sequenceFilePath);
                Console.WriteLine($"Default Sequence file for Robot 2 set to: {sequenceFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred while loading Robot 2 sequence file: {ex.Message}");
            }
        }

        // Reference to the KX2 Robot 2 control
        private KX2RobotControlNamespace.KX2RobotControl _kx2Robot2;
    }
}
