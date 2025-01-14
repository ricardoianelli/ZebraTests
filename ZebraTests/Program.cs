namespace ZebraTests
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var serialNumber = "24316523021017";
            ZebraService zebraService = new ZebraService();

            zebraService.Initialize();
            if (!zebraService.Initialized)
            {
                Console.WriteLine("Couldn't initialize Zebra Service!");
                return;
            }

            var connectedDevices = zebraService.GetConnectedDevices();
            Console.WriteLine($"Connected devices: {string.Join(", ", connectedDevices)}");
            
            zebraService.BarcodeRead += (sender, e) =>
            {
                Console.WriteLine($"Barcode: {e.Barcode}, Scanner: {e.ScannerSerialId}");
            };

            zebraService.Beep(serialNumber, 2);
            zebraService.RequestScan(serialNumber);

            Console.WriteLine("Waiting for barcode scans. Press Ctrl+C to exit...");
            
            // Keep the program alive asynchronously
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