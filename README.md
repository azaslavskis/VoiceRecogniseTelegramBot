# VoiceRecogniseTelegramBot
Simple bot that takes image or video message or video and converts it to text.


### Todo:

- [ ] Debian packages (.deb)
- [ ] CLI
- [%] Update libs and clean code
- [ ] Make CasaOS integration
- [ ] Make Cockpit integration
- [ ] Set up auto builds on Git
- [ ] Create Web UI
- [ ] ARM64 builds
- [ ] Install - Configure script
- [ ] PPA repos
- [ ] Docs for code


Config sample 
```TOML
[path_config]
config_path = "/path/to/config"
model_path = "/path/to/model"
temp_path = "/path/to/temp"
    
[model_options]
model = "GPT-3.5"
language_target = "English"
    
[telegram_config]
api_key = "your_telegram_api_key"
chat_id = "your_chat_id"
```
