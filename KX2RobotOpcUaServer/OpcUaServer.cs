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

                try
                {
                    // Add more detailed logging for server start
                    Console.WriteLine("Starting server with configuration...");
                    _server.Start(_config);
                    Console.WriteLine("OPC UA server started successfully.");

                    // Re-enable equipment initialization since we've created the factory
                    Console.WriteLine("Initializing equipment...");
                    foreach (var equipment in _equipmentManagers)
                    {
                        try
                        {
                            equipment.Initialize();
                        }
                        catch (Exception ex)
                        {
                            // Log the error but continue with other equipment
                            Console.WriteLine($"Error initializing equipment {equipment.GetType().Name}: {ex.Message}");
                            Console.WriteLine("Server will continue running with other equipment.");

                            // If there's an inner exception, log that too
                            if (ex.InnerException != null)
                            {
                                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log detailed error information
                    Console.WriteLine($"Error starting server: {ex.Message}");
                    Console.WriteLine($"Error type: {ex.GetType().Name}");

                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        Console.WriteLine($"Inner exception type: {ex.InnerException.GetType().Name}");

                        if (ex.InnerException.InnerException != null)
                        {
                            Console.WriteLine($"Inner inner exception: {ex.InnerException.InnerException.Message}");
                        }
                    }

                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in server initialization: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
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

                // Re-enable equipment shutdown since we've re-enabled initialization
                Console.WriteLine("Shutting down equipment...");
                foreach (var equipment in _equipmentManagers)
                {
                    try
                    {
                        equipment.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with other equipment
                        Console.WriteLine($"Error shutting down equipment {equipment.GetType().Name}: {ex.Message}");

                        // If there's an inner exception, log that too
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        }
                    }
                }
                Console.WriteLine("Equipment shut down process completed.");
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

            // IMPORTANT: Load the configuration into the application instance
            _application.ApplicationConfiguration = _config;

            // Check/Create the application certificate - FORCE creation
            try
            {
                Console.WriteLine("Checking application certificate...");

                // Set AutoAcceptUntrustedCertificates to true to avoid certificate validation issues
                _config.SecurityConfiguration.AutoAcceptUntrustedCertificates = true;

                // Force creation of a new certificate if needed
                bool certOk = _application.CheckApplicationInstanceCertificate(
                    silent: false,           // Set to false to allow certificate creation
                    minimumKeySize: CertificateFactory.DefaultKeySize,
                    lifeTimeInMonths: CertificateFactory.DefaultLifeTime
                ).Result;

                if (!certOk)
                {
                    throw new Exception("Failed to create or validate application certificate");
                }

                Console.WriteLine("Application certificate validated successfully.");
            }
            catch (Exception ex)
            {
                // Log the actual exception details
                Console.WriteLine($"Certificate error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                // Don't continue - certificate is required
                throw new Exception("Failed to create or validate application certificate.", ex);
            }

            // Create the server
            _server = new StandardServer();

            // Note: We don't need to explicitly set the application instance
            // The server will use the application configuration when Start is called

            Console.WriteLine("Step 2: Creating node manager factory and adding to server...");

            // Create node managers differently using factories
            var nodeManagerFactories = new List<INodeManagerFactory>();

            // Create the KX2 robot factory
            var kx2Factory = new KX2RobotOpcUa.KX2RobotNodeManagerFactory(this);
            nodeManagerFactories.Add(kx2Factory);
            _equipmentManagers.Add((IEquipmentNodeManager)kx2Factory);

            // Create the Tecan factory
            var tecanFactory = new TecanOpcUa.TecanNodeManagerFactory(this);
            nodeManagerFactories.Add(tecanFactory);
            _equipmentManagers.Add((IEquipmentNodeManager)tecanFactory);

            // Create the Tekmatic factory
            var tekmaticFactory = new TekmaticOpcUa.TekmaticNodeManagerFactory(this);
            nodeManagerFactories.Add(tekmaticFactory);
            _equipmentManagers.Add((IEquipmentNodeManager)tekmaticFactory);

            // Add the node manager factories to the server
            Console.WriteLine("Adding node manager factories to server...");
            foreach (var factory in nodeManagerFactories)
            {
                try
                {
                    Console.WriteLine($"Adding factory: {factory.GetType().Name}");
                    _server.AddNodeManager(factory);
                    Console.WriteLine("Factory added successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error adding factory: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                    throw;
                }
            }
        }

        /// <summary>
        /// Creates the application configuration.
        /// </summary>
        private ApplicationConfiguration CreateApplicationConfiguration()
        {
            Console.WriteLine("Creating simplified application configuration...");

            // Create a simplified application configuration with minimal settings
            ApplicationConfiguration config = new ApplicationConfiguration
            {
                ApplicationName = "Laboratory Equipment OPC UA Server",
                ApplicationUri = "urn:localhost:LabEquipmentOpcUaServer",
                ProductUri = "http://persist.com/LabEquipmentOpcUaServer",
                ApplicationType = ApplicationType.Server,

                // Simplified security configuration
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault",
                        SubjectName = "CN=Laboratory Equipment OPC UA Server"
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications"
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities"
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates"
                    },
                    AutoAcceptUntrustedCertificates = true,
                    RejectSHA1SignedCertificates = false,
                    MinimumCertificateKeySize = 1024  // Reduced key size for faster generation
                },

                // Simplified transport configuration
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas
                {
                    OperationTimeout = 120000,  // Increased timeout
                    MaxStringLength = 1048576,
                    MaxByteStringLength = 1048576,
                    MaxArrayLength = 65535,
                    MaxMessageSize = 4194304,
                    MaxBufferSize = 65535,
                    ChannelLifetime = 300000,
                    SecurityTokenLifetime = 3600000
                },

                // Simplified server configuration
                ServerConfiguration = new ServerConfiguration
                {
                    BaseAddresses = new StringCollection
                    {
                        "opc.tcp://localhost:4840/LabEquipmentOpcUaServer"
                    },
                    MinRequestThreadCount = 5,
                    MaxRequestThreadCount = 100,
                    MaxQueuedRequestCount = 200,

                    // Add security policies - None for testing
                    SecurityPolicies = new ServerSecurityPolicyCollection
                    {
                        new ServerSecurityPolicy
                        {
                            SecurityMode = MessageSecurityMode.None,
                            SecurityPolicyUri = SecurityPolicies.None
                        }
                    },

                    UserTokenPolicies = new UserTokenPolicyCollection
                    {
                        new UserTokenPolicy
                        {
                            TokenType = UserTokenType.Anonymous,
                            SecurityPolicyUri = SecurityPolicies.None
                        }
                    }
                },

                // Increased trace level for debugging
                TraceConfiguration = new TraceConfiguration
                {
                    TraceMasks = 515,  // Increased trace level
                    OutputFilePath = "%CommonApplicationData%\\OPC Foundation\\Logs\\LabEquipmentOpcUaServer.log"
                }
            };

            Console.WriteLine("Application configuration created successfully.");
            return config;
        }
        #endregion
    }

    /// <summary>
    /// Interface for equipment node managers
    /// </summary>
    public interface IEquipmentNodeManager : INodeManagerFactory
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
