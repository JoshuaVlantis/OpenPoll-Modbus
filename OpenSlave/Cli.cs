using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using OpenSlave.Models;
using OpenSlave.Services;

namespace OpenSlave;

/// <summary>
/// Headless slave CLI — used by CI containers, scripts and the docker functional test.
/// All flags from the GUI are addressable here so a workspace JSON or a one-liner can
/// reproduce the same behaviour.
/// </summary>
public static class Cli
{
    public static bool IsKnownCommand(string? a) =>
        a is "run" or "help" or "--help" or "-h";

    private static TextWriter _stdout = Console.Out;

    public static int Run(string[] args)
    {
        _stdout = Console.Out;
        Console.SetOut(TextWriter.Null);
        try
        {
            if (args.Length == 0) { PrintUsage(); return 1; }
            return args[0] switch
            {
                "run" => RunServer(args[1..]),
                "help" or "--help" or "-h" => PrintUsage(),
                _ => Fail($"unknown command: {args[0]}"),
            };
        }
        finally { Console.SetOut(_stdout); }
    }

    private static int Fail(string m)
    {
        Console.Error.WriteLine("openslave: " + m);
        return 1;
    }

    private static int PrintUsage()
    {
        _stdout.WriteLine(@"OpenSlave — Modbus slave simulator CLI

USAGE
  openslave run [options]

CORE OPTIONS
  --port <n>             TCP port to listen on               (default 1502)
  --slave <n>            unit identifier the slave answers   (default 1)
  --ignore-unit-id       accept any unit ID

SERIAL RTU (coexists with TCP — omit --port to skip TCP)
  --serial <port>        device path, e.g. /dev/ttyUSB0 or COM3
  --baud <n>             baud rate                            (default 9600)
  --parity <p>           none|even|odd|mark|space             (default none)
  --stopbits <p>         one|two|onepointfive                 (default one)

UDP (coexists with TCP and serial)
  --udp <port>           UDP port for Modbus-over-UDP datagrams

ADDITIONAL TCP TRANSPORTS
  --rtu-over-tcp <port>  TCP port serving RTU framing (no MBAP, CRC-16 check)
  --ascii <port>         TCP port serving Modbus ASCII (':' + hex + LRC + CRLF)
  --tls <port>           TCP port serving Modbus over TLS (self-signed cert auto-generated)

ADDITIONAL UDP / SERIAL TRANSPORTS
  --rtu-over-udp <port>  UDP port carrying RTU framing
  --ascii-over-udp <port> UDP port carrying ASCII framing
  --ascii-serial <port>   serial device serving Modbus ASCII (7-bit, ':' framing)

DATA SEEDING
  --hr <pairs>           seed holding registers, e.g. --hr 1=100,2=200
  --coil <pairs>         seed coils, e.g. --coil 1=1,3=1
  --di <pairs>           seed discrete inputs
  --ir <pairs>           seed input registers

ERROR SIMULATION
  --response-delay <ms>  delay every response by N milliseconds
  --skip-responses       silently drop 1-in-10 responses
  --exception-busy       reply to every request with exception 06 (Slave Busy)

WORKSPACE
  --config <path>        load a .openslave workspace before applying flags

OUTPUT
  --tick                 pretty-print client connect/changes to stdout
  --quiet                suppress all stdout

EXAMPLES
  openslave run --port 1502 --slave 7
  openslave run --port 1502 --hr 0=100,1=200,2=300 --coil 0=1,2=1
  openslave run --port 1502 --response-delay 250 --skip-responses
  openslave run --config bench.openslave --tick
");
        return 0;
    }

    private static Dictionary<int, int> ParsePairs(string? raw)
    {
        var dict = new Dictionary<int, int>();
        if (string.IsNullOrWhiteSpace(raw)) return dict;
        foreach (var pair in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var bits = pair.Split('=');
            if (bits.Length != 2) continue;
            if (int.TryParse(bits[0].Trim(), out var k) && int.TryParse(bits[1].Trim(), out var v))
                dict[k] = v;
        }
        return dict;
    }

    private static string? GetArg(string[] argv, string name)
    {
        for (int i = 0; i < argv.Length; i++)
        {
            if (argv[i] == "--" + name)
            {
                if (i + 1 < argv.Length && !argv[i + 1].StartsWith("--")) return argv[i + 1];
                return "true";
            }
        }
        return null;
    }

    private static int GetIntArg(string[] argv, string name, int fallback)
    {
        var v = GetArg(argv, name);
        return v is not null && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    private static bool GetFlag(string[] argv, string name) => GetArg(argv, name) == "true";

    private static int RunServer(string[] argv)
    {
        var quiet = GetFlag(argv, "quiet");
        var tick = GetFlag(argv, "tick");

        SlaveDocument? document = null;

        var configPath = GetArg(argv, "config");
        if (configPath is not null)
        {
            try { document = WorkspaceFileService.Load(configPath); }
            catch (Exception ex) { return Fail($"failed to load workspace: {ex.Message}"); }
        }

        document ??= new SlaveDocument(new SlaveDefinition
        {
            Port = 1502,
            SlaveId = 1,
            StartAddress = 0,
            Quantity = 100,
            AddressBase = AddressBase.Zero,
        });

        // Flags override anything loaded from the workspace.
        var def = document.Definition;
        def.Port = GetIntArg(argv, "port", def.Port);
        def.SlaveId = GetIntArg(argv, "slave", def.SlaveId);
        if (GetFlag(argv, "ignore-unit-id")) def.IgnoreUnitId = true;
        def.ErrorSimulation.ResponseDelayMs = GetIntArg(argv, "response-delay", def.ErrorSimulation.ResponseDelayMs);
        if (GetFlag(argv, "skip-responses")) def.ErrorSimulation.SkipResponses = true;
        if (GetFlag(argv, "exception-busy")) def.ErrorSimulation.ReturnExceptionBusy = true;

        foreach (var kv in ParsePairs(GetArg(argv, "hr"))) document.SeedHoldingRegister(kv.Key, (ushort)kv.Value);
        foreach (var kv in ParsePairs(GetArg(argv, "ir"))) document.SeedInputRegister(kv.Key, (ushort)kv.Value);
        foreach (var kv in ParsePairs(GetArg(argv, "coil"))) document.SeedCoil(kv.Key, kv.Value != 0);
        foreach (var kv in ParsePairs(GetArg(argv, "di"))) document.SeedDiscrete(kv.Key, kv.Value != 0);

        document.RebuildCells();

        if (tick && !quiet)
        {
            document.RequestHandled += ev =>
                _stdout.WriteLine($"[{DateTime.Now:HH:mm:ss}] FC{ev.FunctionCode:X2} @ {ev.Address} × {ev.Quantity} {ev.Detail}".TrimEnd());
            document.ConnectedClientsChanged += n =>
                _stdout.WriteLine($"[{DateTime.Now:HH:mm:ss}] clients = {n}");
        }

        document.Start();
        if (!quiet) _stdout.WriteLine($"OpenSlave listening on 0.0.0.0:{def.Port} (unit id {def.SlaveId})");

        var serialPort = GetArg(argv, "serial");
        if (serialPort is not null)
        {
            var baud = GetIntArg(argv, "baud", 9600);
            var parity = Enum.TryParse<System.IO.Ports.Parity>(GetArg(argv, "parity"), true, out var p) ? p : System.IO.Ports.Parity.None;
            var sb = Enum.TryParse<System.IO.Ports.StopBits>(GetArg(argv, "stopbits"), true, out var s) ? s : System.IO.Ports.StopBits.One;
            try
            {
                document.StartSerial(serialPort, baud, parity, sb);
                if (!quiet) _stdout.WriteLine($"OpenSlave also serving RTU on {serialPort}@{baud} {parity}/{sb}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"openslave: serial open failed: {ex.Message}");
            }
        }

        var udpPort = GetIntArg(argv, "udp", -1);
        if (udpPort > 0)
        {
            try { document.StartUdp(udpPort); if (!quiet) _stdout.WriteLine($"OpenSlave also serving UDP on 0.0.0.0:{udpPort}"); }
            catch (Exception ex) { Console.Error.WriteLine($"openslave: UDP listen failed: {ex.Message}"); }
        }

        var rtuTcpPort = GetIntArg(argv, "rtu-over-tcp", -1);
        if (rtuTcpPort > 0)
        {
            try { document.StartRtuOverTcp(rtuTcpPort); if (!quiet) _stdout.WriteLine($"OpenSlave also serving RTU-over-TCP on 0.0.0.0:{rtuTcpPort}"); }
            catch (Exception ex) { Console.Error.WriteLine($"openslave: RTU-over-TCP listen failed: {ex.Message}"); }
        }

        var asciiPort = GetIntArg(argv, "ascii", -1);
        if (asciiPort > 0)
        {
            try { document.StartAsciiOverTcp(asciiPort); if (!quiet) _stdout.WriteLine($"OpenSlave also serving ASCII on 0.0.0.0:{asciiPort}"); }
            catch (Exception ex) { Console.Error.WriteLine($"openslave: ASCII listen failed: {ex.Message}"); }
        }

        var tlsPort = GetIntArg(argv, "tls", -1);
        if (tlsPort > 0)
        {
            try { document.StartTls(tlsPort); if (!quiet) _stdout.WriteLine($"OpenSlave also serving TLS on 0.0.0.0:{tlsPort} (self-signed cert)"); }
            catch (Exception ex) { Console.Error.WriteLine($"openslave: TLS listen failed: {ex.Message}"); }
        }

        var rtuUdpPort = GetIntArg(argv, "rtu-over-udp", -1);
        if (rtuUdpPort > 0)
        {
            try { document.StartRtuOverUdp(rtuUdpPort); if (!quiet) _stdout.WriteLine($"OpenSlave also serving RTU-over-UDP on 0.0.0.0:{rtuUdpPort}"); }
            catch (Exception ex) { Console.Error.WriteLine($"openslave: RTU-over-UDP listen failed: {ex.Message}"); }
        }

        var asciiUdpPort = GetIntArg(argv, "ascii-over-udp", -1);
        if (asciiUdpPort > 0)
        {
            try { document.StartAsciiOverUdp(asciiUdpPort); if (!quiet) _stdout.WriteLine($"OpenSlave also serving ASCII-over-UDP on 0.0.0.0:{asciiUdpPort}"); }
            catch (Exception ex) { Console.Error.WriteLine($"openslave: ASCII-over-UDP listen failed: {ex.Message}"); }
        }

        var asciiSerialPort = GetArg(argv, "ascii-serial");
        if (asciiSerialPort is not null)
        {
            var baud = GetIntArg(argv, "baud", 9600);
            var parity = Enum.TryParse<System.IO.Ports.Parity>(GetArg(argv, "parity"), true, out var p) ? p : System.IO.Ports.Parity.None;
            var sb = Enum.TryParse<System.IO.Ports.StopBits>(GetArg(argv, "stopbits"), true, out var s) ? s : System.IO.Ports.StopBits.One;
            try { document.StartAsciiOverSerial(asciiSerialPort, baud, parity, sb); if (!quiet) _stdout.WriteLine($"OpenSlave also serving ASCII on {asciiSerialPort}@{baud}"); }
            catch (Exception ex) { Console.Error.WriteLine($"openslave: ASCII-serial open failed: {ex.Message}"); }
        }

        using var stop = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        stop.Wait();

        document.Stop();
        document.Dispose();
        if (!quiet) _stdout.WriteLine("OpenSlave stopped.");
        return 0;
    }
}
