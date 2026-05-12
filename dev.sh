#!/usr/bin/env bash
# OpenPoll — interactive dev menu
# Build, run, restart, publish, clean. Designed for the local Linux dev loop.
set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

APP_PROJ="OpenPoll"
SLAVE_PROJ="OpenSlave"
SERVER_PROJ="OpenPoll.TestServer"
SOLUTION="OpenPoll.sln"

LOG_DIR="${TMPDIR:-/tmp}/openpoll-dev"
mkdir -p "$LOG_DIR"
SERVER_LOG="$LOG_DIR/server.log"
SLAVE_LOG="$LOG_DIR/slave.log"

# ─── colour helpers (skipped when stdout isn't a TTY) ───────────────────
if [[ -t 1 ]]; then
  C_CYAN=$'\e[36m'; C_YEL=$'\e[33m'; C_RED=$'\e[31m'; C_GRN=$'\e[32m'; C_DIM=$'\e[2m'; C_RST=$'\e[0m'
else
  C_CYAN=; C_YEL=; C_RED=; C_GRN=; C_DIM=; C_RST=
fi
say()  { printf '%s%s%s\n' "$C_CYAN" "$*" "$C_RST"; }
warn() { printf '%s%s%s\n' "$C_YEL"  "$*" "$C_RST"; }
err()  { printf '%s%s%s\n' "$C_RED"  "$*" "$C_RST" >&2; }
ok()   { printf '%s%s%s\n' "$C_GRN"  "$*" "$C_RST"; }

# ─── process discovery ──────────────────────────────────────────────────
server_pid()      { pgrep -f "$SERVER_PROJ"                           2>/dev/null | head -1; }
app_pid()         { pgrep -f "bin/Debug/net8.0/$APP_PROJ\.dll"        2>/dev/null | head -1; }
slave_pid()       { pgrep -f "bin/Debug/net8.0/$SLAVE_PROJ\.dll"      2>/dev/null | head -1; }
server_running()  { [[ -n "$(server_pid)" ]]; }
app_running()     { [[ -n "$(app_pid)"    ]]; }
slave_running()   { [[ -n "$(slave_pid)"  ]]; }

# ─── commands ───────────────────────────────────────────────────────────
cmd_build() {
  say "Building solution…"
  dotnet build "$SOLUTION"
}

cmd_run_app() {
  say "Running app (close the window or Ctrl+C here to return to the menu)…"
  dotnet run --project "$APP_PROJ" || true
}

cmd_run_server_bg() {
  if server_running; then
    warn "Test server already running (pid $(server_pid))"
    return
  fi
  say "Starting test server in background…"
  nohup dotnet run --project "$SERVER_PROJ" >"$SERVER_LOG" 2>&1 &
  disown || true
  sleep 1
  if server_running; then
    ok "Test server up (pid $(server_pid))"
    printf '%slog: %s%s\n' "$C_DIM" "$SERVER_LOG" "$C_RST"
  else
    err "Server failed to start"
    tail -20 "$SERVER_LOG" 2>/dev/null || true
  fi
}

cmd_stop_server() {
  if ! server_running; then
    warn "Test server not running"
    return
  fi
  pkill -f "$SERVER_PROJ" 2>/dev/null || true
  sleep 0.3
  if server_running; then
    err "Server didn't stop. Try: pkill -9 -f $SERVER_PROJ"
  else
    ok "Stopped test server"
  fi
}

cmd_stop_app() {
  if ! app_running; then
    warn "App not running"
    return
  fi
  pkill -f "bin/Debug/net8.0/$APP_PROJ\.dll" 2>/dev/null || true
  sleep 0.3
  ok "Stopped app"
}

cmd_restart_app() {
  cmd_stop_app
  cmd_run_app
}

cmd_run_slave() {
  say "Running slave (close the window or Ctrl+C here to return to the menu)…"
  dotnet run --project "$SLAVE_PROJ" || true
}

cmd_stop_slave() {
  if ! slave_running; then
    warn "Slave not running"
    return
  fi
  pkill -f "bin/Debug/net8.0/$SLAVE_PROJ\.dll" 2>/dev/null || true
  sleep 0.3
  ok "Stopped slave"
}

cmd_restart_slave() {
  cmd_stop_slave
  cmd_run_slave
}

cmd_run_all() {
  cmd_run_server_bg
  cmd_run_app
  cmd_stop_server
}

cmd_run_slave_bg() {
  if slave_running; then
    warn "Slave already running (pid $(slave_pid))"
    return
  fi
  say "Starting slave window in background…"
  nohup dotnet run --project "$SLAVE_PROJ" >"$SLAVE_LOG" 2>&1 &
  disown || true
  sleep 1
  if slave_running; then
    ok "Slave up (pid $(slave_pid))"
    printf '%slog: %s%s\n' "$C_DIM" "$SLAVE_LOG" "$C_RST"
    warn "Reminder: click ● Start in the slave window before connecting from the app."
  else
    err "Slave failed to start"
    tail -20 "$SLAVE_LOG" 2>/dev/null || true
  fi
}

cmd_run_slave_and_app() {
  cmd_run_slave_bg
  cmd_run_app
  cmd_stop_slave
}

cmd_publish_linux() {
  say "Publishing self-contained linux-x64…"
  dotnet publish "$APP_PROJ" -c Release -r linux-x64 --self-contained -o publish/linux-x64
  ok "→ $(pwd)/publish/linux-x64/$APP_PROJ"
}

cmd_publish_windows() {
  say "Publishing self-contained win-x64…"
  dotnet publish "$APP_PROJ" -c Release -r win-x64 --self-contained -o publish/win-x64
  ok "→ $(pwd)/publish/win-x64/$APP_PROJ.exe"
}

cmd_clean() {
  say "Cleaning bin/, obj/, publish/…"
  find . -type d \( -name bin -o -name obj -o -name publish \) ! -path "./.git/*" -exec rm -rf {} + 2>/dev/null || true
  ok "Clean."
}

cmd_tail_server_log() {
  if [[ ! -f "$SERVER_LOG" ]]; then
    warn "No server log yet ($SERVER_LOG). Start the server first."
    return
  fi
  say "Tailing $SERVER_LOG — Ctrl+C to return to menu"
  tail -f "$SERVER_LOG" || true
}

cmd_status() {
  if app_running;    then ok   "  app:    running (pid $(app_pid))";    else warn "  app:    not running"; fi
  if slave_running;  then ok   "  slave:  running (pid $(slave_pid))";  else warn "  slave:  not running"; fi
  if server_running; then ok   "  server: running (pid $(server_pid))"; else warn "  server: not running"; fi
}

# ─── menu ───────────────────────────────────────────────────────────────
print_menu() {
  cat <<EOF

${C_CYAN}═══ OpenPoll dev menu ════════════════════════${C_RST}
  1) Build solution
  ─── App (OpenPoll) ──────────────────────────
  2) Run app
  3) Restart app
  ─── Slave (OpenSlave) ───────────────────────
  4) Run slave
  5) Restart slave
  ─── Test server ─────────────────────────────
  6) Start test server (background)
  7) Stop test server
  8) Tail test server log
  ─── Combined ────────────────────────────────
  9) Run server + app together
 10) Run slave + app together
  ─── Build / Publish ─────────────────────────
 11) Publish self-contained Linux x64
 12) Publish self-contained Windows x64
 13) Clean (remove bin/ obj/ publish/)
  ─────────────────────────────────────────────
 14) Status
  q) Quit
EOF
  cmd_status
}

on_exit() {
  if server_running; then
    echo
    warn "Heads-up: test server still running (pid $(server_pid))"
    warn "Stop it with: pkill -f $SERVER_PROJ"
  fi
  if slave_running; then
    echo
    warn "Heads-up: slave still running (pid $(slave_pid))"
    warn "Stop it with: pkill -f 'bin/Debug/net8.0/$SLAVE_PROJ\.dll'"
  fi
}
trap on_exit EXIT

main() {
  while true; do
    print_menu
    read -rp $'\nchoice> ' choice
    echo
    case "${choice,,}" in
      1)             cmd_build ;;
      2)             cmd_run_app ;;
      3)             cmd_restart_app ;;
      4)             cmd_run_slave ;;
      5)             cmd_restart_slave ;;
      6)             cmd_run_server_bg ;;
      7)             cmd_stop_server ;;
      8)             cmd_tail_server_log ;;
      9)             cmd_run_all ;;
      10)            cmd_run_slave_and_app ;;
      11)            cmd_publish_linux ;;
      12)            cmd_publish_windows ;;
      13)            cmd_clean ;;
      14)            cmd_status ;;
      q|quit|exit)   exit 0 ;;
      "")            ;;
      *)             warn "Unknown choice: $choice" ;;
    esac
  done
}

main "$@"
