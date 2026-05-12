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

        using var stop = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Set(); };
        stop.Wait();

        document.Stop();
        document.Dispose();
        if (!quiet) _stdout.WriteLine("OpenSlave stopped.");
        return 0;
    }
}
