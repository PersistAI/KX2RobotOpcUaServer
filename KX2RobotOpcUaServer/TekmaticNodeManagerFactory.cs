using System;
using Opc.Ua;
using Opc.Ua.Server;
using LabEquipmentOpcUa;
using TekmaticOpcUa;

namespace TekmaticOpcUa
{
    /// <summary>
    /// Factory class for creating TekmaticNodeManager instances
    /// </summary>
    public class TekmaticNodeManagerFactory : IEquipmentNodeManager
    {
        private readonly OpcUaServer _opcServer;

        /// <summary>
        /// Initializes a new instance of the TekmaticNodeManagerFactory class.
        /// </summary>
        public TekmaticNodeManagerFactory(OpcUaServer opcServer)
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
                namespaces.Add("http://persist.com/Tekmatic");
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
                Console.WriteLine("Creating TekmaticNodeManager...");

                // Create a new TekmaticControl if it doesn't exist
                if (_tekmaticControl == null)
                {
                    Console.WriteLine("Creating new TekmaticControl instance...");
                    _tekmaticControl = new TekmaticControl();
                }

                // Create the node manager with the server and configuration
                Console.WriteLine("Creating TekmaticNodeManager with server and configuration...");
                return new TekmaticNodeManager(server, configuration, _tekmaticControl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating node manager: {ex.Message}");
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
                // Initialize the Tekmatic device
                Console.WriteLine("Initializing Tekmatic device...");
                bool initSuccess = _tekmaticControl.Initialize();

                if (initSuccess)
                {
                    Console.WriteLine("Tekmatic device initialized successfully.");
                }
                else
                {
                    Console.WriteLine("Tekmatic device initialization failed, but server will continue running.");
                    // We don't throw an exception here, allowing the server to continue running
                    // even if the Tekmatic device is not available
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Tekmatic device: {ex.Message}");
                // Don't rethrow the exception, allowing the server to continue running
                Console.WriteLine("Server will continue running despite Tekmatic initialization error.");
            }
        }

        /// <summary>
        /// Shuts down the equipment.
        /// </summary>
        public void Shutdown()
        {
            try
            {
                // Shutdown the Tekmatic device
                Console.WriteLine("Shutting down Tekmatic device...");
                _tekmaticControl.Shutdown();
                Console.WriteLine("Tekmatic device shut down successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error shutting down Tekmatic device: {ex.Message}");
            }
        }

        // Reference to the Tekmatic control
        private TekmaticControl _tekmaticControl;
    }
}
