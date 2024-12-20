using System.Xml.Linq;
using CoreScanner;

namespace ZebraTests;

public class ZebraService : IDisposable
{
    public event EventHandler<BarcodeEvent> BarcodeRead;
    
    public bool Initialized { get; private set; }

    
    private const int DefaultScanTimeoutMs = 1000;

    private const int OpcodeRegisterForEvents = 1001;
    private const int OpcodeDevicePullTrigger = 2011;
    private const int OpcodeDeviceReleaseTrigger = 2012;
    private const int OpcodeBeep = 2018;
    private const int OpcodeStatusCheck = 5506;
    
    private const int StatusSuccess = 0;

    private readonly CCoreScannerClass _scannerServices;
    private readonly Dictionary<string, int> _scannerIdBySerialNumber;
    
    private bool _waitingForBarcodeScan = false;

    public ZebraService()
    {
        _scannerServices = new CCoreScannerClass();
        _scannerIdBySerialNumber = new Dictionary<string, int>();
    }
    
    public void Dispose()
    {
        _scannerServices.BarcodeEvent -= HandleBarcodeEvent;
        _scannerServices.Close(0, out int status);
    }
    
    public void Initialize()
    {
        if (ConnectToService() && DiscoverScanners() && RegisterForScannerEvents())
        {
            Initialized = true;
            Console.WriteLine("CoreScanner service initialized.");
        }
        else
        {
            Initialized = false;
        }
    }
    
    public bool CheckScannerHealth(string serialNumber)
    {
        int scannerId = GetScannerId(serialNumber);
        if (scannerId == -1)
        {
            Console.WriteLine($"Health Check Failed: Scanner with serial number {serialNumber} not found.");
            return false;
        }

        var inXml = $"<inArgs><scannerID>{scannerId}</scannerID></inArgs>";
        ExecuteCommand(OpcodeStatusCheck, ref inXml, out string outXml, out int status);

        if (status == StatusSuccess)
        {
            Console.WriteLine($"Scanner {serialNumber} is healthy.");
            return true;
        }

        Console.WriteLine($"Health Check Failed: Scanner {serialNumber} responded with status {status}.");
        return false;
    }
    
    public void BeepScanner(string serialNumber, int beepPattern)
    {
        int scannerId = GetScannerId(serialNumber);
        if (scannerId == -1) return;

        string inXml = "<inArgs>" +
                       "<scannerID>1</scannerID>" + // The scanner you need to beep
                       "<cmdArgs>" +
                       $"<arg-int>{beepPattern}</arg-int>" + // 4 high short beep pattern
                       "</cmdArgs>" + 
                       "</inArgs>";

        _scannerServices.ExecCommand(OpcodeBeep, ref inXml, out string outXml, out var status);
        Console.WriteLine($"Beep status: {status}");
    }
    
    public void RequestScan(string serialNumber, int timeoutMilliseconds = DefaultScanTimeoutMs)
    {
        int scannerId = GetScannerId(serialNumber);
        if (scannerId == -1) return;

        var inXml = $"<inArgs><scannerID>{scannerId}</scannerID></inArgs>";
        
        ExecuteCommand(OpcodeDevicePullTrigger, ref inXml, out string outXml, out int status);
        
        _waitingForBarcodeScan = true;
        
        if (status == StatusSuccess)
        {
            Console.WriteLine("Scanner triggered. Waiting for barcode...");
            // Wait for the timeout
            Task.Delay(timeoutMilliseconds).ContinueWith(_ =>
            {
                if (!_waitingForBarcodeScan) return;
                
                ReleaseTrigger(scannerId);
                _waitingForBarcodeScan = false;
            });
            return;
        }

        Console.WriteLine($"Failed to trigger scanner. Status: {status}");
    }
    
    private bool ConnectToService()
    {
        var scannerTypes = new short[] {1};
        var numberOfScannerTypes = (short) scannerTypes.Length;

        _scannerServices.Open(0, scannerTypes, numberOfScannerTypes, out int status);
        if (status == StatusSuccess) return true;
        
        Console.WriteLine($"Failed to initialize CoreScanner service. Status: {status}");
        return false;
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

    private int GetScannerId(string serialNumber)
    {
        if (_scannerIdBySerialNumber.TryGetValue(serialNumber, out int scannerId)) return scannerId;
        
        Console.WriteLine($"Serial number {serialNumber} not found.");
        return -1;
    }

    private void ExecuteCommand(int opcode, ref string inXml, out string outXml, out int status)
    {
        try
        {
            _scannerServices.ExecCommand(opcode, ref inXml, out outXml, out status);
            if (status != StatusSuccess)
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

    private bool DiscoverScanners()
    {
        var connectedScannerIdList = new int[255];    // List of scanner IDs to be returned 

        _scannerServices.GetScanners(out short numberOfScanners, connectedScannerIdList, out string outXml, out int status);
        
        if (status == StatusSuccess)
        {
            ParseScannersFromXml(outXml);
            Console.WriteLine(outXml);
            return true;
        }

        Console.WriteLine("Error discovering scanners.");
        return false;
    }

    private void ReleaseTrigger(int scannerId)
    {
        var inXml = $"<inArgs><scannerID>{scannerId}</scannerID></inArgs>";
        ExecuteCommand(OpcodeDeviceReleaseTrigger, ref inXml, out string outXml, out int status);

        Console.WriteLine(status == StatusSuccess
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
    
    private bool RegisterForScannerEvents()
    {
        const int subscribeBarcode = 1;
        string inXml = GetInXml(1, subscribeBarcode.ToString());

        ExecuteCommand(OpcodeRegisterForEvents, ref inXml, out _, out var status);
        if (status != StatusSuccess) return false;
        
        _scannerServices.BarcodeEvent += HandleBarcodeEvent;
        return true;

    }
}