# Nmap 8.0 packaging for native Zenmap

This directory documents how native Zenmap GUIs are packaged across platforms.
Classic GTK3/Python `zenmap` and native front ends can coexist side by side.

Version numbers for Linux packaging scripts are read from `nmap.h` via
`packaging/nmap-version.py` (or `packaging/nmap-version.sh`).

## Layout

| Platform | Native GUI | Packaging location |
|----------|------------|-------------------|
| Linux DEB | `zenmap-native` (GTK4) | `packaging/debian/` |
| Linux RPM | `zenmap-native` | `zenmap-native.spec.in` (repo root) |
| Arch | `zenmap-native` | `packaging/arch/PKGBUILD` |
| macOS | `Zenmap.app` (SwiftUI) | `macosx/release-*.sh`, `macosx/pkg-nmap-macos.sh` |
| Windows | `Zenmap.exe` (WinUI) | `mswin32/` + `zenmap/windows/build-for-installer.ps1` |

Legacy GTK3 Zenmap packaging (jhbuild bundle on macOS, classic `zenmap.spec.in`
RPM) remains in the tree but is separate from the native GUI installers below.

---

## Linux

### Autotools install (from source)

```bash
./configure
make build-zenmap-native
sudo make install-zenmap-native
```

Or install everything, including native Zenmap:

```bash
./configure
make
sudo make install
```

Disable native Zenmap while keeping classic Zenmap:

```bash
./configure --without-zenmap-native
```

Verify from source:

```bash
make check-zenmap-native
# or
bash zenmap/linux/verify-linux-native.sh
```

### RPM

Build on Fedora/RHEL/CentOS Stream with `rpmbuild` installed:

```bash
./packaging/build-zenmap-native-rpm.sh
```

Artifacts land in `packaging/rpm-build/RPMS/`. The script reads `nmap.h` for
the version and substitutes `@VERSION@` in `zenmap-native.spec.in`.

### Debian / Ubuntu / Kali

```bash
sudo apt install debhelper dh-python python3-build python3-wheel devscripts \
  desktop-file-utils python3-gi gir1.2-gtk-4.0 gir1.2-adw-1
./packaging/build-zenmap-native-deb.sh
```

Packages are written to `packaging/deb-build/`. The overlay in
`packaging/debian/` is copied into the source tree before `dpkg-buildpackage`.

Docker example (when no native `.deb` tools are available):

```bash
docker run --rm -v "$PWD:/src" -w /src debian:bookworm bash -lc '
  apt-get update &&
  apt-get install -y debhelper dh-python python3-build python3-wheel devscripts \
    desktop-file-utils python3-gi gir1.2-gtk-4.0 gir1.2-adw-1 rsync &&
  ./packaging/build-zenmap-native-deb.sh
'
```

### Arch Linux

On Arch with `base-devel` installed:

```bash
./packaging/build-zenmap-native-arch.sh
```

Or copy `packaging/arch/PKGBUILD` into an `nmap-${pkgver}.tar.gz` source tree
and run `makepkg -sf`. The build script patches `pkgver` from `nmap.h`.

### Linux package contents

| Path | Purpose |
|------|---------|
| `/usr/bin/zenmap-native` | GUI launcher |
| `/usr/share/applications/org.nmap.ZenmapNativeLinux.desktop` | Desktop entry |
| `/usr/share/icons/hicolor/256x256/apps/zenmap.png` | Application icon |
| `/usr/share/man/man1/zenmap-native.1` | Man page |
| Python site-packages `zenmap.linux*` | Application code |

### Linux runtime dependencies

- `nmap`
- `python3`
- `python3-gobject` / `PyGObject`
- `gtk4`
- `libadwaita`
- `polkit` / `pkexec`

---

## macOS

Native macOS packaging uses the Xcode `Zenmap` scheme (SwiftUI) and produces
`.pkg` installers plus a release `.dmg`.

### Prerequisites

- Xcode with macOS SDK
- Homebrew `openssl@3` and `libssh2` (for bundling into `Zenmap.app`)
- Built native CLI tools (`nmap`, `ncat`, `nping`) in the repo root

### Verify build

```bash
bash zenmap/macos/verify-macos-native.sh
```

### Release pipeline

Build CLI bundles, Zenmap.app, component packages, and the distribution DMG:

```bash
# 1. CLI app bundles + /usr/local launchers
bash macosx/release-nmap-cli-macos.sh

# 2. Zenmap.app (build, bundle dylibs, copy to dist/)
bash macosx/release-zenmap-macos.sh

# 3. Component .pkg files + NmapComplete.pkg / NmapCLI.pkg / ZenmapOnly.pkg
bash macosx/pkg-nmap-macos.sh

# 4. DMG with all three product installers
bash macosx/release-nmap-complete-dmg-macos.sh
```

Output:

- `dist/Zenmap.app` — standalone GUI bundle
- `dist/pkg/NmapComplete.pkg` — CLI tools + Zenmap
- `dist/pkg/NmapCLI.pkg` — CLI only
- `dist/pkg/ZenmapOnly.pkg` — Zenmap only
- `dist/nmap-${VERSION}-macOS-${ARCH}.dmg` — release disk image

Package scripts read the version from `packaging/nmap-version.py` (or from the
built `nmap` binary when available).

### Legacy macOS path

`macosx/Makefile` (jhbuild + GTK3 `make-bundle.sh`) builds the old Python Zenmap
bundle. Use the `release-*.sh` scripts above for native SwiftUI Zenmap.

---

## Windows

Native Windows Zenmap is a WinUI 3 / .NET 8 app. **The GUI no longer uses
Python, GTK, or Cygwin** — but the **official combined nmap installer** still
uses the `mswin32/` Cygwin Makefile and NSIS to bundle CLI tools + Zenmap.

### Native GUI vs installer tooling (read this first)

| Question | Answer |
|----------|--------|
| Is native Zenmap = WinUI / `dotnet publish`? | **Yes.** No Python/GTK. |
| Does the full nmap Windows *distribution* still use Cygwin + NSIS? | **Yes.** `mswin32/Makefile` orchestrates MSVC builds, staging, signing, and NSIS. Cygwin is the build shell, not the Zenmap runtime. |
| Can Zenmap ship standalone without NSIS? | **Yes.** `dotnet publish` produces a self-contained `Zenmap.exe` + DLLs you can zip or distribute directly. |
| Is Cygwin still required to build Zenmap itself? | **No.** Only PowerShell + .NET SDK + VS workload. |
| Is NSIS required for Zenmap alone? | **No.** NSIS is only for the combined `nmap-${VERSION}-setup.exe`. |

### Path A — Zenmap standalone (PowerShell only)

Minimal distribution of the native GUI only. Requires nmap on PATH or configured
in Zenmap settings.

```powershell
cd zenmap\windows
powershell -File verify-windows-native.ps1

# Or publish a redistributable folder:
powershell -File build-for-installer.ps1 -OutputDir C:\path\to\Zenmap-dist
# Produces: C:\path\to\Zenmap-dist\Zenmap.exe (+ WinUI / Windows App SDK DLLs)
```

Zip `Zenmap-dist\` and ship. No Cygwin, no NSIS, no MSVC needed for this path.

### Path B — Full nmap + Zenmap installer (Cygwin + MSVC + NSIS)

Official release installer bundling nmap.exe, Ncat, Nping, Ndiff, Npcap, and Zenmap.

From Cygwin/MSYS in `mswin32/` after building nmap with MSVC:

```bash
make stage          # MSVC nmap + dotnet publish Zenmap via build-for-installer.ps1
make sign-files     # requires code-signing cert
make bundle-nsis    # produces nmap-${VERSION}-setup.exe
make bundle-zip     # CLI zip (Zenmap excluded from OEM zip)
```

`stage-common` in `mswin32/Makefile` calls PowerShell to publish native Zenmap:

```powershell
zenmap/windows/build-for-installer.ps1 -OutputDir mswin32/nmap-${VERSION}/zenmap
```

NSIS installs `$INSTDIR\zenmap\Zenmap.exe` (see `mswin32/nsis/Nmap.nsi`,
`SecZenmapFiles` in `nmap-common.nsh`). Shortcuts point at the native executable.
Use `/ZENMAP=NO` to skip GUI installation.

Requirements on the build host:

- Cygwin/MSYS (Makefile orchestration only)
- Visual Studio 2019+ (x86 Release for nmap.exe)
- .NET SDK (for `dotnet publish` of Zenmap)
- NSIS large-strings build (combined installer only)
- Windows App SDK (via project dependencies)

---

## Upstream release checklist

**Do not bump `nmap.h` to 8.0 until upstream approves.** Packaging scripts read
the current version (7.99) automatically via `packaging/nmap-version.py`.

When upstream approves the 8.0 release:

1. Bump `NMAP_MAJOR` / `NMAP_MINOR` in `nmap.h` (packaging scripts follow automatically).
2. Run `zenmap/install_scripts/utils/version_update.py X.YY` for Zenmap Python metadata.
3. Update `packaging/debian/changelog` with the new Debian version.
4. Build and smoke-test each platform (commands above).
5. Sign macOS `.pkg`/`.dmg` and Windows `.exe` with project certificates before publishing.

Classic `zenmap` (GTK3) packages remain available for distros that have not
yet adopted native front ends; native packages use distinct names/paths
(`zenmap-native` on Linux, `Zenmap.app` / `Zenmap.exe` on desktop).
