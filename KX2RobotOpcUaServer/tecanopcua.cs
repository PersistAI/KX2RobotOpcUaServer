using System;
using System.Collections.Generic;
using System.Threading;
using Opc.Ua;
using Opc.Ua.Server;
using Tecan.At.Measurement.Server;
using Tecan.At.Instrument.Common;
using Tecan.At.Common.Settings;
using LabEquipmentOpcUa;
using Tecan.At.Measurement;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Text;
using Tecan.At.Communication.Port;
using Tecan.At.Instrument;
using Tecan.At.Common;
using Tecan.At.Common.DocumentManagement;
using Tecan.At.Common.DocumentManagement.Reader;
using Tecan.At.XFluor.Device;
using Tecan.At.Measurement.Grid;
using System.Globalization;
using Tecan.At.XFluor.Connect;
using Tecan.At.XFluor.Core;
using Tecan.At.XFluor.ExcelOutput;
using Tecan.At.Common.FileManagement;
using Tecan.At.Instrument.Common.Reader;


namespace TecanOpcUa
{
    /// <summary>
    /// Constants for measurement modes
    /// </summary>
    public static class MeasMode
    {
        public static class Lumi
        {
            public const String Fixed = "LUM.FIXED";
        }

        public static class Abs
        {
            public const String Fixed = "ABS.FIXED";
            public const String Scan = "ABS.SCAN";
            public const String Cuv = "ABS.CUV";
        }

        public static class FI
        {
            public static class Top
            {
                public const String Fixed = "FI.TOP.FIXED";
                public const String ExScan = "FI.TOP.EXSCAN";
                public const String EmScan = "FI.TOP.EMSCAN";
            }

            public static class Bottom
            {
                public const String Fixed = "FI.BOTTOM.FIXED";
                public const String ExScan = "FI.BOTTOM.EXSCAN";
                public const String EmScan = "FI.BOTTOM.EMSCAN";
            }
        }

        public static class Pol
        {
            public const String Fixed = "POL";
        }

        public static class Cuvette
        {
            public const String Fixed = "CUVETTE.FIXED";
            public const String Scan = "CUVETTE.SCAN";
        }
    }

    /// <summary>
    /// Constants for filter usage
    /// </summary>
    public static class FilterUsage
    {
        public const string Ex = "Ex";
        public const string Em = "Em";

        public static class LUM
        {
            public const string OD = "OD";
            public const string Empty = "Empty";
        }
    }

    /// <summary>
    /// Constants for measurement modes
    /// </summary>
    public static class MeasMode2
    {
        public const string Absorbance = "Absorbance";
        public const string Fluorescence = "Fluorescence";
        public const string Luminescence = "Luminescence";
    }

    /// <summary>
    /// Helper class for Tecan device capabilities
    /// </summary>
    public class TecanHelper
    {
        // Properties to store reader information
        public TecanReaderDefinition ReaderDefinition { get; set; }
        public IReader Reader { get; set; }

        // Device capability methods
        public bool HasLuminescenceFixed()
        {
            try
            {
                if (Reader != null && Reader.ReaderCapabilities != null)
                {
                    return Reader.ReaderCapabilities.HasLuminescenceFixed();
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HasLuminescenceFixed: {ex.Message}");
                return false;
            }
        }

        public bool HasAbsorbanceFixed()
        {
            try
            {
                if (Reader != null && Reader.ReaderCapabilities != null)
                {
                    return Reader.ReaderCapabilities.HasAbsorbanceFixed();
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HasAbsorbanceFixed: {ex.Message}");
                return false;
            }
        }

        public bool HasCuvette()
        {
            try
            {
                if (Reader != null && Reader.ReaderCapabilities != null)
                {
                    return Reader.ReaderCapabilities.HasCuvette();
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HasCuvette: {ex.Message}");
                return false;
            }
        }

        public bool HasAbsorbanceScan()
        {
            try
            {
                if (Reader != null && Reader.ReaderCapabilities != null)
                {
                    return Reader.ReaderCapabilities.HasAbsorbanceScan();
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HasAbsorbanceScan: {ex.Message}");
                return false;
            }
        }

        public bool HasFluoresenceFixed()
        {
            try
            {
                if (Reader != null && Reader.ReaderCapabilities != null)
                {
                    return Reader.ReaderCapabilities.HasFluoresenceFixed();
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HasFluoresenceFixed: {ex.Message}");
                return false;
            }
        }

        public bool HasFluoresenceScan()
        {
            try
            {
                if (Reader != null && Reader.ReaderCapabilities != null)
                {
                    return Reader.ReaderCapabilities.HasFluoresenceScan();
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HasFluoresenceScan: {ex.Message}");
                return false;
            }
        }

        public bool HasFluorescencePolarization()
        {
            try
            {
                if (Reader != null && Reader.ReaderCapabilities != null)
                {
                    return Reader.ReaderCapabilities.HasFluorescencePolarization();
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HasFluorescencePolarization: {ex.Message}");
                return false;
            }
        }

        public bool HasHeating()
        {
            try
            {
                if (Reader != null && Reader.ReaderCapabilities != null)
                {
                    return Reader.ReaderCapabilities.HasHeating();
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HasHeating: {ex.Message}");
                return false;
            }
        }

        public bool HasShaking()
        {
            try
            {
                if (Reader != null && Reader.ReaderCapabilities != null)
                {
                    return Reader.ReaderCapabilities.HasShaking();
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HasShaking: {ex.Message}");
                return false;
            }
        }

        public bool HasInjection()
        {
            try
            {
                if (Reader != null && Reader.ReaderCapabilities != null)
                {
                    return Reader.ReaderCapabilities.HasInjection();
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HasInjection: {ex.Message}");
                return false;
            }
        }

        public bool HasBarcode()
        {
            try
            {
                if (ReaderDefinition != null && ReaderDefinition.CommonDef != null && ReaderDefinition.CommonDef.Functions != null)
                {
                    return ReaderDefinition.CommonDef.GetFunctionList().IsAvailable(FUNCTION.Barcode);
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HasBarcode: {ex.Message}");
                return false;
            }
        }

        public bool IsMonochromator()
        {
            try
            {
                if (Reader != null && Reader.ReaderCapabilities != null)
                {
                    return Reader.ReaderCapabilities.IsMonochromator();
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in IsMonochromator: {ex.Message}");
                return false;
            }
        }

        public void GetTemperatureRange(ref int nMin, ref int nMax)
        {
            try
            {
                if (ReaderDefinition != null && ReaderDefinition.TemperatureDef != null)
                {
                    string sResult;
                    if (ReaderDefinition.TemperatureDef.TemperatureTargetDef.TryGetValue(TEMPERATURE.Plate, out sResult) == true)
                    {
                        NumRange oRange = new NumRange(sResult);
                        nMin = oRange.Min;
                        nMax = oRange.Max;
                    }
                    else
                    {
                        nMin = 0;
                        nMax = 420; // Default max 42.0°C
                    }
                }
                else
                {
                    nMin = 0;
                    nMax = 420; // Default max 42.0°C
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTemperatureRange: {ex.Message}");
                nMin = 0;
                nMax = 420; // Default max 42.0°C
            }
        }
    }

    /// <summary>
    /// Represents a discovered Tecan device
    /// </summary>
    public class TecanDevice
    {
        /// <summary>
        /// The name of the device (e.g., "Infinite 200Pro")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of the device (e.g., "READER")
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The serial number of the device (e.g., "1812003347")
        /// </summary>
        public string Serial { get; set; }

        /// <summary>
        /// The port of the device (e.g., "USB/USB0")
        /// </summary>
        public string Port { get; set; }

        /// <summary>
        /// The driver information for the device
        /// </summary>
        public string Driver { get; set; }

        /// <summary>
        /// The connection string for the device
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Creates a string representation of the device
        /// </summary>
        public override string ToString()
        {
            return $"{Name} ({Type}) - {Serial} on {Port}";
        }
    }

    /// <summary>
    /// Represents a measurement result
    /// </summary>
    public class MeasurementResult
    {
        /// <summary>
        /// The unique identifier for this measurement
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The type of measurement (Absorbance, Fluorescence, Luminescence)
        /// </summary>
        public string MeasurementType { get; set; }

        /// <summary>
        /// The plate type used for the measurement
        /// </summary>
        public string PlateType { get; set; }

        /// <summary>
        /// The well range used for the measurement (e.g., "A1:H12")
        /// </summary>
        public string WellRange { get; set; }

        /// <summary>
        /// The path to the result file
        /// </summary>
        public string ResultFilePath { get; set; }

        /// <summary>
        /// The timestamp when the measurement was performed
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Additional parameters specific to the measurement type
        /// </summary>
        public Dictionary<string, string> Parameters { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public MeasurementResult()
        {
            Id = Guid.NewGuid().ToString();
            Timestamp = DateTime.Now;
            Parameters = new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Class for handling measurement operations
    /// </summary>
    public class MeasurementOperations
    {
        private TecanControl _tecan;
        private string _outputFolder;
        private string _debugFolder;
        private List<MeasurementResult> _measurementResults = new List<MeasurementResult>();
        private ResultOutput _resultOutput = null;
        private Guid _currentMeasurementGuid;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="tecan">The TecanControl instance</param>
        public MeasurementOperations(TecanControl tecan)
        {
            _tecan = tecan;

            // Create output folder
            _outputFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "TecanOpcUa",
                "Measurements");

            if (!Directory.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
            }

            // Create debug folder
            _debugFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "TecanOpcUa",
                "Debug");

            if (!Directory.Exists(_debugFolder))
            {
                Directory.CreateDirectory(_debugFolder);
            }
        }

        /// <summary>
        /// Gets the list of measurement results
        /// </summary>
        public List<MeasurementResult> MeasurementResults
        {
            get { return _measurementResults; }
        }

        /// <summary>
        /// Performs an absorbance measurement
        /// </summary>
        /// <param name="plateType">The plate type to use</param>
        /// <param name="wellRange">The well range to measure (e.g., "A1:H12")</param>
        /// <param name="wavelength">The wavelength in nm</param>
        /// <param name="numberOfFlashes">The number of flashes</param>
        /// <param name="settleTime">The settle time in ms</param>
        /// <returns>The measurement result object</returns>
        public MeasurementResult PerformAbsorbanceMeasurement(
            string plateType,
            string wellRange,
            int wavelength,
            int numberOfFlashes,
            int settleTime)
        {
            if (!_tecan.IsConnected())
                throw new InvalidOperationException("Not connected to a Tecan device");

            if (_tecan._helper == null || !_tecan._helper.HasAbsorbanceFixed())
                throw new InvalidOperationException("Device does not support absorbance measurements");

            try
            {
                Console.WriteLine($"Performing absorbance measurement with parameters: plateType={plateType}, wellRange={wellRange}, wavelength={wavelength}, numberOfFlashes={numberOfFlashes}, settleTime={settleTime}");

                // Create a measurement result object
                MeasurementResult result = new MeasurementResult
                {
                    MeasurementType = "Absorbance",
                    PlateType = plateType,
                    WellRange = wellRange,
                    Parameters = new Dictionary<string, string>
                    {
                        { "Wavelength", wavelength.ToString() },
                        { "NumberOfFlashes", numberOfFlashes.ToString() },
                        { "SettleTime", settleTime.ToString() }
                    }
                };

                if (_tecan._measurementServer != null && _tecan._measurementServer.ConnectedReader != null)
                {
                    try
                    {
                        // Generate the measurement script
                        TecanFile measurementScript = GenerateAbsorbanceScript(
                            plateType,
                            wellRange,
                            wavelength,
                            numberOfFlashes,
                            settleTime,
                            "Absorbance_" + result.Id);

                        // Ensure plate is in
                        _tecan.MovePlateIn();

                        // Create a GUID for this measurement run
                        _currentMeasurementGuid = Guid.NewGuid();

                        // Set script to measurement server
                        _tecan._measurementServer.ActionsAsObjects = measurementScript;

                        // Setup the output mechanism (critical for measurement execution)
                        SetupOutput(_tecan._measurementServer, measurementScript);

                        // Configure in-process messaging (critical for measurement execution)
                        _tecan._measurementServer.UseInprocMessagingService = true;

                        // Start the measurement
                        _tecan._measurementServer.NewRunState(_currentMeasurementGuid);
                        _tecan._measurementServer.Run(_currentMeasurementGuid);

                        // Wait for measurement to complete with proper message processing
                        // This is a blocking call that waits for the measurement to finish
                        // or until timeout is reached
                        WaitForMeasurementCompletion(30000);  // 30 second timeout

                        // Stop the output mechanism
                        StopOutput();

                        // Generate a result file path
                        string resultFilePath = Path.Combine(
                            _outputFolder,
                            $"Absorbance_{DateTime.Now:yyyyMMdd_HHmmss}_{result.Id}.xml");

                        result.ResultFilePath = resultFilePath;

                        // Add to results list
                        _measurementResults.Add(result);

                        Console.WriteLine($"Absorbance measurement completed. Result ID: {result.Id}");
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during measurement execution: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        throw;
                    }
                }
                else
                {
                    throw new InvalidOperationException("Measurement server not initialized");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error performing absorbance measurement: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Performs a fluorescence measurement
        /// </summary>
        /// <param name="plateType">The plate type to use</param>
        /// <param name="wellRange">The well range to measure (e.g., "A1:H12")</param>
        /// <param name="excitationWavelength">The excitation wavelength in nm</param>
        /// <param name="emissionWavelength">The emission wavelength in nm</param>
        /// <param name="gain">The gain value</param>
        /// <param name="numberOfFlashes">The number of flashes</param>
        /// <param name="integrationTime">The integration time in μs</param>
        /// <param name="settleTime">The settle time in ms</param>
        /// <param name="readingMode">The reading mode (Top or Bottom)</param>
        /// <returns>The measurement result object</returns>
        public MeasurementResult PerformFluorescenceMeasurement(
            string plateType,
            string wellRange,
            int excitationWavelength,
            int emissionWavelength,
            int gain,
            int numberOfFlashes,
            int integrationTime,
            int settleTime,
            string readingMode)
        {
            if (!_tecan.IsConnected())
                throw new InvalidOperationException("Not connected to a Tecan device");

            if (_tecan._helper == null || !_tecan._helper.HasFluoresenceFixed())
                throw new InvalidOperationException("Device does not support fluorescence measurements");

            try
            {
                Console.WriteLine($"Performing fluorescence measurement with parameters: plateType={plateType}, wellRange={wellRange}, " +
                    $"excitationWavelength={excitationWavelength}, emissionWavelength={emissionWavelength}, gain={gain}, " +
                    $"numberOfFlashes={numberOfFlashes}, integrationTime={integrationTime}, settleTime={settleTime}, readingMode={readingMode}");

                // Create a measurement result object
                MeasurementResult result = new MeasurementResult
                {
                    MeasurementType = "Fluorescence",
                    PlateType = plateType,
                    WellRange = wellRange,
                    Parameters = new Dictionary<string, string>
                    {
                        { "ExcitationWavelength", excitationWavelength.ToString() },
                        { "EmissionWavelength", emissionWavelength.ToString() },
                        { "Gain", gain.ToString() },
                        { "NumberOfFlashes", numberOfFlashes.ToString() },
                        { "IntegrationTime", integrationTime.ToString() },
                        { "SettleTime", settleTime.ToString() },
                        { "ReadingMode", readingMode }
                    }
                };

                if (_tecan._measurementServer != null && _tecan._measurementServer.ConnectedReader != null)
                {
                    try
                    {
                        // Generate the measurement script
                        TecanFile measurementScript = GenerateFluorescenceScript(
                            plateType,
                            wellRange,
                            excitationWavelength,
                            emissionWavelength,
                            gain,
                            numberOfFlashes,
                            integrationTime,
                            settleTime,
                            readingMode,
                            "Fluorescence_" + result.Id);

                        // Ensure plate is in
                        _tecan.MovePlateIn();

                        // Create a GUID for this measurement run
                        _currentMeasurementGuid = Guid.NewGuid();

                        // Set script to measurement server
                        _tecan._measurementServer.ActionsAsObjects = measurementScript;

                        // Setup the output mechanism (critical for measurement execution)
                        SetupOutput(_tecan._measurementServer, measurementScript);

                        // Configure in-process messaging (critical for measurement execution)
                        _tecan._measurementServer.UseInprocMessagingService = true;

                        // Start the measurement
                        _tecan._measurementServer.NewRunState(_currentMeasurementGuid);
                        _tecan._measurementServer.Run(_currentMeasurementGuid);

                        // Wait for measurement to complete with proper message processing
                        Wait(10000);  // 10 second timeout

                        // Stop the output mechanism
                        StopOutput();

                        // Generate a result file path
                        string resultFilePath = Path.Combine(
                            _outputFolder,
                            $"Fluorescence_{DateTime.Now:yyyyMMdd_HHmmss}_{result.Id}.xml");

                        result.ResultFilePath = resultFilePath;

                        // Add to results list
                        _measurementResults.Add(result);

                        Console.WriteLine($"Fluorescence measurement completed. Result ID: {result.Id}");
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during measurement execution: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        throw;
                    }
                }
                else
                {
                    throw new InvalidOperationException("Measurement server not initialized");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error performing fluorescence measurement: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Performs a luminescence measurement
        /// </summary>
        /// <param name="plateType">The plate type to use</param>
        /// <param name="wellRange">The well range to measure (e.g., "A1:H12")</param>
        /// <param name="integrationTime">The integration time in ms</param>
        /// <param name="settleTime">The settle time in ms</param>
        /// <param name="attenuation">The attenuation filter to use</param>
        /// <returns>The measurement result object</returns>
        public MeasurementResult PerformLuminescenceMeasurement(
            string plateType,
            string wellRange,
            int integrationTime,
            int settleTime,
            string attenuation)
        {
            if (!_tecan.IsConnected())
                throw new InvalidOperationException("Not connected to a Tecan device");

            if (_tecan._helper == null || !_tecan._helper.HasLuminescenceFixed())
                throw new InvalidOperationException("Device does not support luminescence measurements");

            try
            {
                Console.WriteLine($"Performing luminescence measurement with parameters: plateType={plateType}, wellRange={wellRange}, " +
                    $"integrationTime={integrationTime}, settleTime={settleTime}, attenuation={attenuation}");

                // Create a measurement result object
                MeasurementResult result = new MeasurementResult
                {
                    MeasurementType = "Luminescence",
                    PlateType = plateType,
                    WellRange = wellRange,
                    Parameters = new Dictionary<string, string>
                    {
                        { "IntegrationTime", integrationTime.ToString() },
                        { "SettleTime", settleTime.ToString() },
                        { "Attenuation", attenuation }
                    }
                };

                if (_tecan._measurementServer != null && _tecan._measurementServer.ConnectedReader != null)
                {
                    try
                    {
                        // Generate the measurement script
                        TecanFile measurementScript = GenerateLuminescenceScript(
                            plateType,
                            wellRange,
                            integrationTime,
                            settleTime,
                            attenuation,
                            "Luminescence_" + result.Id);

                        // Ensure plate is in
                        _tecan.MovePlateIn();

                        // Create a GUID for this measurement run
                        _currentMeasurementGuid = Guid.NewGuid();

                        // Set script to measurement server
                        _tecan._measurementServer.ActionsAsObjects = measurementScript;

                        // Setup the output mechanism (critical for measurement execution)
                        SetupOutput(_tecan._measurementServer, measurementScript);

                        // Configure in-process messaging (critical for measurement execution)
                        _tecan._measurementServer.UseInprocMessagingService = true;

                        // Start the measurement
                        _tecan._measurementServer.NewRunState(_currentMeasurementGuid);
                        _tecan._measurementServer.Run(_currentMeasurementGuid);

                        // Wait for measurement to complete with proper message processing
                        Wait(10000);  // 10 second timeout

                        // Stop the output mechanism
                        StopOutput();

                        // Generate a result file path
                        string resultFilePath = Path.Combine(
                            _outputFolder,
                            $"Luminescence_{DateTime.Now:yyyyMMdd_HHmmss}_{result.Id}.xml");

                        result.ResultFilePath = resultFilePath;

                        // Add to results list
                        _measurementResults.Add(result);

                        Console.WriteLine($"Luminescence measurement completed. Result ID: {result.Id}");
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during measurement execution: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        throw;
                    }
                }
                else
                {
                    throw new InvalidOperationException("Measurement server not initialized");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error performing luminescence measurement: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Generates a script for absorbance measurement
        /// </summary>
        private TecanFile GenerateAbsorbanceScript(
            string plateName,
            string wellRange,
            int wavelength,
            int numberOfReads,
            int settleTime,
            string labelName)
        {
            try
            {
                Console.WriteLine($"Generating absorbance script with parameters: plateType={plateName}, wellRange={wellRange}, wavelength={wavelength}, numberOfReads={numberOfReads}, settleTime={settleTime}");

                // Create a new TecanFile with proper structure
                int nID = 0;
                TecanFile oFile = CreateTecanFile();
                TecanMeasurement oMeasurement = CreateTecanMeasurement(++nID);
                MeasurementManualCycle oCycle = CreateManualCycle(++nID);

                // Set plate
                CyclePlate oPlate = CreatePlate(plateName, ++nID);

                // Set well range
                PlateRange oRange = CreateRange(wellRange, ++nID);

                // Create absorbance measurement with proper settings
                MeasurementAbsorbance oAbsMeas = new MeasurementAbsorbance();
                oAbsMeas.Name = "ABS";
                oAbsMeas.ID = ++nID;

                // Create well
                Well oWell = CreateWell(++nID);

                // Create measurement reading with proper settings
                MeasurementReading oMeasurementReading = new MeasurementReading();
                oMeasurementReading.ID = ++nID;
                oMeasurementReading.BeamDiameter = 700; // Set to 700 to match sample app
                oMeasurementReading.BeamGridType = BeamGridType.Single;
                oMeasurementReading.BeamGridSize = 1;
                oMeasurementReading.BeamEdgeDistance = "auto";

                // Create reading label with proper parameters
                ReadingLabel oReadingLabel = new ReadingLabel();
                oReadingLabel.ID = ++nID;
                oReadingLabel.Name = labelName;
                oReadingLabel.ScanType = ScanMode.ScanFixed;

                // Create settings with proper values
                ReadingSettings oSettings = new ReadingSettings();
                oSettings.Number = numberOfReads;
                oSettings.Rate = 25000; // Default rate from sample app

                // Create timing with proper values
                ReadingTime oReadingTime = new ReadingTime();
                oReadingTime.ReadDelay = settleTime;
                oReadingTime.Flash = 0;
                oReadingTime.Dark = 0;
                oReadingTime.ExcitationTime = 0;
                oReadingTime.IntegrationTime = 0;
                oReadingTime.LagTime = 0;

                // Create filter with proper values and constants
                ReadingFilter oFilter = new ReadingFilter();
                oFilter.Wavelength = (wavelength * 10).ToString(); // Multiply by 10 to match format
                oFilter.Bandwidth = "50";      // Default bandwidth (5.0 nm)
                oFilter.Usage = "Absorbance";  // Use string constant to match sample app
                oFilter.Type = "Ex";           // Use string constant to match sample app

                // Assemble the objects with proper hierarchy
                oReadingLabel.Timing = oReadingTime;
                oReadingLabel.Settings = oSettings;
                oReadingLabel.ExFilter = oFilter;
                oMeasurementReading.Actions.Add(oReadingLabel);
                oWell.Actions.Add(oMeasurementReading);
                oAbsMeas.Actions.Add(oWell);
                oRange.Actions.Add(oAbsMeas);
                oPlate.Actions.Add(oRange);
                oCycle.Actions.Add(oPlate);
                oMeasurement.Actions.Add(oCycle);
                oFile.DocumentContent = oMeasurement;

                // For debugging - save the script to a file
                try
                {
                    string debugPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "TecanOpcUa",
                        "Debug");

                    if (!Directory.Exists(debugPath))
                    {
                        Directory.CreateDirectory(debugPath);
                    }

                    string scriptPath = Path.Combine(debugPath, $"AbsScript_{DateTime.Now:yyyyMMdd_HHmmss}.xml");
                    Tecan.At.Common.FileManagement.FileHandling.Save(oFile, scriptPath);
                    Console.WriteLine($"Saved debug script to: {scriptPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not save debug script: {ex.Message}");
                }

                Console.WriteLine("Successfully generated absorbance script");
                return oFile;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating absorbance script: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Generates a script for fluorescence measurement
        /// </summary>
        private TecanFile GenerateFluorescenceScript(
            string plateName,
            string wellRange,
            int excitationWavelength,
            int emissionWavelength,
            int gain,
            int numberOfReads,
            int integrationTime,
            int settleTime,
            string readingMode,
            string labelName)
        {
            try
            {
                Console.WriteLine($"Generating fluorescence script with parameters: plateType={plateName}, wellRange={wellRange}, " +
                    $"excitationWavelength={excitationWavelength}, emissionWavelength={emissionWavelength}, gain={gain}, " +
                    $"numberOfReads={numberOfReads}, integrationTime={integrationTime}, settleTime={settleTime}, readingMode={readingMode}");

                // Create a new TecanFile with proper structure
                int nID = 0;
                TecanFile oFile = CreateTecanFile();
                TecanMeasurement oMeasurement = CreateTecanMeasurement(++nID);
                MeasurementManualCycle oCycle = CreateManualCycle(++nID);

                // Set plate
                CyclePlate oPlate = CreatePlate(plateName, ++nID);

                // Set well range
                PlateRange oRange = CreateRange(wellRange, ++nID);

                // Create fluorescence measurement
                MeasurementFluoInt oFluoMeas = new MeasurementFluoInt();
                oFluoMeas.Name = "FLUO";
                oFluoMeas.ID = ++nID;

                // Convert string readingMode to ReadingMode enum
                if (readingMode.Equals("Top", StringComparison.OrdinalIgnoreCase))
                {
                    oFluoMeas.ReadingMode = Tecan.At.Common.DocumentManagement.ReadingMode.Top;
                }
                else if (readingMode.Equals("Bottom", StringComparison.OrdinalIgnoreCase))
                {
                    oFluoMeas.ReadingMode = Tecan.At.Common.DocumentManagement.ReadingMode.Bottom;
                }
                else
                {
                    // Default to Top if not recognized
                    oFluoMeas.ReadingMode = Tecan.At.Common.DocumentManagement.ReadingMode.Top;
                    Console.WriteLine($"Warning: Unrecognized reading mode '{readingMode}', defaulting to Top");
                }

                // Create well
                Well oWell = CreateWell(++nID);

                // Create measurement reading
                MeasurementReading oMeasurementReading = new MeasurementReading();
                oMeasurementReading.ID = ++nID;
                oMeasurementReading.BeamDiameter = 700; // Default beam diameter
                oMeasurementReading.BeamGridType = BeamGridType.Single;
                oMeasurementReading.BeamGridSize = 1;
                oMeasurementReading.BeamEdgeDistance = "auto";

                // Create reading label
                ReadingLabel oReadingLabel = new ReadingLabel();
                oReadingLabel.ID = ++nID;
                oReadingLabel.Name = labelName;
                oReadingLabel.ScanType = ScanMode.ScanFixed;

                // Create settings
                ReadingSettings oSettings = new ReadingSettings();
                oSettings.Number = numberOfReads;
                oSettings.Rate = 25000; // Default rate

                // Create gain
                ReadingGain oGain = new ReadingGain();
                oGain.Mode = GainMode.Manual;
                oGain.Gain = gain;

                // Create timing
                ReadingTime oReadingTime = new ReadingTime();
                oReadingTime.ReadDelay = settleTime;
                oReadingTime.IntegrationTime = integrationTime;
                oReadingTime.LagTime = 0;

                // Create excitation filter
                ReadingFilter oExFilter = new ReadingFilter();
                oExFilter.ID = ++nID;
                oExFilter.Wavelength = (excitationWavelength * 10).ToString();
                oExFilter.Bandwidth = "90"; // Default bandwidth
                oExFilter.Usage = "Fluorescence"; // Use string constant to match sample app
                oExFilter.Type = "Ex"; // Use string constant to match sample app
                oExFilter.Attenuation = "0";

                // Create emission filter
                ReadingFilter oEmFilter = new ReadingFilter();
                oEmFilter.ID = ++nID;
                oEmFilter.Wavelength = (emissionWavelength * 10).ToString();
                oEmFilter.Bandwidth = "90"; // Default bandwidth
                oEmFilter.Usage = "Fluorescence"; // Use string constant to match sample app
                oEmFilter.Type = "Em"; // Use string constant to match sample app
                oEmFilter.Attenuation = "0";

                // Assemble the objects
                oReadingLabel.Settings = oSettings;
                oReadingLabel.Gain = oGain;
                oReadingLabel.Timing = oReadingTime;
                oReadingLabel.ExFilter = oExFilter;
                oReadingLabel.EmFilter = oEmFilter;
                oMeasurementReading.Actions.Add(oReadingLabel);
                oWell.Actions.Add(oMeasurementReading);
                oFluoMeas.Actions.Add(oWell);
                oRange.Actions.Add(oFluoMeas);
                oPlate.Actions.Add(oRange);
                oCycle.Actions.Add(oPlate);
                oMeasurement.Actions.Add(oCycle);
                oFile.DocumentContent = oMeasurement;

                // For debugging - save the script to a file
                try
                {
                    string debugPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "TecanOpcUa",
                        "Debug");

                    if (!Directory.Exists(debugPath))
                    {
                        Directory.CreateDirectory(debugPath);
                    }

                    string scriptPath = Path.Combine(debugPath, $"FluoScript_{DateTime.Now:yyyyMMdd_HHmmss}.xml");
                    Tecan.At.Common.FileManagement.FileHandling.Save(oFile, scriptPath);
                    Console.WriteLine($"Saved debug script to: {scriptPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not save debug script: {ex.Message}");
                }

                Console.WriteLine("Successfully generated fluorescence script");
                return oFile;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating fluorescence script: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Generates a script for luminescence measurement
        /// </summary>
        private TecanFile GenerateLuminescenceScript(
            string plateName,
            string wellRange,
            int integrationTime,
            int settleTime,
            string attenuation,
            string labelName)
        {
            try
            {
                Console.WriteLine($"Generating luminescence script with parameters: plateType={plateName}, wellRange={wellRange}, " +
                    $"integrationTime={integrationTime}, settleTime={settleTime}, attenuation={attenuation}");

                // Create a new TecanFile with proper structure
                int nID = 0;
                TecanFile oFile = CreateTecanFile();
                TecanMeasurement oMeasurement = CreateTecanMeasurement(++nID);
                MeasurementManualCycle oCycle = CreateManualCycle(++nID);

                // Set plate
                CyclePlate oPlate = CreatePlate(plateName, ++nID);

                // Set well range
                PlateRange oRange = CreateRange(wellRange, ++nID);

                // Create luminescence measurement
                MeasurementLuminescence oLumiMeas = new MeasurementLuminescence();
                oLumiMeas.Name = "LUMI";
                oLumiMeas.ID = ++nID;

                // Create well
                Well oWell = CreateWell(++nID);

                // Create measurement reading
                MeasurementReading oMeasurementReading = new MeasurementReading();
                oMeasurementReading.ID = ++nID;

                // Create reading label
                ReadingLabel oReadingLabel = new ReadingLabel();
                oReadingLabel.ID = ++nID;
                oReadingLabel.Name = labelName;
                oReadingLabel.ScanType = ScanMode.ScanFixed;

                // Create filter for attenuation
                ReadingFilter oFilter = new ReadingFilter();
                oFilter.ID = ++nID;
                oFilter.Wavelength = "0";
                oFilter.Usage = "Empty"; // Use string constant to match sample app
                oFilter.Bandwidth = "0";
                oFilter.Attenuation = "0";
                oFilter.Type = "Em"; // Use string constant to match sample app

                // Create timing
                ReadingTime oReadingTime = new ReadingTime();
                oReadingTime.IntegrationTime = integrationTime * 1000; // Convert to μs
                oReadingTime.ReadDelay = settleTime * 1000; // Convert to μs

                // Assemble the objects
                oReadingLabel.EmFilter = oFilter;
                oReadingLabel.Timing = oReadingTime;
                oMeasurementReading.Actions.Add(oReadingLabel);
                oWell.Actions.Add(oMeasurementReading);
                oLumiMeas.Actions.Add(oWell);
                oRange.Actions.Add(oLumiMeas);
                oPlate.Actions.Add(oRange);
                oCycle.Actions.Add(oPlate);
                oMeasurement.Actions.Add(oCycle);
                oFile.DocumentContent = oMeasurement;

                // For debugging - save the script to a file
                try
                {
                    string debugPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "TecanOpcUa",
                        "Debug");

                    if (!Directory.Exists(debugPath))
                    {
                        Directory.CreateDirectory(debugPath);
                    }

                    string scriptPath = Path.Combine(debugPath, $"LumiScript_{DateTime.Now:yyyyMMdd_HHmmss}.xml");
                    Tecan.At.Common.FileManagement.FileHandling.Save(oFile, scriptPath);
                    Console.WriteLine($"Saved debug script to: {scriptPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not save debug script: {ex.Message}");
                }

                Console.WriteLine("Successfully generated luminescence script");
                return oFile;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating luminescence script: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Creates a TecanFile
        /// </summary>
        private TecanFile CreateTecanFile()
        {
            TecanFile oFile = new TecanFile();
            oFile.FileFormat = "Tecan.At.Measurement";
            oFile.FileVersion = "1.0";

            oFile.TecanFileInfo = new Tecan.At.Common.DocumentManagement.FileInfo();
            oFile.TecanFileInfo.InfoCreatedFrom = "TecanOpcUa";
            oFile.TecanFileInfo.InfoCreatedAt = DateTime.Now;
            oFile.TecanFileInfo.InfoCreatedWith = "TecanOpcUa";
            oFile.TecanFileInfo.InfoDescription = "Measurement created via OPC UA";
            oFile.TecanFileInfo.InfoType = "";
            oFile.TecanFileInfo.InfoVersion = "1.0";

            return oFile;
        }

        /// <summary>
        /// Creates a TecanMeasurement
        /// </summary>
        private TecanMeasurement CreateTecanMeasurement(int nID)
        {
            TecanMeasurement oMeasurement = new TecanMeasurement();
            oMeasurement.ID = nID;
            oMeasurement.MeasurementClass = "Measurement";

            return oMeasurement;
        }

        /// <summary>
        /// Creates a MeasurementManualCycle
        /// </summary>
        private MeasurementManualCycle CreateManualCycle(int nID)
        {
            MeasurementManualCycle oCycle = new MeasurementManualCycle();
            oCycle.CycleType = CycleType.Standard;
            oCycle.Number = 1;
            oCycle.ID = nID;

            return oCycle;
        }

        /// <summary>
        /// Creates a CyclePlate
        /// </summary>
        private CyclePlate CreatePlate(string plateName, int nID)
        {
            CyclePlate oPlate = new CyclePlate();
            oPlate.PlateName = plateName;
            oPlate.ID = nID;

            return oPlate;
        }

        /// <summary>
        /// Creates a PlateRange
        /// </summary>
        private PlateRange CreateRange(string wellRange, int nID)
        {
            PlateRange oRange = new PlateRange();
            oRange.Range = wellRange;
            oRange.Auto = true;
            oRange.ID = nID;

            return oRange;
        }

        /// <summary>
        /// Creates a Well
        /// </summary>
        private Well CreateWell(int nID)
        {
            Well oWell = new Well();
            oWell.ID = nID;

            return oWell;
        }

        /// <summary>
        /// Sets up the output mechanism for measurement
        /// </summary>
        private void SetupOutput(MeasurementServer server, TecanFile tecanFile)
        {
            try
            {
                Console.WriteLine("Setting up output mechanism for measurement...");

                // Get all versions of software components
                Dictionary<string, string> oAssemblies = new Dictionary<string, string>();

                // Get instrument versions (to provide the output mechanism with some additional information about the instrument, e.g. Version information about various modules)
                TecanFile oInstrumentDefinitions = server.ConnectedReader.Information.GetInstrumentDefinitions();
                TecanReaderDefinition oDefinitions = (TecanReaderDefinition)oInstrumentDefinitions.DocumentContent;

                // First convert the TecanFile to XML string and back to ensure proper XML structure
                // This is CRITICAL - the sample application does this conversion before passing to ResultOutput
                XmlSupport objXML = new XmlSupport();
                XmlNode objNode = tecanFile.GetXmlNode(objXML);
                objXML.AddXmlNode(objNode);
                string xmlString = objXML.XmlDocumentAsString();

                // Now convert back to TecanFile object
                TecanFile processedTecanFile = FileHandling.LoadXml(xmlString) as TecanFile;
                if (processedTecanFile == null)
                {
                    throw new InvalidOperationException("Failed to convert TecanFile to XML and back");
                }

                // Create an output "worker" with the processed TecanFile
                _resultOutput = new ResultOutput(processedTecanFile, oAssemblies, oDefinitions, _currentMeasurementGuid);

                string sDeviceName = server.ConnectedReader.Information.GetProductName();
                string sFileName = sDeviceName + "_" + DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss", DateTimeFormatInfo.InvariantInfo) + ".xml";

                // Create XML output (simplified - no Excel output for server)
                string xmlFilePath = Path.Combine(_outputFolder, sFileName);
                XmlOutput xmlOutput = new XmlOutput(xmlFilePath);
                _resultOutput.MeasurementDataOutput = xmlOutput;

                _resultOutput.DeviceName = sDeviceName;

                // Simulation mode?
                _resultOutput.Simulation = IsSimulated(server);

                // Exception listener (must not be null!)
                _resultOutput.AddExceptionListener(new Tecan.At.Common.ExceptionListener(OutputExceptionListener));

                bool bOutputOK = _resultOutput.Init();
                if (!bOutputOK)
                {
                    throw new InvalidOperationException("Unable to initialize output mechanism");
                }

                // Feed the progress mechanism with data during measurement
                _resultOutput.UpdateDataDelegate = new QueueProcessor.UpdateData(MeasurementProgress);

                // Start listening - EXACTLY like sample app
                server.UseInprocMessagingService = true;
                _resultOutput.StartListening(server.MessagingService);

                Console.WriteLine("Output mechanism setup completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting up output: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Checks if the server is in simulation mode
        /// </summary>
        private bool IsSimulated(MeasurementServer server)
        {
            try
            {
                if (server != null && server.ConnectedReader != null)
                {
                    string instrumentName = server.ConnectedReader.Information.GetInstrumentName();
                    return instrumentName.Contains("SIM");
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking simulation mode: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stops the output mechanism
        /// </summary>
        private void StopOutput()
        {
            try
            {
                if (_resultOutput != null)
                {
                    Console.WriteLine("Stopping output mechanism...");

                    // Stop listening for measurement data
                    _resultOutput.StopListening(false);

                    // Remove exception listeners
                    _resultOutput.RemoveAllExceptionListeners();

                    Console.WriteLine("Output mechanism stopped successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping output: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Exception listener for output errors
        /// </summary>
        private void OutputExceptionListener(object sender, Exception e)
        {
            Console.WriteLine($"Output error: {e.Message}");
            Console.WriteLine($"Stack trace: {e.StackTrace}");
        }

        /// <summary>
        /// Waits for the specified timeout while processing messages
        /// This is an adaptation of the Wait method from Form1.cs but enhanced for server-side use
        /// </summary>
        /// <param name="timeoutMillis">The timeout in milliseconds</param>
        private void Wait(int timeoutMillis)
        {
            Console.WriteLine($"Waiting for measurement to complete (timeout: {timeoutMillis}ms)...");

            // Create a TimeSpan for the timeout - exactly as in Form1.cs
            TimeSpan timeSpan = new TimeSpan(0, 0, 0, 0, timeoutMillis);
            DateTime startTime = DateTime.Now;

            // Setup progress monitoring if available - this is additional to Form1.cs
            // but necessary for server-side operation
            if (_resultOutput != null)
            {
                // Add progress monitoring delegate
                _resultOutput.UpdateDataDelegate = new QueueProcessor.UpdateData(MeasurementProgress);
            }

            // Wait loop - exactly as in Form1.cs
            while (true)
            {
                // Check if we've exceeded the timeout - exactly as in Form1.cs
                DateTime currentTime = DateTime.Now;
                if (((currentTime - startTime) > timeSpan) && timeoutMillis > 0)
                {
                    Console.WriteLine("Wait timeout reached");
                    break;
                }

                // Process any pending messages - equivalent to Application.DoEvents() in Form1.cs
                // but adapted for server-side use since we can't use Application.DoEvents()
                Thread.Sleep(10);
            }

            // Remove progress monitoring - additional to Form1.cs but necessary for cleanup
            if (_resultOutput != null)
            {
                _resultOutput.UpdateDataDelegate = null;
            }

            Console.WriteLine("Wait completed");
        }

        /// <summary>
        /// Waits for measurement completion with improved detection of actual completion
        /// </summary>
        /// <param name="timeoutMillis">Maximum time to wait in milliseconds</param>
        private void WaitForMeasurementCompletion(int timeoutMillis)
        {
            Console.WriteLine($"Waiting for measurement to complete (timeout: {timeoutMillis}ms)...");

            // Create a TimeSpan for the timeout
            TimeSpan timeSpan = new TimeSpan(0, 0, 0, 0, timeoutMillis);
            DateTime startTime = DateTime.Now;

            // Track when we last saw progress
            DateTime lastProgressTime = DateTime.Now;
            bool hasSeenData = false;
            bool measurementCompleted = false;
            int dataPointsReceived = 0;
            string lastActionTag = string.Empty;

            // Setup progress monitoring
            if (_resultOutput != null)
            {
                // Add progress monitoring delegate that will update lastProgressTime
                _resultOutput.UpdateDataDelegate = new QueueProcessor.UpdateData(delegate (Tecan.At.Common.Results.ProgressArguments args) {
                    // Update the last progress time
                    lastProgressTime = DateTime.Now;

                    // Check if we're seeing actual measurement data
                    if (args != null)
                    {
                        if (args.ChangeReason == Tecan.At.Common.Results.ProgressArguments.ChangeType.Data)
                        {
                            hasSeenData = true;
                            dataPointsReceived++;
                            Console.WriteLine($"Measurement data received: {args.Value} at well {(char)('A' + args.Row - 1)}{args.Column}");
                        }

                        // Log progress information
                        if (!string.IsNullOrEmpty(args.ActionTag))
                        {
                            lastActionTag = args.ActionTag;
                            Console.WriteLine($"Measurement action: {args.ActionTag}");

                            // Some action tags might indicate completion
                            if (args.ActionTag.Contains("Complete") ||
                                args.ActionTag.Contains("Finished") ||
                                args.ActionTag.Contains("End"))
                            {
                                Console.WriteLine("Measurement completion action detected");
                                measurementCompleted = true;
                            }
                        }

                        // Check for action change which might indicate completion
                        if (args.ChangeReason == Tecan.At.Common.Results.ProgressArguments.ChangeType.Action)
                        {
                            Console.WriteLine($"Action change detected: {args.ActionTag}");

                            // If we've already seen data and now we're getting an action change,
                            // it might indicate we're moving to the next phase or completing
                            if (hasSeenData && dataPointsReceived > 0)
                            {
                                Console.WriteLine("Action change after data received - possible completion indicator");
                            }
                        }
                    }
                });
            }

            // Wait loop with improved completion detection
            while (true)
            {
                // Check if we've exceeded the timeout
                DateTime currentTime = DateTime.Now;
                if (((currentTime - startTime) > timeSpan) && timeoutMillis > 0)
                {
                    Console.WriteLine("Wait timeout reached");
                    break;
                }

                // Check if measurement is complete based on explicit completion flag
                if (measurementCompleted)
                {
                    Console.WriteLine("Measurement completed successfully (explicit completion)");
                    break;
                }

                // Check if we've seen data but no progress for a while (indicates completion)
                if (hasSeenData && (currentTime - lastProgressTime).TotalSeconds > 3)
                {
                    Console.WriteLine("Measurement appears complete (no progress for 3 seconds after receiving data)");
                    break;
                }

                // Check if we've received a significant amount of data points (indicates progress)
                if (dataPointsReceived > 10 && (currentTime - lastProgressTime).TotalSeconds > 2)
                {
                    Console.WriteLine($"Measurement likely complete (received {dataPointsReceived} data points with no activity for 2 seconds)");
                    break;
                }

                // Process any pending messages
                Thread.Sleep(10);
            }

            // Remove progress monitoring
            if (_resultOutput != null)
            {
                _resultOutput.UpdateDataDelegate = null;
            }

            Console.WriteLine("Wait for measurement completion finished");
        }

        // Track the last time we saw progress
        private DateTime _lastProgressTime = DateTime.MinValue;

        /// <summary>
        /// Callback for measurement progress updates
        /// </summary>
        /// <param name="args">Progress arguments</param>
        private void MeasurementProgress(Tecan.At.Common.Results.ProgressArguments args)
        {
            try
            {
                // Update the last progress time
                _lastProgressTime = DateTime.Now;

                // Log progress information
                if (args != null)
                {
                    // Log action tag if available
                    if (!string.IsNullOrEmpty(args.ActionTag))
                    {
                        Console.WriteLine($"Measurement action: {args.ActionTag}");
                    }

                    // Log label if available
                    if (!string.IsNullOrEmpty(args.Label))
                    {
                        Console.WriteLine($"Measurement label: {args.Label}");
                    }

                    // Calculate and log progress percentage if cycle information is available
                    if (args.NrOfCycles > 0)
                    {
                        int progressPercentage = (int)((args.CurrentCycle / (double)args.NrOfCycles) * 100);
                        Console.WriteLine($"Progress: {progressPercentage}% (Cycle {args.CurrentCycle} of {args.NrOfCycles})");
                    }

                    // Log well position if available
                    if (args.Row > 0 && args.Column > 0)
                    {
                        char rowChar = (char)('A' + args.Row - 1);
                        Console.WriteLine($"Current well: {rowChar}{args.Column}");
                    }

                    // Log measurement value if available
                    if (args.ChangeReason == Tecan.At.Common.Results.ProgressArguments.ChangeType.Data)
                    {
                        Console.WriteLine($"Measurement value: {args.Value}");
                    }

                    // Log change type
                    Console.WriteLine($"Change type: {args.ChangeReason}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in progress callback: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Tecan Control class for communicating with Tecan devices
    /// </summary>
    public class TecanControl
    {
        // Properties to store Tecan state
        private bool _isConnected = false;
        private bool _isPlateIn = false;
        private string _serialNumber = "";
        private double _temperature = 37.0;
        internal MeasurementServer _measurementServer = null;
        private InstrumentServer _instrumentServer = null;
        internal TecanHelper _helper = null;

        // List of discovered devices
        private List<TecanDevice> _discoveredDevices = new List<TecanDevice>();

        // Currently connected device
        private TecanDevice _connectedDevice = null;


        // Constructor
        public TecanControl()
        {
            // Initialize DocumentManagement.Reader - CRITICAL for XML deserialization
            ObjectFactory.AddEntryPoint(new Tecan.At.Common.DocumentManagement.Reader.DocumentEntryPoint());

            // Initialize any required resources
            InitializeAppSettings();
        }

        /// <summary>
        /// Initializes the AppSettings with a configuration file
        /// </summary>
        private void InitializeAppSettings()
        {
            try
            {
                // Create a configuration file path where settings can be stored
                string configPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                configPath = Path.Combine(configPath, "TecanOpcUa");

                if (!Directory.Exists(configPath))
                {
                    Directory.CreateDirectory(configPath);
                }

                string configFile = Path.Combine(configPath, "TecanOpcUa_Config.xml");

                if (!File.Exists(configFile))
                {
                    // Create an empty file
                    using (FileStream fs = File.Create(configFile))
                    {
                        // Just create the file
                    }
                }

                Console.WriteLine($"Using configuration file: {configFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing AppSettings: {ex.Message}");
            }
        }

        /// <summary>
        /// Discovers available Tecan devices using the InstrumentServer methods
        /// </summary>
        /// <returns>Number of devices discovered</returns>
        public int DiscoverDevices()
        {
            try
            {
                Console.WriteLine("Discovering Tecan devices...");

                // Clear previous discoveries
                _discoveredDevices.Clear();

                try
                {
                    // Create and configure AppSettings
                    AppSettings appSettings = new AppSettings();

                    // Create a configuration file path where settings can be stored
                    string configPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    configPath = Path.Combine(configPath, "TecanOpcUa");

                    if (!Directory.Exists(configPath))
                    {
                        Directory.CreateDirectory(configPath);
                    }

                    string configFile = Path.Combine(configPath, "TecanOpcUa_Config.xml");

                    // Load settings from file
                    appSettings.Load(configFile);

                    // Set important discovery settings
                    appSettings.SetVal("Connection", "ShowTecanInstruments", "TRUE");
                    appSettings.SetVal("Connection", "ShowSimulators", "FALSE");

                    // We'll work with InstrumentOnPort objects
                    List<InstrumentOnPort> instruments = null;

                    // Use InstrumentServer directly to avoid MeasurementServer dependency issues
                    Console.WriteLine("Using InstrumentServer for device discovery...");
                    InstrumentServer instrumentServer = new InstrumentServer();
                    instrumentServer.AppSettings = appSettings;

                    // Try multiple approaches to discover instruments
                    try
                    {
                        // Try with USB READER filter (no simulators)
                        Console.WriteLine("Trying GetInstruments with USB READER filter...");
                        string connectionString = "PORTTYPE=USB, TYPE=READER";
                        Console.WriteLine($"Using connection string: {connectionString}");
                        instruments = instrumentServer.GetInstruments(connectionString);
                        Console.WriteLine($"Found {(instruments != null ? instruments.Count : 0)} instruments using GetInstruments with USB READER filter");

                        // If that didn't work, try with a more generic filter
                        if (instruments == null || instruments.Count == 0)
                        {
                            Console.WriteLine("Trying GetInstruments with generic filter...");
                            connectionString = "PORTTYPE=USB";
                            Console.WriteLine($"Using connection string: {connectionString}");
                            instruments = instrumentServer.GetInstruments(connectionString);
                            Console.WriteLine($"Found {(instruments != null ? instruments.Count : 0)} instruments using GetInstruments with generic filter");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error using InstrumentServer.GetInstruments: {ex.Message}");
                        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    }

                    Console.WriteLine("Instrument discovery attempts completed");

                    if (instruments != null && instruments.Count > 0)
                    {
                        Console.WriteLine($"Found {instruments.Count} Tecan instruments");

                        // Convert InstrumentOnPort objects to our TecanDevice format
                        foreach (InstrumentOnPort instrument in instruments)
                        {
                            // Get the device information from the instrument
                            DeviceOnPort device = instrument.Device;

                            Console.WriteLine($"Processing instrument: {instrument.Instrument.InstrumentName} - {device.m_sInstrumentSerial}");

                            var tecanDevice = new TecanDevice
                            {
                                Name = instrument.Instrument.InstrumentName,
                                Type = device.m_sInstrumentType,
                                Serial = device.m_sInstrumentSerial,
                                Port = $"{device.m_sPortType}/{device.m_sPort}",
                                Driver = device.m_sDriver,
                                ConnectionString = BuildConnectionString(device)
                            };

                            _discoveredDevices.Add(tecanDevice);
                            Console.WriteLine($"  - {tecanDevice}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("No Tecan instruments found (instruments list is null or empty)");
                    }
                }
                catch (ArgumentNullException anex)
                {
                    Console.WriteLine($"ArgumentNullException in DetectDevices:");
                    Console.WriteLine($"Parameter: {anex.ParamName}");
                    Console.WriteLine($"Message: {anex.Message}");
                    Console.WriteLine($"Stack Trace: {anex.StackTrace}");
                    if (anex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {anex.InnerException}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error using InstrumentServer:");
                    Console.WriteLine($"Exception Type: {ex.GetType().FullName}");
                    Console.WriteLine($"Message: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {ex.InnerException}");
                    }
                }

                return _discoveredDevices.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error discovering devices: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Builds a connection string for a device
        /// </summary>
        private string BuildConnectionString(DeviceOnPort device)
        {
            return $"porttype={device.m_sPortType}, type={device.m_sInstrumentType.ToLower()}, option=default, name={device.m_sInstrumentName}";
        }

        /// <summary>
        /// Gets the list of discovered devices
        /// </summary>
        public List<TecanDevice> GetDiscoveredDevices()
        {
            return _discoveredDevices;
        }

        /// <summary>
        /// Gets the currently connected device
        /// </summary>
        public TecanDevice GetConnectedDevice()
        {
            return _connectedDevice;
        }

        /// <summary>
        /// Connect to a Tecan device by serial number
        /// </summary>
        /// <param name="deviceSerial">The serial number of the device to connect to</param>
        /// <returns>0 for success, -1 for failure, -2 for device not found</returns>
        public int ConnectBySerial(string deviceSerial)
        {
            try
            {
                if (_isConnected)
                {
                    Console.WriteLine("Already connected to a device. Disconnecting first...");
                    Disconnect();
                }

                Console.WriteLine($"Connecting to Tecan device with serial: {deviceSerial}");

                // Find the device in the discovered devices list
                var device = _discoveredDevices.FirstOrDefault(d => d.Serial == deviceSerial);

                if (device == null)
                {
                    Console.WriteLine($"Device with serial {deviceSerial} not found in discovered devices");
                    return -2; // Device not found
                }

                // Create and configure AppSettings
                AppSettings appSettings = new AppSettings();

                // Create a configuration file path where settings can be stored
                string configPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                configPath = Path.Combine(configPath, "TecanOpcUa");
                string configFile = Path.Combine(configPath, "TecanOpcUa_Config.xml");

                // Load settings from file
                appSettings.Load(configFile);

                // Set important connection settings
                appSettings.SetVal("Connection", "ShowTecanInstruments", "TRUE");
                appSettings.SetVal("Connection", "ShowSimulators", "FALSE");

                try
                {
                    // First, use InstrumentServer for device discovery only
                    Console.WriteLine("Using InstrumentServer for device discovery...");
                    InstrumentServer instrumentServer = new InstrumentServer();
                    instrumentServer.AppSettings = appSettings;

                    // Store the instrument server reference for later use (discovery only)
                    _instrumentServer = instrumentServer;

                    // Now use MeasurementServer for the actual connection
                    Console.WriteLine("Using MeasurementServer for device connection...");
                    _measurementServer = new MeasurementServer(appSettings, null);

                    // Use the correct connection string format with the device's serial (USB only)
                    string connectionString = $"porttype=USB, type={device.Type.ToLower()}, option=default, name={device.Name}, serial={device.Serial}";
                    Console.WriteLine($"Using connection string: {connectionString}");

                    // Connect to the device using MeasurementServer
                    bool connected = _measurementServer.Connect(InstrumentConnectionMethod.Manually, connectionString);

                    if (connected)
                    {
                        _isConnected = true;
                        _connectedDevice = device;
                        _serialNumber = device.Serial;

                        // Initialize the TecanHelper class for device capabilities
                        try
                        {
                            _helper = new TecanHelper();

                            // Set the reader definition and reader for the helper
                            TecanFile oInstrumentDefinitions = _measurementServer.ConnectedReader.Information.GetInstrumentDefinitions();
                            TecanReaderDefinition oReaderDef = oInstrumentDefinitions.DocumentContent as TecanReaderDefinition;

                            _helper.ReaderDefinition = oReaderDef;
                            _helper.Reader = _measurementServer.ConnectedReader;

                            Console.WriteLine("TecanHelper class initialized successfully");
                        }
                        catch (Exception helperEx)
                        {
                            Console.WriteLine($"Error initializing Helper class: {helperEx.Message}");
                            // Continue even if helper initialization fails
                        }

                        Console.WriteLine($"Connected to Tecan device: {device}");
                        return 0; // Success
                    }

                    Console.WriteLine($"Failed to connect to Tecan device: {device}");
                    return -1; // Failed
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error connecting to device: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    return -1; // Error
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to Tecan by serial: {ex.Message}");
                return -1; // Error
            }
        }

        /// <summary>
        /// Connect to the Tecan device - simple method with no parameters
        /// Uses the first discovered device or a default connection string
        /// </summary>
        public int Connect()
        {
            try
            {
                if (_isConnected)
                    return 0; // Already connected

                // If we have no discovered devices, discover them now
                if (_discoveredDevices.Count == 0)
                {
                    DiscoverDevices();
                }

                // If we have discovered devices, use the first one
                if (_discoveredDevices.Count > 0)
                {
                    return ConnectBySerial(_discoveredDevices[0].Serial);
                }

                // Otherwise, use a default connection string
                Console.WriteLine("Connecting to Tecan device with default parameters...");

                // Create and configure AppSettings
                AppSettings appSettings = new AppSettings();

                // Create a configuration file path where settings can be stored
                string configPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                configPath = Path.Combine(configPath, "TecanOpcUa");
                string configFile = Path.Combine(configPath, "TecanOpcUa_Config.xml");

                // Load settings from file
                appSettings.Load(configFile);

                // Set important connection settings
                appSettings.SetVal("Connection", "ShowTecanInstruments", "TRUE");
                appSettings.SetVal("Connection", "ShowSimulators", "FALSE");

                try
                {
                    // First, use InstrumentServer for device discovery only
                    Console.WriteLine("Using InstrumentServer for device discovery...");
                    InstrumentServer instrumentServer = new InstrumentServer();
                    instrumentServer.AppSettings = appSettings;

                    // Store the instrument server reference for later use (discovery only)
                    _instrumentServer = instrumentServer;

                    // Now use MeasurementServer for the actual connection
                    Console.WriteLine("Using MeasurementServer for device connection...");
                    _measurementServer = new MeasurementServer(appSettings, null);

                    // Use the correct connection string format (USB only)
                    string connectionString = "porttype=USB, type=reader, option=default, name=*";
                    Console.WriteLine($"Using connection string: {connectionString}");

                    // Connect to the device with UserDialog method using MeasurementServer
                    bool connected = _measurementServer.Connect(InstrumentConnectionMethod.UserDialog, connectionString);

                    if (connected)
                    {
                        _isConnected = true;
                        _serialNumber = _measurementServer.ConnectedReader.Information.GetInstrumentSerial();

                        // Create a device object for the connected device
                        _connectedDevice = new TecanDevice
                        {
                            Name = _measurementServer.ConnectedReader.Information.GetProductName(),
                            Type = "READER",
                            Serial = _serialNumber,
                            Port = "USB/USB0",
                            Driver = "Unknown",
                            ConnectionString = connectionString
                        };

                        // Initialize the TecanHelper class for device capabilities
                        try
                        {
                            _helper = new TecanHelper();

                            // Set the reader definition and reader for the helper
                            TecanFile oInstrumentDefinitions = _measurementServer.ConnectedReader.Information.GetInstrumentDefinitions();
                            TecanReaderDefinition oReaderDef = oInstrumentDefinitions.DocumentContent as TecanReaderDefinition;

                            _helper.ReaderDefinition = oReaderDef;
                            _helper.Reader = _measurementServer.ConnectedReader;

                            Console.WriteLine("TecanHelper class initialized successfully");
                        }
                        catch (Exception helperEx)
                        {
                            Console.WriteLine($"Error initializing Helper class: {helperEx.Message}");
                            // Continue even if helper initialization fails
                        }

                        Console.WriteLine($"Connected to Tecan device: {_serialNumber}");
                        return 0; // Success
                    }

                    Console.WriteLine("Failed to connect to Tecan device");
                    return -1; // Failed
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error connecting to device: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    return -1; // Error
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to Tecan: {ex.Message}");
                return -1; // Error
            }
        }

        /// <summary>
        /// Disconnect from the Tecan device
        /// </summary>
        public int Disconnect()
        {
            try
            {
                if (!_isConnected)
                    return 0; // Already disconnected

                Console.WriteLine("Disconnecting from Tecan device...");

                // Disconnect from the measurement server first (this is the main connection)
                if (_measurementServer != null)
                {
                    _measurementServer.Disconnect();
                    _measurementServer = null;
                }

                // Disconnect from the instrument server if it exists (used for discovery)
                if (_instrumentServer != null)
                {
                    _instrumentServer.Disconnect();
                    _instrumentServer = null;
                }

                _isConnected = false;
                _connectedDevice = null;
                _helper = null;
                Console.WriteLine("Disconnected from Tecan device");
                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disconnecting from Tecan: {ex.Message}");
                return -1; // Error
            }
        }

        // Move plate in - follows the pattern from Form1.cs
        public int MovePlateIn()
        {
            if (!_isConnected)
                return -1;

            try
            {
                Console.WriteLine("Moving plate into reader...");

                // Check if MeasurementServer is available
                if (_measurementServer != null && _measurementServer.ConnectedReader != null)
                {
                    // Use the MeasurementServer to move the plate in
                    _measurementServer.ConnectedReader.Movement.PlateIn();
                    _isPlateIn = true;
                    return 0; // Success
                }
                else
                {
                    // If MeasurementServer is not available, we can't move the plate
                    Console.WriteLine("Error: MeasurementServer not available for plate movement");
                    return -1; // Error - MeasurementServer required
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error moving plate in: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return -1; // Error
            }
        }

        // Move plate out - follows the pattern from Form1.cs
        public int MovePlateOut()
        {
            if (!_isConnected)
                return -1;

            try
            {
                Console.WriteLine("Moving plate out of reader...");

                // Check if MeasurementServer is available
                if (_measurementServer != null && _measurementServer.ConnectedReader != null)
                {
                    // Use the MeasurementServer to move the plate out
                    _measurementServer.ConnectedReader.Movement.PlateOut();
                    _isPlateIn = false;
                    return 0; // Success
                }
                else
                {
                    // If MeasurementServer is not available, we can't move the plate
                    Console.WriteLine("Error: MeasurementServer not available for plate movement");
                    return -1; // Error - MeasurementServer required
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error moving plate out: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return -1; // Error
            }
        }

        // Get plate position
        public bool IsPlateIn()
        {
            if (!_isConnected)
                return false;

            try
            {
                // In a real implementation, this would query the actual device
                return _isPlateIn;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking plate position: {ex.Message}");
                return false;
            }
        }

        // Get temperature
        public double GetTemperature()
        {
            if (!_isConnected)
                return 0.0;

            try
            {
                // In a real implementation, this would query the actual device
                return _temperature;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting temperature: {ex.Message}");
                return 0.0;
            }
        }

        // Check if device is connected
        public bool IsConnected()
        {
            return _isConnected;
        }

        // Set temperature
        public int SetTemperature(double temperature)
        {
            if (!_isConnected)
                return -1;

            try
            {
                Console.WriteLine($"Setting temperature to {temperature}°C...");
                // In a real implementation, this would control the actual device
                _temperature = temperature;
                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting temperature: {ex.Message}");
                return -1; // Error
            }
        }
    }

    /// <summary>
    /// Tecan Node Manager for OPC UA Server
    /// </summary>
    public class TecanNodeManager : CustomNodeManager2, IEquipmentNodeManager, INodeManagerFactory
    {
        private const int UPDATE_INTERVAL_MS = 1000;

        // Private fields
        private TecanControl _tecan;
        private ushort _namespaceIndex;
        private uint _lastUsedId;
        private Timer _updateTimer;
        private MeasurementOperations _measurementOperations;

        // Folders
        private FolderState _tecanFolder;
        private FolderState _statusFolder;
        private FolderState _commandsFolder;
        private FolderState _discoveredDevicesFolder;
        private FolderState _deviceCapabilitiesFolder;
        private FolderState _measurementsFolder;

        // Status variables
        private BaseDataVariableState _isConnectedVariable;
        private BaseDataVariableState _isPlateInVariable;
        private BaseDataVariableState _temperatureVariable;
        private BaseDataVariableState _serialNumberVariable;
        private BaseDataVariableState _productNameVariable;
        private BaseDataVariableState _modelVariable;
        private BaseDataVariableState _firmwareVersionVariable;
        private BaseDataVariableState _connectedDeviceSerialVariable;
        private BaseDataVariableState _deviceCountVariable;
        private BaseDataVariableState _instrumentAliasVariable;
        private BaseDataVariableState _instrumentInternalNameVariable;

        // Device capabilities variables
        private BaseDataVariableState _hasLuminescenceFixedVariable;
        private BaseDataVariableState _hasAbsorbanceFixedVariable;
        private BaseDataVariableState _hasCuvetteVariable;
        private BaseDataVariableState _hasAbsorbanceScanVariable;
        private BaseDataVariableState _hasFluorescenceFixedVariable;
        private BaseDataVariableState _hasFluorescenceScanVariable;
        private BaseDataVariableState _hasFluorescencePolarizationVariable;
        private BaseDataVariableState _hasHeatingVariable;
        private BaseDataVariableState _hasShakingVariable;
        private BaseDataVariableState _hasInjectionVariable;
        private BaseDataVariableState _hasBarcodeVariable;
        private BaseDataVariableState _isMonochromatorVariable;

        // Temperature range variables
        private BaseDataVariableState _temperatureMinVariable;
        private BaseDataVariableState _temperatureMaxVariable;

        // List to keep track of device variables
        private List<FolderState> _deviceFolders = new List<FolderState>();

        // Constructor
        public TecanNodeManager(
            IServerInternal server,
            ApplicationConfiguration configuration,
            TecanControl tecan)
        : base(server, configuration, new string[] { "http://persist.com/Tecan" })
        {
            try
            {
                Console.WriteLine("TecanNodeManager constructor called");

                // Store the Tecan control
                _tecan = tecan;
                _lastUsedId = 0;

                // Create the measurement operations
                _measurementOperations = new MeasurementOperations(_tecan);

                // Start a timer to update the Tecan status
                _updateTimer = new Timer(UpdateTecanStatus, null, UPDATE_INTERVAL_MS, UPDATE_INTERVAL_MS);

                Console.WriteLine("TecanNodeManager constructor completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TecanNodeManager constructor: {ex.Message}");
                throw;
            }
        }

        // INodeManagerFactory implementation
        public StringCollection NamespacesUris
        {
            get
            {
                StringCollection namespaces = new StringCollection();
                foreach (string uri in base.NamespaceUris)
                {
                    namespaces.Add(uri);
                }
                return namespaces;
            }
        }

        public INodeManager Create(IServerInternal server, ApplicationConfiguration configuration)
        {
            return new TecanNodeManager(server, configuration, _tecan);
        }

        // IEquipmentNodeManager implementation
        public void Initialize()
        {
            try
            {
                Console.WriteLine("TecanNodeManager.Initialize called - discovering devices");
                if (_tecan != null)
                {
                    // Discover devices first
                    _tecan.DiscoverDevices();

                    // Update the device folders in the address space
                    UpdateDeviceFolders();

                    // Don't automatically connect - let the user choose which device to connect to
                    Console.WriteLine("Tecan devices discovered. Ready for connection.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TecanNodeManager.Initialize: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the device folders in the address space based on discovered devices
        /// </summary>
        private void UpdateDeviceFolders()
        {
            try
            {
                if (_discoveredDevicesFolder == null || _tecan == null)
                    return;

                // Get the list of discovered devices
                var devices = _tecan.GetDiscoveredDevices();

                // Update the device count
                if (_deviceCountVariable != null)
                {
                    _deviceCountVariable.Value = devices.Count;
                    _deviceCountVariable.ClearChangeMasks(SystemContext, false);
                }

                // Clear existing device folders
                foreach (var folder in _deviceFolders)
                {
                    _discoveredDevicesFolder.RemoveChild(folder);
                }
                _deviceFolders.Clear();

                Console.WriteLine($"Creating folders for {devices.Count} discovered devices");

                // Create a folder for each device
                foreach (var device in devices)
                {
                    try
                    {
                        // Create a folder for the device with a clear name
                        string folderName = $"Device_{device.Serial}";
                        string displayName = device.Name;

                        Console.WriteLine($"Creating device folder: {folderName} ({displayName})");

                        FolderState deviceFolder = CreateFolder(_discoveredDevicesFolder, folderName, displayName);
                        _deviceFolders.Add(deviceFolder);

                        // Add only Name and Serial variables
                        BaseDataVariableState nameVar = CreateVariable(deviceFolder, "Name", "Name", DataTypeIds.String, ValueRanks.Scalar);
                        nameVar.Value = device.Name;
                        nameVar.ClearChangeMasks(SystemContext, false);

                        BaseDataVariableState serialVar = CreateVariable(deviceFolder, "Serial", "Serial", DataTypeIds.String, ValueRanks.Scalar);
                        serialVar.Value = device.Serial;
                        serialVar.ClearChangeMasks(SystemContext, false);

                        // Add a method to connect to this device
                        MethodState connectMethod = CreateMethod(deviceFolder, "Connect", "Connect");

                        // Define empty input arguments for Connect
                        connectMethod.InputArguments = new PropertyState<Argument[]>(connectMethod);
                        connectMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                        connectMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                        connectMethod.InputArguments.DisplayName = connectMethod.InputArguments.BrowseName.Name;
                        connectMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                        connectMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                        connectMethod.InputArguments.DataType = DataTypeIds.Argument;
                        connectMethod.InputArguments.ValueRank = ValueRanks.OneDimension;
                        connectMethod.InputArguments.Value = new Argument[0]; // No input arguments

                        // Define output arguments for Connect (returns result code)
                        connectMethod.OutputArguments = new PropertyState<Argument[]>(connectMethod);
                        connectMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                        connectMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                        connectMethod.OutputArguments.DisplayName = connectMethod.OutputArguments.BrowseName.Name;
                        connectMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                        connectMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                        connectMethod.OutputArguments.DataType = DataTypeIds.Argument;
                        connectMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                        Argument resultArgument = new Argument();
                        resultArgument.Name = "Result";
                        resultArgument.Description = new LocalizedText("Result code: 0=success, -1=failure");
                        resultArgument.DataType = DataTypeIds.Int32;
                        resultArgument.ValueRank = ValueRanks.Scalar;

                        connectMethod.OutputArguments.Value = new Argument[] { resultArgument };

                        // Make sure the folder and its children are properly registered with the address space
                        AddPredefinedNode(SystemContext, deviceFolder);

                        Console.WriteLine($"Successfully created device folder for {device.Name} ({device.Serial})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating device folder for {device.Name}: {ex.Message}");
                    }
                }

                // Make sure the discovered devices folder is updated in the address space
                _discoveredDevicesFolder.ClearChangeMasks(SystemContext, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating device folders: {ex.Message}");
            }
        }

        public void Shutdown()
        {
            try
            {
                Console.WriteLine("TecanNodeManager.Shutdown called - disconnecting from device");

                // Disconnect from the device
                if (_tecan != null)
                {
                    _tecan.Disconnect();
                }

                // Stop the update timer
                if (_updateTimer != null)
                {
                    _updateTimer.Dispose();
                    _updateTimer = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TecanNodeManager.Shutdown: {ex.Message}");
            }
        }

        // Overridden methods
        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            try
            {
                Console.WriteLine("TecanNodeManager.CreateAddressSpace - Starting address space creation");

                lock (Lock)
                {
                    // Get the namespace index
                    _namespaceIndex = Server.NamespaceUris.GetIndexOrAppend(NamespaceUris.First());

                    // Create the Tecan folder
                    _tecanFolder = CreateFolder(null, "Tecan", "Tecan");

                    // Add the root folder to the Objects folder
                    IList<IReference> references = null;
                    if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out references))
                    {
                        externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
                    }

                    // Add references
                    references.Add(new NodeStateReference(ReferenceTypes.Organizes, false, _tecanFolder.NodeId));
                    _tecanFolder.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);

                    // Create status folder
                    _statusFolder = CreateFolder(_tecanFolder, "Status", "Status");

                    _isConnectedVariable = CreateVariable(_statusFolder, "IsConnected", "IsConnected", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _isConnectedVariable.Value = false;

                    _isPlateInVariable = CreateVariable(_statusFolder, "IsPlateIn", "IsPlateIn", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _isPlateInVariable.Value = false;

                    _temperatureVariable = CreateVariable(_statusFolder, "Temperature", "Temperature", DataTypeIds.Double, ValueRanks.Scalar);
                    _temperatureVariable.Value = 0.0;

                    _connectedDeviceSerialVariable = CreateVariable(_statusFolder, "ConnectedDeviceSerial", "Connected Device Serial", DataTypeIds.String, ValueRanks.Scalar);
                    _connectedDeviceSerialVariable.Value = string.Empty;

                    // Create device info variables
                    FolderState deviceInfoFolder = CreateFolder(_statusFolder, "DeviceInfo", "DeviceInfo");

                    _serialNumberVariable = CreateVariable(deviceInfoFolder, "SerialNumber", "SerialNumber", DataTypeIds.String, ValueRanks.Scalar);
                    _serialNumberVariable.Value = string.Empty;
                    _serialNumberVariable.Description = new LocalizedText("en", "The serial number of the connected device");

                    _productNameVariable = CreateVariable(deviceInfoFolder, "ProductName", "ProductName", DataTypeIds.String, ValueRanks.Scalar);
                    _productNameVariable.Value = string.Empty;
                    _productNameVariable.Description = new LocalizedText("en", "The product name of the connected device");

                    _modelVariable = CreateVariable(deviceInfoFolder, "Model", "Model", DataTypeIds.String, ValueRanks.Scalar);
                    _modelVariable.Value = string.Empty;
                    _modelVariable.Description = new LocalizedText("en", "The model type of the connected device");

                    _firmwareVersionVariable = CreateVariable(deviceInfoFolder, "FirmwareVersion", "FirmwareVersion", DataTypeIds.String, ValueRanks.Scalar);
                    _firmwareVersionVariable.Value = string.Empty;
                    _firmwareVersionVariable.Description = new LocalizedText("en", "The firmware version of the connected device");

                    _instrumentAliasVariable = CreateVariable(deviceInfoFolder, "InstrumentAlias", "Instrument Alias", DataTypeIds.String, ValueRanks.Scalar);
                    _instrumentAliasVariable.Value = string.Empty;
                    _instrumentAliasVariable.Description = new LocalizedText("en", "The alias name of the connected device");

                    _instrumentInternalNameVariable = CreateVariable(deviceInfoFolder, "InternalName", "Internal Name", DataTypeIds.String, ValueRanks.Scalar);
                    _instrumentInternalNameVariable.Value = string.Empty;
                    _instrumentInternalNameVariable.Description = new LocalizedText("en", "The internal name of the connected device");

                    // Create device capabilities folder
                    _deviceCapabilitiesFolder = CreateFolder(_statusFolder, "DeviceCapabilities", "Device Capabilities");

                    _hasLuminescenceFixedVariable = CreateVariable(_deviceCapabilitiesFolder, "HasLuminescenceFixed", "Has Luminescence Fixed", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _hasLuminescenceFixedVariable.Value = false;
                    _hasLuminescenceFixedVariable.Description = new LocalizedText("en", "Indicates whether the device has fixed luminescence capability");

                    _hasAbsorbanceFixedVariable = CreateVariable(_deviceCapabilitiesFolder, "HasAbsorbanceFixed", "Has Absorbance Fixed", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _hasAbsorbanceFixedVariable.Value = false;
                    _hasAbsorbanceFixedVariable.Description = new LocalizedText("en", "Indicates whether the device has fixed absorbance capability");

                    _hasCuvetteVariable = CreateVariable(_deviceCapabilitiesFolder, "HasCuvette", "Has Cuvette", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _hasCuvetteVariable.Value = false;
                    _hasCuvetteVariable.Description = new LocalizedText("en", "Indicates whether the device has cuvette capability");

                    _hasAbsorbanceScanVariable = CreateVariable(_deviceCapabilitiesFolder, "HasAbsorbanceScan", "Has Absorbance Scan", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _hasAbsorbanceScanVariable.Value = false;
                    _hasAbsorbanceScanVariable.Description = new LocalizedText("en", "Indicates whether the device has absorbance scan capability");

                    _hasFluorescenceFixedVariable = CreateVariable(_deviceCapabilitiesFolder, "HasFluorescenceFixed", "Has Fluorescence Fixed", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _hasFluorescenceFixedVariable.Value = false;
                    _hasFluorescenceFixedVariable.Description = new LocalizedText("en", "Indicates whether the device has fixed fluorescence capability");

                    _hasFluorescenceScanVariable = CreateVariable(_deviceCapabilitiesFolder, "HasFluorescenceScan", "Has Fluorescence Scan", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _hasFluorescenceScanVariable.Value = false;
                    _hasFluorescenceScanVariable.Description = new LocalizedText("en", "Indicates whether the device has fluorescence scan capability");

                    _hasFluorescencePolarizationVariable = CreateVariable(_deviceCapabilitiesFolder, "HasFluorescencePolarization", "Has Fluorescence Polarization", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _hasFluorescencePolarizationVariable.Value = false;
                    _hasFluorescencePolarizationVariable.Description = new LocalizedText("en", "Indicates whether the device has fluorescence polarization capability");

                    _hasHeatingVariable = CreateVariable(_deviceCapabilitiesFolder, "HasHeating", "Has Heating", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _hasHeatingVariable.Value = false;
                    _hasHeatingVariable.Description = new LocalizedText("en", "Indicates whether the device has heating capability");

                    _hasShakingVariable = CreateVariable(_deviceCapabilitiesFolder, "HasShaking", "Has Shaking", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _hasShakingVariable.Value = false;
                    _hasShakingVariable.Description = new LocalizedText("en", "Indicates whether the device has shaking capability");

                    _hasInjectionVariable = CreateVariable(_deviceCapabilitiesFolder, "HasInjection", "Has Injection", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _hasInjectionVariable.Value = false;
                    _hasInjectionVariable.Description = new LocalizedText("en", "Indicates whether the device has injection capability");

                    _hasBarcodeVariable = CreateVariable(_deviceCapabilitiesFolder, "HasBarcode", "Has Barcode", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _hasBarcodeVariable.Value = false;
                    _hasBarcodeVariable.Description = new LocalizedText("en", "Indicates whether the device has barcode reading capability");

                    _isMonochromatorVariable = CreateVariable(_deviceCapabilitiesFolder, "IsMonochromator", "Is Monochromator", DataTypeIds.Boolean, ValueRanks.Scalar);
                    _isMonochromatorVariable.Value = false;
                    _isMonochromatorVariable.Description = new LocalizedText("en", "Indicates whether the device is a monochromator");

                    // Create temperature range variables
                    FolderState temperatureRangeFolder = CreateFolder(_deviceCapabilitiesFolder, "TemperatureRange", "Temperature Range");

                    _temperatureMinVariable = CreateVariable(temperatureRangeFolder, "MinTemperature", "Minimum Temperature", DataTypeIds.Double, ValueRanks.Scalar);
                    _temperatureMinVariable.Value = 0.0;
                    _temperatureMinVariable.Description = new LocalizedText("en", "The minimum temperature setting in degrees Celsius");

                    _temperatureMaxVariable = CreateVariable(temperatureRangeFolder, "MaxTemperature", "Maximum Temperature", DataTypeIds.Double, ValueRanks.Scalar);
                    _temperatureMaxVariable.Value = 0.0;
                    _temperatureMaxVariable.Description = new LocalizedText("en", "The maximum temperature setting in degrees Celsius");

                    // Create discovered devices folder
                    _discoveredDevicesFolder = CreateFolder(_statusFolder, "DiscoveredDevices", "Discovered Devices");

                    // Add device count variable
                    _deviceCountVariable = CreateVariable(_discoveredDevicesFolder, "DeviceCount", "Device Count", DataTypeIds.Int32, ValueRanks.Scalar);
                    _deviceCountVariable.Value = 0;

                    // Create measurements folder
                    _measurementsFolder = CreateFolder(_tecanFolder, "Measurements", "Measurements");

                    // Create absorbance measurement method
                    MethodState absorbanceMeasurementMethod = CreateMethod(_measurementsFolder, "PerformAbsorbanceMeasurement", "Perform Absorbance Measurement");

                    // Define input arguments for absorbance measurement
                    absorbanceMeasurementMethod.InputArguments = new PropertyState<Argument[]>(absorbanceMeasurementMethod);
                    absorbanceMeasurementMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    absorbanceMeasurementMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    absorbanceMeasurementMethod.InputArguments.DisplayName = absorbanceMeasurementMethod.InputArguments.BrowseName.Name;
                    absorbanceMeasurementMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    absorbanceMeasurementMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    absorbanceMeasurementMethod.InputArguments.DataType = DataTypeIds.Argument;
                    absorbanceMeasurementMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument plateTypeArgument = new Argument();
                    plateTypeArgument.Name = "PlateType";
                    plateTypeArgument.Description = new LocalizedText("The plate type to use");
                    plateTypeArgument.DataType = DataTypeIds.String;
                    plateTypeArgument.ValueRank = ValueRanks.Scalar;

                    Argument wellRangeArgument = new Argument();
                    wellRangeArgument.Name = "WellRange";
                    wellRangeArgument.Description = new LocalizedText("The well range to measure (e.g., 'A1:H12')");
                    wellRangeArgument.DataType = DataTypeIds.String;
                    wellRangeArgument.ValueRank = ValueRanks.Scalar;

                    Argument wavelengthArgument = new Argument();
                    wavelengthArgument.Name = "Wavelength";
                    wavelengthArgument.Description = new LocalizedText("The wavelength in nm");
                    wavelengthArgument.DataType = DataTypeIds.Int32;
                    wavelengthArgument.ValueRank = ValueRanks.Scalar;

                    Argument numberOfFlashesArgument = new Argument();
                    numberOfFlashesArgument.Name = "NumberOfFlashes";
                    numberOfFlashesArgument.Description = new LocalizedText("The number of flashes");
                    numberOfFlashesArgument.DataType = DataTypeIds.Int32;
                    numberOfFlashesArgument.ValueRank = ValueRanks.Scalar;

                    Argument settleTimeArgument = new Argument();
                    settleTimeArgument.Name = "SettleTime";
                    settleTimeArgument.Description = new LocalizedText("The settle time in ms");
                    settleTimeArgument.DataType = DataTypeIds.Int32;
                    settleTimeArgument.ValueRank = ValueRanks.Scalar;

                    absorbanceMeasurementMethod.InputArguments.Value = new Argument[]
                    {
                        plateTypeArgument,
                        wellRangeArgument,
                        wavelengthArgument,
                        numberOfFlashesArgument,
                        settleTimeArgument
                    };

                    // Define output arguments for absorbance measurement
                    absorbanceMeasurementMethod.OutputArguments = new PropertyState<Argument[]>(absorbanceMeasurementMethod);
                    absorbanceMeasurementMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    absorbanceMeasurementMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    absorbanceMeasurementMethod.OutputArguments.DisplayName = absorbanceMeasurementMethod.OutputArguments.BrowseName.Name;
                    absorbanceMeasurementMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    absorbanceMeasurementMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    absorbanceMeasurementMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    absorbanceMeasurementMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument measurementIdArgument = new Argument();
                    measurementIdArgument.Name = "MeasurementId";
                    measurementIdArgument.Description = new LocalizedText("The unique identifier for the measurement");
                    measurementIdArgument.DataType = DataTypeIds.String;
                    measurementIdArgument.ValueRank = ValueRanks.Scalar;

                    absorbanceMeasurementMethod.OutputArguments.Value = new Argument[] { measurementIdArgument };

                    // Create fluorescence measurement method
                    MethodState fluorescenceMeasurementMethod = CreateMethod(_measurementsFolder, "PerformFluorescenceMeasurement", "Perform Fluorescence Measurement");

                    // Define input arguments for fluorescence measurement
                    fluorescenceMeasurementMethod.InputArguments = new PropertyState<Argument[]>(fluorescenceMeasurementMethod);
                    fluorescenceMeasurementMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    fluorescenceMeasurementMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    fluorescenceMeasurementMethod.InputArguments.DisplayName = fluorescenceMeasurementMethod.InputArguments.BrowseName.Name;
                    fluorescenceMeasurementMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    fluorescenceMeasurementMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    fluorescenceMeasurementMethod.InputArguments.DataType = DataTypeIds.Argument;
                    fluorescenceMeasurementMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument excitationWavelengthArgument = new Argument();
                    excitationWavelengthArgument.Name = "ExcitationWavelength";
                    excitationWavelengthArgument.Description = new LocalizedText("The excitation wavelength in nm");
                    excitationWavelengthArgument.DataType = DataTypeIds.Int32;
                    excitationWavelengthArgument.ValueRank = ValueRanks.Scalar;

                    Argument emissionWavelengthArgument = new Argument();
                    emissionWavelengthArgument.Name = "EmissionWavelength";
                    emissionWavelengthArgument.Description = new LocalizedText("The emission wavelength in nm");
                    emissionWavelengthArgument.DataType = DataTypeIds.Int32;
                    emissionWavelengthArgument.ValueRank = ValueRanks.Scalar;

                    Argument gainArgument = new Argument();
                    gainArgument.Name = "Gain";
                    gainArgument.Description = new LocalizedText("The gain value");
                    gainArgument.DataType = DataTypeIds.Int32;
                    gainArgument.ValueRank = ValueRanks.Scalar;

                    Argument integrationTimeArgument = new Argument();
                    integrationTimeArgument.Name = "IntegrationTime";
                    integrationTimeArgument.Description = new LocalizedText("The integration time in μs");
                    integrationTimeArgument.DataType = DataTypeIds.Int32;
                    integrationTimeArgument.ValueRank = ValueRanks.Scalar;

                    Argument readingModeArgument = new Argument();
                    readingModeArgument.Name = "ReadingMode";
                    readingModeArgument.Description = new LocalizedText("The reading mode (Top or Bottom)");
                    readingModeArgument.DataType = DataTypeIds.String;
                    readingModeArgument.ValueRank = ValueRanks.Scalar;

                    fluorescenceMeasurementMethod.InputArguments.Value = new Argument[]
                    {
                        plateTypeArgument,
                        wellRangeArgument,
                        excitationWavelengthArgument,
                        emissionWavelengthArgument,
                        gainArgument,
                        numberOfFlashesArgument,
                        integrationTimeArgument,
                        settleTimeArgument,
                        readingModeArgument
                    };

                    // Define output arguments for fluorescence measurement
                    fluorescenceMeasurementMethod.OutputArguments = new PropertyState<Argument[]>(fluorescenceMeasurementMethod);
                    fluorescenceMeasurementMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    fluorescenceMeasurementMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    fluorescenceMeasurementMethod.OutputArguments.DisplayName = fluorescenceMeasurementMethod.OutputArguments.BrowseName.Name;
                    fluorescenceMeasurementMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    fluorescenceMeasurementMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    fluorescenceMeasurementMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    fluorescenceMeasurementMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    fluorescenceMeasurementMethod.OutputArguments.Value = new Argument[] { measurementIdArgument };

                    // Create luminescence measurement method
                    MethodState luminescenceMeasurementMethod = CreateMethod(_measurementsFolder, "PerformLuminescenceMeasurement", "Perform Luminescence Measurement");

                    // Define input arguments for luminescence measurement
                    luminescenceMeasurementMethod.InputArguments = new PropertyState<Argument[]>(luminescenceMeasurementMethod);
                    luminescenceMeasurementMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    luminescenceMeasurementMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    luminescenceMeasurementMethod.InputArguments.DisplayName = luminescenceMeasurementMethod.InputArguments.BrowseName.Name;
                    luminescenceMeasurementMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    luminescenceMeasurementMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    luminescenceMeasurementMethod.InputArguments.DataType = DataTypeIds.Argument;
                    luminescenceMeasurementMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument attenuationArgument = new Argument();
                    attenuationArgument.Name = "Attenuation";
                    attenuationArgument.Description = new LocalizedText("The attenuation filter to use");
                    attenuationArgument.DataType = DataTypeIds.String;
                    attenuationArgument.ValueRank = ValueRanks.Scalar;

                    luminescenceMeasurementMethod.InputArguments.Value = new Argument[]
                    {
                        plateTypeArgument,
                        wellRangeArgument,
                        integrationTimeArgument,
                        settleTimeArgument,
                        attenuationArgument
                    };

                    // Define output arguments for luminescence measurement
                    luminescenceMeasurementMethod.OutputArguments = new PropertyState<Argument[]>(luminescenceMeasurementMethod);
                    luminescenceMeasurementMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    luminescenceMeasurementMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    luminescenceMeasurementMethod.OutputArguments.DisplayName = luminescenceMeasurementMethod.OutputArguments.BrowseName.Name;
                    luminescenceMeasurementMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    luminescenceMeasurementMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    luminescenceMeasurementMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    luminescenceMeasurementMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    luminescenceMeasurementMethod.OutputArguments.Value = new Argument[] { measurementIdArgument };

                    // Create commands folder
                    _commandsFolder = CreateFolder(_tecanFolder, "Commands", "Commands");

                    // Create DiscoverDevices method
                    MethodState discoverDevicesMethod = CreateMethod(_commandsFolder, "DiscoverDevices", "Discover Devices");

                    // Define empty input arguments for DiscoverDevices
                    discoverDevicesMethod.InputArguments = new PropertyState<Argument[]>(discoverDevicesMethod);
                    discoverDevicesMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    discoverDevicesMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    discoverDevicesMethod.InputArguments.DisplayName = discoverDevicesMethod.InputArguments.BrowseName.Name;
                    discoverDevicesMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    discoverDevicesMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    discoverDevicesMethod.InputArguments.DataType = DataTypeIds.Argument;
                    discoverDevicesMethod.InputArguments.ValueRank = ValueRanks.OneDimension;
                    discoverDevicesMethod.InputArguments.Value = new Argument[0]; // No input arguments

                    // Define output arguments for DiscoverDevices (returns device count)
                    discoverDevicesMethod.OutputArguments = new PropertyState<Argument[]>(discoverDevicesMethod);
                    discoverDevicesMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    discoverDevicesMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    discoverDevicesMethod.OutputArguments.DisplayName = discoverDevicesMethod.OutputArguments.BrowseName.Name;
                    discoverDevicesMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    discoverDevicesMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    discoverDevicesMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    discoverDevicesMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument deviceCountArgument = new Argument();
                    deviceCountArgument.Name = "DeviceCount";
                    deviceCountArgument.Description = new LocalizedText("Number of devices discovered");
                    deviceCountArgument.DataType = DataTypeIds.Int32;
                    deviceCountArgument.ValueRank = ValueRanks.Scalar;

                    discoverDevicesMethod.OutputArguments.Value = new Argument[] { deviceCountArgument };

                    // Create ConnectBySerial method with device serial parameter
                    MethodState connectBySerialMethod = CreateMethod(_commandsFolder, "ConnectBySerial", "Connect By Serial");

                    // Define input arguments for ConnectBySerial
                    connectBySerialMethod.InputArguments = new PropertyState<Argument[]>(connectBySerialMethod);
                    connectBySerialMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    connectBySerialMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    connectBySerialMethod.InputArguments.DisplayName = connectBySerialMethod.InputArguments.BrowseName.Name;
                    connectBySerialMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    connectBySerialMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    connectBySerialMethod.InputArguments.DataType = DataTypeIds.Argument;
                    connectBySerialMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument deviceSerialArgument = new Argument();
                    deviceSerialArgument.Name = "DeviceSerial";
                    deviceSerialArgument.Description = new LocalizedText("Serial number of the device to connect to");
                    deviceSerialArgument.DataType = DataTypeIds.String;
                    deviceSerialArgument.ValueRank = ValueRanks.Scalar;

                    connectBySerialMethod.InputArguments.Value = new Argument[] { deviceSerialArgument };

                    // Define output arguments for ConnectBySerial (returns result code)
                    connectBySerialMethod.OutputArguments = new PropertyState<Argument[]>(connectBySerialMethod);
                    connectBySerialMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    connectBySerialMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    connectBySerialMethod.OutputArguments.DisplayName = connectBySerialMethod.OutputArguments.BrowseName.Name;
                    connectBySerialMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    connectBySerialMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    connectBySerialMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    connectBySerialMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument connectResultArgument = new Argument();
                    connectResultArgument.Name = "Result";
                    connectResultArgument.Description = new LocalizedText("Result code: 0=success, -1=failure, -2=device not found");
                    connectResultArgument.DataType = DataTypeIds.Int32;
                    connectResultArgument.ValueRank = ValueRanks.Scalar;

                    connectBySerialMethod.OutputArguments.Value = new Argument[] { connectResultArgument };

                    // Create Connect method with no parameters (connects to first available device)
                    MethodState connectMethod = CreateMethod(_commandsFolder, "Connect", "Connect");

                    // Define empty input arguments for Connect
                    connectMethod.InputArguments = new PropertyState<Argument[]>(connectMethod);
                    connectMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    connectMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    connectMethod.InputArguments.DisplayName = connectMethod.InputArguments.BrowseName.Name;
                    connectMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    connectMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    connectMethod.InputArguments.DataType = DataTypeIds.Argument;
                    connectMethod.InputArguments.ValueRank = ValueRanks.OneDimension;
                    connectMethod.InputArguments.Value = new Argument[0]; // No input arguments

                    // Define output arguments for Connect (returns result code)
                    connectMethod.OutputArguments = new PropertyState<Argument[]>(connectMethod);
                    connectMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    connectMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    connectMethod.OutputArguments.DisplayName = connectMethod.OutputArguments.BrowseName.Name;
                    connectMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    connectMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    connectMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    connectMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                    Argument defaultConnectResultArgument = new Argument();
                    defaultConnectResultArgument.Name = "Result";
                    defaultConnectResultArgument.Description = new LocalizedText("Result code: 0=success, -1=failure");
                    defaultConnectResultArgument.DataType = DataTypeIds.Int32;
                    defaultConnectResultArgument.ValueRank = ValueRanks.Scalar;

                    connectMethod.OutputArguments.Value = new Argument[] { defaultConnectResultArgument };

                    // Create Disconnect method with no parameters
                    MethodState disconnectMethod = CreateMethod(_commandsFolder, "Disconnect", "Disconnect");

                    // Define empty input arguments for Disconnect
                    disconnectMethod.InputArguments = new PropertyState<Argument[]>(disconnectMethod);
                    disconnectMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    disconnectMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    disconnectMethod.InputArguments.DisplayName = disconnectMethod.InputArguments.BrowseName.Name;
                    disconnectMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    disconnectMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    disconnectMethod.InputArguments.DataType = DataTypeIds.Argument;
                    disconnectMethod.InputArguments.ValueRank = ValueRanks.OneDimension;
                    disconnectMethod.InputArguments.Value = new Argument[0]; // No input arguments

                    // Define empty output arguments for Disconnect
                    disconnectMethod.OutputArguments = new PropertyState<Argument[]>(disconnectMethod);
                    disconnectMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    disconnectMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    disconnectMethod.OutputArguments.DisplayName = disconnectMethod.OutputArguments.BrowseName.Name;
                    disconnectMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    disconnectMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    disconnectMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    disconnectMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;
                    disconnectMethod.OutputArguments.Value = new Argument[0]; // No output arguments

                    // Create MovePlateIn method with arguments
                    MethodState movePlateInMethod = CreateMethod(_commandsFolder, "MovePlateIn", "MovePlateIn");

                    // Define empty input arguments for MovePlateIn
                    movePlateInMethod.InputArguments = new PropertyState<Argument[]>(movePlateInMethod);
                    movePlateInMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    movePlateInMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    movePlateInMethod.InputArguments.DisplayName = movePlateInMethod.InputArguments.BrowseName.Name;
                    movePlateInMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    movePlateInMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    movePlateInMethod.InputArguments.DataType = DataTypeIds.Argument;
                    movePlateInMethod.InputArguments.ValueRank = ValueRanks.OneDimension;
                    movePlateInMethod.InputArguments.Value = new Argument[0];

                    // Define empty output arguments for MovePlateIn
                    movePlateInMethod.OutputArguments = new PropertyState<Argument[]>(movePlateInMethod);
                    movePlateInMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    movePlateInMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    movePlateInMethod.OutputArguments.DisplayName = movePlateInMethod.OutputArguments.BrowseName.Name;
                    movePlateInMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    movePlateInMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    movePlateInMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    movePlateInMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;
                    movePlateInMethod.OutputArguments.Value = new Argument[0];

                    // Create MovePlateOut method with arguments
                    MethodState movePlateOutMethod = CreateMethod(_commandsFolder, "MovePlateOut", "MovePlateOut");

                    // Define empty input arguments for MovePlateOut
                    movePlateOutMethod.InputArguments = new PropertyState<Argument[]>(movePlateOutMethod);
                    movePlateOutMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    movePlateOutMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    movePlateOutMethod.InputArguments.DisplayName = movePlateOutMethod.InputArguments.BrowseName.Name;
                    movePlateOutMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    movePlateOutMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    movePlateOutMethod.InputArguments.DataType = DataTypeIds.Argument;
                    movePlateOutMethod.InputArguments.ValueRank = ValueRanks.OneDimension;
                    movePlateOutMethod.InputArguments.Value = new Argument[0];

                    // Define empty output arguments for MovePlateOut
                    movePlateOutMethod.OutputArguments = new PropertyState<Argument[]>(movePlateOutMethod);
                    movePlateOutMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    movePlateOutMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    movePlateOutMethod.OutputArguments.DisplayName = movePlateOutMethod.OutputArguments.BrowseName.Name;
                    movePlateOutMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    movePlateOutMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    movePlateOutMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    movePlateOutMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;
                    movePlateOutMethod.OutputArguments.Value = new Argument[0];

                    // Create SetTemperature method with arguments
                    MethodState setTemperatureMethod = CreateMethod(_commandsFolder, "SetTemperature", "SetTemperature");

                    // Define input arguments for SetTemperature
                    setTemperatureMethod.InputArguments = new PropertyState<Argument[]>(setTemperatureMethod);
                    setTemperatureMethod.InputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    setTemperatureMethod.InputArguments.BrowseName = BrowseNames.InputArguments;
                    setTemperatureMethod.InputArguments.DisplayName = setTemperatureMethod.InputArguments.BrowseName.Name;
                    setTemperatureMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    setTemperatureMethod.InputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    setTemperatureMethod.InputArguments.DataType = DataTypeIds.Argument;
                    setTemperatureMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                    // Define the temperature argument
                    Argument temperatureArgument = new Argument();
                    temperatureArgument.Name = "Temperature";
                    temperatureArgument.Description = new LocalizedText("The temperature to set in degrees Celsius");
                    temperatureArgument.DataType = DataTypeIds.Double;
                    temperatureArgument.ValueRank = ValueRanks.Scalar;

                    setTemperatureMethod.InputArguments.Value = new Argument[] { temperatureArgument };

                    // Define output arguments (empty for this method)
                    setTemperatureMethod.OutputArguments = new PropertyState<Argument[]>(setTemperatureMethod);
                    setTemperatureMethod.OutputArguments.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
                    setTemperatureMethod.OutputArguments.BrowseName = BrowseNames.OutputArguments;
                    setTemperatureMethod.OutputArguments.DisplayName = setTemperatureMethod.OutputArguments.BrowseName.Name;
                    setTemperatureMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                    setTemperatureMethod.OutputArguments.ReferenceTypeId = ReferenceTypes.HasProperty;
                    setTemperatureMethod.OutputArguments.DataType = DataTypeIds.Argument;
                    setTemperatureMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;
                    setTemperatureMethod.OutputArguments.Value = new Argument[0];

                    // Register all nodes with the address space
                    AddPredefinedNode(SystemContext, _tecanFolder);

                    Console.WriteLine("Address space creation completed successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreateAddressSpace: {ex.Message}");
                throw;
            }
        }

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

        // Method to update Tecan status
        private void UpdateTecanStatus(object state)
        {
            try
            {
                // Only update variables if we have a reference to the Tecan control
                if (_tecan != null)
                {
                    // Update connection status
                    if (_isConnectedVariable != null)
                    {
                        _isConnectedVariable.Value = _tecan.IsConnected();
                    }

                    // Get the connected device
                    var connectedDevice = _tecan.GetConnectedDevice();

                    // Update connected device serial
                    if (_connectedDeviceSerialVariable != null)
                    {
                        _connectedDeviceSerialVariable.Value = connectedDevice != null ? connectedDevice.Serial : string.Empty;
                    }

                    // Only update other variables if the device is connected
                    if (_tecan.IsConnected())
                    {
                        // Update plate position
                        if (_isPlateInVariable != null)
                        {
                            _isPlateInVariable.Value = _tecan.IsPlateIn();
                        }

                        // Update temperature
                        if (_temperatureVariable != null)
                        {
                            _temperatureVariable.Value = _tecan.GetTemperature();
                        }

                        // Update device info from the connected device
                        if (connectedDevice != null)
                        {
                            if (_serialNumberVariable != null)
                            {
                                _serialNumberVariable.Value = connectedDevice.Serial;
                            }

                            if (_productNameVariable != null)
                            {
                                _productNameVariable.Value = connectedDevice.Name;
                            }

                            if (_modelVariable != null)
                            {
                                _modelVariable.Value = connectedDevice.Type;
                            }

                            // Update additional device information if MeasurementServer is available
                            if (_tecan._measurementServer != null && _tecan._measurementServer.ConnectedReader != null)
                            {
                                try
                                {
                                    var reader = _tecan._measurementServer.ConnectedReader;

                                    // Update instrument alias and internal name
                                    if (_instrumentAliasVariable != null)
                                    {
                                        _instrumentAliasVariable.Value = reader.Information.GetInstrumentAlias();
                                    }

                                    if (_instrumentInternalNameVariable != null)
                                    {
                                        _instrumentInternalNameVariable.Value = reader.Information.GetInstrumentName();
                                    }

                                    // Update device capabilities if helper is available
                                    if (_tecan._helper != null)
                                    {
                                        // Update capabilities
                                        if (_hasLuminescenceFixedVariable != null)
                                        {
                                            _hasLuminescenceFixedVariable.Value = _tecan._helper.HasLuminescenceFixed();
                                        }

                                        if (_hasAbsorbanceFixedVariable != null)
                                        {
                                            _hasAbsorbanceFixedVariable.Value = _tecan._helper.HasAbsorbanceFixed();
                                        }

                                        if (_hasCuvetteVariable != null)
                                        {
                                            _hasCuvetteVariable.Value = _tecan._helper.HasCuvette();
                                        }

                                        if (_hasAbsorbanceScanVariable != null)
                                        {
                                            _hasAbsorbanceScanVariable.Value = _tecan._helper.HasAbsorbanceScan();
                                        }

                                        if (_hasFluorescenceFixedVariable != null)
                                        {
                                            _hasFluorescenceFixedVariable.Value = _tecan._helper.HasFluoresenceFixed();
                                        }

                                        if (_hasFluorescenceScanVariable != null)
                                        {
                                            _hasFluorescenceScanVariable.Value = _tecan._helper.HasFluoresenceScan();
                                        }

                                        if (_hasFluorescencePolarizationVariable != null)
                                        {
                                            _hasFluorescencePolarizationVariable.Value = _tecan._helper.HasFluorescencePolarization();
                                        }

                                        if (_hasHeatingVariable != null)
                                        {
                                            _hasHeatingVariable.Value = _tecan._helper.HasHeating();
                                        }

                                        if (_hasShakingVariable != null)
                                        {
                                            _hasShakingVariable.Value = _tecan._helper.HasShaking();
                                        }

                                        if (_hasInjectionVariable != null)
                                        {
                                            _hasInjectionVariable.Value = _tecan._helper.HasInjection();
                                        }

                                        if (_hasBarcodeVariable != null)
                                        {
                                            _hasBarcodeVariable.Value = _tecan._helper.HasBarcode();
                                        }

                                        if (_isMonochromatorVariable != null)
                                        {
                                            _isMonochromatorVariable.Value = _tecan._helper.IsMonochromator();
                                        }

                                        // Update temperature range
                                        if (_temperatureMinVariable != null && _temperatureMaxVariable != null)
                                        {
                                            int minTemp = 0;
                                            int maxTemp = 0;
                                            _tecan._helper.GetTemperatureRange(ref minTemp, ref maxTemp);
                                            _temperatureMinVariable.Value = minTemp / 10.0; // Convert to degrees
                                            _temperatureMaxVariable.Value = maxTemp / 10.0; // Convert to degrees
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error updating extended device information: {ex.Message}");
                                }
                            }
                        }
                    }
                    else
                    {
                        // Clear device info when not connected
                        if (_serialNumberVariable != null)
                        {
                            _serialNumberVariable.Value = string.Empty;
                        }

                        if (_productNameVariable != null)
                        {
                            _productNameVariable.Value = string.Empty;
                        }

                        if (_modelVariable != null)
                        {
                            _modelVariable.Value = string.Empty;
                        }

                        if (_firmwareVersionVariable != null)
                        {
                            _firmwareVersionVariable.Value = string.Empty;
                        }

                        // Clear additional device info
                        if (_instrumentAliasVariable != null)
                        {
                            _instrumentAliasVariable.Value = string.Empty;
                        }

                        if (_instrumentInternalNameVariable != null)
                        {
                            _instrumentInternalNameVariable.Value = string.Empty;
                        }

                        // Reset capabilities to false
                        if (_hasLuminescenceFixedVariable != null) _hasLuminescenceFixedVariable.Value = false;
                        if (_hasAbsorbanceFixedVariable != null) _hasAbsorbanceFixedVariable.Value = false;
                        if (_hasCuvetteVariable != null) _hasCuvetteVariable.Value = false;
                        if (_hasAbsorbanceScanVariable != null) _hasAbsorbanceScanVariable.Value = false;
                        if (_hasFluorescenceFixedVariable != null) _hasFluorescenceFixedVariable.Value = false;
                        if (_hasFluorescenceScanVariable != null) _hasFluorescenceScanVariable.Value = false;
                        if (_hasFluorescencePolarizationVariable != null) _hasFluorescencePolarizationVariable.Value = false;
                        if (_hasHeatingVariable != null) _hasHeatingVariable.Value = false;
                        if (_hasShakingVariable != null) _hasShakingVariable.Value = false;
                        if (_hasInjectionVariable != null) _hasInjectionVariable.Value = false;
                        if (_hasBarcodeVariable != null) _hasBarcodeVariable.Value = false;
                        if (_isMonochromatorVariable != null) _isMonochromatorVariable.Value = false;

                        // Reset temperature range
                        if (_temperatureMinVariable != null) _temperatureMinVariable.Value = 0.0;
                        if (_temperatureMaxVariable != null) _temperatureMaxVariable.Value = 0.0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateTecanStatus: {ex.Message}");
            }
        }

        // Helper methods for creating nodes
        private FolderState CreateFolder(NodeState parent, string name, string displayName)
        {
            FolderState folder = new FolderState(parent);

            folder.SymbolicName = name;
            folder.ReferenceTypeId = ReferenceTypes.Organizes;
            folder.TypeDefinitionId = ObjectTypeIds.FolderType;

            // Increment the ID and create a NodeId
            _lastUsedId++;
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

            return folder;
        }

        private BaseDataVariableState CreateVariable(NodeState parent, string name, string displayName, NodeId dataType, int valueRank)
        {
            BaseDataVariableState variable = new BaseDataVariableState(parent);

            variable.SymbolicName = name;
            variable.ReferenceTypeId = ReferenceTypes.Organizes;
            variable.TypeDefinitionId = VariableTypeIds.BaseDataVariableType;

            // Increment the ID and create a NodeId
            _lastUsedId++;
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

            return variable;
        }

        // Get default value for a data type
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

            if (dataType == DataTypeIds.String)
            {
                return string.Empty;
            }

            if (dataType == DataTypeIds.Double)
            {
                return 0.0;
            }

            // Add more data types as needed

            return null;
        }

        // Create a method node
        private MethodState CreateMethod(NodeState parent, string name, string displayName)
        {
            MethodState method = new MethodState(parent);

            method.SymbolicName = name;
            method.ReferenceTypeId = ReferenceTypes.HasComponent;
            method.NodeId = new NodeId(++_lastUsedId, _namespaceIndex);
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

            // Set up method callbacks
            method.OnCallMethod = OnCallMethod;

            return method;
        }

        // Method call handler
        private ServiceResult OnCallMethod(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Handle method calls based on the method name
                switch (method.BrowseName.Name)
                {
                    case "DiscoverDevices":
                        return OnDiscoverDevices(context, method, inputArguments, outputArguments);

                    case "ConnectBySerial":
                        return OnConnectBySerial(context, method, inputArguments, outputArguments);

                    case "Connect":
                        // Check if this is a device-specific Connect method
                        if (method.Parent != null && method.Parent.BrowseName.Name.StartsWith("Device_"))
                        {
                            return OnDeviceConnect(context, method, inputArguments, outputArguments);
                        }
                        else
                        {
                            return OnConnect(context, method, inputArguments, outputArguments);
                        }

                    case "Disconnect":
                        return OnDisconnect(context, method, inputArguments, outputArguments);

                    case "MovePlateIn":
                        return OnMovePlateIn(context, method, inputArguments, outputArguments);

                    case "MovePlateOut":
                        return OnMovePlateOut(context, method, inputArguments, outputArguments);

                    case "SetTemperature":
                        return OnSetTemperature(context, method, inputArguments, outputArguments);

                    case "PerformAbsorbanceMeasurement":
                        return OnPerformAbsorbanceMeasurement(context, method, inputArguments, outputArguments);

                    case "PerformFluorescenceMeasurement":
                        return OnPerformFluorescenceMeasurement(context, method, inputArguments, outputArguments);

                    case "PerformLuminescenceMeasurement":
                        return OnPerformLuminescenceMeasurement(context, method, inputArguments, outputArguments);

                    default:
                        return StatusCodes.BadMethodInvalid;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnCallMethod: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // DiscoverDevices method implementation
        private ServiceResult OnDiscoverDevices(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Call the Tecan control method
                int deviceCount = _tecan.DiscoverDevices();

                // Update the device folders in the address space
                UpdateDeviceFolders();

                // Set the output argument (device count)
                outputArguments[0] = deviceCount;

                return StatusCodes.Good;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnDiscoverDevices: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // ConnectBySerial method implementation
        private ServiceResult OnConnectBySerial(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Check if we have the right number of input arguments
                if (inputArguments.Count != 1)
                {
                    return StatusCodes.BadArgumentsMissing;
                }

                // Get the device serial
                string deviceSerial;
                try
                {
                    deviceSerial = inputArguments[0].ToString();
                }
                catch
                {
                    return StatusCodes.BadTypeMismatch;
                }

                // Call the Tecan control method
                int result = _tecan.ConnectBySerial(deviceSerial);

                // Set the output argument (result code)
                outputArguments[0] = result;

                if (result == 0)
                {
                    return StatusCodes.Good;
                }
                else if (result == -2)
                {
                    return StatusCodes.BadNodeIdUnknown;
                }
                else
                {
                    return StatusCodes.BadUnexpectedError;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnConnectBySerial: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // Device-specific Connect method implementation
        private ServiceResult OnDeviceConnect(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Get the device serial from the parent folder name
                string folderName = method.Parent.BrowseName.Name;
                string deviceSerial = folderName.Substring("Device_".Length);

                // Call the Tecan control method
                int result = _tecan.ConnectBySerial(deviceSerial);

                // Set the output argument (result code)
                outputArguments[0] = result;

                if (result == 0)
                {
                    return StatusCodes.Good;
                }
                else if (result == -2)
                {
                    return StatusCodes.BadNodeIdUnknown;
                }
                else
                {
                    return StatusCodes.BadUnexpectedError;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnDeviceConnect: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // Connect method implementation
        private ServiceResult OnConnect(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Call the Tecan control method
                int result = _tecan.Connect();

                if (result == 0)
                {
                    return StatusCodes.Good;
                }
                else
                {
                    return StatusCodes.BadUnexpectedError;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnConnect: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // Disconnect method implementation
        private ServiceResult OnDisconnect(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Call the Tecan control method
                int result = _tecan.Disconnect();

                if (result == 0)
                {
                    return StatusCodes.Good;
                }
                else
                {
                    return StatusCodes.BadUnexpectedError;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnDisconnect: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // MovePlateIn method implementation
        private ServiceResult OnMovePlateIn(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Call the Tecan control method
                int result = _tecan.MovePlateIn();

                if (result == 0)
                {
                    return StatusCodes.Good;
                }
                else
                {
                    return StatusCodes.BadUnexpectedError;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnMovePlateIn: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // MovePlateOut method implementation
        private ServiceResult OnMovePlateOut(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Call the Tecan control method
                int result = _tecan.MovePlateOut();

                if (result == 0)
                {
                    return StatusCodes.Good;
                }
                else
                {
                    return StatusCodes.BadUnexpectedError;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnMovePlateOut: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // SetTemperature method implementation
        private ServiceResult OnSetTemperature(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Check if we have the right number of input arguments
                if (inputArguments.Count != 1)
                {
                    return StatusCodes.BadArgumentsMissing;
                }

                // Get the temperature value
                double temperature;
                try
                {
                    temperature = Convert.ToDouble(inputArguments[0]);
                }
                catch
                {
                    return StatusCodes.BadTypeMismatch;
                }

                // Call the Tecan control method
                int result = _tecan.SetTemperature(temperature);

                if (result == 0)
                {
                    return StatusCodes.Good;
                }
                else
                {
                    return StatusCodes.BadUnexpectedError;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnSetTemperature: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // PerformAbsorbanceMeasurement method implementation
        private ServiceResult OnPerformAbsorbanceMeasurement(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Check if we have the right number of input arguments
                if (inputArguments.Count != 5)
                {
                    return StatusCodes.BadArgumentsMissing;
                }

                // Get the input arguments
                string plateType = inputArguments[0].ToString();
                string wellRange = inputArguments[1].ToString();
                int wavelength = Convert.ToInt32(inputArguments[2]);
                int numberOfFlashes = Convert.ToInt32(inputArguments[3]);
                int settleTime = Convert.ToInt32(inputArguments[4]);

                // Check if the device is connected
                if (!_tecan.IsConnected())
                {
                    return StatusCodes.BadDeviceFailure;
                }

                // Perform the measurement
                MeasurementResult result = _measurementOperations.PerformAbsorbanceMeasurement(
                    plateType,
                    wellRange,
                    wavelength,
                    numberOfFlashes,
                    settleTime);

                // Set the output argument (measurement ID)
                outputArguments[0] = result.Id;

                return StatusCodes.Good;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnPerformAbsorbanceMeasurement: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // PerformFluorescenceMeasurement method implementation
        private ServiceResult OnPerformFluorescenceMeasurement(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Check if we have the right number of input arguments
                if (inputArguments.Count != 9)
                {
                    return StatusCodes.BadArgumentsMissing;
                }

                // Get the input arguments
                string plateType = inputArguments[0].ToString();
                string wellRange = inputArguments[1].ToString();
                int excitationWavelength = Convert.ToInt32(inputArguments[2]);
                int emissionWavelength = Convert.ToInt32(inputArguments[3]);
                int gain = Convert.ToInt32(inputArguments[4]);
                int numberOfFlashes = Convert.ToInt32(inputArguments[5]);
                int integrationTime = Convert.ToInt32(inputArguments[6]);
                int settleTime = Convert.ToInt32(inputArguments[7]);
                string readingMode = inputArguments[8].ToString();

                // Check if the device is connected
                if (!_tecan.IsConnected())
                {
                    return StatusCodes.BadDeviceFailure;
                }

                // Perform the measurement
                MeasurementResult result = _measurementOperations.PerformFluorescenceMeasurement(
                    plateType,
                    wellRange,
                    excitationWavelength,
                    emissionWavelength,
                    gain,
                    numberOfFlashes,
                    integrationTime,
                    settleTime,
                    readingMode);

                // Set the output argument (measurement ID)
                outputArguments[0] = result.Id;

                return StatusCodes.Good;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnPerformFluorescenceMeasurement: {ex.Message}");
                return new ServiceResult(ex);
            }
        }

        // PerformLuminescenceMeasurement method implementation
        private ServiceResult OnPerformLuminescenceMeasurement(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
        {
            try
            {
                // Check if we have the right number of input arguments
                if (inputArguments.Count != 5)
                {
                    return StatusCodes.BadArgumentsMissing;
                }

                // Get the input arguments
                string plateType = inputArguments[0].ToString();
                string wellRange = inputArguments[1].ToString();
                int integrationTime = Convert.ToInt32(inputArguments[2]);
                int settleTime = Convert.ToInt32(inputArguments[3]);
                string attenuation = inputArguments[4].ToString();

                // Check if the device is connected
                if (!_tecan.IsConnected())
                {
                    return StatusCodes.BadDeviceFailure;
                }

                // Perform the measurement
                MeasurementResult result = _measurementOperations.PerformLuminescenceMeasurement(
                    plateType,
                    wellRange,
                    integrationTime,
                    settleTime,
                    attenuation);

                // Set the output argument (measurement ID)
                outputArguments[0] = result.Id;

                return StatusCodes.Good;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnPerformLuminescenceMeasurement: {ex.Message}");
                return new ServiceResult(ex);
            }
        }
    }
}
