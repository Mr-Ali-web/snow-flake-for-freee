#!/bin/bash
# Builds build/SnowFlakeUz.app (needs Xcode or Xcode Command Line Tools)
set -e
cd "$(dirname "$0")"
APP="build/SnowFlakeUz.app"
rm -rf build
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
swiftc -O -swift-version 5 Sources/*.swift -o "$APP/Contents/MacOS/SnowFlakeUz" \
    -framework AppKit -framework ServiceManagement
cp Info.plist "$APP/Contents/Info.plist"
codesign --force --sign - "$APP" >/dev/null 2>&1 || true
echo "Tayyor: $APP"
echo "Ishga tushirish: open $APP"
