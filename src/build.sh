dotnet publish -c Release --self-contained -r linux-x64 -o .\publish
vpk pack -u VoiceRecogniseBot -v 1.0.0 -p .\publish 
