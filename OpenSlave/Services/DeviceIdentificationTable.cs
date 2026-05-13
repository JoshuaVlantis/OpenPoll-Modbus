using System.Text;

namespace OpenSlave.Services;

/// <summary>
/// Slave-side store for the seven standard Modbus FC 43 device-identification objects.
/// Defaults brand the slave as OpenSlave; callers can overwrite any slot at runtime.
/// </summary>
public sealed class DeviceIdentificationTable
{
    public string VendorName { get; set; } = "OpenPoll Project";
    public string ProductCode { get; set; } = "OpenSlave";
    public string MajorMinorRevision { get; set; } = "2.1.0";
    public string VendorUrl { get; set; } = "https://github.com/JoshuaVlantis/OpenPoll-Modbus";
    public string ProductName { get; set; } = "OpenSlave Modbus TCP Slave";
    public string ModelName { get; set; } = "OpenSlave";
    public string UserApplicationName { get; set; } = "OpenSlave";

    public byte[] GetUtf8(byte objectId) => objectId switch
    {
        0x00 => Encoding.UTF8.GetBytes(VendorName ?? ""),
        0x01 => Encoding.UTF8.GetBytes(ProductCode ?? ""),
        0x02 => Encoding.UTF8.GetBytes(MajorMinorRevision ?? ""),
        0x03 => Encoding.UTF8.GetBytes(VendorUrl ?? ""),
        0x04 => Encoding.UTF8.GetBytes(ProductName ?? ""),
        0x05 => Encoding.UTF8.GetBytes(ModelName ?? ""),
        0x06 => Encoding.UTF8.GetBytes(UserApplicationName ?? ""),
        _    => System.Array.Empty<byte>(),
    };
}
