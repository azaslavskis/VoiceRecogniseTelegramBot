#!/usr/bin/env bash
set -euo pipefail

SERVICE_NAME="${SERVICE_NAME:-voice-recognise-bot.service}"
APP_DIR="${APP_DIR:-/opt/VoiceRecogniseTelegramBot}"
CONFIG_HOME="${CONFIG_HOME:-/var/lib/voice-recognise-bot}"

usage() {
  cat <<EOF
Usage: ${0##*/} <command>

Commands:
  setup       Install/update systemd service
  start       Start the bot service
  stop        Stop the bot service
  restart     Restart the bot service
  status      Show service status
  logs        Follow service logs
  enable      Enable service on boot
  disable     Disable service on boot
  config-path Show config path used by service env
  set-token   Set Telegram bot token

Examples:
  sudo ./${0##*/} setup
  sudo ./${0##*/} set-token "123456:ABCDEF"
  sudo ./${0##*/} restart
  sudo ./${0##*/} logs
EOF
}

require_systemctl() {
  if ! command -v systemctl >/dev/null 2>&1; then
    echo "systemctl is not available on this system." >&2
    exit 1
  fi
}

require_root() {
  if [ "${EUID}" -ne 0 ]; then
    echo "Please run this command with sudo/root." >&2
    exit 1
  fi
}

setup_service() {
  require_root

  if [ ! -x "$APP_DIR/VoiceRecogniseBot" ]; then
    echo "Error: executable not found: $APP_DIR/VoiceRecogniseBot" >&2
    echo "Build/publish the app first, or set APP_DIR=/path/to/app" >&2
    exit 1
  fi

  mkdir -p "$CONFIG_HOME/.config"
  chmod -R 700 "$CONFIG_HOME"

  cat > "/etc/systemd/system/$SERVICE_NAME" <<EOF
[Unit]
Description=VoiceRecogniseTelegramBot
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=root
WorkingDirectory=$APP_DIR

Environment=HOME=$CONFIG_HOME
Environment=XDG_CONFIG_HOME=$CONFIG_HOME/.config
Environment=FFMPEG_PATH=/usr/bin
Environment=VOICE_RECOGNISEBOT_WEB_URLS=http://0.0.0.0:5000

ExecStart=$APP_DIR/VoiceRecogniseBot run bot

Restart=on-failure
RestartSec=10

NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=full
ProtectHome=true
ReadWritePaths=$CONFIG_HOME

[Install]
WantedBy=multi-user.target
EOF

  systemctl daemon-reload
  systemctl enable "$SERVICE_NAME"

  echo "Service installed: $SERVICE_NAME"
  echo "App dir: $APP_DIR"
  echo "Config home: $CONFIG_HOME"
  echo
  echo "Next:"
  echo "  sudo ${0} set-token \"YOUR_TELEGRAM_TOKEN\""
  echo "  sudo ${0} start"
  echo "  sudo ${0} logs"
}

set_token() {
  require_root

  local token="${1:-}"

  if [ -z "$token" ]; then
    echo "Usage: ${0##*/} set-token \"YOUR_TELEGRAM_TOKEN\"" >&2
    exit 1
  fi

  cd "$APP_DIR"

  HOME="$CONFIG_HOME" \
  XDG_CONFIG_HOME="$CONFIG_HOME/.config" \
  "$APP_DIR/VoiceRecogniseBot" config-set --token "$token"

  echo "Token saved."
}

show_config_path() {
  cd "$APP_DIR"

  HOME="$CONFIG_HOME" \
  XDG_CONFIG_HOME="$CONFIG_HOME/.config" \
  "$APP_DIR/VoiceRecogniseBot" config-path
}

main() {
  local command="${1:-}"
  shift || true

  require_systemctl

  case "$command" in
    setup)
      setup_service
      ;;
    set-token)
      set_token "${1:-}"
      ;;
    config-path)
      show_config_path
      ;;
    start|stop|restart|status|enable|disable)
      sudo systemctl "$command" "$SERVICE_NAME"
      ;;
    logs)
      sudo journalctl -u "$SERVICE_NAME" -f
      ;;
    ""|-h|--help|help)
      usage
      ;;
    *)
      echo "Unknown command: $command" >&2
      usage >&2
      exit 1
      ;;
  esac
}

main "$@"
