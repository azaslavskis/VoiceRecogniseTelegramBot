#!/usr/bin/env bash
set -euo pipefail

SERVICE_NAME="${SERVICE_NAME:-voice-recognise-bot.service}"

usage() {
  cat <<EOF
Usage: ${0##*/} <command>

Commands:
  start       Start the bot service
  stop        Stop the bot service
  restart     Restart the bot service
  status      Show service status
  logs        Follow service logs
  enable      Enable service on boot
  disable     Disable service on boot
EOF
}

require_systemctl() {
  if ! command -v systemctl >/dev/null 2>&1; then
    echo "systemctl is not available on this system." >&2
    exit 1
  fi
}

main() {
  local command="${1:-}"
  require_systemctl

  case "$command" in
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
