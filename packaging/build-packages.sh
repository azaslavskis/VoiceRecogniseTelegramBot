#!/usr/bin/env bash
# Builds the Linux release artifacts for one architecture into dist/:
#   .deb, .rpm, .AppImage and a plain .tar.gz
#
# Usage: packaging/build-packages.sh <version> <x64|arm64>
#
# Runs on an x86-64 Linux host for both architectures. Needs the .NET SDK, curl and tar;
# nfpm and appimagetool are downloaded into build/tools on first use.
set -euo pipefail

NFPM_VERSION=2.47.0
APPIMAGETOOL_VERSION=1.9.1

USAGE="usage: build-packages.sh <version> <x64|arm64>"
VERSION="${1:?$USAGE}"
ARCH="${2:?$USAGE}"

case "$ARCH" in
    x64)   PKG_ARCH=amd64; APPIMAGE_ARCH=x86_64 ;;
    arm64) PKG_ARCH=arm64; APPIMAGE_ARCH=aarch64 ;;
    *) echo "Unsupported architecture '$ARCH'. Use x64 or arm64." >&2; exit 1 ;;
esac

cd "$(dirname "$0")/.."

BUILD_DIR="build/linux-$ARCH"
PUBLISH_DIR="$BUILD_DIR/publish"
TOOLS_DIR="build/tools"
DIST_DIR="dist"

rm -rf "$BUILD_DIR"
mkdir -p "$TOOLS_DIR" "$DIST_DIR"

echo "==> Publishing $VERSION for linux-$ARCH"
dotnet publish src/VoiceRecogniseBot.csproj \
    --configuration Release \
    --runtime "linux-$ARCH" \
    --self-contained true \
    -p:Version="$VERSION" \
    -p:DebugType=none \
    --output "$PUBLISH_DIR"
chmod 755 "$PUBLISH_DIR/VoiceRecogniseBot"

echo "==> tar.gz"
tar -czf "$DIST_DIR/voice-recognise-bot-$VERSION-linux-$ARCH.tar.gz" \
    --transform "s,^\.,voice-recognise-bot-$VERSION," -C "$PUBLISH_DIR" .

echo "==> deb and rpm"
if [ ! -x "$TOOLS_DIR/nfpm" ]; then
    curl -fsSL "https://github.com/goreleaser/nfpm/releases/download/v$NFPM_VERSION/nfpm_${NFPM_VERSION}_Linux_x86_64.tar.gz" \
        | tar -xz -C "$TOOLS_DIR" nfpm
fi

# nfpm only expands environment variables in some fields, so fill in the placeholders here.
sed -e "s|\${PKG_VERSION}|$VERSION|g" \
    -e "s|\${PKG_ARCH}|$PKG_ARCH|g" \
    -e "s|\${PUBLISH_DIR}|$PUBLISH_DIR|g" \
    packaging/nfpm.yaml > "$BUILD_DIR/nfpm.yaml"

for packager in deb rpm; do
    "$TOOLS_DIR/nfpm" package --config "$BUILD_DIR/nfpm.yaml" --packager "$packager" --target "$DIST_DIR/"
done

echo "==> AppImage"
if [ ! -x "$TOOLS_DIR/appimagetool" ]; then
    curl -fsSL -o "$TOOLS_DIR/appimagetool" \
        "https://github.com/AppImage/appimagetool/releases/download/$APPIMAGETOOL_VERSION/appimagetool-x86_64.AppImage"
    chmod +x "$TOOLS_DIR/appimagetool"
fi

# The runtime is the small executable at the front of the AppImage; it has to match the
# target architecture, not the build host.
RUNTIME="$TOOLS_DIR/runtime-$APPIMAGE_ARCH"
if [ ! -f "$RUNTIME" ]; then
    curl -fsSL -o "$RUNTIME" "https://github.com/AppImage/type2-runtime/releases/download/continuous/runtime-$APPIMAGE_ARCH"
fi

APPDIR="$BUILD_DIR/AppDir"
mkdir -p "$APPDIR/usr/lib"
cp -a "$PUBLISH_DIR" "$APPDIR/usr/lib/voice-recognise-bot"
install -m 755 packaging/appimage/AppRun "$APPDIR/AppRun"
install -m 644 packaging/appimage/voice-recognise-bot.desktop "$APPDIR/voice-recognise-bot.desktop"
install -m 644 src/wwwroot/assets/icon.svg "$APPDIR/voice-recognise-bot.svg"

# --appimage-extract-and-run lets appimagetool itself run where FUSE is unavailable (CI, containers).
ARCH="$APPIMAGE_ARCH" "$TOOLS_DIR/appimagetool" --appimage-extract-and-run \
    --no-appstream \
    --runtime-file "$RUNTIME" \
    "$APPDIR" "$DIST_DIR/VoiceRecogniseBot-$VERSION-$APPIMAGE_ARCH.AppImage"

echo "==> Done"
ls -lh "$DIST_DIR"
