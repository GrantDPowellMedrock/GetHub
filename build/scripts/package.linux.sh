#!/usr/bin/env bash

set -e
set -o
set -u
set pipefail

# ICU versions to support (Debian has no virtual package, must list all)
# Format: space-separated version numbers
ICU_VERSIONS="78 77 76 74 72 71 70 69 68 67 66 65 63"

arch=
appimage_arch=
target=
case "$RUNTIME" in
    linux-x64)
        arch=amd64
        appimage_arch=x86_64
        target=x86_64;;
    linux-arm64)
        arch=arm64
        appimage_arch=arm_aarch64
        target=aarch64;;
    *)
        echo "Unknown runtime $RUNTIME"
        exit 1;;
esac

APPIMAGETOOL_URL=https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage

cd build

if [[ ! -f "appimagetool" ]]; then
    curl -o appimagetool -L "$APPIMAGETOOL_URL"
    chmod +x appimagetool
fi

rm -f GetHub/*.dbg
rm -f GetHub/*.pdb

mkdir -p GetHub.AppDir/opt
mkdir -p GetHub.AppDir/usr/share/metainfo
mkdir -p GetHub.AppDir/usr/share/applications

cp -r GetHub GetHub.AppDir/opt/gethub
desktop-file-install resources/_common/applications/gethub.desktop --dir GetHub.AppDir/usr/share/applications \
    --set-icon com.gethub_scm.GetHub --set-key=Exec --set-value=AppRun
mv GetHub.AppDir/usr/share/applications/{gethub,com.gethub_scm.GetHub}.desktop
cp resources/_common/icons/gethub.png GetHub.AppDir/com.gethub_scm.GetHub.png
ln -rsf GetHub.AppDir/opt/gethub/gethub GetHub.AppDir/AppRun
ln -rsf GetHub.AppDir/usr/share/applications/com.gethub_scm.GetHub.desktop GetHub.AppDir
cp resources/appimage/gethub.appdata.xml GetHub.AppDir/usr/share/metainfo/com.gethub_scm.GetHub.appdata.xml

ARCH="$appimage_arch" ./appimagetool -v GetHub.AppDir "gethub-$VERSION.linux.$arch.AppImage"

mkdir -p resources/deb/opt/gethub/
mkdir -p resources/deb/usr/bin
mkdir -p resources/deb/usr/share/applications
mkdir -p resources/deb/usr/share/icons
cp -f GetHub/* resources/deb/opt/gethub
ln -rsf resources/deb/opt/gethub/gethub resources/deb/usr/bin
cp -r resources/_common/applications resources/deb/usr/share
cp -r resources/_common/icons resources/deb/usr/share

# Calculate installed size in KB
installed_size=$(du -sk resources/deb | cut -f1)

# Generate ICU dependencies string for Debian
# Debian lacks libicu virtual package, must list all versions with OR operator
icu_deps="libicu"
for v in $ICU_VERSIONS; do
    icu_deps="$icu_deps | libicu$v"
done

# Update the control file (replace placeholder, not whole Depends line)
sed -i -e "s/^Version:.*/Version: $VERSION/" \
    -e "s/^Architecture:.*/Architecture: $arch/" \
    -e "s/^Installed-Size:.*/Installed-Size: $installed_size/" \
    -e "s/@ICU_DEPS@/$icu_deps/" \
    resources/deb/DEBIAN/control

# Build deb package with gzip compression
dpkg-deb -Zgzip --root-owner-group --build resources/deb "gethub_$VERSION-1_$arch.deb"

rpmbuild -bb --target="$target" resources/rpm/SPECS/build.spec --define "_topdir $(pwd)/resources/rpm" --define "_version $VERSION"
mv "resources/rpm/RPMS/$target/gethub-$VERSION-1.$target.rpm" ./
