#!/usr/bin/env bash
# Run a Modbus master/slave round-trip test against published binaries inside a
# Docker container. Verifies that the slave seeds register/coil values, the
# master reads them back, and a write/read-back cycle succeeds.
#
# Usage: run-functional-test.sh <platform>
#   platform: linux-x64 | win-x64
#
# Notes:
#   linux-x64 — fully exercised inside mcr.microsoft.com/dotnet/runtime-deps:8.0.
#   win-x64   — best-effort under wine64. Wine 9.0 currently cannot load .NET 8
#               self-contained single-file binaries (CoreCLR HRESULT 0x8007046C),
#               so the test will fail on stock Wine. Kept here for future runs
#               against newer Wine or alternative Windows runners.

set -uo pipefail

PLATFORM="${1:-linux-x64}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PUBLISH_DIR="$PROJECT_ROOT/publish/$PLATFORM"

if [[ ! -d "$PUBLISH_DIR" ]]; then
  echo "FAIL: publish dir not found: $PUBLISH_DIR" >&2
  exit 1
fi

case "$PLATFORM" in
  linux-x64)
    IMAGE="mcr.microsoft.com/dotnet/runtime-deps:8.0"
    OPENPOLL_BIN="/app/OpenPoll"
    OPENSLAVE_BIN="/app/OpenSlave"
    PRE_CMD=""
    ;;
  win-x64)
    IMAGE="ubuntu:24.04"
    OPENPOLL_BIN="/usr/lib/wine/wine64 /app/OpenPoll.exe"
    OPENSLAVE_BIN="/usr/lib/wine/wine64 /app/OpenSlave.exe"
    # Ubuntu's wine64 package only installs the binary under /usr/lib/wine — no /usr/bin symlink.
    PRE_CMD="set -e; export DEBIAN_FRONTEND=noninteractive WINEDEBUG=-all DISPLAY= WINEDLLOVERRIDES='mscoree,mshtml='; apt-get update -qq; apt-get install -y --no-install-recommends -qq wine64 ca-certificates; mkdir -p /tmp/wineprefix; export WINEPREFIX=/tmp/wineprefix HOME=/tmp; /usr/lib/wine/wine64 wineboot --init >/dev/null 2>&1 || true; set +e"
    ;;
  *)
    echo "FAIL: unknown platform: $PLATFORM (expected linux-x64 or win-x64)" >&2
    exit 1
    ;;
esac

read -r -d '' TEST_SCRIPT <<EOSCRIPT || true
set -u
${PRE_CMD}

echo ">>> [${PLATFORM}] starting OpenSlave (port 1502, seeded HRs and coils)"
${OPENSLAVE_BIN} run --port 1502 --hr 1=111,2=222,3=333 --coil 1=1,2=0,3=1 --quiet &
SLAVE_PID=\$!
trap "kill -TERM \$SLAVE_PID 2>/dev/null || true" EXIT

# Wait up to 30s for the listener to come up.
for i in \$(seq 1 30); do
  if (echo > /dev/tcp/127.0.0.1/1502) 2>/dev/null; then
    echo ">>> OpenSlave listening (after \${i}s)"
    break
  fi
  sleep 1
done

if ! (echo > /dev/tcp/127.0.0.1/1502) 2>/dev/null; then
  echo "FAIL: OpenSlave never opened port 1502"
  exit 1
fi

# Give the wine'd .NET runtime an extra grace beat once the port is up.
sleep 1

run_step() {
  local label="\$1"; shift
  local pattern="\$1"; shift
  echo ">>> [\$label] \$*"
  local out
  out=\$("\$@" 2>&1)
  echo "    out: \$out"
  if ! echo "\$out" | grep -q '"ok":true'; then
    echo "FAIL: [\$label] missing \"ok\":true"
    exit 1
  fi
  if ! echo "\$out" | grep -q "\$pattern"; then
    echo "FAIL: [\$label] expected pattern: \$pattern"
    exit 1
  fi
  echo "    PASS"
}

run_step "FC03 read holding regs"      '\[111,222,333\]' ${OPENPOLL_BIN} read  --ip 127.0.0.1 --port 1502 --slave 1 --address 0 --amount 3 --function 03
run_step "FC01 read coils"             '\[true,false,true\]' ${OPENPOLL_BIN} read  --ip 127.0.0.1 --port 1502 --slave 1 --address 0 --amount 3 --function 01
run_step "FC06 write HR @4 = 999"      '"ok":true' ${OPENPOLL_BIN} write --ip 127.0.0.1 --port 1502 --slave 1 --address 4 --value 999 --function 06
run_step "FC03 read back HR @4"        '\[999\]'  ${OPENPOLL_BIN} read  --ip 127.0.0.1 --port 1502 --slave 1 --address 4 --amount 1 --function 03
run_step "FC05 write coil @5 = on"     '"ok":true' ${OPENPOLL_BIN} write --ip 127.0.0.1 --port 1502 --slave 1 --address 5 --value 1 --function 05
run_step "FC01 read back coil @5"      '\[true\]' ${OPENPOLL_BIN} read  --ip 127.0.0.1 --port 1502 --slave 1 --address 5 --amount 1 --function 01

echo ">>> [${PLATFORM}] all assertions PASS"
EOSCRIPT

echo "=== Functional test: $PLATFORM ($IMAGE) ==="
docker run --rm \
  -v "$PUBLISH_DIR:/app:ro" \
  --entrypoint bash \
  "$IMAGE" \
  -c "$TEST_SCRIPT"
