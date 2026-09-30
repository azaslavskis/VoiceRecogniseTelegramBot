# VoiceRecogniseTelegramBot

A Telegram bot that transcribes voice, audio and video messages into text. Transcription runs
locally with [Whisper](https://github.com/sandrohanea/whisper.net); nothing is sent to a
speech-to-text service.

- Replies to voice messages, audio files, video notes and videos with their text
- Each chat picks its own recognition language from a list you configure
- Web UI for settings, bot messages, statistics and logs
- One command-line tool for everything: see the [command-line reference](docs/cli.md)
- Packages for Linux (`.deb`, `.rpm`, AppImage, tarball), Windows (zip) and Docker

## Install

Download the files from the [latest release](https://github.com/azaslavskis/VoiceRecogniseTelegramBot/releases/latest).
The Linux builds exist for x86-64 and ARM64 and include the .NET runtime.

You need a bot token from [@BotFather](https://t.me/BotFather) and `ffmpeg`.

### Debian / Ubuntu

```bash
sudo apt install ./voice-recognise-bot_<version>_amd64.deb
sudo voice-recognise-bot config set --token "123456:ABC-DEF"
```

### Fedora / RHEL

```bash
sudo dnf install ./voice-recognise-bot-<version>-1.x86_64.rpm
sudo voice-recognise-bot config set --token "123456:ABC-DEF"
```

`ffmpeg` is a recommended rather than a required dependency of the `.rpm`, because not every
RPM distribution ships it in its base repositories. Install it separately if `dnf` did not.

Both packages install a `voice-recognise-bot` systemd service that starts on boot and runs as
its own `voicebot` user. The bot connects a few seconds after the token is saved.

| What | Where |
| --- | --- |
| Program | `/opt/voice-recognise-bot` |
| Configuration, statistics, logs, models | `/var/lib/voice-recognise-bot` |
| Service environment (web UI address, ffmpeg path) | `/etc/default/voice-recognise-bot` |
| Service logs | `journalctl -u voice-recognise-bot -f` |

Run `voice-recognise-bot` with `sudo` to manage the service's configuration; without `sudo` it
uses your own per-user configuration. Removing the package keeps the data directory;
`apt purge` deletes it.

### AppImage

```bash
chmod +x VoiceRecogniseBot-<version>-x86_64.AppImage
./VoiceRecogniseBot-<version>-x86_64.AppImage config set --token "123456:ABC-DEF"
./VoiceRecogniseBot-<version>-x86_64.AppImage run
```

The AppImage needs `ffmpeg` and the OpenMP runtime (`libgomp1` on Debian and Ubuntu, `libgomp`
on Fedora) from the system.

### Tarball and Windows zip

Unpack and run `./VoiceRecogniseBot` (`VoiceRecogniseBot.exe` on Windows) with the same
commands. On Windows, install ffmpeg with `winget install Gyan.FFmpeg`.

### Docker

```bash
docker build -t voice-recognise-bot .

mkdir -p ./voicebot-data
docker run --rm -v "$PWD/voicebot-data:/data" voice-recognise-bot \
  config set --token "123456:ABC-DEF"

docker run -d --name voice-recognise-bot --restart unless-stopped \
  -p 127.0.0.1:5010:5010 -v "$PWD/voicebot-data:/data" voice-recognise-bot
```

The container keeps its configuration, statistics and models in `/data`.

## Web UI

`voice-recognise-bot run` serves the web UI at <http://localhost:5010>:

- **Overview**: bot status, message and transcription counters, messages per day
- **Settings**: bot token, Whisper model, recognition languages
- **Bot messages**: every button label and reply the bot sends
- **Logs**: the most recent log lines, refreshed live
- **JSON**: the same settings as raw JSON

Changes are applied to the running bot without a restart, including a new token.

The web UI has no login. By default it only accepts connections from the same machine. To
reach it from elsewhere, set `VOICE_RECOGNISEBOT_WEB_URLS=http://0.0.0.0:5010` (or pass
`--urls`), and only do that on a network you trust: anyone who can open the page can change
the bot's settings. An SSH tunnel (`ssh -L 5010:localhost:5010 server`) avoids exposing it.

## Command line

```bash
voice-recognise-bot info                                   # version, file locations, setup check
voice-recognise-bot config set --lang EN,RU,LV --default-lang EN
voice-recognise-bot config set --model ggml-small
voice-recognise-bot model download                         # fetch the model ahead of time
voice-recognise-bot transcribe voice.ogg --lang EN         # try it without Telegram
voice-recognise-bot logs --follow
voice-recognise-bot run                                    # bot + web UI
```

The full list of commands and options is in the [command-line reference](docs/cli.md).

## Configuration

The settings live in `appsettings.json` in the data directory (`voice-recognise-bot config path`
prints where). Edit them with `config set`, in the web UI, or by hand.

```json
{
  "Model": "ggml-base",
  "Token": "123456:ABC-DEF",
  "WebServer": true,
  "Lang": ["RU", "LV", "EN"],
  "DefaultLang": "EN",
  "BotText": {
    "SetLanguageButton": "Set Lang",
    "LogButton": "Log",
    "AboutButton": "About",
    "MainMenuPrompt": "Choose an action:",
    "LanguagePrompt": "Choose the recognition language:",
    "AboutMessage": "This bot transcribes Telegram voice and audio messages into text.",
    "UnknownCommandMessage": "Unknown command. Send 'start' to open the bot menu.",
    "TranscriptionInProgressMessage": "Transcription in progress...",
    "TranscriptionResultPrefix": "Recognised message:",
    "LanguageChangedPrefix": "Changed message recognition language to",
    "InternalErrorMessage": "Transcription failed due to an internal error. Check the bot logs for details."
  }
}
```

- `Model` is a Whisper model name such as `ggml-base` or `ggml-large-v3-turbo`, or the path to
  a model file. Named models are downloaded into `models/` in the data directory on first use.
- `Lang` is the list users choose from in Telegram; `DefaultLang` must be one of them.
- `WebServer` controls whether a plain `run` also starts the web UI.
- `BotText` holds every button label and message the bot sends.

## Using the bot in Telegram

- Send `start` or `/start` to open the keyboard
- **Set Lang** shows the configured languages; the choice applies to that chat
- **About** prints a short description
- **Log** replies with the most recent log lines. Every user of the bot can press it, so treat
  the log as visible to them
- Send or forward a voice message, audio file, video note or video to get its transcription

## Build from source

Requires the .NET 10 SDK.

```bash
dotnet build src/VoiceRecogniseBot.sln
dotnet run --project src/VoiceRecogniseBot.csproj -- run
```

To build the Linux packages locally (on x86-64 Linux, for either architecture):

```bash
packaging/build-packages.sh 2.2.0 x64      # or arm64
ls dist/
```

```text
src/                     application
  Program.cs             entry point
  Cli.cs                 command-line interface
  TelegramAPI.cs         Telegram update handling
  WhisperAPI.cs          model download and transcription
  AudioToWav.cs          ffmpeg conversion
  WebUI.cs               web server and JSON endpoints
  wwwroot/               web UI (plain HTML, CSS and JavaScript)
  ConfigStore.cs         configuration file
  StatsStore.cs          message counters
  AppLog.cs, AppPaths.cs logging and file locations
packaging/               deb, rpm and AppImage definitions
scripts/                 manual systemd setup for a build from source
docs/cli.md              command-line reference
```

## Releases

Every push builds the packages and smoke-tests them in GitHub Actions. To publish a release,
add a section for the version to [CHANGELOG.md](CHANGELOG.md), which becomes the release notes,
and push a tag:

```bash
git tag v2.2.0
git push origin v2.2.0
```

The tag sets the version of the binaries and packages, and the release gets the `.deb`, `.rpm`,
AppImage and tarball for x86-64 and ARM64, the Windows zip, and a `SHA256SUMS` file.

## License

MIT, see [src/LICENSE.txt](src/LICENSE.txt).
