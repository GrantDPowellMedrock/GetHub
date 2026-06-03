#!/usr/bin/env bash

set -e
set -o
set -u
set pipefail

cd build

mkdir -p GetHub.app/Contents/Resources
mv GetHub GetHub.app/Contents/MacOS
cp resources/app/App.icns GetHub.app/Contents/Resources/App.icns
sed "s/SOURCE_GIT_VERSION/$VERSION/g" resources/app/App.plist > GetHub.app/Contents/Info.plist
rm -rf GetHub.app/Contents/MacOS/GetHub.dsym
rm -f GetHub.app/Contents/MacOS/*.pdb

# Bundle "how to open" instructions (app is unsigned/un-notarized -> Gatekeeper blocks it)
cat > HOW-TO-OPEN.txt <<'EOF'
GetHub for macOS
================

This app is not notarized by Apple, so Gatekeeper blocks it on first open
("GetHub.app is damaged" or "cannot be opened"). To allow it:

  1. Drag GetHub.app to /Applications
  2. Open Terminal and run ONCE:

       xattr -cr /Applications/GetHub.app

     (If you keep it elsewhere, point the path at wherever GetHub.app is.)

  3. Open GetHub normally.

Requires Git: install Xcode Command Line Tools (run: xcode-select --install)
or Homebrew git (brew install git).
EOF

zip "gethub_$VERSION.$RUNTIME.zip" -r GetHub.app HOW-TO-OPEN.txt
