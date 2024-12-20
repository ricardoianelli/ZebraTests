using System.Xml.Linq;
using CoreScanner;

namespace ZebraTests;

public class ZebraService
{
    public event EventHandler<BarcodeEvent> BarcodeRead;
    public string LastScannedBarcode { get; private set; } = "";
    
    private const int DefaultScanTimeout = 1000;
    
    private readonly CCoreScannerClass _scannerServices;

    private readonly Dictionary<string, int> _scannerIdBySerialNumber;

    private const int RegisterForEvents = 1001;
    private const int DevicePullTrigger = 2011;
    private const int DeviceReleaseTrigger = 2012;
    
    private const int StatusFail = 1;
    private const int StatusSuccess = 0;

    private bool _waitingForBarcodeScan = false;

    public ZebraService()
    {
        _scannerServices = new CCoreScannerClass();
        _scannerIdBySerialNumber = new Dictionary<string, int>();
    }

    public bool Initialize()
    {
        //Call Open API
        var scannerTypes = new short[1];    // Scanner Types you are interested in
        scannerTypes[0] = 1;                    // 1 for all scanner types
        short numberOfScannerTypes = 1;         // Size of the scannerTypes array 

        _scannerServices.Open(0, scannerTypes, numberOfScannerTypes, out int status);
        
        if (status == 0)
        {
            Console.WriteLine("CoreScanner service initialized.");
            RegisterForScannerEvents();
            _scannerServices.BarcodeEvent += HandleBarcodeEvent;
        }
        else
        {
            Console.WriteLine($"Failed to initialize CoreScanner service. Status: {status}");
        }

        return status == 0;
    }

    private void ParseScannersFromXml(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            foreach (var scanner in doc.Descendants("scanner"))
            {
                string? scannerIdStr = scanner.Element("scannerID")?.Value;
                string? serialNumber = scanner.Element("serialnumber")?.Value;

                if (scannerIdStr is null || serialNumber is null) continue;

                serialNumber = serialNumber.Trim();
                int scannerId = int.Parse(scannerIdStr);
                Console.WriteLine($"Scanner ID: {scannerId}, Serial: {serialNumber}");
                _scannerIdBySerialNumber[serialNumber] = Convert.ToInt32(scannerId);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error parsing scanners from XML: {e}");
            throw;
        }
    }
    
    public int GetScannerId(string serialNumber)
    {
        if (_scannerIdBySerialNumber.TryGetValue(serialNumber, out int scannerId)) return scannerId;
        
        Console.WriteLine($"Serial number {serialNumber} not found.");
        return -1;
    }

    public void ExecuteCommand(int opcode, ref string inXml, out string outXml, out int status)
    {
        try
        {
            _scannerServices.ExecCommand(opcode, ref inXml, out outXml, out status);
            if (status != 0)
            {
                Console.WriteLine($"Command failed with status: {status}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public bool DiscoverScanners()
    {
        short numberOfScanners;                         // Number of scanners expect to be used 
        int[] connectedScannerIDList = new int[255];    // List of scanner IDs to be returned 

        _scannerServices.GetScanners(out numberOfScanners, connectedScannerIDList, out string outXml, out int status);
        
        if (status == 0)
        {
            ParseScannersFromXml(outXml);
            Console.WriteLine(outXml);
        }
        else
        {
            Console.WriteLine("Error discovering scanners.");
        }

        return status == 0;
    }

    public void BeepScanner(string serialNumber, int beepPattern)
    {
        int scannerId = GetScannerId(serialNumber);
        if (scannerId == -1) return;
        
        // Let's beep the beeper
        int opcode = 2018;  // Method for Beep the beeper 
        string outXML;      // Output
        string inXML = "<inArgs>" +
                       "<scannerID>1</scannerID>" + // The scanner you need to beep
                       "<cmdArgs>" +
                       "<arg-int>3</arg-int>" + // 4 high short beep pattern
                       "</cmdArgs>" + 
                       "</inArgs>";

        _scannerServices.ExecCommand(opcode, ref inXML, out outXML, out var status);
        Console.WriteLine($"Beep status: {status}");
    }
    
    public void RequestScan(string serialNumber, int timeoutMilliseconds = DefaultScanTimeout)
    {
        int scannerId = GetScannerId(serialNumber);
        if (scannerId == -1) return;

        var inXml = $"<inArgs><scannerID>{scannerId}</scannerID></inArgs>";

        // Trigger the scanner
        ExecuteCommand(DevicePullTrigger, ref inXml, out string outXml, out int status);
        
        _waitingForBarcodeScan = true;
        
        if (status == 0)
        {
            Console.WriteLine("Scanner triggered. Waiting for barcode...");
            // Wait for the timeout
            Task.Delay(timeoutMilliseconds).ContinueWith(_ =>
            {
                if (!_waitingForBarcodeScan) return;
                
                ReleaseTrigger(scannerId);
                _waitingForBarcodeScan = false;
            });
        }
        else
        {
            Console.WriteLine($"Failed to trigger scanner. Status: {status}");
        }
    }

    private void ReleaseTrigger(int scannerId)
    {
        var inXml = $"<inArgs><scannerID>{scannerId}</scannerID></inArgs>";
        ExecuteCommand(DeviceReleaseTrigger, ref inXml, out string outXml, out int status);

        Console.WriteLine(status == 0
            ? "Scanner trigger released due to timeout."
            : $"Failed to release scanner trigger. Status: {status}");
    }

    
    private void HandleBarcodeEvent(short eventType, ref string scanData)
    {
        Console.WriteLine("BarcodeEvent triggered!");
        var scannedBarcode = "";
        var scannerSerial = "";
        
        try
        {
            var doc = XDocument.Parse(scanData);
            var datalabelHex = doc.Root?.Descendants("datalabel")?.FirstOrDefault()?.Value;
            var serialId = doc.Root?.Descendants("serialnumber")?.FirstOrDefault()?.Value;
            
            if (serialId != null)
            {
                scannerSerial = serialId.Trim();
            }

            if (!string.IsNullOrEmpty(datalabelHex))
            {
                scannedBarcode = ParseHexToAscii(datalabelHex);
                Console.WriteLine($"Scanned Barcode: {scannedBarcode}");
                _waitingForBarcodeScan = false;
            }
            else
            {
                Console.WriteLine("No barcode data received.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling barcode data: {ex.Message}");
        }

        LastScannedBarcode = scannedBarcode;
        BarcodeRead?.Invoke(this, new BarcodeEvent(scannedBarcode, scannerSerial));
    }

    private string ParseHexToAscii(string hexData)
    {
        string[] hexBytes = hexData.Replace("0x", "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var asciiChars = new char[hexBytes.Length];

        for (var i = 0; i < hexBytes.Length; i++)
        {
            asciiChars[i] = (char)Convert.ToByte(hexBytes[i], 16);
        }

        return new string(asciiChars);
    }

    
    private string GetInXml(int numberOfParameters, string parameters)
    {
        return "<inArgs>"
               + " <cmdArgs>"
               + "<arg-int>" + numberOfParameters + "</arg-int>" //number of parameters
               + "<arg-int>" + parameters + "</arg-int>"
               + " </cmdArgs>"
               + "</inArgs>";
    }
    
    private void RegisterForScannerEvents()
    {
        const int subscribeBarcode = 1;
        string inXml = GetInXml(1, subscribeBarcode.ToString());

        ExecuteCommand(RegisterForEvents, ref inXml, out _, out var status);
        Console.WriteLine($"Register for events status: {status}");
    }

}