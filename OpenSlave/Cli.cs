using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using EasyModbus;

namespace OpenSlave;

/// <summary>
/// CLI for OpenSlave. Headless slave simulator suitable for CI / scripted tests.
///
/// Subcommands: run | help
/// </summary>
public static class Cli
{
    public static bool IsKnownCommand(string? a) =>
        a is "run" or "help" or "--help" or "-h";

    private static System.IO.TextWriter _stdout = Console.Out;

    public static int Run(string[] args)
    {
        _stdout = Console.Out;
        Console.SetOut(System.IO.TextWriter.Null);
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

OPTIONS
  --port <n>         TCP port to listen on               (default 1502)
  --slave <n>        unit identifier the slave answers   (default 1)
  --hr <pairs>       seed holding registers, e.g. --hr 1=100,2=200
  --coil <pairs>     seed coils, e.g. --coil 1=1,3=1
  --di <pairs>       seed discrete inputs
  --ir <pairs>       seed input registers
  --tick             pretty-print client connect/changes to stdout
  --quiet            suppress all stdout

EXAMPLES
  openslave run --port 1502
  openslave run --port 1502 --hr 1=100,2=200,3=300 --coil 1=1,3=1
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

    private static int RunServer(string[] argv)
    {
        var port = GetIntArg(argv, "port", 1502);
        var quiet = GetArg(argv, "quiet") == "true";
        var tick = GetArg(argv, "tick") == "true";

        var server = new ModbusServer { Port = port };
        server.Listen();
        if (!quiet) _stdout.WriteLine($"OpenSlave listening on 0.0.0.0:{port}");

        // Seed values
        foreach (var kv in ParsePairs(GetArg(argv, "hr"))) server.holdingRegisters[kv.Key] = (short)kv.Value;
        foreach (var kv in ParsePairs(GetArg(argv, "ir"))) server.inputRegisters[kv.Key] = (short)kv.Value;
        foreach (var kv in ParsePairs(GetArg(argv, "coil"))) server.coils[kv.Key] = kv.Value != 0;
        foreach (var kv in ParsePairs(GetArg(argv, "di"))) server.discreteInputs[kv.Key] = kv.Value != 0;

        if (tick && !quiet)
        {
            server.NumberOfConnectedClientsChanged += () =>
                _stdout.WriteLine($"[{DateTime.Now:HH:mm:ss}] clients = {server.NumberOfConnections}");
            server.CoilsChanged += (a, q) =>
                _stdout.WriteLine($"[{DateTime.Now:HH:mm:ss}] write coils @ {a} × {q}");
            server.HoldingRegistersChanged += (a, q) =>
                _stdout.WriteLine($"[{DateTime.Now:HH:mm:ss}] write HRs @ {a} × {q}");
        }

        using var stop = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        stop.Wait();

        server.StopListening();
        if (!quiet) _stdout.WriteLine("OpenSlave stopped.");
        return 0;
    }
}
