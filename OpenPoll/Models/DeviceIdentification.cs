using System.Collections.Generic;

namespace OpenPoll.Models;

/// <summary>
/// Decoded result of a Modbus FC 43 / MEI Type 14 (Read Device Identification) exchange.
/// Object IDs 0..6 are the standard set in §6.21 of the Modbus Application Protocol spec.
/// </summary>
public sealed record DeviceIdentification(
    byte ConformityLevel,
    bool MoreFollows,
    byte NextObjectId,
    IReadOnlyList<DeviceIdObject> Objects);

public sealed record DeviceIdObject(byte Id, string Value)
{
    public string Name => Id switch
    {
        0x00 => "VendorName",
        0x01 => "ProductCode",
        0x02 => "MajorMinorRevision",
        0x03 => "VendorUrl",
        0x04 => "ProductName",
        0x05 => "ModelName",
        0x06 => "UserApplicationName",
        _ => $"Object[0x{Id:X2}]",
    };
}

/// <summary>Modbus FC 43 sub-request codes ("Read Dev Id Code", spec §6.21).</summary>
public enum ReadDeviceIdCode : byte
{
    /// <summary>0x01 — Stream access, basic block (objects 0..2, mandatory).</summary>
    Basic = 0x01,
    /// <summary>0x02 — Stream access, regular block (objects 0..6).</summary>
    Regular = 0x02,
    /// <summary>0x03 — Stream access, extended block (objects 0..n, vendor-defined).</summary>
    Extended = 0x03,
    /// <summary>0x04 — Individual access of a specific object id.</summary>
    Specific = 0x04,
}
