#!/bin/sh
set -e

if [ -d /run/systemd/system ]; then
    systemctl daemon-reload || true
fi

# Configuration, statistics and downloaded models are kept unless the package is purged.
if [ "${1:-}" = purge ]; then
    rm -rf /var/lib/voice-recognise-bot
fi
