# OpenPoll · OpenSlave

Free, open-source desktop replacement for **Modbus Poll** and **Modbus Slave** — runs natively on Linux, Windows, and macOS via .NET 8 + Avalonia 11.

- **OpenPoll** — Modbus master / client (read & write registers from any slave). GUI + headless CLI.
- **OpenSlave** — Modbus slave / server simulator. GUI + headless CLI.
- **OpenPoll.TestServer** — small headless test slave (legacy; use OpenSlave's CLI now)

Modbus has been an open protocol since 1979. Tooling around it shouldn't cost money.

## Features

- Modbus TCP and RTU connection setup
- Live polling of coils, discrete inputs, holding registers, input registers
- Per-cell data type display (signed, unsigned, hex, binary)
- Bit-level binary editor for control words
- Range scanner (sweep registers, search slave IDs, sweep IP range)
- Live time-series chart (LiveCharts2, up to 5 series, with windowed memory)

## Project layout

```
.
├── OpenPoll.sln
├── OpenPoll/                 — Avalonia desktop app
│   ├── OpenPoll.csproj       — .NET 8, Avalonia 11.2
│   ├── Program.cs · App.axaml     — entry point + app shell
│   ├── Models/                    — AppSettings, ModbusFunction, RegisterRow
│   ├── Services/                  — SettingsService, ModbusSession, ValueFormatter
│   ├── Views/                     — HomeView, SetupView, ConnectionSetupView,
│   │                                BinaryEditorView, ModbusScraperView, LiveChartView
│   └── Assets/
└── OpenPoll.TestServer/      — small Modbus TCP server for end-to-end testing
    └── Program.cs                 — listens on 127.0.0.1:1502, animated test data
```

Settings are persisted as JSON at:
- Linux:   `~/.config/OpenPoll/settings.json`
- Windows: `%APPDATA%\OpenPoll\settings.json`

## Build

Requires the .NET 8 SDK.

```bash
dotnet build OpenPoll.sln
```

## Run — GUI

```bash
dotnet run --project OpenPoll          # the master / client GUI
dotnet run --project OpenSlave         # the slave / simulator GUI
```

In OpenPoll: **File → Connection Setup**, set IP `127.0.0.1`, port `1502`, save.
**File → Setup**, set Node ID `1`, Address `1`, Amount `10`, Function `4x — Holding Registers`.
Click **Connect**. You should see ten registers ticking up once a second.

## Run — CLI (headless)

Both binaries detect a CLI subcommand on `argv[0]` and skip the GUI. JSON output for machine parsing.

```bash
# OpenSlave: start a slave with seeded values, listen until Ctrl+C
dotnet run --project OpenSlave -- run --port 1502 --hr "1=100,2=200,3=300" --quiet

# OpenPoll: one-shot read of 5 holding registers (in another terminal)
dotnet run --project OpenPoll -- read --ip 127.0.0.1 --port 1502 --address 0 --amount 5 --function 03
# → {"ok":true,"function":"holdingRegisters","address":0,"amount":5,"values":[100,200,300,...],"error":null}

# Single register write (FC 06)
dotnet run --project OpenPoll -- write --ip 127.0.0.1 --port 1502 --address 0 --value 42 --function 06

# Multi-register write (FC 16)
dotnet run --project OpenPoll -- write --ip 127.0.0.1 --port 1502 --address 0 --value "11,22,33" --function 16

# Slave-ID scan
dotnet run --project OpenPoll -- scan --type id --ip 127.0.0.1 --port 1502 --start 1 --end 247

# Subnet sweep
dotnet run --project OpenPoll -- scan --type ip --base 192.168.1.0 --port 502 --timeout 500

# Start the embedded HTTP API only (no GUI)
dotnet run --project OpenPoll -- serve --http 8080
# → curl http://localhost:8080/api/polls

# Help
dotnet run --project OpenPoll -- help
dotnet run --project OpenSlave -- help
```

Once published to a self-contained binary, the same commands work via `./OpenPoll read ...` (no `dotnet run`).

## Cross-platform publish (self-contained)

```bash
# Linux x64 (~93 MB output)
dotnet publish OpenPoll -c Release -r linux-x64 --self-contained -o publish/linux-x64

# Windows x64 (~97 MB output)
dotnet publish OpenPoll -c Release -r win-x64 --self-contained -o publish/win-x64
```

Each `publish/<rid>/` is a standalone tree — copy it to a target machine and run `OpenPoll` (Linux) or `OpenPoll.exe` (Windows). No .NET install needed.

## Linux serial port permissions

Modbus RTU on Linux opens devices like `/dev/ttyUSB0`, `/dev/ttyACM0`, or `/dev/ttyS0`. Non-root users need to be in the `dialout` group:

```bash
sudo usermod -aG dialout $USER
# then log out and back in
```

If access is denied, the scanner surfaces the OS error in the status row.

## Dependencies

- [Avalonia 11.2](https://avaloniaui.net/) — UI
- [LiveChartsCore.SkiaSharpView.Avalonia 2.0](https://livecharts.dev/) — live charts
- [EasyModbusTCP 5.6](https://github.com/rossmann-engineering/EasyModbusTCP.NET) — Modbus protocol
- `System.IO.Ports 8.0` — cross-platform serial

## License

MIT — see [LICENSE](LICENSE).
