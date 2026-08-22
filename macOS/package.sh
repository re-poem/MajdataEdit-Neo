#!/bin/sh
set -eu

if [ "$#" -ne 2 ]; then
    echo "Usage: $0 /path/to/MajdataViewX.app /path/to/runtime-assets" >&2
    exit 64
fi

view_app=$1
runtime_assets=$2

if [ ! -x "$view_app/Contents/MacOS/MajdataViewX" ]; then
    echo "Invalid MajdataViewX app: $view_app" >&2
    exit 66
fi
if [ ! -f "$view_app/Contents/PlugIns/libbass.dylib" ]; then
    echo "MajdataViewX app is missing libbass.dylib: $view_app" >&2
    exit 66
fi
if [ ! -d "$runtime_assets/SFX" ] || [ ! -d "$runtime_assets/Skin" ]; then
    echo "Runtime assets must contain SFX and Skin directories: $runtime_assets" >&2
    exit 66
fi

repo_root=$(CDPATH= cd "$(dirname "$0")/.." && pwd)
dotnet_path=$(command -v dotnet || true)
if [ -z "$dotnet_path" ]; then
    echo ".NET 10 SDK was not found in PATH." >&2
    exit 69
fi
case $("$dotnet_path" --version) in
    10.*) ;;
    *) echo ".NET 10 SDK is required." >&2; exit 69 ;;
esac

stage_dir=$(mktemp -d "${TMPDIR:-/tmp}/majdata-macos.XXXXXX")
trap 'rm -rf "$stage_dir"' EXIT
publish_dir="$stage_dir/publish"
app="$stage_dir/MajdataEdit-Neo.app"
helper_app="$app/Contents/Helpers/MajdataViewX.app"

"$dotnet_path" publish "$repo_root/MajdataEdit-Neo.csproj" \
    -p:PublishProfile=macOS-arm64 \
    -o "$publish_dir"

mkdir -p "$app/Contents/MacOS" "$app/Contents/Helpers"
cp "$repo_root/macOS/Info.plist" "$app/Contents/Info.plist"
ditto "$publish_dir" "$app/Contents/MacOS"
ditto "$view_app" "$helper_app"
ditto "$runtime_assets/SFX" "$helper_app/Contents/MacOS/SFX"
ditto "$runtime_assets/Skin" "$helper_app/Contents/MacOS/Skin"

codesign --force --deep --sign - "$helper_app"
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict "$app"

mkdir -p "$repo_root/artifacts"
output="$repo_root/artifacts/MajdataEdit-Neo.app"
rm -rf "$output"
mv "$app" "$output"
echo "$output"
