# OpenPoll / OpenSlave — Test Plan

> **Automated suite: 142/142 passing** (112 unit + 30 integration).
> Two real defects were caught and fixed during testing — see §A.0 below.
> The GUI tests in §B onward need your eyes.

Run `./dev.sh` (option 6) for GUI testing. Run `bash tests/integration_test.sh` to re-run the integration suite. Run `dotnet test tests/OpenPoll.Tests/OpenPoll.Tests.csproj` for unit tests.

---

## A.0 · Real bugs found and fixed by the test suite

| # | Where | Bug | Fix |
|---|---|---|---|
| 1 | `ValueFormatter.cs` (Format Double) | `G15` precision lost `double.MaxValue`, parsed back as `Infinity` | Use `G17` (round-trip-safe) |
| 2 | `ValueFormatter.cs` (Format Float) | `G7` could lose precision near `float.MaxValue` (defensive) | Use `G9` |
| 3 | `HttpApiHost.cs` | `GET /` returned 404 because `TrimEnd('/')` of `"/"` is `""`, never matched `"/"` | Accept both `""` and `"/"` |
| — | EasyModbus 5.6 (library) | Reading exactly 125 registers throws `IndexOutOfRangeException` (Modbus protocol max is 125, but EasyModbus's internal handling has an off-by-one). | Documented; tests use 100. Real fix would be a library patch or replacement. |

---

## A · Automated tests (all 142 PASS)

### A.1 Unit tests (112 of 112 — `dotnet test`)

```text
ValueFormatterTests          76 tests   — every type × every word order, edge cases, round-trips
ModbusSessionTests           12 tests   — connect, read, write, multi-write, reconnect, fail paths
PollDefinitionTests           3 tests   — clone deep-copy, defaults
CellDataTypeTests            22 tests   — WordCount, prefix, IsWritable, IsRegister
```

Includes:
- All 12 `CellDataType` × all 4 `WordOrder` round-trip combinations
- Min/max for every numeric type (int.MinValue, double.MaxValue, ushort overflow…)
- Hex parsing with/without `0x` prefix, mixed case
- Binary parsing with/without spaces, length validation
- Coil parsing accepts `0/1/true/false/on/off`, rejects everything else
- Bad input rejection (non-numeric, overflow, malformed)
- Specific wire-byte assertions for ABCD/CDAB/BADC/DCBA conventions

### A.2 Integration tests (30 of 30 — `bash tests/integration_test.sh`)

Runs against a live `OpenSlave run` instance on a dynamic port.

```text
Build                                             1 test
Slave lifecycle                                   1 test
Read every function code (01, 02, 03, 04)         4 tests
Write every function code (05, 06, 15, 16)        5 tests (with read-back verification)
Edge cases (signed wrap, 100 regs, OOB)           3 tests
Failure paths (refused, unreachable, wrong slave) 3 tests
Scans (ID, register sweep, IP sweep)              4 tests
Concurrency (10 parallel readers)                 1 test
Stress (50 rapid writes + verify)                 1 test
32-bit float round-trip via FC 16 + FC 03         1 test
HTTP API (listen, GET /, /api/polls, 404 path)    5 tests
Workspace .openpoll JSON format                   1 test
```

### A.3 Smoke

- [x] `dotnet build OpenPoll.sln` — 0 warnings, 0 errors
- [x] `dotnet run --project OpenPoll -- help` — usage text printed
- [x] `dotnet run --project OpenSlave -- help` — usage text printed

### A.2 OpenSlave runs headless

```bash
dotnet run --project OpenSlave -- run --port 1505 \
  --hr "1=100,2=200,3=300,4=400,5=500" \
  --ir "1=-100,2=-200,3=-300" \
  --coil "1=1,2=0,3=1,4=0,5=1" \
  --di "1=1,2=1,3=0,4=0,5=1" \
  --quiet
```

- [x] binds port 1505 (verified via `ss -tln`)
- [x] seeds all four register tables
- [x] terminates cleanly on SIGINT

### A.3 OpenPoll read — every read function code

| FC | Command | Result |
|---|---|---|
| 03 | `read --port 1505 --address 0 --amount 5 --function 03` | `[100,200,300,400,500]` ✅ |
| 04 | `read --port 1505 --address 0 --amount 3 --function 04` | `[-100,-200,-300]` ✅ |
| 01 | `read --port 1505 --address 0 --amount 5 --function 01` | `[true,false,true,false,true]` ✅ |
| 02 | `read --port 1505 --address 0 --amount 5 --function 02` | `[true,true,false,false,true]` ✅ |

### A.4 OpenPoll write — every write function code, verified by readback

| FC | Op | Verification |
|---|---|---|
| 05 | Write coil @ 10 = true | Read FC 01 @ 10 = `[true]` ✅ |
| 06 | Write reg @ 20 = 12345 | Read FC 03 @ 20 = `[12345]` ✅ |
| 15 | Write coils @ 30 = `1,1,0,1,0` | Read FC 01 @ 30 = `[true,true,false,true,false]` ✅ |
| 16 | Write regs @ 40 = `1000,2000,3000` | Read FC 03 @ 40 = `[1000,2000,3000]` ✅ |

### A.5 Failure paths

- [x] Connection-refused: `read --port 9999` → `{"ok":false,"stage":"connect","error":"Network: Connection refused"}` (exit code 1)

### A.6 Scans

- [x] **ID scan**: `scan --type id --port 1505 --start 1 --end 3` → only id=1 found, others "Node ID error"
- [x] **Register sweep**: `scan --type registers --port 1505 --address 0 --amount 5 --function 03` → emits `{addr,ok,value}` per register
- [x] **IP sweep**: `scan --type ip --base 127.0.0.0 --port 1505 --timeout 50` → emits per-IP responding flag (loopback responded as expected)

### A.7 HTTP API

- [x] `serve --http 8082` starts; `curl http://localhost:8082/api/polls` returns workspace JSON
- [x] `curl http://localhost:8082/` returns the HTML cheat-sheet
- [x] Server stops cleanly on SIGINT (port released)

### A.8 Cross-platform publish

- [x] `dotnet publish OpenPoll -c Release -r linux-x64 --self-contained` → 93 MB folder, ELF binary launches
- [x] `dotnet publish OpenSlave -c Release -r linux-x64 --self-contained` → 92 MB folder, ELF binary launches
- [ ] Same on `-r win-x64` and verified on Windows (needs your machine)

---

## B · GUI testing (your turn)

Each item is a checkbox — work top to bottom. **Bold** items are the killer features.
For everything below, start `./dev.sh` option 6 (server + app together) unless noted.

---

## 0 · Smoke

- [ ] `dotnet build OpenPoll.sln` → 0 warnings, 0 errors
- [ ] `./dev.sh` option 1 (build) succeeds
- [ ] `./dev.sh` option 2 launches OpenPoll
- [ ] `dotnet run --project OpenSlave` launches OpenSlave (separate window)
- [ ] Both windows show the OPENPOLL / OPENSLAVE letterspaced title
- [ ] No flicker / black screens during launch

## 1 · Phase 0 rename verification

- [ ] Settings file is at `~/.config/OpenPoll/settings.json`, not `EasyBus`
- [ ] Brand bar reads "OPENPOLL" (not "EASYBUS")
- [ ] No remaining `ModbusScanner` strings in window titles or menus

## 2 · Phase A — single-poll basics (regression check)

- [ ] **Connect** to `127.0.0.1:1502` (start `./dev.sh` option 3 first)
- [ ] Status pill turns green, Poll counter increments
- [ ] Holding registers 1..10 show ticking values
- [ ] **Stop** → pill goes grey, counter freezes
- [ ] Reconnect works without app restart
- [ ] **File → Setup**: change Amount to 25, save → grid resizes immediately
- [ ] Editing Amount/Address mid-poll takes effect on next tick (no manual reconnect needed)
- [ ] Closing the window stops polling cleanly (no orphan threads in `ps aux`)

## 3 · Phase B — multi-register types

With holding registers selected, Connect:

- [ ] Right-click a value cell → menu shows three sections: 16-bit, 32-bit, 64-bit
- [ ] **Pick "Float (32)"** on row 1 → cell shows a decimal number
- [ ] Pick "Signed (32)" on row 2 → cell shows a signed 32-bit value (combines reg 2 + 3)
- [ ] Pick "Hex (32)" on row 3 → "0xXXXXXXXX"
- [ ] Pick "Hex (64)" on row 4 → "0xXXXXXXXXXXXXXXXX" (combines 4 registers)
- [ ] Edit a Float cell, type `3.14`, press Enter → writes back to slave
- [ ] **Setup → Word order: BADC — byte swap** → values change as expected
- [ ] Switch back to "ABCD — big-endian" → values restore

## 4 · Phase C — multi-tab UI

- [ ] **Ctrl+N** (or File → New poll) opens a second tab named "Poll 2"
- [ ] Click the second tab → status bar / data grid switch to it
- [ ] Tab strip shows × close button on each tab
- [ ] Configure Poll 2 with a different Address (e.g. 50) → independent of Poll 1
- [ ] **Connect each tab independently** — Poll 1 polling holding regs, Poll 2 polling coils, both run in parallel
- [ ] **File → Duplicate poll** clones the active tab's settings
- [ ] **File → Close poll** removes a tab; can't close the last one (it resets instead)
- [ ] Switching tabs does NOT lose poll state of inactive tabs

## 5 · Phase D — function codes

- [ ] **Single-coil write**: with Coils function, edit a row's value `0`/`1`, Enter → writes via FC 05
- [ ] **Single-register write**: with Holding Registers, edit a row, Enter → FC 06
- [ ] **Multi-register write** (32-bit): edit a Float cell, Enter → FC 16 (verify in Traffic Monitor below)
- [ ] All edits show the new value reflected on the next poll tick

## 6 · Phase E — traffic monitor

- [ ] **Tools → Traffic Monitor…** opens a window
- [ ] Start polling — TX (→) and RX (←) entries appear with timestamps and addresses
- [ ] Each Read shows function code (e.g. "03 ReadHoldingRegisters") and a value summary `[100,200,...]`
- [ ] Edit a register → an entry like "10 WriteMultipleRegisters" appears
- [ ] **Pause** button stops new entries from appearing
- [ ] **Resume** then **Clear** wipes the log
- [ ] Disconnecting/connection-fail produces a red ✗ Error entry with the reason

## 7 · Phase F — workspace save/load

- [ ] Open 3 tabs with different settings
- [ ] **File → Save workspace…** → pick a path, save as `test.openpoll`
- [ ] Open the saved file in a text editor → JSON with `version`, `polls[]` array
- [ ] **File → Open workspace…** → pick the same file → all 3 tabs restored with their settings
- [ ] Close the app, reopen, load the workspace → tabs recreated correctly

## 8 · Phase G — OpenSlave

- [ ] `dotnet run --project OpenSlave` launches a separate window
- [ ] Set port to 1502 (or a free port), Unit ID = 1, click **● Start**
- [ ] Status pill goes green; "Listening on 0.0.0.0:1502"
- [ ] **From OpenPoll**, point a poll at `127.0.0.1:1502`, NodeID 1, Holding Registers 1..10, Connect
- [ ] OpenPoll grid shows zeros (default values from the slave grid)
- [ ] **In OpenSlave**, Holding Registers tab → edit row 1 to `42`, press Enter → OpenPoll's row 1 shows `42` on next tick
- [ ] **In OpenPoll**, edit a register and write → OpenSlave's grid updates within ~200ms (sync timer interval)
- [ ] OpenSlave **Request log** tab shows "clients changed HRs @ X × Y" entries when OpenPoll writes
- [ ] OpenSlave **Stop** button stops the listener; OpenPoll connection drops with an error
- [ ] OpenSlave Coils / Discrete inputs / Input registers tabs work the same way

## 9 · Phase H — RTU over serial *(needs hardware)*

- [ ] **Connection Setup → Mode: Modbus Serial (RTU)** — visible
- [ ] Serial port dropdown enumerates `/dev/ttyUSB*` / `/dev/ttyACM*` (Linux) or `COM*` (Windows)
- [ ] With a USB-RS485 dongle and a real Modbus slave (or another USB dongle running OpenSlave-RTU eventually):
    - [ ] Connect succeeds
    - [ ] Polling reads valid data
    - [ ] Writes work
- [ ] **Permission test on Linux**: without `dialout` group → expect a clear "Permission denied" message
- [ ] **Permission fix**: `sudo usermod -aG dialout $USER`, log out/in → connection works

> Code path is plumbed; only real-hardware loop remaining. EasyModbus's serial side has been the same since 2019; runtime risk is low.

## 10 · Phase I — quality of life

### Address base toggle

- [ ] **File → Setup → Address base: 1-indexed (PLC display)**
- [ ] Grid ADDR column now starts at `1` instead of `0`
- [ ] Wire-level reads still hit the same registers (verify in Traffic Monitor — TX shows the wire address, ADDR shows display address)

### Word order

- [ ] **File → Setup → Word order: CDAB — word swap**
- [ ] 32-bit values change accordingly
- [ ] Float values change

## 11 · Phase J — HTTP API

- [ ] **Tools → Start HTTP API (:8080)** menu item
- [ ] Status briefly shows "HTTP API on http://localhost:8080"
- [ ] Open browser to `http://localhost:8080/` → see the small HTML cheat sheet
- [ ] `curl http://localhost:8080/api/polls` → JSON listing the open polls
- [ ] `curl http://localhost:8080/api/polls/Poll%201/values` → JSON array of registers (only meaningful while polling is connected)
- [ ] **Tools → Stop HTTP API** stops it; subsequent curl gets connection-refused
- [ ] Restart the app → port is released cleanly

## 12 · Cross-platform

- [ ] `dotnet publish OpenPoll -c Release -r linux-x64 --self-contained -o publish/linux-x64/openpoll` produces ~93 MB folder
- [ ] `dotnet publish OpenSlave -c Release -r linux-x64 --self-contained -o publish/linux-x64/openslave` produces ~92 MB folder
- [ ] `./publish/linux-x64/openpoll/OpenPoll` launches standalone
- [ ] `./publish/linux-x64/openslave/OpenSlave` launches standalone
- [ ] Repeat publish with `-r win-x64` → `OpenPoll.exe` / `OpenSlave.exe` produced (smoke test on Windows machine when available)

---

## Known gaps (NOT implemented yet — flag if you want any)

- **Per-cell scaling** (`raw × k + offset`): collected in `PollDefinition.WordOrder` but no per-cell expression evaluator yet. Phase I+ work.
- **Conditional cell colours** ("if value > 1000 turn red"): UI work deferred.
- **"Write selected" command** (multi-row write in one click): multi-register writes work for individual cells via FC 16, but no batch-write-selected command yet.
- **FC 22** (Mask Write Register): not exposed.
- **FC 23** (Read/Write Multiple): not exposed.
- **FC 43/14** (Read Device ID): not exposed.
- **FC 8 / 11 / 17** (serial diagnostics): not exposed.
- **Multi-slave in OpenSlave**: currently one slave per OpenSlave instance. Run multiple OpenSlave processes for now.
- **Simulated value patterns** in OpenSlave (sine, ramp, replay-from-CSV): not implemented.
- **WebSocket live updates** in HTTP API: only REST is wired; no `/ws/...` endpoint yet.
- **POST /api/polls/{name}/write** endpoint: not yet implemented (read-only API for now).
- **HTTP API auth**: none. Localhost-only by default.
- **Recent files menu** for workspace: not implemented.
- **Modbus ASCII** and **UDP** transports: code paths not written (only TCP and RTU TCP/serial via EasyModbus).
- **Real RTU hardware test**: needs USB-RS485 + Modbus slave on your end.

---

## How to report a regression

If something is broken: paste the exact step from the list, expected behaviour, what you saw, and a screenshot if it's visual. Skip the build/setup steps (1-2) — assume those work and only flag if they don't.
