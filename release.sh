#!/usr/bin/env bash
# Build the mod and package a release zip ready to upload to GitHub Releases.
# Output: dist/dev_console-<version>.zip with this structure:
#   dev_console/manifest.json
#   dev_console/preview.png
#   dev_console/assembly/common/DevConsole.dll
set -euo pipefail

cd "$(dirname "$0")"

MOD_ID="dev_console"
ASSEMBLY_NAME="DevConsole"
TFM="netstandard2.1"
MANIFEST="mod/manifest.json"
PREVIEW="mod/preview.png"

if ! command -v jq >/dev/null 2>&1; then
    echo "error: jq is required to read the version from $MANIFEST" >&2
    exit 1
fi

VERSION=$(jq -r '.asset.Version' "$MANIFEST")
if [[ -z "$VERSION" || "$VERSION" == "null" ]]; then
    echo "error: could not read .asset.Version from $MANIFEST" >&2
    exit 1
fi

echo ">> building $ASSEMBLY_NAME v$VERSION"
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build -c Release

DLL="bin/Release/$TFM/$ASSEMBLY_NAME.dll"
if [[ ! -f "$DLL" ]]; then
    echo "error: build did not produce $DLL" >&2
    exit 1
fi

STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT

mkdir -p "$STAGE/$MOD_ID/assembly/common"
cp "$MANIFEST" "$STAGE/$MOD_ID/manifest.json"
cp "$PREVIEW" "$STAGE/$MOD_ID/preview.png"
cp "$DLL" "$STAGE/$MOD_ID/assembly/common/$ASSEMBLY_NAME.dll"

mkdir -p dist
ZIP="dist/${MOD_ID}-${VERSION}.zip"
rm -f "$ZIP"
(cd "$STAGE" && zip -r "$OLDPWD/$ZIP" "$MOD_ID")

echo ">> wrote $ZIP"
unzip -l "$ZIP"
