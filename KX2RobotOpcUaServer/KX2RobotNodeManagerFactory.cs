using System;
using Opc.Ua;
using Opc.Ua.Server;
using LabEquipmentOpcUa;

namespace KX2RobotOpcUa
{
    /// <summary>
    /// Factory class for creating KX2RobotNodeManager instances
    /// </summary>
    public class KX2RobotNodeManagerFactory : IEquipmentNodeManager
    {
        private readonly OpcUaServer _opcServer;

        /// <summary>
        /// Initializes a new instance of the KX2RobotNodeManagerFactory class.
        /// </summary>
        public KX2RobotNodeManagerFactory(OpcUaServer opcServer)
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
                namespaces.Add("http://persist.com/KX2Robot");
                return namespaces;
            }
        }

        /// <summary>
        /// Creates a new node manager.
        /// </summary>
        public INodeManager Create(IServerInternal server, ApplicationConfiguration configuration)
        {
            // Create a new KX2RobotNodeManager
            var kx2Robot = new KX2RobotControlNamespace.KX2RobotControl();
            _kx2Robot = kx2Robot;
            return new KX2RobotNodeManager(server, configuration, kx2Robot);
        }

        /// <summary>
        /// Initializes the equipment.
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
        /// Shuts down the equipment.
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
                if (!System.IO.File.Exists(teachPointsFilePath))
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
                string appDirPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TeachPoints.ini");
                if (System.IO.File.Exists(appDirPath))
                {
                    Console.WriteLine($"Found TeachPoints.ini in application directory: {appDirPath}");
                    return appDirPath;
                }

                // If not found, check one level up (bin folder)
                string binDirPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\TeachPoints.ini");
                if (System.IO.File.Exists(binDirPath))
                {
                    string fullPath = System.IO.Path.GetFullPath(binDirPath);
                    Console.WriteLine($"Found TeachPoints.ini one level up: {fullPath}");
                    return fullPath;
                }

                // If still not found, check two levels up (project root directory)
                string projectDirPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\TeachPoints.ini");
                if (System.IO.File.Exists(projectDirPath))
                {
                    string fullPath = System.IO.Path.GetFullPath(projectDirPath);
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
                string defaultPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TeachPoints.ini");
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
                if (!System.IO.File.Exists(sequenceFilePath))
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
                string appDirPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sequences.ini");
                if (System.IO.File.Exists(appDirPath))
                {
                    Console.WriteLine($"Found Sequences.ini in application directory: {appDirPath}");
                    return appDirPath;
                }

                // If not found, check one level up (bin folder)
                string binDirPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\Sequences.ini");
                if (System.IO.File.Exists(binDirPath))
                {
                    string fullPath = System.IO.Path.GetFullPath(binDirPath);
                    Console.WriteLine($"Found Sequences.ini one level up: {fullPath}");
                    return fullPath;
                }

                // If still not found, check two levels up (project root directory)
                string projectDirPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\Sequences.ini");
                if (System.IO.File.Exists(projectDirPath))
                {
                    string fullPath = System.IO.Path.GetFullPath(projectDirPath);
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
                string defaultPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sequences.ini");
                Console.WriteLine($"Error finding Sequences.ini: {ex.Message}. Will use default path: {defaultPath}");
                return defaultPath;
            }
        }

        // Reference to the KX2 robot control
        private KX2RobotControlNamespace.KX2RobotControl _kx2Robot;
    }
}
