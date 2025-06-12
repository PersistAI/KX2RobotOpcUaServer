using System;
using Opc.Ua;
using Opc.Ua.Server;
using LabEquipmentOpcUa;

namespace TecanOpcUa
{
    public class TecanNodeManagerFactory : IEquipmentNodeManager
    {
        private readonly OpcUaServer _opcServer;
        private TecanControl _tecan;

        public TecanNodeManagerFactory(OpcUaServer opcServer)
        {
            _opcServer = opcServer;
        }

        public StringCollection NamespacesUris
        {
            get
            {
                StringCollection namespaces = new StringCollection();
                namespaces.Add("http://persist.com/Tecan");
                return namespaces;
            }
        }

        public INodeManager Create(IServerInternal server, ApplicationConfiguration configuration)
        {
            try
            {
                Console.WriteLine("Creating TecanNodeManager...");

                // Create a new TecanControl if it doesn't exist
                if (_tecan == null)
                {
                    Console.WriteLine("Creating new TecanControl instance...");
                    _tecan = new TecanControl();
                }

                // Create the node manager with the server and configuration
                Console.WriteLine("Creating TecanNodeManager with server and configuration...");
                return new TecanNodeManager(server, configuration, _tecan);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating node manager: {ex.Message}");
                throw;
            }
        }

        public void Initialize()
        {
            try
            {
                // Initialize the Tecan
                Console.WriteLine("Initializing Tecan...");
                // We don't automatically connect here - the user will call Connect() explicitly
                Console.WriteLine("Tecan ready for connection.");
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
                if (_tecan != null)
                {
                    _tecan.Disconnect();
                    Console.WriteLine("Tecan shut down successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error shutting down Tecan: {ex.Message}");
            }
        }
    }
}
