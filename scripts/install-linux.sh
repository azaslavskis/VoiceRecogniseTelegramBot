#!/usr/bin/env bash
set -euo pipefail

APP_NAME="voice-recognise-bot"
SERVICE_NAME="voice-recognise-bot.service"
APP_USER="voicebot"
INSTALL_DIR="/opt/voice-recognise-bot"
DATA_DIR="/var/lib/voice-recognise-bot"
PROJECT_FILE="src/VoiceRecogniseBot.csproj"
SERVICE_TEMPLATE="scripts/voice-recognise-bot.service"
RUNTIME_ID="${RUNTIME_ID:-linux-x64}"
SELF_CONTAINED="${SELF_CONTAINED:-false}"
CONFIGURATION="${CONFIGURATION:-Release}"

usage() {
  cat <<EOF
Usage: sudo ./scripts/install-linux.sh [options]

Options:
  --token TOKEN          Set the Telegram bot token after install
  --model MODEL          Set the Whisper model name or local model path
  --lang LIST            Set comma-separated languages, for example EN,RU,LV
  --default-lang LANG    Set the default recognition language
  --no-enable            Do not enable the service on boot
  --no-start             Do not start/restart the service after install
  -h, --help             Show this help

Environment:
  RUNTIME_ID=linux-x64
  SELF_CONTAINED=false
  CONFIGURATION=Release
EOF
}

TOKEN=""
MODEL=""
LANGS=""
DEFAULT_LANG=""
ENABLE_SERVICE="true"
START_SERVICE="true"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --token)
      TOKEN="${2:?Missing value for --token}"
      shift 2
      ;;
    --model)
      MODEL="${2:?Missing value for --model}"
      shift 2
      ;;
    --lang)
      LANGS="${2:?Missing value for --lang}"
      shift 2
      ;;
    --default-lang)
      DEFAULT_LANG="${2:?Missing value for --default-lang}"
      shift 2
      ;;
    --no-enable)
      ENABLE_SERVICE="false"
      shift
      ;;
    --no-start)
      START_SERVICE="false"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run this installer with sudo or as root." >&2
  exit 1
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

require_command() {
  local command="$1"
  if ! command -v "$command" >/dev/null 2>&1; then
    echo "Required command not found: $command" >&2
    exit 1
  fi
}

run_as_app_user() {
  if command -v runuser >/dev/null 2>&1; then
    runuser -u "$APP_USER" -- "$@"
    return
  fi

  if command -v sudo >/dev/null 2>&1; then
    sudo -u "$APP_USER" "$@"
    return
  fi

  echo "Neither runuser nor sudo is available to run commands as $APP_USER." >&2
  exit 1
}

install_packages_if_possible() {
  if command -v apt-get >/dev/null 2>&1; then
    apt-get update
    apt-get install -y ffmpeg ca-certificates dotnet-sdk-10.0 aspnetcore-runtime-10.0
    return
  fi

  if command -v dnf >/dev/null 2>&1; then
    dnf install -y ffmpeg dotnet-sdk-10.0 aspnetcore-runtime-10.0
    return
  fi

  if command -v yum >/dev/null 2>&1; then
    yum install -y ffmpeg dotnet-sdk-10.0 aspnetcore-runtime-10.0
    return
  fi

  echo "No supported package manager found. Install .NET 10 SDK/runtime and ffmpeg, then rerun this script." >&2
  exit 1
}

ensure_user() {
  if id "$APP_USER" >/dev/null 2>&1; then
    return
  fi

  useradd --system --home-dir "$DATA_DIR" --shell /usr/sbin/nologin "$APP_USER"
}

publish_app() {
  local temp_publish
  temp_publish="$(mktemp -d)"
  trap 'rm -rf "$temp_publish"' EXIT

  dotnet publish "$PROJECT_FILE" \
    --configuration "$CONFIGURATION" \
    --runtime "$RUNTIME_ID" \
    --self-contained "$SELF_CONTAINED" \
    --output "$temp_publish"

  install -d -o root -g root -m 0755 "$INSTALL_DIR"
  find "$INSTALL_DIR" -mindepth 1 -maxdepth 1 -exec rm -rf {} +
  cp -a "$temp_publish"/. "$INSTALL_DIR"/
  chown -R root:root "$INSTALL_DIR"
}

install_service() {
  install -d -o "$APP_USER" -g "$APP_USER" -m 0750 "$DATA_DIR"
  install -m 0644 "$SERVICE_TEMPLATE" "/etc/systemd/system/$SERVICE_NAME"
  systemctl daemon-reload
}

configure_app() {
  local config_args=()

  [[ -n "$TOKEN" ]] && config_args+=(--token "$TOKEN")
  [[ -n "$MODEL" ]] && config_args+=(--model "$MODEL")
  [[ -n "$LANGS" ]] && config_args+=(--lang "$LANGS")
  [[ -n "$DEFAULT_LANG" ]] && config_args+=(--default-lang "$DEFAULT_LANG")

  if [[ "${#config_args[@]}" -eq 0 ]]; then
    run_as_app_user env VOICE_RECOGNISEBOT_HOME="$DATA_DIR" \
      dotnet "$INSTALL_DIR/VoiceRecogniseBot.dll" config-path >/dev/null
    return
  fi

  run_as_app_user env VOICE_RECOGNISEBOT_HOME="$DATA_DIR" \
    dotnet "$INSTALL_DIR/VoiceRecogniseBot.dll" config-set "${config_args[@]}"
}

install_packages_if_possible
require_command dotnet
require_command ffmpeg
require_command systemctl
ensure_user
publish_app
install_service
configure_app

if [[ "$ENABLE_SERVICE" == "true" ]]; then
  systemctl enable "$SERVICE_NAME"
fi

if [[ "$START_SERVICE" == "true" ]]; then
  systemctl restart "$SERVICE_NAME"
fi

cat <<EOF
Installed $APP_NAME.

Service:
  systemctl status $SERVICE_NAME
  journalctl -u $SERVICE_NAME -f

Config:
  sudo -u $APP_USER VOICE_RECOGNISEBOT_HOME=$DATA_DIR dotnet $INSTALL_DIR/VoiceRecogniseBot.dll config-show
EOF
