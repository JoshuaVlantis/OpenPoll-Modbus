using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenPoll.Models;

namespace OpenPoll.Services;

/// <summary>
/// Convert an OpenPoll <see cref="PollDefinition"/> into a minimal OpenSlave workspace that
/// matches what the poll was reading: same TCP port, same slave id, same address range, zero
/// seed values. Useful when you want to spin up a slave simulator that talks the same shape as
/// a known field device.
/// </summary>
public static class PollToSlaveExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Export(string path, PollDefinition poll)
    {
        var workspace = new
        {
            schema = 1,
            generator = "OpenPoll → OpenSlave exporter",
            createdAt = System.DateTimeOffset.UtcNow.ToString("o"),
            definition = new
            {
                name = string.IsNullOrWhiteSpace(poll.Name) ? "Slave" : poll.Name,
                port = poll.ServerPort > 1024 ? poll.ServerPort : 1502,
                slaveId = poll.NodeId,
                startAddress = poll.Address,
                quantity = poll.Amount,
                addressBase = poll.DisplayOneIndexed ? "One" : "Zero",
                ignoreUnitId = false,
                errorSimulation = new { responseDelayMs = 0, skipResponses = false, returnExceptionBusy = false },
            },
            tables = new
            {
                coils = System.Array.Empty<object>(),
                discreteInputs = System.Array.Empty<object>(),
                holdingRegisters = System.Array.Empty<object>(),
                inputRegisters = System.Array.Empty<object>(),
            },
        };
        File.WriteAllText(path, JsonSerializer.Serialize(workspace, Options));
    }
}
