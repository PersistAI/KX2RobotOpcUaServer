using System;
using System.Collections.Generic;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using Opc.Ua;
using Opc.Ua.Server;
using Opc.Ua.Configuration;

namespace LabEquipmentOpcUa
{
    /// <summary>
    /// Main entry point for the OPC UA Server application
    /// </summary>
    public class Program
    {
        #region Private Fields
        private static OpcUaServer _server;
        private static ManualResetEvent _exitEvent;
        #endregion

        #region Main Entry Point
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static void Main(string[] args)
        {
            Console.WriteLine("Laboratory Equipment OPC UA Server");
            Console.WriteLine("----------------------------------");

            try
            {
                // Initialize the server and process the command line arguments
                _exitEvent = new ManualResetEvent(false);

                // Start the server
                _server = new OpcUaServer();
                _server.Start();

                // Wait for user to exit the program
                Console.WriteLine("OPC UA Server is running. Press Ctrl+C to exit.");

                // Set up console cancellation
                Console.CancelKeyPress += (sender, e) => {
                    e.Cancel = true; // Prevent the process from terminating immediately
                    _exitEvent.Set();
                };

                // Wait for exit signal
                _exitEvent.WaitOne();

                // Stop the server
                _server.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }
        #endregion
    }

    /// <summary>
    /// Main OPC UA Server Implementation
    /// </summary>
    public class OpcUaServer
    {
        #region Private Fields
        private ApplicationInstance _application;
        private ApplicationConfiguration _config;
        private StandardServer _server;
        private List<IEquipmentNodeManager> _equipmentManagers = new List<IEquipmentNodeManager>();
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the OpcUaServer class.
        /// </summary>
        public OpcUaServer()
        {
            // Node managers will be created after server initialization
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Starts the server.
        /// </summary>
        public void Start()
        {
            try
            {
                // Initialize and start the OPC UA server
                Console.WriteLine("Starting OPC UA server...");
                InitializeServer();
                _server.Start(_config);
                Console.WriteLine("OPC UA server started successfully.");

                // Initialize all equipment (after server and node managers are initialized)
                Console.WriteLine("Initializing equipment...");
                foreach (var equipment in _equipmentManagers)
                {
                    equipment.Initialize();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting server: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Stops the server.
        /// </summary>
        public void Stop()
        {
            try
            {
                // Stop the OPC UA server
                if (_server != null)
                {
                    Console.WriteLine("Stopping OPC UA server...");
                    _server.Stop();
                    Console.WriteLine("OPC UA server stopped successfully.");
                }

                // Shutdown all equipment
                Console.WriteLine("Shutting down equipment...");
                foreach (var equipment in _equipmentManagers)
                {
                    equipment.Shutdown();
                }
                Console.WriteLine("Equipment shut down successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping server: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the server instance.
        /// </summary>
        public StandardServer Server
        {
            get { return _server; }
        }

        /// <summary>
        /// Gets the application configuration.
        /// </summary>
        public ApplicationConfiguration Configuration
        {
            get { return _config; }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Initializes the OPC UA server.
        /// </summary>
        private void InitializeServer()
        {
            // Create the application configuration
            _application = new ApplicationInstance
            {
                ApplicationName = "Laboratory Equipment OPC UA Server",
                ApplicationType = ApplicationType.Server,
                ConfigSectionName = "LabEquipmentOpcUaServer"
            };

            // Load the application configuration
            _config = CreateApplicationConfiguration();

            // Check the application certificate
            bool certOk = false;
            try
            {
                // Try to check/create the certificate
                certOk = _application.CheckApplicationInstanceCertificate(false, CertificateFactory.DefaultKeySize, CertificateFactory.DefaultLifeTime).Result;
                if (!certOk)
                {
                    Console.WriteLine("Application instance certificate invalid!");
                }
            }
            catch (Exception ex)
            {
                // Log the actual exception details
                Console.WriteLine($"Certificate error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                // Continue anyway - this will allow the server to run without a valid certificate
                // This is acceptable for development but not for production
                Console.WriteLine("Continuing without valid certificate (OK for development only)");
            }

            // Create the server
            _server = new StandardServer();

            // NOW create and add the node managers (after _server and _config are initialized)
            _equipmentManagers.Add(new KX2RobotOpcUa.KX2RobotNodeManager(this));
            // Future equipment can be added here:
            // _equipmentManagers.Add(new TecanOpcUa.TecanNodeManager(this));

            // Add the node managers to the server
            foreach (var manager in _equipmentManagers)
            {
                _server.AddNodeManager((INodeManagerFactory)manager);
            }
        }

        /// <summary>
        /// Creates the application configuration.
        /// </summary>
        private ApplicationConfiguration CreateApplicationConfiguration()
        {
            // Create the application configuration
            ApplicationConfiguration config = new ApplicationConfiguration
            {
                ApplicationName = "Laboratory Equipment OPC UA Server",
                ApplicationUri = "urn:localhost:LabEquipmentOpcUaServer",
                ProductUri = "http://persist.com/LabEquipmentOpcUaServer",
                ApplicationType = ApplicationType.Server,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.X509Store,
                        StorePath = "CurrentUser\\My",
                        SubjectName = "CN=Laboratory Equipment OPC UA Server"
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.X509Store,
                        StorePath = "CurrentUser\\TrustedPeople"
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.X509Store,
                        StorePath = "CurrentUser\\TrustedPeople"
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.X509Store,
                        StorePath = "CurrentUser\\RejectedCertificates"
                    },
                    AutoAcceptUntrustedCertificates = true,
                    RejectSHA1SignedCertificates = false
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ServerConfiguration = new ServerConfiguration
                {
                    BaseAddresses = new StringCollection
                    {
                        "opc.tcp://localhost:4840/LabEquipmentOpcUaServer"
                    },
                    MinRequestThreadCount = 5,
                    MaxRequestThreadCount = 100,
                    MaxQueuedRequestCount = 200,
                },
                TraceConfiguration = new TraceConfiguration
                {
                    TraceMasks = 1
                }
            };

            return config;
        }
        #endregion
    }

    /// <summary>
    /// Interface for equipment node managers
    /// </summary>
    public interface IEquipmentNodeManager : INodeManager
    {
        /// <summary>
        /// Initializes the equipment.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Shuts down the equipment.
        /// </summary>
        void Shutdown();
    }
}
