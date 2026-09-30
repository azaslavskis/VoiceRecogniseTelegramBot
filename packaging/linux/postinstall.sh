#!/bin/sh
set -e

SERVICE=voice-recognise-bot.service
SERVICE_USER=voicebot
SERVICE_HOME=/var/lib/voice-recognise-bot

if ! id "$SERVICE_USER" >/dev/null 2>&1; then
    useradd --system --user-group --home-dir "$SERVICE_HOME" --shell /usr/sbin/nologin "$SERVICE_USER"
fi

# Recursive, because the directory may already hold files from an installation that ran as root.
mkdir -p "$SERVICE_HOME"
chown -R "$SERVICE_USER:$SERVICE_USER" "$SERVICE_HOME"
chmod 750 "$SERVICE_HOME"

# No systemd in containers and chroots; the package is still usable from the command line.
[ -d /run/systemd/system ] || exit 0

systemctl daemon-reload

# A unit of the same name in /etc, as written by scripts/install-linux.sh, takes precedence
# over the packaged one and would keep running the old installation as root.
if [ -e "/etc/systemd/system/$SERVICE" ]; then
    echo "WARNING: /etc/systemd/system/$SERVICE overrides the unit from this package." >&2
    echo "         Remove it and run 'systemctl daemon-reload' to use the packaged service." >&2
fi

# deb passes "configure" plus the previous version on upgrades; rpm passes 1 on a first install.
fresh_install=false
case "${1:-}" in
    configure) [ -n "${2:-}" ] || fresh_install=true ;;
    1) fresh_install=true ;;
esac

if [ "$fresh_install" = true ]; then
    systemctl enable --now "$SERVICE"
    echo "voice-recognise-bot is running. Set the bot token to finish the setup:"
    echo "  sudo voice-recognise-bot config set --token <token from @BotFather>"
    echo "or open the web UI at http://localhost:5010"
else
    systemctl try-restart "$SERVICE"
fi
