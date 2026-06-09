#!/usr/bin/env bash
set -Eeuo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run this script as root: sudo $0" >&2
  exit 1
fi

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_dir="$(cd -- "${script_dir}/.." && pwd)"
src_dir="${repo_dir}/src"
temp_publish="$(mktemp -d)"

cleanup() {
  rm -rf "${temp_publish}"
}
trap cleanup EXIT

export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp/dotnet-cli-home}"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_NOLOGO=1
mkdir -p "${DOTNET_CLI_HOME}"

apt update
apt install -y wget gpg apt-transport-https

if [[ ! -f /etc/apt/sources.list.d/microsoft-prod.list ]]; then
  wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O "${temp_publish}/packages-microsoft-prod.deb"
  dpkg -i "${temp_publish}/packages-microsoft-prod.deb"
fi

apt update
apt install -y dotnet-sdk-10.0

if [[ ! -d "${src_dir}" ]]; then
  echo "Project source directory not found: ${src_dir}" >&2
  exit 1
fi

dotnet workload update
dotnet publish "${src_dir}" -c Release --self-contained -r linux-x64 -p:PublishSingleFile=true -o "${temp_publish}/linux"

install -d /opt/VoiceRecogniseTelegramBot
cp -a "${temp_publish}/linux/." /opt/VoiceRecogniseTelegramBot/

echo "Installed to /opt/VoiceRecogniseTelegramBot"
