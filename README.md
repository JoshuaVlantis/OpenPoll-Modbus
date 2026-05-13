# OpenPoll · OpenSlave

Free, open-source Modbus tooling for engineers — runs natively on Linux, Windows, and macOS via .NET 8 + Avalonia 11.

- **OpenPoll** — Modbus master / client. Reads and writes registers from any slave. GUI + headless CLI + HTTP API.
- **OpenSlave** — Modbus slave / server simulator with a custom in-process Modbus TCP slave, including wire-level fault simulation. GUI + headless CLI.
- **OpenPoll.TestServer** — small headless test slave with a built-in ticker for visual chart testing. Reuses OpenSlave's protocol stack.

Modbus has been an open protocol since 1979. Tooling around it should be open too.

> **Status:** v2.3.0 · 221 unit tests + 15-step docker functional test, all green · Linux / macOS / Windows builds via `dotnet publish`. Feature parity with Modbus Poll / Modbus Slave from modbustools.com (excluding their Windows-only OLE/COM interface — superseded by our HTTP REST + WebSocket API).

---

## Table of contents

- [Quick start](#quick-start)
- [OpenPoll — master features](#openpoll--master-features)
- [OpenSlave — slave features](#openslave--slave-features)
- [Project layout](#project-layout)
- [Build](#build)
- [Run — GUI](#run--gui)
- [Run — CLI (headless)](#run--cli-headless)
- [HTTP API](#http-api)
- [Workspace files](#workspace-files)
- [File logging](#file-logging)
- [Cross-platform publish](#cross-platform-publish)
- [Linux serial port permissions](#linux-serial-port-permissions)
- [Testing](#testing)
- [Dependencies](#dependencies)
- [License](#license)

---

## Quick start

```bash
# Clone, build, run end-to-end against the bundled slave
git clone https://github.com/JoshuaVlantis/OpenPoll-Modbus.git
cd OpenPoll-Modbus
dotnet build OpenPoll.sln

# Terminal 1: start the slave
dotnet run --project OpenSlave -- run --port 1502 --hr "0=111,1=222,2=333" --quiet

# Terminal 2: read it back from the master
dotnet run --project OpenPoll -- read --ip 127.0.0.1 --port 1502 --address 0 --amount 3 --function 03
# → {"ok":true,"function":"holdingRegisters","address":0,"amount":3,"values":[111,222,333],"error":null}
```

For the GUI, drop the `--` and the args:

```bash
dotnet run --project OpenSlave   # slave simulator window
dotnet run --project OpenPoll    # master window
```

---

## OpenPoll — master features

### Protocol

| FC | Name | OpenPoll | Notes |
|----|------|----------|-------|
| 01 | Read Coils | ✅ | up to 2000 per request |
| 02 | Read Discrete Inputs | ✅ | up to 2000 per request |
| 03 | Read Holding Registers | ✅ | up to 125 per request |
| 04 | Read Input Registers | ✅ | up to 125 per request |
| 05 | Write Single Coil | ✅ | broadcast supported (slave 0) |
| 06 | Write Single Register | ✅ | broadcast supported |
| 15 | Write Multiple Coils | ✅ | up to 1968 per request |
| 16 | Write Multiple Registers | ✅ | up to 123 per request |
| 22 | Mask Write Register | ✅ | **atomic on TCP** in v2.2 (raw PDU); R-M-W fallback on serial RTU |
| 23 | Read/Write Multiple Registers | ✅ | atomic write-then-read |
| 43 / 14 | Read Device Identification | ✅ | Tools menu + `openpoll devid` CLI; supports basic/regular/specific |
| 08 | Diagnostics | ✅ | Full spec sub-function set: 00 Query echo · 01 Restart Comm · 02 Diag Register · 04 Force Listen · 0A Clear Counters · 0B Bus Msg · 0C Comm Err · 0D Excpt Err · 0E Slave Msg · 0F No Resp · 11 Slave Busy |
| 11 | Get Comm Event Counter | ✅ | `openpoll ec` |
| 17 | Report Server ID | ✅ | `openpoll srvid` |

Modbus exception codes 01..0B are surfaced verbatim, e.g. `"Modbus exception 06 (Slave device busy)"`.

### Transports

| Transport | Master flag | Slave flag |
|-----------|-------------|------------|
| Modbus TCP | (default) | `--port` |
| Modbus RTU over Serial | `--serial /dev/ttyUSB0 --baud --parity --stopbits` | `--serial …` |
| Modbus ASCII over Serial | `--ascii-serial /dev/ttyUSB0 --baud …` (7N1 default) | `--ascii-serial …` |
| Modbus over UDP | `--udp <port>` | `--udp <port>` |
| Modbus RTU over TCP | `--rtu-over-tcp <port>` | `--rtu-over-tcp <port>` |
| Modbus ASCII over TCP | `--ascii <port>` | `--ascii <port>` |
| Modbus RTU over UDP | `--rtu-over-udp <port>` | `--rtu-over-udp <port>` |
| Modbus ASCII over UDP | `--ascii-over-udp <port>` | `--ascii-over-udp <port>` |
| Modbus TCP + TLS | `--tls <port>` | `--tls <port>` (self-signed cert auto-generated) |

### Connection settings

- TCP host + port
- Serial port + baud (300..921600) + parity + stop bits
- Configurable **connect timeout**, **response timeout** (separate), and **retries** on transient failure
- Per-poll **scan rate** (polling interval)
- Modbus **unit / slave ID** (1..247 + 0 for broadcast)
- Auto-connect on startup toggle

### Display

- **28 cell formats** per row (right-click → Type):
  - 16-bit: Signed, Unsigned, Hex, Binary (with bit-level editor on double-click)
  - 32-bit: Signed, Unsigned, Hex, IEEE-754 Float
  - 64-bit: Signed, Unsigned, Hex, IEEE-754 Double
- **All four word/byte orders** for multi-register types: Big-endian (ABCD), Little-endian (DCBA), Big-endian byte-swap (BADC), Little-endian byte-swap (CDAB)
- **Per-row scaling**: `displayed = raw × scale + offset`, configurable precision
- **Per-row value names**: map raw integers to text labels (e.g. `0=Idle, 1=Running, 2=Fault`)
- **Address base toggle**: 0-indexed (wire) or 1-indexed (PLC-style 4xxxx)
- Live time-series chart (LiveCharts2): up to 12 series, 60 s / 5 min / 15 min / 1 h windows, pause/resume/clear

### Workflow

- **Multiple poll definitions in tabs** (`Ctrl+N` new, `Ctrl+D` duplicate, `Ctrl+W` close)
- **Workspace save/load** — `.openpoll` JSON file (`Ctrl+S` / `Ctrl+O`)
- **Recent workspaces** submenu (10 most recent, with one-click reopen)
- **Modbus scraper** — sweep registers, search slave IDs, sweep IP range; results to CSV
- **Traffic monitor** — live TX/RX/error feed with timestamps, function code, address, summary; pause / clear / save (text or CSV) / auto-scroll
- **Bit editor dialog** (double-click any Binary cell) — toggle 16 individual bits with live decimal/hex feedback
- **Conditional cell colours** — File → Colour Rules…: per-poll rule list (eq/ne/lt/le/gt/ge/between) drives cell foreground brushes
- **Manual write dialogs (F5/F6/F7/F8)** — Modbus-Poll-style modal entry for FC 05 / 06 / 15 / 16, prefills from the selected row
- **Test Center** — Tools → Test Center: hand-craft a raw Modbus PDU, send it, inspect the bytes coming back
- **Read Device Identification** — Tools → Read Device ID…: pulls FC 43 objects (vendor/product/version/URL/…) into a grid
- **CSV snapshot recorder** — Tools → Start CSV snapshot: appends one wide CSV row per second with the live cell values
- **XLSX snapshot recorder** — Tools → Start XLSX snapshot: same shape, saves a real .xlsx workbook on stop (via ClosedXML)
- **Live chart** — Y-axis min/max inputs, ZoomMode selector (X/Y/Both/None), Reset zoom, Export → PNG, Export → CSV
- **Print to PDF** — Tools → Print to PDF… (Ctrl+P): A4 multi-page table snapshot via SkiaSharp
- **Per-poll font** — File → Font…: family + size, applied to the VALUE column
- **Export to OpenSlave workspace** — File → Export active poll: generate a matching `.openslave` file
- **Test Center framing preview** — see exactly which bytes go on the wire as MBAP / RTU / ASCII
- **Status pill** (idle / connected / error) on every tab

### Automation

- **Headless CLI** — every operation scriptable, JSON output for pipelines
  - `read` / `write` / `mask` (FC 22) / `rw` (FC 23) / `devid` (FC 43) / `diag` (FC 08) / `ec` (FC 11) / `srvid` (FC 17) / `scan` / `serve`
- **HTTP REST API** (toggle from Tools menu or `serve` subcommand) — `GET /api/polls`, `GET /api/polls/{name}/values`, `POST /api/polls/{name}/write`, **`GET /api/ws`** (WebSocket stream of snapshot deltas)
- **HTTP API bearer-token auth** — `--http-token <t>` or env `OPENPOLL_HTTP_TOKEN`; constant-time comparison; opt-in (off by default)
- **JSON workspace format** — diffable, machine-generatable, scriptable

---

## OpenSlave — slave features

### Protocol (custom in-process Modbus TCP slave)

| FC | Name | OpenSlave |
|----|------|-----------|
| 01 | Read Coils | ✅ |
| 02 | Read Discrete Inputs | ✅ |
| 03 | Read Holding Registers | ✅ |
| 04 | Read Input Registers | ✅ |
| 05 | Write Single Coil | ✅ |
| 06 | Write Single Register | ✅ |
| 08 | Diagnostics (sub-fn 0) | ✅ |
| 11 | Get Comm Event Counter | ✅ |
| 15 | Write Multiple Coils | ✅ |
| 16 | Write Multiple Registers | ✅ |
| 17 | Report Server ID | ✅ |
| 22 | Mask Write Register | ✅ (atomic) |
| 23 | Read/Write Multiple Registers | ✅ (atomic) |
| 43 | Read Device Identification | ✅ (configurable vendor/product/version strings) |

Returns Modbus exception codes 01 (Illegal Function), 02 (Illegal Data Address), 03 (Illegal Data Value) per spec, plus 06 (Slave Busy) on demand.

### Transports

OpenSlave serves all six transports concurrently from the same process — flip any combination on from the CLI.

| Transport | CLI flag | Notes |
|-----------|----------|-------|
| Modbus TCP | `--port <n>` | default 1502; non-privileged |
| Modbus RTU over serial | `--serial <port> --baud --parity --stopbits` | CRC-16-MODBUS framing |
| Modbus ASCII over serial | `--ascii-serial <port> --baud …` | 7-bit, `:` + hex + LRC + CRLF |
| Modbus over UDP | `--udp <port>` | one MBAP frame per datagram |
| Modbus RTU over TCP | `--rtu-over-tcp <port>` | gateway-style; no MBAP |
| Modbus ASCII over TCP | `--ascii <port>` | `:` + hex + LRC + CRLF |
| Modbus RTU over UDP | `--rtu-over-udp <port>` | RTU framing in UDP datagrams |
| Modbus ASCII over UDP | `--ascii-over-udp <port>` | ASCII framing in UDP datagrams |
| Modbus TCP over TLS | `--tls <port>` | self-signed cert auto-generated |

Spec-compliant 0-indexed addressing (the original EasyModbus-backed slave was 1-indexed by quirk; **breaking change in v2.1.0**). Up to **65536 registers/coils** per table. Multiple concurrent clients across every transport.

### Slave definition (Setup card)

- **Listen port** (any 1..65535)
- **Unit / Slave ID** (1..247) + **Ignore Unit ID** for promiscuous mode
- **Start address** + **Quantity** (carve a window of the 65536-register space)
- **Address base** display toggle (Base 0 / Base 1)
- Apply changes hot — no restart needed

### Per-cell display (mirrors OpenPoll)

- 16/32/64-bit Signed, Unsigned, Hex, plus Binary, Float, Double — right-click any holding/input row → Type
- All four word/byte orders for multi-register types
- Direct editing — type a value, press Enter, the slave's table updates and the next master poll sees it

### Wire-level fault simulation

- **Response delay** — delay every response by N ms (test masters' timeout handling)
- **Skip 1-in-10 responses** — silent drops, simulates flaky links
- **Return Exception 06 (Slave Busy)** — every request gets an exception response

### Workflow

- **Workspace save/load** — `.openslave` JSON (`Ctrl+O` / `Ctrl+S`); preserves slave definition, all seeded register values, per-cell type/order, error-simulation settings, and pattern generators
- **Multiple slave windows** — `File → New Slave Window` opens another simulator in the same process on the next default port (1502 → 1503 → ...)
- **Pattern generators** — View → Patterns…: sine / triangle / square / sawtooth / random-walk drive register values on every sync tick (~200 ms)
- **Live chart** — View → Live Chart…: real-time line chart of selected holding registers, with X-axis zoom
- **Live request log** with timestamps; clear / save to file / auto-scroll
- **Status pill** + connected-client counter + request counter
- **CLI mode** — full headless slave for CI, scripted scenarios, docker functional tests

---

## Project layout

```
.
├── OpenPoll.sln
├── OpenPoll/                          — Avalonia master client
│   ├── Models/                          PollDefinition, RegisterRow, CellDataType, WordOrder, …
│   ├── Services/                        ModbusSession (NModbus master), ValueFormatter,
│   │                                    PollDocument, Workspace, TrafficLog, FileLogger,
│   │                                    HttpApiHost, RecentFilesService, SerialStreamResource
│   ├── Views/                           HomeView, SetupView, ConnectionSetupView,
│   │                                    BinaryEditorView, ModbusScraperView, LiveChartView,
│   │                                    TrafficMonitorView
│   └── Themes/                          shared dark theme (linked into OpenSlave too)
├── OpenSlave/                         — Avalonia slave simulator
│   ├── Models/                          SlaveDefinition, SlaveCell, ErrorSimulation,
│   │                                    CellDataType, WordOrder, AddressBase, SlaveTableKind
│   ├── Services/                        ModbusTcpSlave (custom protocol stack),
│   │                                    SlaveDocument, ValueFormatter,
│   │                                    WorkspaceFileService, FileLogger
│   └── MainWindow.axaml(.cs)
├── OpenPoll.TestServer/               — headless ticker slave for chart testing
├── tests/
│   ├── OpenPoll.Tests/                  123 unit + integration tests
│   ├── OpenSlave.Tests/                 26 wire-level + workspace tests
│   └── docker/run-functional-test.sh    15-step master↔slave round-trip in Docker
├── dev.sh                             — interactive dev menu (build / run / publish)
├── README.md  ·  TEST_PLAN.md  ·  LICENSE
└── publish/                           — self-contained binaries per RID (.gitignored)
```

Settings are persisted as JSON at:
- Linux:   `~/.local/share/OpenPoll/...` and `~/.config/OpenPoll/settings.json`
- macOS:   `~/Library/Application Support/OpenPoll/...`
- Windows: `%LOCALAPPDATA%\OpenPoll\...`

---

## Build

Requires the .NET 8 SDK.

```bash
dotnet build OpenPoll.sln
```

`dev.sh` provides an interactive menu for build / run / restart / publish / clean.

---

## Run — GUI

```bash
dotnet run --project OpenPoll          # master / client
dotnet run --project OpenSlave         # slave / simulator
```

In OpenSlave: click **● Start** (defaults to TCP port 1502, slave ID 1, registers 0..99).

In OpenPoll: **File → Connection Setup**, set IP `127.0.0.1`, port `1502`, OK.
**File → Setup**, set Slave ID `1`, Address `0`, Amount `10`, Function `4x — Holding Registers`, OK.
Click **Connect**. You should see ten registers (initially `0`) updating in sync with the slave.

---

## Run — CLI (headless)

Both binaries detect a CLI subcommand on `argv[0]` and skip the GUI. JSON output for machine parsing.

### OpenSlave

```bash
# Start a slave with seeded values, listen until Ctrl+C
dotnet run --project OpenSlave -- run \
  --port 1502 \
  --slave 1 \
  --hr "0=111,1=222,2=333" \
  --coil "0=1,1=0,2=1" \
  --quiet

# Add error simulation
dotnet run --project OpenSlave -- run --port 1502 --response-delay 250 --skip-responses

# Force every reply to be Slave-Busy (for testing master's exception handling)
dotnet run --project OpenSlave -- run --port 1502 --exception-busy

# Load a saved scenario
dotnet run --project OpenSlave -- run --config bench.openslave --tick

# Help
dotnet run --project OpenSlave -- help
```

### OpenPoll

```bash
# Read 5 holding registers (TCP)
dotnet run --project OpenPoll -- read \
  --ip 127.0.0.1 --port 1502 --slave 1 --address 0 --amount 5 --function 03

# Read over Serial RTU
dotnet run --project OpenPoll -- read \
  --serial /dev/ttyUSB0 --baud 19200 --parity even --slave 7 \
  --address 100 --amount 8 --function 03

# Single-register write (FC 06)
dotnet run --project OpenPoll -- write \
  --ip 127.0.0.1 --port 1502 --address 4 --value 999 --function 06

# Multi-coil write (FC 15)
dotnet run --project OpenPoll -- write \
  --ip 127.0.0.1 --port 1502 --address 10 --function 15 --value "1,0,1,1,0"

# Multi-register write (FC 16)
dotnet run --project OpenPoll -- write \
  --ip 127.0.0.1 --port 1502 --address 20 --function 16 --value "10,20,30"

# Mask Write Register (FC 22) — apply AND/OR masks atomically (R-M-W emulated)
dotnet run --project OpenPoll -- mask \
  --ip 127.0.0.1 --port 1502 --address 0 --and-mask 0x00FF --or-mask 0x1100

# Read/Write Multiple Registers (FC 23) — atomic write-then-read
dotnet run --project OpenPoll -- rw \
  --ip 127.0.0.1 --port 1502 \
  --read-address 0 --read-amount 4 \
  --address 10 --value "7,8,9"

# Slave-ID scan
dotnet run --project OpenPoll -- scan \
  --type id --ip 127.0.0.1 --port 1502 --start 1 --end 247

# Subnet sweep (TCP probe)
dotnet run --project OpenPoll -- scan \
  --type ip --base 192.168.1.0 --port 502 --timeout 500

# Per-address sweep
dotnet run --project OpenPoll -- scan \
  --type registers --ip 127.0.0.1 --port 1502 --address 0 --amount 50 --function 03

# With retries + separate response timeout
dotnet run --project OpenPoll -- read \
  --ip 127.0.0.1 --port 1502 --address 0 --amount 5 --function 03 \
  --timeout 2000 --response-timeout 500 --retries 3

# Start the embedded HTTP API only (no GUI) until SIGINT
dotnet run --project OpenPoll -- serve --http 8080

# Help
dotnet run --project OpenPoll -- help
```

Once published self-contained, the same commands work via `./OpenPoll read ...` (no `dotnet run`).

---

## HTTP API

Toggle from OpenPoll's **Tools → Start HTTP API** menu, or run `openpoll serve --http 8080`. Drives the live state from any language (Python, Node, curl, browser) without an SDK.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/polls` | List all open poll documents |
| `GET` | `/api/polls/{name}/values` | Current cell values for a poll |
| `POST` | `/api/polls/{name}/write` | Write coils/registers via JSON |

Write payload:

```json
{ "function": "06", "address": 4, "value": 999 }
{ "function": "05", "address": 5, "bool": true }
{ "function": "16", "address": 0, "values": [10, 20, 30] }
{ "function": "15", "address": 0, "bools": [true, false, true] }
```

CORS `*`, JSON camelCase. Read endpoints unauthenticated and read-only — fine for local scripting; do **not** expose to the public internet without an auth proxy.

---

## Workspace files

Both apps use diffable JSON workspaces.

**OpenPoll** — `.openpoll`:

```json
{
  "version": 1,
  "polls": [
    {
      "name": "boiler-1",
      "connectionMode": "Tcp",
      "ipAddress": "192.168.1.50",
      "serverPort": 502,
      "nodeId": 1,
      "address": 0,
      "amount": 20,
      "function": "HoldingRegisters",
      "pollingRateMs": 1000,
      "wordOrder": "BigEndian"
    }
  ]
}
```

**OpenSlave** — `.openslave`:

```json
{
  "schema": 1,
  "definition": {
    "name": "Slave",
    "port": 1502,
    "slaveId": 1,
    "startAddress": 0,
    "quantity": 100,
    "addressBase": "One",
    "errorSimulation": { "responseDelayMs": 0, "skipResponses": false }
  },
  "tables": {
    "holdingRegisters": [
      { "address": 0, "value": 4463, "dataType": "Hex", "wordOrder": "BigEndian" },
      { "address": 1, "value": 222 }
    ],
    "coils": [{ "address": 0, "value": true }]
  }
}
```

---

## File logging

Both apps write a daily-rotating log of every Modbus event. Always-on, zero configuration.

| Platform | Log directory |
|----------|---------------|
| Linux | `~/.local/share/OpenPoll/logs/` and `~/.local/share/OpenSlave/logs/` |
| macOS | `~/Library/Application Support/OpenPoll/logs/` and `OpenSlave/logs/` |
| Windows | `%LOCALAPPDATA%\OpenPoll\logs\` and `%LOCALAPPDATA%\OpenSlave\logs\` |

Use **Tools → Reveal log folder…** in OpenPoll, or **View → Reveal log folder…** in OpenSlave to open the path in your file manager. The log path is also printed to stdout on startup.

Sample line:

```text
12:35:01.123  TX   03 ReadHoldingRegisters    @ 0       × 5
12:35:01.124  ER   03 ReadHoldingRegisters    @ 0       × 5      Modbus exception 06 (Slave device busy)
```

---

## Cross-platform publish

```bash
# Linux x64 (~93 MB self-contained)
dotnet publish OpenPoll  -c Release -r linux-x64 --self-contained -o publish/linux-x64
dotnet publish OpenSlave -c Release -r linux-x64 --self-contained -o publish/linux-x64

# Windows x64
dotnet publish OpenPoll  -c Release -r win-x64   --self-contained -o publish/win-x64
dotnet publish OpenSlave -c Release -r win-x64   --self-contained -o publish/win-x64

# macOS Intel + Apple Silicon
dotnet publish OpenPoll  -c Release -r osx-x64   --self-contained -o publish/osx-x64
dotnet publish OpenPoll  -c Release -r osx-arm64 --self-contained -o publish/osx-arm64
```

Each `publish/<rid>/` is a standalone tree — copy it to a target machine and run `OpenPoll` (Linux/macOS) or `OpenPoll.exe` (Windows). No .NET install needed.

---

## Linux serial port permissions

Modbus RTU on Linux opens devices like `/dev/ttyUSB0`, `/dev/ttyACM0`, or `/dev/ttyS0`. Non-root users need to be in the `dialout` group:

```bash
sudo usermod -aG dialout $USER
# log out and back in
```

If access is denied, the connection setup surfaces the OS error in the status row.

---

## Testing

```bash
dotnet test                          # 149 unit + integration tests
bash tests/docker/run-functional-test.sh linux-x64    # 15-step master↔slave round-trip in Docker
```

Test breakdown:

| Project | Tests | What |
|---------|-------|------|
| `OpenPoll.Tests` | 123 | ValueFormatter (128 perm), ModbusSession (NModbus integration), PollDefinition, CellDataType, RegisterRow scaling/value names |
| `OpenSlave.Tests` | 26 | Wire-level frame assertions for FC 1/3/6/22/23, exception responses, workspace round-trip, ValueFormatter |
| Docker functional | 15 steps | Master↔slave round-trip for FC 01/03/05/06/15/16/22/23 + Slave-Busy assertion |

See [TEST_PLAN.md](TEST_PLAN.md) for the full breakdown.

---

## Dependencies

- [Avalonia 11.2](https://avaloniaui.net/) — cross-platform UI
- [LiveChartsCore.SkiaSharpView.Avalonia 2.0](https://livecharts.dev/) — live charts
- [NModbus 3.0.83](https://github.com/NModbus/NModbus) — Modbus master protocol stack (replaced EasyModbus in v2.1.0 — fixed silent exception-swallowing bug, gained native FC 23 + FC 43)
- `System.IO.Ports 8.0` — cross-platform serial

OpenSlave's slave-side protocol is hand-rolled (`OpenSlave/Services/ModbusTcpSlave.cs`, ~370 LoC) so we own every byte — full control of FC 22 atomicity, Slave-Busy injection, and future FC 8 / FC 17 / FC 43 server-side support.

---

## License

GPL-3.0-or-later — see [LICENSE](LICENSE).

Earlier history of this project was released under MIT (through v1.1, 2022); from v2.0.0 onward it is licensed under GPL-3.0. Past MIT-licensed contributions retain their notice in the git history.
