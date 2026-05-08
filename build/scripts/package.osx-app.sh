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

zip "gethub_$VERSION.$RUNTIME.zip" -r GetHub.app
