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
        arg is "read" or "write" or "scan" or "serve" or "help" or "--help" or "-h";

    /// <summary>EasyModbus prints a copyright banner to Console.Out on every ModbusClient
    /// construction. We redirect Console.Out for the entire CLI session and write our own
    /// JSON output via the saved original.</summary>
    private static TextWriter _stdout = Console.Out;

    public static int Run(string[] args)
    {
        _stdout = Console.Out;
        Console.SetOut(TextWriter.Null);
        if (args.Length == 0) { PrintUsage(); return 1; }
        try
        {
            return args[0] switch
            {
                "read"   => RunRead(args[1..]).GetAwaiter().GetResult(),
                "write"  => RunWrite(args[1..]).GetAwaiter().GetResult(),
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
  write   one-shot write of a single coil or register
  scan    sweep registers, slave IDs, or IP range; one JSON line per result
  serve   start the HTTP API (and nothing else) until SIGINT
  help    this message

COMMON OPTIONS
  --ip <addr>       slave IP address       (default 127.0.0.1)
  --port <n>        TCP port               (default 502)
  --slave <n>       Modbus unit identifier (default 1)
  --timeout <ms>    connect timeout         (default 2000)

  read | write SPECIFIC
    --function <code>  03|04|01|02 for read; 05|06 for write    (default 03 read, 06 write)
    --address <n>      starting address (wire-level / 0-indexed) (default 0)
    --amount <n>       quantity to read                          (default 10)
    --value <v>        value to write (1/0 for coil, integer for register)

  scan SPECIFIC
    --type <kind>      ip | id | registers
    --base <ip>        for ip scan (e.g. 192.168.1.0)
    --start <n>        for id/registers scan (start)
    --end <n>          for id scan (end)
    --amount <n>       for registers scan
    --function <code>  for registers scan

  serve SPECIFIC
    --http <port>      HTTP API port (default 8080)

EXAMPLES
  openpoll read --ip 127.0.0.1 --port 1502 --address 1 --amount 5 --function 03
  openpoll write --ip 127.0.0.1 --port 1502 --address 1 --value 42 --function 06
  openpoll scan --type ip --base 192.168.1.0 --port 502 --timeout 500
");
        return 0;
    }

    private static PollDefinition DefFromArgs(Args a, ModbusFunction defaultFunction = ModbusFunction.HoldingRegisters) => new()
    {
        ConnectionMode = ConnectionMode.Tcp,
        IpAddress = a.Get("ip", "127.0.0.1"),
        ServerPort = a.GetInt("port", 502),
        NodeId = a.GetInt("slave", 1),
        ConnectionTimeoutMs = a.GetInt("timeout", 2000),
        Address = a.GetInt("address", 0),
        Amount = a.GetInt("amount", 10),
        Function = ParseFunction(a.Get("function"), defaultFunction),
    };

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

        var workspace = new Workspace();
        // Seed with a default poll so /api/polls isn't empty
        workspace.AddNew(new PollDefinition { Name = "default" });

        using var host = new HttpApiHost(workspace);
        await host.StartAsync(port);
        _stdout.WriteLine($"OpenPoll HTTP API listening on http://localhost:{port}/api/polls");
        _stdout.WriteLine("Ctrl+C to stop.");

        using var stop = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        stop.Wait();

        await host.StopAsync();
        return 0;
    }
}
