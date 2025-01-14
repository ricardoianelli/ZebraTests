namespace ZebraTests
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // The serial number of the scanner you want to test. Change it to your scanner's serial number.
            var serialNumber = "24316523021017";
            
            // Instantiate a new ZebraService. This is what enables us to communicate with the barcode scanner.
            ZebraService zebraService = new ZebraService();

            // Initialize the Zebra Service.
            // This will interact with CoreScanner in your PC to communicate with barcode scanners.
            Console.WriteLine("Initializing Zebra Service");
            zebraService.Initialize();
            if (!zebraService.Initialized)
            {
                Console.WriteLine("Couldn't initialize Zebra Service!");
                return;
            }

            // Gets a list of all connected barcode scanner devices.
            var connectedDevices = zebraService.GetConnectedDevices();
            Console.WriteLine($"Connected devices: {string.Join(", ", connectedDevices)}");
            
            // Subscribe to ALL barcode read events.
            // This will catch all barcode scans from ALL scanners, not only the one we defined.
            zebraService.BarcodeRead += (sender, e) =>
            {
                Console.WriteLine($"Barcode: {e.Barcode}, Scanner: {e.ScannerSerialId}");
            };

            // Beeps the scanner twice.
            zebraService.Beep(serialNumber, 2);
            
            // Gets a new scan from our scanner.
            string scannedBarcode = await zebraService.Scan(serialNumber);
            Console.WriteLine($"Scanned barcode: {scannedBarcode}");

            // This will keep the program running, so that we could potentially ask for new scans.
            Console.WriteLine("Waiting for barcode scans. Press Ctrl+C to exit...");
            
            // Keep the program alive asynchronously.
            await WaitForExitAsync();
        }

        private static Task WaitForExitAsync()
        {
            var tcs = new TaskCompletionSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                tcs.SetResult();
            };
            return tcs.Task;
        }
    }
}