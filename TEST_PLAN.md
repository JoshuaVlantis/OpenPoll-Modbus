# OpenPoll / OpenSlave — Test Plan

> **Automated suite: 149 tests passing** (123 OpenPoll + 26 OpenSlave) plus a 15-step Docker functional test covering FC 01/03/05/06/15/16/22/23 and Slave-Busy exception surfacing.
> Real defects caught by the test suite during development are listed in §A.0 below.
> The GUI checks in §B onward need your eyes.

```bash
# Unit + integration suites (xUnit)
dotnet test

# Master ↔ slave round-trip in Docker (linux-x64 self-contained binaries)
bash tests/docker/run-functional-test.sh linux-x64
```

---

## A.0 · Real bugs found and fixed by the test suite

| # | Where | Bug | Fix |
|---|---|---|---|
| 1 | `ValueFormatter.cs` (Format Double) | `G15` precision lost `double.MaxValue`, parsed back as `Infinity` | Use `G17` (round-trip-safe) |
| 2 | `ValueFormatter.cs` (Format Float) | `G7` could lose precision near `float.MaxValue` (defensive) | Use `G9` |
| 3 | `HttpApiHost.cs` | `GET /` returned 404 because `TrimEnd('/')` of `"/"` is `""`, never matched `"/"` | Accept both `""` and `"/"` |
| 4 | `OpenSlave/Cli.cs` (pre-rewrite) | `--slave` flag accepted from CLI but never applied to `ModbusServer.UnitIdentifier` | Wired through `SlaveDefinition` and `ModbusTcpSlave` |
| 5 | `OpenSlave/MainWindow.axaml` (pre-rewrite) | `UnitIdInput` field visible in UI but never read on Start | Wired in `OnStart`/`SyncInputsToDefinition` |
| 6 | NModbus `Transport.SlaveBusyUsesRetryCount` | Default `false` makes NModbus retry Slave-Busy (FC exception 06) **forever** even with `Retries = 0` — `ExceptionBusy` test hung indefinitely | Set `Transport.SlaveBusyUsesRetryCount = true` in `ModbusSession.ApplyTunables` |
| 7 | `OpenPoll.Services.ModbusSession` | Passing `address > 65535` silently wrapped via `(ushort)address` cast to a valid in-range register | `RangeError()` guard returns "Illegal data address" before the NModbus call |
| 8 | `OpenSlave.Models.RegisterCell` | `Display` property had no setter, so Avalonia's `DataGridTextColumn` refused to enter edit mode — user couldn't type values into the slave's grid | Added setter that parses input and updates `RawValue`/`RawWords` |
| 9 | `OpenSlave.Services.SlaveDocument` | 200 ms sync timer reassigned `RawWords` on every tick, snapping cells back to the slave's value mid-edit | Added `IsEditing` flag set by `BeginningEdit`/cleared by `RowEditEnded`; sync skips editing rows |
| — | EasyModbus 5.6 (replaced) | Master silently swallowed Modbus exception codes 04..0B (Slave-Busy reads returned `[0]` with `ok=true`), only mapped codes 01..03; FC 22 / 23 / 43 not exposed; printed copyright banner to stdout on every `ModbusClient` construction | **Replaced wholesale with NModbus 3.0.83** in v2.1.0 |

---

## A · Automated tests

### A.1 OpenPoll unit + integration tests (`dotnet test tests/OpenPoll.Tests`) — 123 passing

| File | Tests | Coverage |
|------|-------|----------|
| `ValueFormatterTests.cs` | 76 | every `CellDataType` × every `WordOrder` round-trip, hex prefix forms, binary spacing, edge values (`int.MinValue`, `double.MaxValue`, etc.), bad-input rejection, specific wire-byte assertions for ABCD/CDAB/BADC/DCBA |
| `ModbusSessionTests.cs` | 14 | TCP connect / refused / timeout, FC 1-6 / 15 / 16 round-trip against in-process `ModbusTcpSlave`, **FC 22 mask write**, **FC 23 read/write**, reconnect-same-transport, reconnect-different-unit-id, disconnect-then-read, out-of-range guard, **Slave-Busy exception surfaces correctly**, retries on transient failure |
| `PollDefinitionTests.cs` | 3 | clone deep-copy, defaults |
| `CellDataTypeTests.cs` | 23 | `WordCount`, prefix, `IsWritable`, `IsRegister` |
| `RegisterRowDisplayTests.cs` | 7 | scaling default-off, `raw × scale + offset`, scaling skipped for float types, value-name lookup, value-names take precedence over scaling, `ScalePrecision` clamping |

### A.2 OpenSlave unit tests (`dotnet test tests/OpenSlave.Tests`) — 26 passing

| File | Tests | Coverage |
|------|-------|----------|
| `ValueFormatterTests.cs` | 9 | mirrors OpenPoll's formatter; signed/unsigned/hex/binary, all 4 word orders for 32-bit, float round-trip, double round-trip, parser edge cases |
| `ModbusTcpSlaveTests.cs` | 7 | hand-crafted MBAP frame assertions: FC 03 returns expected bytes, FC 03 out-of-range returns exception 02, FC 06 updates table & echoes, FC 22 atomic AND/OR, FC 23 atomic write-then-read with byte-perfect response, IgnoreUnitId, ExceptionBusy returns 0x83/0x06 on FC 03 |
| `WorkspaceFileServiceTests.cs` | 3 | round-trip preserves definition + cell values + per-cell types/order, schema 0 rejected, forward-compatible schema accepted |
| `OpenSlaveTests` (other small fixtures) | 7 | data-type word counts, etc. |

### A.3 Docker functional test (`bash tests/docker/run-functional-test.sh linux-x64`) — 15 steps

Self-contained linux-x64 binaries inside `mcr.microsoft.com/dotnet/runtime-deps:8.0`. Master CLI ↔ slave CLI on TCP loopback.

```text
FC03 read holding regs            [111,222,333]            ✓
FC01 read coils                   [true,false,true]        ✓
FC06 write HR @4 = 999            ok                       ✓
FC03 read back HR @4              [999]                    ✓
FC05 write coil @5 = on           ok                       ✓
FC01 read back coil @5            [true]                   ✓
FC15 write multi coils @20        ok                       ✓
FC01 read back coils @20          [true,false,...]         ✓
FC16 write multi regs @30         ok                       ✓
FC03 read back regs @30           [10,20,30]               ✓
FC22 mask write @0                ok (AND 0x00FF OR 0x1100) ✓
FC03 readback after mask          [4463]   (= 0x116F)      ✓
FC23 rw — read 3, write [7,8] @50 ok                       ✓
FC03 readback after rw            [7,8]                    ✓
exception-busy slave              "Modbus exception 06"    ✓
```

A `win-x64` lane exists but is best-effort: Wine 9.0 cannot currently load .NET 8 self-contained single-file binaries (CoreCLR HRESULT `0x8007046C`).

---

## B · GUI testing (your turn)

Each item is a checkbox — work top to bottom. **Bold** items are the killer features.
For everything below, start `./dev.sh` (option 6 = server + app together) unless noted.

### B.0 Smoke

- [ ] `dotnet build OpenPoll.sln` → 0 warnings, 0 errors
- [ ] `./dev.sh` option 1 (build) succeeds
- [ ] `./dev.sh` option 2 launches OpenPoll
- [ ] `dotnet run --project OpenSlave` launches OpenSlave (separate window)
- [ ] Both windows show the OPENPOLL / OPENSLAVE letterspaced title
- [ ] On startup each prints `OpenPoll logging to ...` / `OpenSlave logging to ...` to the terminal

### B.1 OpenPoll — basics

- [ ] **Connect** to `127.0.0.1:1502` (start `./dev.sh` option 3 first, or OpenSlave manually)
- [ ] Status pill turns green, Poll counter increments
- [ ] Holding registers 0..9 show ticking values
- [ ] **Stop** → pill goes grey, counter freezes
- [ ] Reconnect works without app restart
- [ ] **File → Setup**: change Amount to 25, save → grid resizes immediately
- [ ] Editing Amount/Address mid-poll takes effect on next tick (no manual reconnect)
- [ ] Closing the window stops polling cleanly (no orphan threads in `ps aux`)

### B.2 Multi-register types & word orders

- [ ] Right-click a value cell → 16-bit / 32-bit / 64-bit sections
- [ ] **Pick "Float (32)"** on row 1 → cell shows a decimal number
- [ ] **Pick "Hex (64)"** on row 4 → "0xXXXXXXXXXXXXXXXX" (combines 4 registers)
- [ ] Edit a Float cell, type `3.14`, Enter → writes back to slave, shows on next poll
- [ ] **Setup → Word order: BADC byte-swap** → values change as expected
- [ ] Switch back to ABCD → values restore

### B.3 Scaling + value names *(new in v2.1.0)*

- [ ] Per-row **Scaling**: enable, set scale=`0.1`, offset=`0`, precision=`2` → raw `100` displays as `10.00`
- [ ] Per-row **Value Names**: import mapping `0=Idle\n1=Running\n2=Fault` → cells with raw 0/1/2 show the labels; other values fall through to numeric display

### B.4 Multi-tab UI

- [ ] **Ctrl+N** opens a second tab named "Poll 2"
- [ ] Tabs switch independently; close button on each tab
- [ ] Configure Poll 2 with a different Address → independent of Poll 1
- [ ] Connect each tab to different slaves — both poll in parallel
- [ ] **File → Duplicate poll** clones the active tab's settings
- [ ] **File → Close poll** removes a tab; can't close the last one (resets instead)

### B.5 Function codes — manual writes

- [ ] **Single-coil write**: with Coils function, edit a row's value `0`/`1`, Enter → FC 05
- [ ] **Single-register write**: with Holding Registers, edit a row, Enter → FC 06
- [ ] **Multi-register write** (32-bit): edit a Float cell, Enter → FC 16 (verify in Traffic Monitor)
- [ ] **Mask write (FC 22)**: `openpoll mask --ip ... --address 0 --and-mask 0x00FF --or-mask 0x1100` → register 0 modified per spec
- [ ] **Read/Write multi (FC 23)**: `openpoll rw --ip ... --read-address 0 --read-amount 4 --address 10 --value "1,2,3"` → atomic write-then-read

### B.6 Traffic monitor

- [ ] **Tools → Traffic Monitor…** opens a window
- [ ] Polling produces TX (→) / RX (←) entries with timestamps and addresses
- [ ] Each Read shows function code (e.g. "03 ReadHoldingRegisters") and a value summary
- [ ] **Pause** stops new entries; **Resume** continues; **Clear** wipes
- [ ] **Save…** writes the buffer to a `.log` (text) or `.csv` file
- [ ] Disconnecting/connection-fail produces a red ✗ Error entry with the reason

### B.7 Workspace save/load + recent files

- [ ] Open 3 tabs with different settings
- [ ] **File → Save workspace…** → save as `test.openpoll`; JSON contains `version`, `polls[]`
- [ ] Close the app, reopen → **File → Recent workspaces → test.openpoll** → all 3 tabs restored
- [ ] Recent menu lists 1..10 most recent in order, with **Clear list** entry

### B.8 OpenSlave — slave simulator

- [ ] Launch `dotnet run --project OpenSlave` (separate window)
- [ ] Set port to 1502, Slave ID = 1, click **● Start** → status pill green, "Listening on 0.0.0.0:1502"
- [ ] In OpenPoll, point at `127.0.0.1:1502`, NodeID 1, Holding Registers 0..9, Connect
- [ ] OpenPoll grid shows zeros (default values from the slave)
- [ ] **In OpenSlave, edit Holding Register row 0 to `42`, press Enter** → OpenPoll's row 0 shows `42` on next tick
- [ ] **From OpenPoll, write a register** → OpenSlave's grid updates within ~200 ms
- [ ] OpenSlave **Request log** tab shows "client FC03 read holding regs @ 0 × 10" lines

### B.9 OpenSlave — error simulation

- [ ] Set **Response delay** = 500 ms, Apply → OpenPoll's polls visibly slow (Tx/Rx delta = ~500 ms in Traffic Monitor)
- [ ] Enable **Skip 1-in-10 responses**, Apply → ~10% of OpenPoll polls show timeout errors
- [ ] Enable **Return Exception 06 (Slave Busy)**, Apply → every OpenPoll read fails with `Modbus exception 06 (Slave device busy)`

### B.10 OpenSlave — workspace + per-cell types

- [ ] Right-click a Holding Register row → set Type → **Float (32)** → cell renders as decimal, spans 2 registers
- [ ] Edit the Float cell → press Enter → writes both underlying registers
- [ ] **File → Save Workspace…** → save as `bench.openslave`
- [ ] Close & reopen, **File → Open Workspace…** → register values, types, and error-sim flags restored

### B.11 RTU over serial *(needs hardware)*

- [ ] OpenPoll **Connection Setup → Mode: Modbus Serial (RTU)** is selectable
- [ ] Serial port dropdown enumerates `/dev/ttyUSB*` / `/dev/ttyACM*` (Linux) or `COM*` (Windows)
- [ ] CLI: `openpoll read --serial /dev/ttyUSB0 --baud 19200 --parity even ...` connects to a real RTU slave
- [ ] **Permission test on Linux** without `dialout` group → expect "Permission denied"
- [ ] **Permission fix**: `sudo usermod -aG dialout $USER` → connection works

> NModbus's serial path is well-trodden; runtime risk is low. OpenSlave-RTU is Wave 2 work.

### B.12 HTTP API

- [ ] **Tools → Start HTTP API (:8080)** — status briefly shows "HTTP API on http://localhost:8080"
- [ ] `curl http://localhost:8080/` → HTML cheat sheet
- [ ] `curl http://localhost:8080/api/polls` → JSON listing the open polls
- [ ] `curl http://localhost:8080/api/polls/Poll%201/values` → JSON array of registers
- [ ] **POST /api/polls/{name}/write** with body `{"function":"06","address":0,"value":42}` → register 0 set to 42 on next poll
- [ ] **Tools → Stop HTTP API** stops it; subsequent curl gets connection-refused

### B.13 File logging

- [ ] On startup, terminal prints `OpenPoll logging to ~/.local/share/OpenPoll/logs/openpoll-YYYY-MM-DD.log`
- [ ] **Tools → Reveal log folder…** opens the folder in the OS file manager
- [ ] After a few polls, the file contains TX/RX/error lines with timestamps
- [ ] Same for OpenSlave: **View → Reveal log folder…** and `openslave-YYYY-MM-DD.log`

### B.14 Cross-platform publish

- [ ] `dotnet publish OpenPoll  -c Release -r linux-x64 --self-contained -o publish/linux-x64` → ~93 MB folder, ELF binary launches
- [ ] `dotnet publish OpenSlave -c Release -r linux-x64 --self-contained -o publish/linux-x64` → ELF binary launches
- [ ] `bash tests/docker/run-functional-test.sh linux-x64` → all 15 assertions pass
- [ ] Same on `-r win-x64` and verified on Windows (when available)

---

## Wave 2 shipped (v2.2.0)

- **FC 22 atomic** on TCP via raw PDU (R-M-W fallback for serial RTU)
- **FC 43 Read Device Identification** — `openpoll devid` CLI + Tools menu dialog; OpenSlave reports configurable vendor/product/version/URL strings
- **FC 8 / 11 / 17 diagnostics** — `openpoll diag` / `ec` / `srvid` CLI; OpenSlave maintains a per-request comm-event counter
- **Conditional cell colours** — per-poll rule list (eq/ne/lt/le/gt/ge/between), colour the VALUE column foreground; persisted in `.openpoll`
- **Manual write dialogs (F5/F6/F7/F8)** — Modbus-Poll-style modal entry for FC 05/06/15/16; menu items in File menu
- **Test Center** — Tools → Test Center: hand-craft hex PDU, inspect raw response
- **CSV snapshot logger** — Tools → Start CSV snapshot: appends one wide row per second to the logs dir
- **Live chart enhancements** — Y-axis min/max inputs, Export → PNG, Export → CSV
- **Multiple slave windows** — File → New Slave Window auto-increments the default port (1502, 1503, …)
- **Pattern generators in OpenSlave** — View → Patterns…: sine / triangle / square / sawtooth / random-walk drive register values
- **WebSocket live updates** — `/api/ws` endpoint streams JSON snapshots whenever a poll ticks
- **HTTP API bearer-token auth** — `--http-token <t>` or env `OPENPOLL_HTTP_TOKEN`; constant-time comparison

## Wave 2 — transport phase 2 shipped

- **Serial RTU slave** in OpenSlave — `openslave run --serial /dev/ttyUSB0 --baud 19200 ...`
- **Modbus over UDP** master and slave — `--udp <port>` on both
- **RTU-over-TCP** master and slave — `--rtu-over-tcp <port>` on both (gateway-style framing)
- **Modbus ASCII over TCP** master and slave — `--ascii <port>` on both
- **Modbus TCP + TLS** master and slave — `--tls <port>`; self-signed cert auto-generated by the slave; master accepts any cert by default
- **Print to PDF** in OpenPoll — Tools → Print to PDF… (Ctrl+P); A4 multi-page table via SkiaSharp

## Wave 3 backlog (none currently)

All Wave 2 work is shipped. Future ideas (not yet planned):
- Native print-dialog support (currently OpenPoll exports a PDF; users print via OS)
- Mutual-TLS (client cert auth) — current TLS is server-cert-only

---

## How to report a regression

If something is broken: paste the exact step from the list, expected behaviour, what you saw, and a screenshot if it's visual. Skip the build/setup steps — assume those work and only flag if they don't.
