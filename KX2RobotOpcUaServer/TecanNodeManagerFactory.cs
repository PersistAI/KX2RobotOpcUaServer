using System;
using Opc.Ua;
using Opc.Ua.Server;
using LabEquipmentOpcUa;
using TecanOpcUa;

namespace TecanOpcUa
{
    /// <summary>
    /// Factory class for creating TecanNodeManager instances
    /// </summary>
    public class TecanNodeManagerFactory : IEquipmentNodeManager
    {
        private readonly OpcUaServer _opcServer;
        private TecanControl _tecanControl;

        /// <summary>
        /// Initializes a new instance of the TecanNodeManagerFactory class.
        /// </summary>
        public TecanNodeManagerFactory(OpcUaServer opcServer)
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
                namespaces.Add("http://persist.com/Tecan");
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
                Console.WriteLine("Creating TecanNodeManager...");

                // Create a new TecanControl if it doesn't exist
                if (_tecanControl == null)
                {
                    Console.WriteLine("Creating new TecanControl instance...");
                    _tecanControl = new TecanControl();
                }

                // Create the node manager with the server and configuration
                Console.WriteLine("Creating TecanNodeManager with server and configuration...");
                return new TecanNodeManager(server, configuration, _tecanControl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating Tecan node manager: {ex.Message}");
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
                // Initialize the Tecan control
                Console.WriteLine("Initializing Tecan device...");

                // Discover available devices
                int deviceCount = _tecanControl.DiscoverDevices();
                Console.WriteLine($"Found {deviceCount} Tecan devices.");

                // Don't automatically connect - let the user choose which device to connect to
                Console.WriteLine("Tecan initialization complete. Ready for connection.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Tecan device: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        /// Shuts down the equipment.
        /// </summary>
        public void Shutdown()
        {
            try
            {
                // Shutdown the Tecan device
                Console.WriteLine("Shutting down Tecan device...");
                if (_tecanControl != null && _tecanControl.IsConnected())
                {
                    _tecanControl.Disconnect();
                }
                Console.WriteLine("Tecan device shut down successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error shutting down Tecan device: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }
    }
}
