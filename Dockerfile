FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/VoiceRecogniseBot.csproj ./VoiceRecogniseBot.csproj
RUN dotnet restore ./VoiceRecogniseBot.csproj

COPY src/ ./
RUN dotnet publish ./VoiceRecogniseBot.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg ca-certificates \
    && rm -rf /var/lib/apt/lists/*

RUN useradd --create-home --shell /usr/sbin/nologin appuser \
    && mkdir -p /data \
    && chown -R appuser:appuser /app /data

COPY --from=build /app/publish ./

ENV VOICE_RECOGNISEBOT_HOME=/data \
    FFMPEG_PATH=/usr/bin \
    VOICE_RECOGNISEBOT_WEB_URLS=http://0.0.0.0:5010 \
    DOTNET_EnableDiagnostics=0

VOLUME ["/data"]
EXPOSE 5010

USER appuser
ENTRYPOINT ["dotnet", "VoiceRecogniseBot.dll"]
CMD ["run"]
