#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUILD_DIR="${BUILD_DIR:-${ROOT}/packaging/deb-build}"

mkdir -p "${BUILD_DIR}"
rsync -a "${ROOT}/" "${BUILD_DIR}/nmap-src/" \
  --exclude .git \
  --exclude .xcode-build \
  --exclude packaging/deb-build

rm -rf "${BUILD_DIR}/nmap-src/debian"
cp -a "${ROOT}/packaging/debian" "${BUILD_DIR}/nmap-src/debian"
chmod +x "${BUILD_DIR}/nmap-src/debian/rules"

VERSION="$(python3 "${ROOT}/packaging/nmap-version.py")"
(cd "${BUILD_DIR}/nmap-src/zenmap" && python3 install_scripts/utils/version_update.py "${VERSION}")

cd "${BUILD_DIR}/nmap-src"
dpkg-buildpackage -b -us -uc

echo "Debian build complete. Packages are in ${BUILD_DIR}"
