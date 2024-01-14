# VoiceRecogniseBot

# Sample appsettings.json

`
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
`

# default paths 


# known bugs 
libwhisper.so is not found.

run: 
```
sudo mkdir -p  /opt/VoiceRecogniseBot/runtimes/linux-x64 && sudo cp /opt/VoiceRecogniseBot/libwhisper.so /opt/VoiceRecogniseBot/runtimes/linux-x64/libwhisper.so && ls /opt/VoiceRecogniseBot/runtimes/linux-x64
```
output should be: 
libwhisper.so 
try again run bot

