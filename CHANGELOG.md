# Changelog

All notable changes to this project are listed here, newest first. Each release on GitHub
uses its section of this file as the release notes.

## 2.2.1 - 2026-09-30

### Fixed

- **`.deb` / `.rpm`: "Access to the path '/var/lib/voice-recognise-bot/appsettings.json' is
  denied".** Files in the data directory that belonged to root, for example from an earlier
  installation with `scripts/install-linux.sh`, locked out the `voicebot` user that the
  service and the `voice-recognise-bot` command run as. The install script now takes ownership
  of the whole data directory, and the command repairs the ownership whenever it finds files
  that do not belong to `voicebot`.
- **`.deb` / `.rpm`:** running `voice-recognise-bot` as root with `VOICE_RECOGNISEBOT_HOME`
  set to `/var/lib/voice-recognise-bot` no longer runs the program as root, which created
  such root-owned files.

### Added

- **`.deb` / `.rpm`:** the install script warns when a unit file in
  `/etc/systemd/system/voice-recognise-bot.service` overrides the packaged service. This is
  the case after a previous setup with `scripts/install-linux.sh`; remove the file and run
  `systemctl daemon-reload` to use the packaged service.

### Upgrading from 2.2.0

Install the new package over the old one (`sudo apt install ./voice-recognise-bot_2.2.1_amd64.deb`
or `sudo dnf install ./voice-recognise-bot-2.2.1-1.x86_64.rpm`). The ownership is repaired
during the installation and the service is restarted.

## 2.2.0 - 2026-09-30

### Added

- **Linux packages:** `.deb`, `.rpm`, AppImage and tarball for x86-64 and ARM64, plus a
  Windows zip. The `.deb` and `.rpm` install a systemd service that runs as its own
  `voicebot` user and keeps its data in `/var/lib/voice-recognise-bot`.
- **Web UI:** new tabbed admin page with an overview (bot status, counters, messages per
  day), settings, bot messages, a live log viewer and a raw JSON editor. Supports dark mode.
- **Command line:** grouped commands `run`, `config`, `stats`, `logs`, `model`, `transcribe`
  and `info`, documented in [docs/cli.md](docs/cli.md).
  - `transcribe <file>` transcribes a local file without Telegram.
  - `model list` and `model download` manage Whisper models.
  - `logs --follow` tails the log; `info` checks the setup.
  - `config set --text Key=Value` changes bot messages.
- Transcription statistics are counted separately from messages, per day.

### Changed

- Settings are re-read while the bot runs: a new token, model, language list or bot message
  applies without a restart. Without a token, the bot waits for one instead of exiting.
- The recognition language is remembered per chat instead of being shared by all chats.
- The Whisper model is loaded once instead of for every message.
- Transcription timestamps are printed as `hh:mm:ss`.
- Logs are written to one rotating file, `logs/bot.log` in the data directory, instead of a
  new temporary file on every start.
- `config show` masks the bot token unless `--show-token` is passed.
- `stats show` prints a table unless `--json` is passed.
- The old command names (`config-set`, `config-show`, `config-path`, `stats-show`,
  `stats-path`) and `--web-server` still work.

### Fixed

- Web UI: saving reset the settings shown in the page to their defaults.
- Web UI: text fields lost focus after every keystroke.
- Web UI: the page was only found when the program was started from its own directory.
- The Log button in Telegram always answered "No log messages yet".
- Transcriptions longer than 4096 characters were rejected by Telegram; they are now sent in
  several messages.
- Two temporary files were left behind for every transcribed message.
- The health endpoint reported the current time as the start time.

### Security

- The bot token is no longer sent to the browser by the web UI.
- The settings endpoint only accepts JSON requests, so other websites open in the same
  browser cannot change the bot's settings.

### Removed

- The GitHub workflow that committed a version bump on every push; releases are now made by
  pushing a `v*` tag.
- IDE and operating system files (`.vs`, `.idea`, `.DS_Store`) from the repository.
