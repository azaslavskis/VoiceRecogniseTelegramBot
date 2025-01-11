
# **VoiceRecogniseBot Documentation**

## **Overview**

**VoiceRecogniseBot** is a modern, cross-platform voice recognition tool built using C# and Whisper. It features an intuitive CLI, easy configuration via JSON, and robust AppImage support for hassle-free deployment.

---

## **Features**
- **AppImage Distribution**: Single binary for simplified installation.
- **Easy Configuration**: Plug-and-play setup with `appsettings.json`.
- **Powerful CLI API**: Manage the bot, update settings, and run commands from the terminal.
- **Cross-Platform**: Runs seamlessly on Windows, Linux, and macOS.
- **Whisper Integration**: State-of-the-art speech recognition for high accuracy.
- **Future Enhancements**:
  - API for programmatic interactions.
  - WebUI for user-friendly management.

---

## **Installation**

### **1. AppImage Installation**
Download and execute the AppImage:

```bash
wget https://example.com/VoiceRecogniseBot.AppImage
chmod +x VoiceRecogniseBot.AppImage
sudo ./VoiceRecogniseBot.AppImage
```

---

## **Configuration**

The bot uses an `appsettings.json` file for configuration. Below is a sample configuration:

```json
{
    "model": "ggml-base.bin",
    "token": "xxxx",
    "lang": [
        "RU",
        "LT",
        "EN"
    ],
    "default_lang": "EN"
}
```

- **model**: Path to the Whisper model file.
- **token**: Authentication token.
- **lang**: Supported languages (array of language codes).
- **default_lang**: Default language if none is specified.

---

## **CLI Commands**

Run the bot and manage configurations using the CLI. Below are common commands:

### **1. Run the Bot**
```bash
sudo ./VoiceRecogniseBot.AppImage -c bot
```

### **2. Update Configuration**
```bash
sudo ./VoiceRecogniseBot.AppImage -c update_config -m ggml-base.bin -t xxxx -l RU,LT,EN -d EN
```

### **3. CLI Help**
```bash
sudo ./VoiceRecogniseBot.AppImage --help
```

### **CLI Options:**
- `-c`, `--command`: Specify the command to run (e.g., `bot`, `update_config`).
- `-m`, `--model`: Set the model name.
- `-t`, `--token`: Set the token.
- `-l`, `--lang`: Set the languages (comma-separated).
- `-d`, `--default-lang`: Set the default language.
- `--help`: Display help information.
- `--version`: Display version information.

---


# Model list
| **Model Name**       | **Model Brief Description**         | **String for Config**    |
|-----------------------|-------------------------------------|-------------------------|
| GgmlType.Tiny         | Tiny model for compact tasks       | ggml-tiny               |
| GgmlType.TinyEn       | Tiny English-specific model        | ggml-tiny.en            |
| GgmlType.Base         | Base model for general tasks       | ggml-base               |
| GgmlType.BaseEn       | Base English-specific model        | ggml-base.en            |
| GgmlType.Small        | Small model with better accuracy   | ggml-small              |
| GgmlType.SmallEn      | Small English-specific model       | ggml-small.en           |
| GgmlType.Medium       | Medium-sized model for accuracy    | ggml-medium             |
| GgmlType.MediumEn     | Medium English-specific model      | ggml-medium.en          |
| GgmlType.LargeV1      | Large model, version 1             | ggml-large-v1           |
| GgmlType.LargeV2      | Large model, version 2             | ggml-large-v2           |
| GgmlType.LargeV3      | Large model, version 3             | ggml-large-v3           |
| GgmlType.LargeV3Turbo | Optimized large model, version 3   | ggml-large-v3-turbo     |
```



## **Known Issues**
Ensure that `libwhisper.so` is available in the expected directory. To resolve missing library issues:

```bash
wget https://github.com/alex5250/VoiceRecogniseTelegramBot/raw/main/libwhisper.so
sudo mkdir -p /opt/VoiceRecogniseBot/runtimes/linux-x64
sudo cp libwhisper.so /opt/VoiceRecogniseBot/runtimes/linux-x64/libwhisper.so
ls /opt/VoiceRecogniseBot/runtimes/linux-x64
```

The output should include:
```plaintext
libwhisper.so
```

---

## **Planned Features**
- **API**: Add programmatic interaction support.
- **WebUI**: Develop a user-friendly web-based management interface.
- **Expanded Configuration Options**: Advanced CLI and JSON support.

---

**VoiceRecogniseBot** is an evolving project designed to simplify voice recognition with cutting-edge features and cross-platform compatibility. Contributions and feedback are always welcome!
****
