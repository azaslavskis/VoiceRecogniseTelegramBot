#!/bin/sh
set -e

[ -d /run/systemd/system ] || exit 0

# Stop the service when the package is removed, but not while it is being upgraded
# (deb passes "upgrade", rpm passes 1).
case "${1:-}" in
    remove|purge|0) systemctl disable --now voice-recognise-bot.service || true ;;
esac
