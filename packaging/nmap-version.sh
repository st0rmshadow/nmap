#!/usr/bin/env bash
# Print the nmap release version from nmap.h (e.g. 7.99, 8.0).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exec python3 "${ROOT}/packaging/nmap-version.py"
