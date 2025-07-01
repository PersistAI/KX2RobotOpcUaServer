# KX2RobotOpcUaServer

## Overview

KX2RobotOpcUaServer is an OPC UA server application designed to provide a standardized interface for laboratory automation equipment. It integrates multiple laboratory devices (KX2 Robot, Tecan instruments, and Tekmatic controllers) into a unified OPC UA server, enabling remote monitoring and control through a consistent, industry-standard protocol.

The server exposes equipment functionality as OPC UA nodes, allowing client applications to:
- Monitor equipment status in real-time
- Execute commands and operations
- Access and modify configuration parameters
- Automate complex workflows across multiple devices

## Architecture

The application is built on the OPC UA .NET Standard stack and follows a modular architecture with three main layers:

1. **OPC UA Server Layer**: The top-level component that handles client connections, security, and coordinates the node managers. This layer exposes a unified OPC UA interface to clients.

2. **Node Manager Layer**: Contains specialized node managers for each equipment type:
   - KX2 Robot Node Manager: Manages nodes for robot control and monitoring
   - Tecan Node Manager: Manages nodes for Tecan laboratory instruments
   - Tekmatic Node Manager: Manages nodes for Tekmatic controllers

3. **Equipment Control Layer**: Implements the low-level communication with the physical devices:
   - KX2 Robot Controller: Communicates with the KX2 Robot hardware
   - Tecan Controller: Interfaces with Tecan laboratory instruments
   - Tekmatic Controller: Interfaces with Tekmatic motion controllers

Each node manager translates between the OPC UA protocol and the equipment-specific protocols, providing a standardized interface for diverse laboratory equipment.

### Key Components:

1. **Main OPC UA Server** (`OpcUaServer.cs`): Initializes and manages the OPC UA server, handles client connections, and coordinates the node managers.

2. **Equipment Node Managers**:
   - **KX2RobotNodeManager**: Manages OPC UA nodes for the KX2 Robot
   - **TecanNodeManager**: Manages OPC UA nodes for Tecan instruments
   - **TekmaticNodeManager**: Manages OPC UA nodes for Tekmatic controllers

3. **Node Manager Factories**:
   - Create and initialize the respective node managers
   - Handle equipment-specific initialization and shutdown procedures

4. **Equipment Controllers**:
   - **KX2RobotControl**: Interfaces with the KX2 Robot hardware
   - **TecanControl**: Interfaces with Tecan instruments
   - **TekmaticControl**: Interfaces with Tekmatic controllers

## Features

### General Features

- **Unified Interface**: Access all laboratory equipment through a single OPC UA server
- **Real-time Monitoring**: Monitor equipment status, positions, and operational parameters
- **Remote Control**: Execute commands and operations on equipment from remote clients
- **Error Handling**: Comprehensive error reporting and handling
- **Automatic Certificate Management**: Self-signed certificate generation and management
- **Configurable Security**: Support for various security policies and user authentication

### KX2 Robot Features

- **Robot Status Monitoring**: Track initialization status, movement status, maintenance requirements
- **Position Control**: Absolute and relative movement commands for all robot axes
- **Teach Points**: Load, save, and move to predefined teach points
- **Sequence Execution**: Run predefined sequences of operations
- **Barcode Reading**: Interface with the robot's barcode reader
- **Variable Management**: Update variables used in robot sequences

### Tecan Instrument Features

- **Device Discovery**: Automatically discover available Tecan instruments
- **Connection Management**: Connect to and disconnect from Tecan devices
- **Status Monitoring**: Monitor instrument status and operational parameters

### Tekmatic Controller Features

- **Controller Initialization**: Initialize and configure Tekmatic controllers
- **Status Monitoring**: Monitor controller status and operational parameters
- **Command Execution**: Execute commands on Tekmatic controllers

## Equipment Integration

### KX2 Robot

The server integrates with KX2 Robot systems, providing control over:

- **Axis Movement**: Control of all robot axes (Shoulder, Z-Axis, Elbow, Wrist, and Rail)
- **Teach Points**: Management of predefined positions
- **Sequences**: Execution of complex operation sequences
- **Barcode Reading**: Integration with the robot's barcode reader

### Tecan Instruments

The server supports Tecan laboratory instruments:

- **Device Discovery**: Automatic discovery of connected Tecan devices
- **Device Control**: Remote control of Tecan instrument operations
- **Status Monitoring**: Real-time monitoring of instrument status

### Tekmatic Controllers

The server integrates with Tekmatic motion controllers:

- **Controller Initialization**: Setup and configuration of Tekmatic controllers
- **Motion Control**: Precise control of Tekmatic-driven motion systems
- **Status Monitoring**: Real-time monitoring of controller status

## Installation

### Prerequisites

- Windows 10 or later
- .NET Framework 4.7.2 or later
- Visual Studio 2019 or later (for development)
- Administrative privileges (for OPC UA certificate management)

### Installation Steps

1. **Clone the repository**:
   ```
   git clone https://github.com/yourusername/KX2RobotOpcUaServer.git
   ```

2. **Open the solution in Visual Studio**:
   - Open `KX2RobotOpcUaServer.sln` in Visual Studio

3. **Restore NuGet packages**:
   - Right-click on the solution in Solution Explorer
   - Select "Restore NuGet Packages"

4. **Build the solution**:
   - Select Build > Build Solution from the menu
   - Or press Ctrl+Shift+B

5. **Deploy the application**:
   - Copy the contents of the `bin/Release` or `bin/Debug` folder to your deployment location

### Required Files

Ensure the following files are present in the deployment directory:

- `KX2RobotOpcUaServer.exe`: The main application executable
- `Communication.Port.USB.Unmanaged.dll`: Required for KX2 Robot communication
- `TeachPoints.ini`: Configuration file for robot teach points (optional)
- `Sequences.ini`: Configuration file for robot sequences (optional)

## Configuration

### Server Configuration

The server uses a simplified configuration approach with hardcoded settings in `OpcUaServer.cs`. Key configuration parameters include:

- **Server Endpoint**: `opc.tcp://0.0.0.0:4840/LabEquipmentOpcUaServer`
- **Security Policies**: Currently set to `None` for testing purposes
- **Certificate Store**: Located at `%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault`

### Equipment Configuration

#### KX2 Robot

The KX2 Robot requires configuration files for teach points and sequences:

- **Teach Points File**: `TeachPoints.ini` - Contains predefined robot positions
- **Sequences File**: `Sequences.ini` - Contains predefined operation sequences

## Usage

### Starting the Server

1. Run `KX2RobotOpcUaServer.exe`
2. The server will initialize and start listening for connections
3. Equipment will be initialized automatically

### Connecting to the Server

Use any OPC UA client to connect to the server at:
```
opc.tcp://[hostname]:4840/LabEquipmentOpcUaServer
```

Where `[hostname]` is the name or IP address of the computer running the server.

### Available OPC UA Nodes

The server exposes the following node structure:

```
Objects
├── KX2Robot
│   ├── Status
│   │   ├── IsInitialized
│   │   ├── IsMoving
│   │   ├── IsRobotOnRail
│   │   ├── IsScriptRunning
│   │   ├── IsZMaintenanceRequired
│   │   ├── IsRailMaintenanceRequired
│   │   ├── MoveCount
│   │   ├── LastBarcode
│   │   ├── BarcodeReaderVersion
│   │   ├── BarcodeReaderPort
│   │   ├── Axis1Position (Shoulder)
│   │   ├── Axis2Position (Z-Axis)
│   │   ├── Axis3Position (Elbow)
│   │   ├── Axis4Position (Wrist)
│   │   └── Axis5Position (Rail)
│   ├── Commands
│   │   ├── Initialize
│   │   ├── Shutdown
│   │   ├── MoveAbsolute
│   │   ├── MoveRelative
│   │   ├── LoadTeachPoints
│   │   ├── MoveToTeachPoint
│   │   ├── ExecuteSequence
│   │   ├── UpdateVariable
│   │   └── ReadBarcode
│   ├── TeachPoints
│   │   └── [TeachPoint1...N]
│   └── Sequences
│       └── [Sequence1...N]
├── Tecan
│   ├── Status
│   │   ├── IsConnected
│   │   ├── IsPlateIn
│   │   ├── Temperature
│   │   ├── ConnectedDeviceSerial
│   │   ├── DeviceCount
│   │   ├── DeviceInfo
│   │   │   ├── SerialNumber
│   │   │   ├── ProductName
│   │   │   ├── Model
│   │   │   ├── FirmwareVersion
│   │   │   ├── InstrumentAlias
│   │   │   └── InternalName
│   │   ├── DeviceCapabilities
│   │   │   ├── HasAbsorbanceFixed
│   │   │   ├── HasAbsorbanceScan
│   │   │   └── TemperatureRange
│   │   │       ├── MinTemperature
│   │   │       └── MaxTemperature
│   │   └── DiscoveredDevices
│   │       ├── DeviceCount
│   │       └── [Device1...N]
│   │           ├── Name
│   │           ├── Serial
│   │           └── Connect
│   ├── Commands
│   │   ├── DiscoverDevices
│   │   ├── ConnectBySerial
│   │   ├── Connect
│   │   ├── Disconnect
│   │   ├── MovePlateIn
│   │   ├── MovePlateOut
│   │   └── SetTemperature
│   └── Measurements
│       └── PerformAbsorbanceMeasurement
└── Tekmatic
    ├── Status
    │   ├── IsConnected
    │   ├── DeviceCount
    │   ├── Slot1Device
    │   ├── Slot1Temperature
    │   ├── Slot2Device
    │   ├── Slot2Temperature
    │   ├── Slot3Device
    │   ├── Slot3Temperature
    │   ├── Slot4Device
    │   ├── Slot4Temperature
    │   ├── Slot5Device
    │   ├── Slot5Temperature
    │   ├── Slot6Device
    │   └── Slot6Temperature
    └── Commands
        ├── DiscoverDevices
        ├── ControlTemperature
        ├── ControlShaking
        ├── ClearErrorCodes
        └── ExecuteRawCommand
```

### Example Operations

#### KX2 Robot Operations

##### Initializing the KX2 Robot

Call the `Initialize` method on the KX2Robot.Commands node.

##### Moving the Robot to a Position

Call the `MoveAbsolute` method on the KX2Robot.Commands node with the following parameters:
- Axis1: Position for Shoulder axis (degrees)
- Axis2: Position for Z-Axis (millimeters)
- Axis3: Position for Elbow axis (degrees)
- Axis4: Position for Wrist axis (degrees)
- Velocity: Movement velocity percentage (0-100)
- Acceleration: Movement acceleration percentage (0-100)

##### Reading a Barcode

Call the `ReadBarcode` method on the KX2Robot.Commands node. The result will be returned as a string and also stored in the `LastBarcode` variable.

##### Moving to a Teach Point

Call the `MoveToTeachPoint` method on the KX2Robot.Commands node with the following parameters:
- TeachPointName: Name of the teach point
- Velocity: Movement velocity percentage (0-100)
- Acceleration: Movement acceleration percentage (0-100)

##### Executing a Sequence

Call the `ExecuteSequence` method on the KX2Robot.Commands node with the following parameter:
- SequenceName: Name of the sequence to execute

#### Tecan Operations

##### Discovering Tecan Devices

Call the `DiscoverDevices` method on the Tecan.Commands node. This will populate the DiscoveredDevices folder with available Tecan instruments.

##### Connecting to a Tecan Device

Call the `ConnectBySerial` method on the Tecan.Commands node with the following parameter:
- DeviceSerial: Serial number of the device to connect to

Alternatively, call the `Connect` method on a specific device node in the DiscoveredDevices folder.

##### Moving Plate In/Out

Call the `MovePlateIn` or `MovePlateOut` method on the Tecan.Commands node to control plate movement.

##### Performing an Absorbance Measurement

Call the `PerformAbsorbanceMeasurement` method on the Tecan.Measurements node with the following parameters:
- PlateType: The plate type to use (e.g., "GRE96ft")
- WellRange: The well range to measure (e.g., "A1:H12")
- Wavelength: The wavelength in nm (e.g., 250)
- NumberOfFlashes: The number of flashes (e.g., 10)
- SettleTime: The settle time in ms (e.g., 0)

The method returns a MeasurementId that can be used to reference the measurement results.

#### Tekmatic Operations

##### Discovering Tekmatic Devices

Call the `DiscoverDevices` method on the Tekmatic.Commands node. This will update the slot device information in the Status folder.

##### Controlling Temperature

Call the `ControlTemperature` method on the Tekmatic.Commands node with the following parameters:
- SlotId: The slot ID (1-6) to control
- Temperature: The target temperature in degrees Celsius
- Enable: True to enable temperature control, false to disable

##### Controlling Shaking

Call the `ControlShaking` method on the Tekmatic.Commands node with the following parameters:
- SlotId: The slot ID (1-6) to control
- Rpm: The target shaking RPM
- Enable: True to enable shaking, false to disable

##### Executing a Raw Command

Call the `ExecuteRawCommand` method on the Tekmatic.Commands node with the following parameter:
- Command: Full command string including slot ID prefix (e.g., '1SSR300' to set shaking RPM for slot 1)

The method returns the response from the device.

## Development

### Adding New Equipment

To add support for new laboratory equipment:

1. Create a new equipment control class (e.g., `NewEquipmentControl.cs`)
2. Create a new node manager class (e.g., `NewEquipmentNodeManager.cs`)
3. Create a new node manager factory class (e.g., `NewEquipmentNodeManagerFactory.cs`)
4. Add the new factory to the `OpcUaServer.cs` initialization code

### Extending Existing Equipment

To add new functionality to existing equipment:

1. Add new methods to the equipment control class
2. Add new nodes, variables, or methods to the equipment node manager
3. Update the node manager to expose the new functionality

### Building from Source

1. Clone the repository
2. Open the solution in Visual Studio
3. Build the solution using Visual Studio

## Troubleshooting

### Common Issues

#### Server Fails to Start

- **Certificate Issues**: Ensure the application has permission to create/access certificates
  - Run the application as Administrator
  - Check the certificate store at `%CommonApplicationData%\OPC Foundation\CertificateStores\`

- **Port Conflicts**: Ensure port 4840 is not in use by another application
  - Change the port in `OpcUaServer.cs` if needed

#### Equipment Initialization Failures

- **KX2 Robot**: 
  - Ensure the robot is powered on and connected
  - Check that `Communication.Port.USB.Unmanaged.dll` is present
  - Verify teach points and sequence files exist

- **Tecan Instruments**:
  - Ensure the instruments are powered on and connected
  - Check that any required Tecan drivers are installed

- **Tekmatic Controllers**:
  - Ensure the controllers are powered on and connected
  - Check that any required Tekmatic drivers are installed

### Logging

The server logs detailed information to the console, including:
- Server initialization steps
- Equipment initialization status
- Error messages and exceptions

For persistent logging, redirect the console output to a file:
```
KX2RobotOpcUaServer.exe > server_log.txt
```
