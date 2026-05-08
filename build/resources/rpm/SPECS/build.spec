Name: gethub
Version: %_version
Release: 1
Summary: Open-source & Free Git Gui Client
License: MIT
URL: https://gethub-scm.github.io/
Source: https://github.com/gethub-scm/gethub/archive/refs/tags/v%_version.tar.gz
Requires: libX11.so.6()(%{__isa_bits}bit)
Requires: libSM.so.6()(%{__isa_bits}bit)
Requires: libicu
Requires: xdg-utils

%define _build_id_links none

%description
Open-source & Free Git Gui Client

%install
mkdir -p %{buildroot}/opt/gethub
mkdir -p %{buildroot}/%{_bindir}
mkdir -p %{buildroot}/usr/share/applications
mkdir -p %{buildroot}/usr/share/icons
cp -f %{_topdir}/../../GetHub/* %{buildroot}/opt/gethub/
ln -rsf %{buildroot}/opt/gethub/gethub %{buildroot}/%{_bindir}
cp -r %{_topdir}/../_common/applications %{buildroot}/%{_datadir}
cp -r %{_topdir}/../_common/icons %{buildroot}/%{_datadir}
chmod 755 -R %{buildroot}/opt/gethub
chmod 755 %{buildroot}/%{_datadir}/applications/gethub.desktop

%files
%dir /opt/gethub/
/opt/gethub/*
/usr/share/applications/gethub.desktop
/usr/share/icons/*
%{_bindir}/gethub

%changelog
# skip
