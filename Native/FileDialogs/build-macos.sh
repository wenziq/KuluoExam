#!/bin/sh
set -eu
root=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
xcrun clang++ -dynamiclib -fobjc-arc -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 \
  -framework AppKit -O2 "$root/Native/FileDialogs/SokobanFileDialogs.mm" \
  -o "$root/Assets/Project/Plugins/macOS/libSokobanFileDialogs.dylib"
