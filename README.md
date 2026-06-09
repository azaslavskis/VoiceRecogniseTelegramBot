# VoiceRecogniseTelegramBot

`VoiceRecogniseTelegramBot` is a .NET 10 Telegram bot that downloads voice or audio messages, converts them to WAV, and runs Whisper transcription locally.

The project is small, but it now has a clearer command-line interface and a simpler configuration story:

- `run` starts the Telegram bot and the local stats endpoint
- `config-show` prints the active config
- `config-set` updates config values without hand-editing JSON
- `config-path` and `stats-path` show where runtime files live
- `stats-show` prints the saved message counters

## Project Layout

## Nice WebUI
<img width="1888" height="824" alt="Screenshot 2026-06-09 at 23 49 11" src="https://github.com/user-attachments/assets/fe8de99c-d001-4575-abfd-948979aebdbd" />

```text
src/
  Program.cs            CLI entrypoint
  TelegramAPI.cs        Telegram update handling
  WhisperAPI.cs         Whisper model loading and transcription
  AudioToWav.cs         Audio conversion helpers
  Config.cs             Config read/write helper
  CreateFiles.cs        First-run file bootstrap
  SettingsPathClass.cs  Runtime path resolution
  Stats.cs              Message counters
  WebUIAPI.cs           Local HTTP stats endpoint
```
## Requirements

- .NET 10 SDK
- A Telegram bot token from BotFather
- `ffmpeg` available in `PATH`, or set via `FFMPEG_PATH`
- Whisper runtime dependencies required by `Whisper.net`

## Build

```bash
dotnet build src/VoiceRecogniseBot.sln
```

## Docker

Build the image:

```bash
docker build -t voice-recognise-bot .
```

Create a persistent data directory and initialize the config:

```bash
mkdir -p ./voicebot-data
docker run --rm \
  -v "$PWD/voicebot-data:/data" \
  voice-recognise-bot config-set \
  --token "123456:telegram-token" \
  --model ggml-base \
  --lang EN,RU,LV \
  --default-lang EN
```

Run the bot:

```bash
docker run -d \
  --name voice-recognise-bot \
  --restart unless-stopped \
  -v "$PWD/voicebot-data:/data" \
  voice-recognise-bot
```

Run only the web UI and expose it on port 5010:

```bash
docker run --rm \
  -p 5010:5010 \
  -v "$PWD/voicebot-data:/data" \
  voice-recognise-bot run web-ui
```

The container stores config, stats, and managed Whisper models in `/data`.

## Linux Systemd Install

Install and start the bot as a `systemd` service:

```bash
sudo ./scripts/install-linux.sh \
  --token "123456:telegram-token" \
  --model ggml-base \
  --lang EN,RU,LV \
  --default-lang EN
```

The installer publishes the app to `/opt/voice-recognise-bot`, stores runtime data in `/var/lib/voice-recognise-bot`, installs `/etc/systemd/system/voice-recognise-bot.service`, enables the service, and starts it.

Service helper:

```bash
./scripts/voicebotctl.sh status
./scripts/voicebotctl.sh logs
./scripts/voicebotctl.sh restart
```

## Configuration

On first start the app creates `appsettings.json` and `stats.json` in its runtime config directory.

Default locations:

- Linux: `${XDG_CONFIG_HOME:-~/.config}/VoiceRecogniseBot`
- macOS: `~/Library/Application Support/VoiceRecogniseBot`
- Windows: `%LocalAppData%\VoiceRecogniseBot`

You can override this location with:

```bash
export VOICE_RECOGNISEBOT_HOME=/path/to/runtime-data
```

Example config:

```json
{
  "Model": "ggml-base",
  "Token": "123456:telegram-token",
  "Lang": [
    "RU",
    "LV",
    "EN"
  ],
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

Notes:

- `Model` can be a Whisper model name such as `ggml-base` or a path to an existing local model file
- Built-in model aliases are downloaded into the runtime data directory under `models/`
- `Lang` is the list shown to Telegram users in the language keyboard
- `DefaultLang` should also appear in `Lang`
- `FFMPEG_PATH` can point to the folder containing `ffmpeg` if it is not on `PATH`
- `BotText` lets you customize bot buttons and user-facing Telegram messages without changing code

## Cross-Platform Publish

The project is no longer pinned to one operating system. It can be built on any supported .NET 10 host and published for Windows, Linux, or macOS by choosing the runtime identifier at publish time.

Example Windows publish:

```bash
dotnet publish src/VoiceRecogniseBot.csproj -c Release -r win-x64 --self-contained false
```

Example Linux publish:

```bash
dotnet publish src/VoiceRecogniseBot.csproj -c Release -r linux-x64 --self-contained false
```

Example macOS publish:

```bash
dotnet publish src/VoiceRecogniseBot.csproj -c Release -r osx-arm64 --self-contained false
```

## CLI Usage

Show help:

```bash
dotnet run --project src/VoiceRecogniseBot.csproj -- --help
```

Print the config path:

```bash
dotnet run --project src/VoiceRecogniseBot.csproj -- config-path
```

Show the current config:

```bash
dotnet run --project src/VoiceRecogniseBot.csproj -- config-show
```

Update config values:

```bash
dotnet run --project src/VoiceRecogniseBot.csproj -- config-set \
  --token "123456:telegram-token" \
  --model ggml-base \
  --lang EN,RU,LV \
  --default-lang EN
```

Show stats:

```bash
dotnet run --project src/VoiceRecogniseBot.csproj -- stats-show
```

Run the bot:

```bash
dotnet run --project src/VoiceRecogniseBot.csproj -- run
```

## Runtime Behavior

- The Telegram bot listens for voice, audio, video note, and video updates
- Voice, audio, and video files are converted to 16 kHz mono WAV with `ffmpeg` through `FFMpegCore` before Whisper transcription
- A simple local HTTP server listens on `http://localhost:5010/` and returns basic JSON stats
- Message counters are written to `stats.json`

## Telegram Controls

- Send `start` or `/start` to open the bot keyboard
- `Set Lang` shows the configured language list
- `Log` returns the in-memory application log
- `About` prints a short bot description

## Current Limitations

- The project now depends on an external `ffmpeg` binary being installed, while media conversion is managed through the `FFMpegCore` wrapper
- If `ffmpeg` is missing or conversion fails, the bot now returns a direct Telegram error message instead of failing silently
- The bot responses and keyboard labels are still hard-coded
- The local HTTP server is a minimal stats endpoint, not a full Web UI
- I did not verify a full build in this workspace because `dotnet` is not installed in the current shell

## Suggested Next Cleanup

- Split Telegram command handling into smaller methods
- Replace hard-coded response text with config-driven strings
- Add automated tests around config parsing and stats persistence
