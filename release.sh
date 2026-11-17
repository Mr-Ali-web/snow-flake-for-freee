#!/bin/bash
# Builds a universal (Apple Silicon + Intel) app and zips it for GitHub Releases.
# Output: build/SnowFlakeUz-macOS.zip
set -e
cd "$(dirname "$0")"
APP="build/SnowFlakeUz.app"
rm -rf build
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" build/tmp
for ARCH in arm64 x86_64; do
  swiftc -O -swift-version 5 -target "$ARCH-apple-macos12.0" Sources/*.swift \
      -o "build/tmp/SnowFlakeUz-$ARCH" -framework AppKit -framework ServiceManagement
done
lipo -create build/tmp/SnowFlakeUz-arm64 build/tmp/SnowFlakeUz-x86_64 -output "$APP/Contents/MacOS/SnowFlakeUz"
cp Info.plist "$APP/Contents/Info.plist"
codesign --force --sign - "$APP"
ditto -c -k --keepParent "$APP" build/SnowFlakeUz-macOS.zip
echo "Tayyor: build/SnowFlakeUz-macOS.zip  (GitHub Releases'ga yuklang)"
