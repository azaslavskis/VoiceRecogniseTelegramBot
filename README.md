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
```
System.IO.FileNotFoundException: Native Library not found in path /opt/VoiceRecogniseBot/runtimes/linux-x64/libwhisper.so. Verify you have have included the native Whisper library in your application, or install the default libraries with the Whisper.net.Runtime NuGet.
   at Whisper.net.LibraryLoader.NativeLibraryLoader.LoadNativeLibrary(String path, Boolean bypassLoading)
   at Whisper.net.WhisperFactory.<>c.<.cctor>b__11_0()
   at System.Lazy`1.ViaFactory(LazyThreadSafetyMode mode)
   at System.Lazy`1.ExecutionAndPublication(LazyHelper executionAndPublication, Boolean useDefaultConstructor)
   at System.Lazy`1.CreateValue()
```

run: 
```
sudo mkdir -p  /opt/VoiceRecogniseBot/runtimes/linux-x64 && sudo cp /opt/VoiceRecogniseBot/libwhisper.so /opt/VoiceRecogniseBot/runtimes/linux-x64/libwhisper.so && ls /opt/VoiceRecogniseBot/runtimes/linux-x64
```
output should be: 
libwhisper.so 
try again run bot

