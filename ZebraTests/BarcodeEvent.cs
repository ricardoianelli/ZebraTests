namespace ZebraTests;

public record BarcodeEvent(string Barcode, string ScannerSerialId)
{
    public string Barcode { get; set; } = Barcode;
    public string ScannerSerialId { get; set; } = ScannerSerialId;
}