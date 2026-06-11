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

# Ad-hoc code-sign the whole bundle. We have no Apple Developer cert (so it
# can't be notarized), but an ad-hoc signature ("-") makes the bundle/​binaries
# internally consistent. Without this, a downloaded unsigned app is often
# hard-blocked as "GetHub.app is damaged and can't be opened" — especially on
# Apple Silicon, which refuses to run an unsigned/invalid Mach-O at all.
# With an ad-hoc signature the user can right-click -> Open (or clear quarantine).
codesign --force --deep --sign - GetHub.app || echo "warning: codesign failed (continuing)"

# Bundle "how to open" instructions (unsigned/un-notarized -> Gatekeeper warns).
cat > HOW-TO-OPEN.txt <<'EOF'
GetHub for macOS
================

GetHub is ad-hoc signed but NOT notarized by Apple, so Gatekeeper warns on
first launch. Pick whichever works:

EASIEST — right-click to open:
  1. Drag GetHub.app to /Applications
  2. Right-click (or Control-click) GetHub.app -> "Open"
  3. In the dialog, click "Open" again
  (You only have to do this once; after that it launches normally.)

IF IT STILL WON'T OPEN ("damaged" / "can't be opened") — clear quarantine:
  Open Terminal and run ONCE:

       xattr -cr /Applications/GetHub.app

  then open GetHub normally. (Point the path wherever GetHub.app actually is.)

Make sure you downloaded the right build:
  - Apple Silicon (M1/M2/M3/M4): gethub_<ver>.osx-arm64.zip
  - Intel Macs:                  gethub_<ver>.osx-x64.zip

Requires Git: install Xcode Command Line Tools (run: xcode-select --install)
or Homebrew git (brew install git).
EOF

zip "gethub_$VERSION.$RUNTIME.zip" -r GetHub.app HOW-TO-OPEN.txt
