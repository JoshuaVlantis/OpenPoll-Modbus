using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using OpenPoll.Models;
using OpenPoll.Services;

namespace OpenPoll;

/// <summary>
/// Command-line interface for OpenPoll. Lets the entire client be driven from terminal
/// for scripting, CI, and headless testing. JSON output by default for machine parsing.
///
/// Subcommands: read | write | scan | serve | help
/// </summary>
public static class Cli
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        WriteIndented = false,
    };

    public static bool IsKnownCommand(string? arg) =>
        arg is "read" or "write" or "rw" or "mask" or "devid" or "diag" or "ec" or "srvid"
        or "scan" or "serve" or "help" or "--help" or "-h";

    private static TextWriter _stdout = Console.Out;

    public static int Run(string[] args)
    {
        _stdout = Console.Out;
        if (args.Length == 0) { PrintUsage(); return 1; }
        try
        {
            return args[0] switch
            {
                "read"   => RunRead(args[1..]).GetAwaiter().GetResult(),
                "write"  => RunWrite(args[1..]).GetAwaiter().GetResult(),
                "rw"     => RunReadWrite(args[1..]).GetAwaiter().GetResult(),
                "mask"   => RunMask(args[1..]).GetAwaiter().GetResult(),
                "devid"  => RunDeviceId(args[1..]).GetAwaiter().GetResult(),
                "diag"   => RunDiag(args[1..]).GetAwaiter().GetResult(),
                "ec"     => RunEventCounter(args[1..]).GetAwaiter().GetResult(),
                "srvid"  => RunServerId(args[1..]).GetAwaiter().GetResult(),
                "scan"   => RunScan(args[1..]).GetAwaiter().GetResult(),
                "serve"  => RunServe(args[1..]).GetAwaiter().GetResult(),
                "help" or "--help" or "-h" => PrintUsage(),
                _ => Fail($"unknown command: {args[0]}"),
            };
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    // ─────────── helpers ────────────────────────────────────────────────

    private sealed class Args
    {
        private readonly Dictionary<string, string> _kv = new(StringComparer.OrdinalIgnoreCase);
        public Args(string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                var t = tokens[i];
                if (!t.StartsWith("--")) continue;
                var key = t[2..];
                var val = i + 1 < tokens.Length && !tokens[i + 1].StartsWith("--") ? tokens[++i] : "true";
                _kv[key] = val;
            }
        }
        public string? Get(string k) => _kv.TryGetValue(k, out var v) ? v : null;
        public string Get(string k, string fallback) => _kv.TryGetValue(k, out var v) ? v : fallback;
        public int GetInt(string k, int fallback) =>
            _kv.TryGetValue(k, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : fallback;
        public bool Has(string k) => _kv.ContainsKey(k);
        public T GetEnum<T>(string k, T fallback) where T : struct =>
            _kv.TryGetValue(k, out var v) && Enum.TryParse<T>(v, true, out var p) ? p : fallback;
    }

    private static int Fail(string msg)
    {
        Console.Error.WriteLine($"openpoll: {msg}");
        return 1;
    }

    private static void Emit(object payload) =>
        _stdout.WriteLine(JsonSerializer.Serialize(payload, Json));

    private static int PrintUsage()
    {
        _stdout.WriteLine(@"OpenPoll — Modbus master CLI

USAGE
  openpoll <command> [options]

COMMANDS
  read    one-shot read of N registers/coils, JSON to stdout
  write   one-shot write of a single coil or register (FC 05/06/15/16)
  rw      atomic FC 23 Read/Write Multiple Registers
  mask    FC 22 Mask Write Register: result = (cur AND and) OR (or AND NOT and)
  devid   FC 43 Read Device Identification (vendor, product, revision, ...)
  diag    FC 08 Diagnostics (sub-function 0 = echo round-trip)
  ec      FC 11 Get Comm Event Counter
  srvid   FC 17 Report Server ID
  scan    sweep registers, slave IDs, or IP range; one JSON line per result
  serve   start the HTTP API (and nothing else) until SIGINT
  help    this message

TRANSPORT (any subcommand that talks Modbus)
  TCP (default):
    --ip <addr>           slave IP address              (default 127.0.0.1)
    --port <n>            TCP port                       (default 502)

  Serial RTU (RS-232/RS-485, USB serial converters):
    --serial <port>       e.g. /dev/ttyUSB0 or COM3
    --baud <n>            300|600|1200|2400|4800|9600|14400|19200|38400|
                          57600|115200|...               (default 9600)
    --parity <p>          none|even|odd|mark|space       (default none)
    --stopbits <p>        one|two|onepointfive           (default one)

  Modbus over UDP (same MBAP frame as TCP, one frame per datagram):
    --udp <port>          target UDP port on --ip
  RTU framing over TCP (gateway boxes):
    --rtu-over-tcp <port> target TCP port on --ip
  Modbus ASCII over TCP (':' + hex + LRC + CRLF):
    --ascii <port>        target TCP port on --ip
  Modbus TCP secured with TLS (server cert NOT validated by default):
    --tls <port>          target TLS port on --ip

  Common:
    --slave <n>           Modbus unit identifier         (default 1)
    --timeout <ms>        connect timeout                (default 2000)
    --response-timeout <ms>  per-request response timeout (default = --timeout)
    --retries <n>         retry attempts after a failure (default 0)

  read | write SPECIFIC
    --function <code>  read: 01|02|03|04 · write: 05|06|15|16  (default 03 / 06)
    --address <n>      starting address (wire-level / 0-indexed) (default 0)
    --amount <n>       quantity to read                          (default 10)
    --value <v>        write value: ""1""/""0"" for coil, integer for reg, ""1,2,3"" for multi

  rw SPECIFIC (FC 23)
    --read-address <n>     start address to read
    --read-amount <n>      quantity to read
    --address <n>          start address to write
    --value <list>         comma-separated registers to write

  mask SPECIFIC (FC 22)
    --address <n>          register address
    --and-mask <hex|int>   AND mask (e.g. 0x00FF or 255)
    --or-mask  <hex|int>   OR  mask

  devid SPECIFIC (FC 43)
    --code <1|2|3|4>       1 basic (default) · 2 regular · 3 extended · 4 specific
    --object <id>          starting object id (default 0; required for --code 4)

  scan SPECIFIC
    --type <kind>      ip | id | registers
    --base <ip>        for ip scan (e.g. 192.168.1.0)
    --start <n>        for id/registers scan (start)
    --end <n>          for id scan (end)
    --amount <n>       for registers scan
    --function <code>  for registers scan

  serve SPECIFIC
    --http <port>      HTTP API port (default 8080)
    --http-token <t>   require `Authorization: Bearer <t>` on /api/* (also reads
                       OPENPOLL_HTTP_TOKEN env var; empty / unset = no auth)

EXAMPLES
  openpoll read --ip 127.0.0.1 --port 1502 --address 1 --amount 5 --function 03
  openpoll write --ip 127.0.0.1 --port 1502 --address 1 --value 42 --function 06
  openpoll write --serial /dev/ttyUSB0 --baud 19200 --parity even --address 0 --value 99
  openpoll mask --ip 127.0.0.1 --port 1502 --address 0 --and-mask 0x00FF --or-mask 0x1100
  openpoll rw   --ip 127.0.0.1 --port 1502 --read-address 0 --read-amount 4 --address 10 --value 7,8,9
  openpoll devid --ip 127.0.0.1 --port 1502 --code 2
  openpoll scan --type ip --base 192.168.1.0 --port 502 --timeout 500
");
        return 0;
    }

    private static PollDefinition DefFromArgs(Args a, ModbusFunction defaultFunction = ModbusFunction.HoldingRegisters)
    {
        var serialPort = a.Get("serial");
        var connectTimeout = a.GetInt("timeout", 2000);
        var mode = serialPort is not null ? ConnectionMode.Serial
                 : a.Has("udp")          ? ConnectionMode.Udp
                 : a.Has("rtu-over-tcp") ? ConnectionMode.RtuOverTcp
                 : a.Has("ascii")        ? ConnectionMode.AsciiOverTcp
                 : a.Has("tls")          ? ConnectionMode.TcpTls
                 : ConnectionMode.Tcp;
        // Some transport flags carry the port; if the user passed --udp 5020 use that as the port.
        int defaultPort = mode == ConnectionMode.Udp ? a.GetInt("udp", 502)
                        : mode == ConnectionMode.RtuOverTcp ? a.GetInt("rtu-over-tcp", 502)
                        : mode == ConnectionMode.AsciiOverTcp ? a.GetInt("ascii", 502)
                        : mode == ConnectionMode.TcpTls ? a.GetInt("tls", 802)
                        : a.GetInt("port", 502);
        var def = new PollDefinition
        {
            ConnectionMode = mode,
            IpAddress = a.Get("ip", "127.0.0.1"),
            ServerPort = defaultPort,
            SerialPortName = serialPort ?? "",
            BaudRate = a.GetInt("baud", 9600),
            Parity = a.GetEnum("parity", System.IO.Ports.Parity.None),
            StopBits = a.GetEnum("stopbits", System.IO.Ports.StopBits.One),
            NodeId = a.GetInt("slave", 1),
            ConnectionTimeoutMs = connectTimeout,
            ResponseTimeoutMs = a.GetInt("response-timeout", connectTimeout),
            Retries = a.GetInt("retries", 0),
            Address = a.GetInt("address", 0),
            Amount = a.GetInt("amount", 10),
            Function = ParseFunction(a.Get("function"), defaultFunction),
        };
        return def;
    }

    private static ModbusFunction ParseFunction(string? raw, ModbusFunction fallback)
    {
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        // Accept numeric codes (1, 2, 3, 4, 5, 6) and names ("HoldingRegisters")
        if (int.TryParse(raw, out var n))
        {
            return n switch
            {
                1 => ModbusFunction.Coils,
                2 => ModbusFunction.DiscreteInputs,
                3 => ModbusFunction.HoldingRegisters,
                4 => ModbusFunction.InputRegisters,
                5 => ModbusFunction.Coils,             // write single coil — same data type
                6 => ModbusFunction.HoldingRegisters,  // write single register
                15 => ModbusFunction.Coils,
                16 => ModbusFunction.HoldingRegisters,
                _ => fallback,
            };
        }
        if (Enum.TryParse<ModbusFunction>(raw, true, out var f)) return f;
        return fallback;
    }

    // ─────────── read ───────────────────────────────────────────────────

    private static Task<int> RunRead(string[] argv)
    {
        var a = new Args(argv);
        var def = DefFromArgs(a);

        using var session = new ModbusSession();
        var connect = session.Connect(def);
        if (!connect.Success)
        {
            Emit(new { ok = false, stage = "connect", error = connect.Error });
            return Task.FromResult(1);
        }

        switch (def.Function)
        {
            case ModbusFunction.Coils:
                var rc = session.ReadCoils(def.Address, def.Amount);
                Emit(new { ok = rc.Success, function = def.Function, address = def.Address, amount = def.Amount, values = rc.Value, error = rc.Error });
                return Task.FromResult(rc.Success ? 0 : 1);
            case ModbusFunction.DiscreteInputs:
                var rd = session.ReadDiscreteInputs(def.Address, def.Amount);
                Emit(new { ok = rd.Success, function = def.Function, address = def.Address, amount = def.Amount, values = rd.Value, error = rd.Error });
                return Task.FromResult(rd.Success ? 0 : 1);
            case ModbusFunction.HoldingRegisters:
                var rh = session.ReadHoldingRegisters(def.Address, def.Amount);
                Emit(new { ok = rh.Success, function = def.Function, address = def.Address, amount = def.Amount, values = rh.Value, error = rh.Error });
                return Task.FromResult(rh.Success ? 0 : 1);
            case ModbusFunction.InputRegisters:
                var ri = session.ReadInputRegisters(def.Address, def.Amount);
                Emit(new { ok = ri.Success, function = def.Function, address = def.Address, amount = def.Amount, values = ri.Value, error = ri.Error });
                return Task.FromResult(ri.Success ? 0 : 1);
        }
        return Task.FromResult(Fail("unknown function for read"));
    }

    // ─────────── write ──────────────────────────────────────────────────

    private static Task<int> RunWrite(string[] argv)
    {
        var a = new Args(argv);
        var def = DefFromArgs(a, ModbusFunction.HoldingRegisters);
        var raw = a.Get("function");
        var fnCode = int.TryParse(raw, out var n) ? n : 0;
        var addr = a.GetInt("address", 0);
        var value = a.Get("value", "0");

        using var session = new ModbusSession();
        var connect = session.Connect(def);
        if (!connect.Success)
        {
            Emit(new { ok = false, stage = "connect", error = connect.Error });
            return Task.FromResult(1);
        }

        ModbusResult result;
        if (fnCode == 5 || (def.Function == ModbusFunction.Coils && fnCode != 15))
        {
            var coil = value is "1" or "true" or "on";
            result = session.WriteSingleCoil(addr, coil);
            Emit(new { ok = result.Success, function = "WriteSingleCoil", address = addr, value = coil, error = result.Error });
        }
        else if (fnCode == 15)
        {
            var bools = value.Split(',').Select(s => s.Trim() is "1" or "true" or "on").ToArray();
            result = session.WriteMultipleCoils(addr, bools);
            Emit(new { ok = result.Success, function = "WriteMultipleCoils", address = addr, count = bools.Length, error = result.Error });
        }
        else if (fnCode == 16)
        {
            var ints = value.Split(',').Select(s => int.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
            result = session.WriteMultipleRegisters(addr, ints);
            Emit(new { ok = result.Success, function = "WriteMultipleRegisters", address = addr, count = ints.Length, error = result.Error });
        }
        else
        {
            var reg = int.Parse(value, CultureInfo.InvariantCulture);
            result = session.WriteSingleRegister(addr, reg);
            Emit(new { ok = result.Success, function = "WriteSingleRegister", address = addr, value = reg, error = result.Error });
        }
        return Task.FromResult(result.Success ? 0 : 1);
    }

    // ─────────── mask (FC 22) ───────────────────────────────────────────

    private static Task<int> RunMask(string[] argv)
    {
        var a = new Args(argv);
        var def = DefFromArgs(a);
        var addr = a.GetInt("address", 0);
        var andMask = ParseUshort(a.Get("and-mask", "0xFFFF"));
        var orMask  = ParseUshort(a.Get("or-mask",  "0x0000"));

        using var session = new ModbusSession();
        var connect = session.Connect(def);
        if (!connect.Success)
        {
            Emit(new { ok = false, stage = "connect", error = connect.Error });
            return Task.FromResult(1);
        }

        var result = session.MaskWriteRegister(addr, andMask, orMask);
        Emit(new
        {
            ok = result.Success,
            function = "MaskWriteRegister",
            address = addr,
            andMask = $"0x{andMask:X4}",
            orMask  = $"0x{orMask:X4}",
            error = result.Error,
        });
        return Task.FromResult(result.Success ? 0 : 1);
    }

    // ─────────── rw (FC 23) ─────────────────────────────────────────────

    private static Task<int> RunReadWrite(string[] argv)
    {
        var a = new Args(argv);
        var def = DefFromArgs(a);
        var readAddr = a.GetInt("read-address", 0);
        var readQty  = a.GetInt("read-amount", 1);
        var writeAddr = a.GetInt("address", 0);
        var values = (a.Get("value") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();

        using var session = new ModbusSession();
        var connect = session.Connect(def);
        if (!connect.Success)
        {
            Emit(new { ok = false, stage = "connect", error = connect.Error });
            return Task.FromResult(1);
        }

        var result = session.ReadWriteMultipleRegisters(writeAddr, values, readAddr, readQty);
        Emit(new
        {
            ok = result.Success,
            function = "ReadWriteMultipleRegisters",
            readAddress = readAddr,
            readAmount = readQty,
            writeAddress = writeAddr,
            written = values.Length,
            values = result.Value,
            error = result.Error,
        });
        return Task.FromResult(result.Success ? 0 : 1);
    }

    // ─────── diag / ec / srvid (FC 08 / 11 / 17) ────────────────────────

    private static Task<int> RunDiag(string[] argv)
    {
        var a = new Args(argv);
        var def = DefFromArgs(a);
        ushort sub  = (ushort)a.GetInt("sub", 0);
        ushort data = (ushort)a.GetInt("data", 0);
        using var s = new ModbusSession();
        var c = s.Connect(def);
        if (!c.Success) { Emit(new { ok = false, stage = "connect", error = c.Error }); return Task.FromResult(1); }
        var r = s.Diagnostic(sub, data);
        Emit(new { ok = r.Success, function = "Diagnostic", subFunction = $"0x{sub:X4}", echo = r.Success ? $"0x{r.Value:X4}" : null, error = r.Error });
        return Task.FromResult(r.Success ? 0 : 1);
    }

    private static Task<int> RunEventCounter(string[] argv)
    {
        var a = new Args(argv);
        var def = DefFromArgs(a);
        using var s = new ModbusSession();
        var c = s.Connect(def);
        if (!c.Success) { Emit(new { ok = false, stage = "connect", error = c.Error }); return Task.FromResult(1); }
        var r = s.GetCommEventCounter();
        Emit(new { ok = r.Success, function = "GetCommEventCounter", status = r.Success ? $"0x{r.Value.Status:X4}" : null, count = r.Success ? r.Value.Count : 0, error = r.Error });
        return Task.FromResult(r.Success ? 0 : 1);
    }

    private static Task<int> RunServerId(string[] argv)
    {
        var a = new Args(argv);
        var def = DefFromArgs(a);
        using var s = new ModbusSession();
        var c = s.Connect(def);
        if (!c.Success) { Emit(new { ok = false, stage = "connect", error = c.Error }); return Task.FromResult(1); }
        var r = s.ReportServerId();
        Emit(new { ok = r.Success, function = "ReportServerId", id = r.Success ? r.Value.Id : null, runStatus = r.Success ? r.Value.RunStatus : false, error = r.Error });
        return Task.FromResult(r.Success ? 0 : 1);
    }

    // ─────── devid (FC 43) ───────────────────────────────────────────────

    private static Task<int> RunDeviceId(string[] argv)
    {
        var a = new Args(argv);
        var def = DefFromArgs(a);
        var code = (ReadDeviceIdCode)a.GetInt("code", (int)ReadDeviceIdCode.Basic);
        var objectId = (byte)a.GetInt("object", 0);

        using var session = new ModbusSession();
        var connect = session.Connect(def);
        if (!connect.Success)
        {
            Emit(new { ok = false, stage = "connect", error = connect.Error });
            return Task.FromResult(1);
        }

        var result = session.ReadDeviceIdentification(code, objectId);
        if (!result.Success)
        {
            Emit(new { ok = false, function = "ReadDeviceIdentification", code = code.ToString(), error = result.Error });
            return Task.FromResult(1);
        }
        var di = result.Value!;
        Emit(new
        {
            ok = true,
            function = "ReadDeviceIdentification",
            code = code.ToString(),
            conformity = $"0x{di.ConformityLevel:X2}",
            moreFollows = di.MoreFollows,
            nextObjectId = di.NextObjectId,
            objects = di.Objects.Select(o => new { id = o.Id, name = o.Name, value = o.Value }).ToArray(),
        });
        return Task.FromResult(0);
    }

    private static ushort ParseUshort(string raw)
    {
        var s = raw.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToUInt16(s[2..], 16);
        return ushort.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    // ─────────── scan ───────────────────────────────────────────────────

    private static async Task<int> RunScan(string[] argv)
    {
        var a = new Args(argv);
        var type = a.Get("type", "ip");
        return type switch
        {
            "ip" => await ScanIps(a),
            "id" => await ScanIds(a),
            "registers" or "reg" => await ScanRegisters(a),
            _ => Fail($"unknown scan type: {type}"),
        };
    }

    private static Task<int> ScanIps(Args a)
    {
        var basePart = a.Get("base", "127.0.0.1");
        var port = a.GetInt("port", 502);
        var timeout = a.GetInt("timeout", 500);

        var dot = basePart.LastIndexOf('.');
        if (dot < 0) return Task.FromResult(Fail("--base must contain dots, e.g. 192.168.1.0"));
        var prefix = basePart[..(dot + 1)];

        for (int last = 1; last < 255; last++)
        {
            var ip = prefix + last;
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(ip, port);
            var done = connectTask.Wait(timeout);
            var responding = done && client.Connected;
            Emit(new { ip, port, responding });
        }
        return Task.FromResult(0);
    }

    private static Task<int> ScanIds(Args a)
    {
        var ip = a.Get("ip", "127.0.0.1");
        var port = a.GetInt("port", 502);
        var lo = a.GetInt("start", 1);
        var hi = a.GetInt("end", 247);
        var timeout = a.GetInt("timeout", 500);

        var def = new PollDefinition
        {
            ConnectionMode = ConnectionMode.Tcp,
            IpAddress = ip, ServerPort = port,
            ConnectionTimeoutMs = timeout, NodeId = lo,
        };

        using var session = new ModbusSession();
        var c = session.Connect(def);
        if (!c.Success) { Emit(new { ok = false, stage = "connect", error = c.Error }); return Task.FromResult(1); }

        for (int id = lo; id <= hi; id++)
        {
            def.NodeId = id;
            session.Connect(def);
            var probe = session.ReadHoldingRegisters(0, 1);
            var found = probe.Success || probe.Error == "Illegal data address";
            Emit(new { id, found, error = found ? null : probe.Error });
        }
        return Task.FromResult(0);
    }

    private static Task<int> ScanRegisters(Args a)
    {
        var def = DefFromArgs(a);
        var startAddr = def.Address;
        var amount = def.Amount;

        using var session = new ModbusSession();
        var c = session.Connect(def);
        if (!c.Success) { Emit(new { ok = false, stage = "connect", error = c.Error }); return Task.FromResult(1); }

        for (int i = 0; i < amount; i++)
        {
            var addr = startAddr + i;
            var r = def.Function switch
            {
                ModbusFunction.Coils            => (object?)session.ReadCoils(addr, 1),
                ModbusFunction.DiscreteInputs   => session.ReadDiscreteInputs(addr, 1),
                ModbusFunction.HoldingRegisters => session.ReadHoldingRegisters(addr, 1),
                ModbusFunction.InputRegisters   => session.ReadInputRegisters(addr, 1),
                _ => null,
            };
            switch (r)
            {
                case ModbusResult<bool[]> b:
                    Emit(new { addr, ok = b.Success, value = b.Value?[0], error = b.Error });
                    break;
                case ModbusResult<int[]> ints:
                    Emit(new { addr, ok = ints.Success, value = ints.Value?[0], error = ints.Error });
                    break;
            }
        }
        return Task.FromResult(0);
    }

    // ─────────── serve ──────────────────────────────────────────────────

    private static async Task<int> RunServe(string[] argv)
    {
        var a = new Args(argv);
        var port = a.GetInt("http", 8080);
        var token = a.Get("http-token") ?? Environment.GetEnvironmentVariable("OPENPOLL_HTTP_TOKEN");

        var workspace = new Workspace();
        // Seed with a default poll so /api/polls isn't empty
        workspace.AddNew(new PollDefinition { Name = "default" });

        using var host = new HttpApiHost(workspace) { AuthToken = token };
        await host.StartAsync(port);
        _stdout.WriteLine($"OpenPoll HTTP API listening on http://localhost:{port}/api/polls");
        if (!string.IsNullOrEmpty(token))
            _stdout.WriteLine("Bearer auth enabled — requests must include `Authorization: Bearer <token>` or `?token=...`.");
        _stdout.WriteLine("Ctrl+C to stop.");

        using var stop = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        stop.Wait();

        await host.StopAsync();
        return 0;
    }
}
