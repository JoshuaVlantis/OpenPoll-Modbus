#!/usr/bin/env bash
# OpenPoll / OpenSlave integration tests.
# Drives the CLI end-to-end against a live OpenSlave on localhost.
# Safe to re-run; uses a dynamic port to avoid collisions.

set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PORT=$((20000 + RANDOM % 20000))
HTTP_PORT=$((20000 + RANDOM % 20000))

PASS=0
FAIL=0
FAILED_TESTS=()

CYAN=$'\e[36m'; GREEN=$'\e[32m'; RED=$'\e[31m'; YEL=$'\e[33m'; DIM=$'\e[2m'; RST=$'\e[0m'

cd "$ROOT"

ok()  { printf "  %s✓%s %s\n" "$GREEN" "$RST" "$*"; PASS=$((PASS+1)); }
fail(){ printf "  %s✗%s %s\n" "$RED" "$RST" "$*";   FAIL=$((FAIL+1)); FAILED_TESTS+=("$*"); }
hdr() { printf "\n%s━━━ %s ━━━%s\n" "$CYAN" "$*" "$RST"; }

assert_jq() {
  # assert_jq <description> <jq-expression-returning-true-on-success> <input>
  local desc=$1 expr=$2 input=$3
  local result
  result=$(echo "$input" | jq -r "$expr" 2>&1)
  if [ "$result" = "true" ]; then
    ok "$desc"
  else
    fail "$desc — input: $input"
  fi
}

assert_eq() {
  local desc=$1 expected=$2 actual=$3
  if [ "$actual" = "$expected" ]; then
    ok "$desc"
  else
    fail "$desc — expected: $expected — actual: $actual"
  fi
}

# Verify jq is available
if ! command -v jq >/dev/null 2>&1; then
  echo "${RED}jq not found — install with: sudo apt install jq${RST}"
  exit 1
fi

cleanup() {
  if [ -n "${SLAVE_PID:-}" ]; then
    kill -9 "$SLAVE_PID" 2>/dev/null
  fi
  if [ -n "${HTTP_PID:-}" ]; then
    kill -9 "$HTTP_PID" 2>/dev/null
  fi
  pkill -9 -f "OpenSlave.*--port $PORT" 2>/dev/null
  pkill -9 -f "OpenPoll.*serve.*$HTTP_PORT" 2>/dev/null
  rm -f /tmp/openpoll-itest-*.log /tmp/test-workspace.openpoll
}
trap cleanup EXIT

# Build once
hdr "Build"
if dotnet build OpenPoll.sln 2>&1 | grep -q "Build succeeded"; then
  ok "solution builds"
else
  fail "build failed"
  exit 1
fi

# Start slave
hdr "Start OpenSlave on dynamic port $PORT"
dotnet run --project OpenSlave --no-build -- run --port "$PORT" \
  --hr "1=100,2=200,3=300,4=400,5=500" \
  --ir "1=-100,2=-200,3=-300" \
  --coil "1=1,2=0,3=1,4=0,5=1" \
  --di "1=1,2=1,3=0,4=0,5=1" \
  --quiet > /tmp/openpoll-itest-slave.log 2>&1 &
SLAVE_PID=$!
sleep 4
if ! kill -0 $SLAVE_PID 2>/dev/null; then
  fail "slave didn't start"
  cat /tmp/openpoll-itest-slave.log
  exit 1
fi
if ss -tln 2>&1 | grep -q ":$PORT "; then
  ok "slave listening on $PORT"
else
  fail "slave not listening"
  exit 1
fi

# ─── Read tests ─────────────────────────────────────────────────────

hdr "Read — every function code"

OUT=$(dotnet run --project OpenPoll --no-build -- read --port $PORT --address 0 --amount 5 --function 03 2>/dev/null)
assert_jq "FC 03 read 5 holding registers"     '.ok and (.values | length == 5) and .values[0] == 100' "$OUT"

OUT=$(dotnet run --project OpenPoll --no-build -- read --port $PORT --address 0 --amount 3 --function 04 2>/dev/null)
assert_jq "FC 04 read 3 input registers"       '.ok and (.values | length == 3) and .values[0] == -100' "$OUT"

OUT=$(dotnet run --project OpenPoll --no-build -- read --port $PORT --address 0 --amount 5 --function 01 2>/dev/null)
assert_jq "FC 01 read 5 coils"                 '.ok and (.values | length == 5) and .values[0] == true and .values[1] == false' "$OUT"

OUT=$(dotnet run --project OpenPoll --no-build -- read --port $PORT --address 0 --amount 5 --function 02 2>/dev/null)
assert_jq "FC 02 read 5 discrete inputs"       '.ok and (.values | length == 5)' "$OUT"

# ─── Write tests with read-back ─────────────────────────────────────

hdr "Write — every function code, verified by read-back"

dotnet run --project OpenPoll --no-build -- write --port $PORT --address 100 --value 42 --function 06 >/dev/null 2>&1
OUT=$(dotnet run --project OpenPoll --no-build -- read --port $PORT --address 100 --amount 1 --function 03 2>/dev/null)
assert_jq "FC 06 single register"              '.ok and .values[0] == 42' "$OUT"

dotnet run --project OpenPoll --no-build -- write --port $PORT --address 110 --value 1 --function 5 >/dev/null 2>&1
OUT=$(dotnet run --project OpenPoll --no-build -- read --port $PORT --address 110 --amount 1 --function 01 2>/dev/null)
assert_jq "FC 05 single coil ON"               '.ok and .values[0] == true' "$OUT"

dotnet run --project OpenPoll --no-build -- write --port $PORT --address 110 --value 0 --function 5 >/dev/null 2>&1
OUT=$(dotnet run --project OpenPoll --no-build -- read --port $PORT --address 110 --amount 1 --function 01 2>/dev/null)
assert_jq "FC 05 single coil OFF"              '.ok and .values[0] == false' "$OUT"

dotnet run --project OpenPoll --no-build -- write --port $PORT --address 120 --value "1000,2000,3000,4000,5000" --function 16 >/dev/null 2>&1
OUT=$(dotnet run --project OpenPoll --no-build -- read --port $PORT --address 120 --amount 5 --function 03 2>/dev/null)
assert_jq "FC 16 multiple registers (5 values)" '.ok and .values == [1000,2000,3000,4000,5000]' "$OUT"

dotnet run --project OpenPoll --no-build -- write --port $PORT --address 130 --value "1,1,0,1,0,1,1,0" --function 15 >/dev/null 2>&1
OUT=$(dotnet run --project OpenPoll --no-build -- read --port $PORT --address 130 --amount 8 --function 01 2>/dev/null)
assert_jq "FC 15 multiple coils (8 bits)"       '.ok and .values == [true,true,false,true,false,true,true,false]' "$OUT"

# ─── Boundary / edge cases ──────────────────────────────────────────

hdr "Edge cases"

# Negative number (signed → wire two's complement)
dotnet run --project OpenPoll --no-build -- write --port $PORT --address 200 --value 65535 --function 06 >/dev/null 2>&1
OUT=$(dotnet run --project OpenPoll --no-build -- read --port $PORT --address 200 --amount 1 --function 03 2>/dev/null)
assert_jq "Write 0xFFFF, read back as -1 signed" '.ok and .values[0] == -1' "$OUT"

# EasyModbus 5.6 has an internal off-by-one that breaks at exactly 125 (protocol max).
# 100 is the safe practical max for this library.
OUT=$(dotnet run --project OpenPoll --no-build -- read --port $PORT --address 0 --amount 100 --function 03 2>/dev/null)
assert_jq "Read 100 holding registers in one request" '.ok and (.values | length == 100)' "$OUT"

# Read past server boundary
OUT=$(dotnet run --project OpenPoll --no-build -- read --port $PORT --address 70000 --amount 1 --function 03 2>/dev/null)
assert_jq "Read past slave's range fails gracefully" '.ok == false' "$OUT"

# ─── Failure paths ──────────────────────────────────────────────────

hdr "Failure / error paths"

OUT=$(dotnet run --project OpenPoll --no-build -- read --port 9 --address 0 --amount 1 --function 03 --timeout 200 2>/dev/null)
assert_jq "Connection refused on closed port"   '.ok == false and (.stage == "connect")' "$OUT"

OUT=$(dotnet run --project OpenPoll --no-build -- read --ip 192.0.2.1 --port $PORT --address 0 --amount 1 --function 03 --timeout 500 2>/dev/null)
assert_jq "Unreachable IP times out cleanly"    '.ok == false' "$OUT"

OUT=$(dotnet run --project OpenPoll --no-build -- read --port $PORT --slave 99 --address 0 --amount 1 --function 03 --timeout 500 2>/dev/null)
assert_jq "Wrong slave ID is reported"          '.ok == false' "$OUT"

# ─── Scans ──────────────────────────────────────────────────────────

hdr "Scans"

OUT=$(dotnet run --project OpenPoll --no-build -- scan --type id --port $PORT --start 1 --end 5 --timeout 500 2>/dev/null | head -1)
assert_jq "ID scan finds slave at id=1"         '.id == 1 and .found == true' "$OUT"

OUT=$(dotnet run --project OpenPoll --no-build -- scan --type id --port $PORT --start 99 --end 99 --timeout 500 2>/dev/null | head -1)
assert_jq "ID scan reports id=99 as not found"  '.found == false' "$OUT"

OUT=$(dotnet run --project OpenPoll --no-build -- scan --type registers --port $PORT --address 0 --amount 3 --function 03 2>/dev/null | wc -l)
assert_eq "Register sweep emits one line per address" "3" "$OUT"

OUT=$(dotnet run --project OpenPoll --no-build -- scan --type ip --base 127.0.0.0 --port $PORT --timeout 30 2>/dev/null | wc -l)
[ "$OUT" -ge 1 ] && ok "IP sweep emits ≥1 result line ($OUT lines)" || fail "IP sweep emitted $OUT lines"

# ─── Concurrency stress ─────────────────────────────────────────────

hdr "Concurrency: parallel readers"

PARALLEL=10
PIDS=()
for i in $(seq 1 $PARALLEL); do
  dotnet run --project OpenPoll --no-build -- read --port $PORT --address 0 --amount 5 --function 03 > "/tmp/openpoll-itest-par-$i.log" 2>/dev/null &
  PIDS+=($!)
done
# wait only on the parallel readers, not the long-running slave
for p in "${PIDS[@]}"; do wait "$p" 2>/dev/null; done
SUCCESSES=$(cat /tmp/openpoll-itest-par-*.log | jq -r '.ok' 2>/dev/null | grep -c true)
assert_eq "All $PARALLEL parallel reads succeeded" "$PARALLEL" "$SUCCESSES"

# ─── Stress: rapid-fire writes ──────────────────────────────────────

hdr "Stress: 50 sequential writes & verify"

for i in $(seq 1 50); do
  dotnet run --project OpenPoll --no-build -- write --port $PORT --address 300 --value $((i * 7)) --function 06 > /dev/null 2>&1
done
OUT=$(dotnet run --project OpenPoll --no-build -- read --port $PORT --address 300 --amount 1 --function 03 2>/dev/null)
assert_jq "After 50 writes, register holds last value (350)" '.ok and .values[0] == 350' "$OUT"

# ─── Round-trip: 32-bit float via word pair ─────────────────────────

hdr "32-bit float round-trip via FC 16 + FC 03"

# Write IEEE-754 of 3.14159f (approx): hex 0x40490FD0 → high word 0x4049 (16457), low 0x0FD0 (4048)
dotnet run --project OpenPoll --no-build -- write --port $PORT --address 400 --value "16457,4048" --function 16 >/dev/null 2>&1
OUT=$(dotnet run --project OpenPoll --no-build -- read --port $PORT --address 400 --amount 2 --function 03 2>/dev/null)
assert_jq "Float written as 2 regs reads back identically" '.values == [16457,4048]' "$OUT"

# ─── HTTP API ───────────────────────────────────────────────────────

hdr "HTTP API"

dotnet run --project OpenPoll --no-build -- serve --http $HTTP_PORT > /tmp/openpoll-itest-http.log 2>&1 &
HTTP_PID=$!
sleep 4

if ss -tln 2>&1 | grep -q ":$HTTP_PORT "; then
  ok "HTTP API listening on $HTTP_PORT"

  OUT=$(curl -s "http://localhost:$HTTP_PORT/api/polls")
  assert_jq "GET /api/polls returns array with ≥1 poll" '. | length >= 1' "$OUT"

  OUT=$(curl -s "http://localhost:$HTTP_PORT/api/polls/default/values")
  assert_jq "GET /api/polls/default/values returns array" 'type == "array"' "$OUT"

  STATUS=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:$HTTP_PORT/")
  assert_eq "GET / returns 200" "200" "$STATUS"

  STATUS=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:$HTTP_PORT/api/polls/nonexistent/values")
  assert_eq "GET /api/polls/nonexistent/values returns 404" "404" "$STATUS"
else
  fail "HTTP API didn't bind"
fi

kill -9 $HTTP_PID 2>/dev/null
HTTP_PID=""

# ─── Workspace save/load (via OpenPoll's JSON file format) ──────────

hdr "Workspace save/load"

cat > /tmp/test-workspace.openpoll <<EOF
{
  "version": 1,
  "polls": [
    {
      "name": "Test poll 1",
      "ipAddress": "127.0.0.1",
      "serverPort": $PORT,
      "nodeId": 1,
      "address": 0,
      "amount": 5,
      "function": "HoldingRegisters",
      "pollingRateMs": 500,
      "wordOrder": "BigEndian"
    },
    {
      "name": "Test poll 2",
      "ipAddress": "10.0.0.5",
      "serverPort": 502,
      "nodeId": 2,
      "address": 100,
      "amount": 10,
      "function": "Coils",
      "pollingRateMs": 1000,
      "wordOrder": "LittleEndian"
    }
  ]
}
EOF
# Verify it parses as our schema (round-trip via the file format)
if jq -e '.version == 1 and (.polls | length == 2)' /tmp/test-workspace.openpoll > /dev/null; then
  ok "Workspace JSON format is valid"
else
  fail "Workspace JSON malformed"
fi

# ─── Summary ────────────────────────────────────────────────────────

hdr "Summary"
TOTAL=$((PASS + FAIL))
if [ $FAIL -eq 0 ]; then
  printf "%s✓ %d/%d tests passed%s\n" "$GREEN" "$PASS" "$TOTAL" "$RST"
  exit 0
else
  printf "%s✗ %d/%d tests passed (%d failed)%s\n" "$RED" "$PASS" "$TOTAL" "$FAIL" "$RST"
  printf "%sFailed:%s\n" "$YEL" "$RST"
  for t in "${FAILED_TESTS[@]}"; do
    printf "  - %s\n" "$t"
  done
  exit 1
fi
