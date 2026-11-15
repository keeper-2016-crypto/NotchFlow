#!/usr/bin/env bash
# Executed over pinned-host SSH as the unprivileged notchflow-deploy user.
set -euo pipefail
umask 027

STAGE="${1:?staging directory required}"
VERSION="${2:?version required}"
BASE="/var/www/notchflow"
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "Invalid version"; exit 1; }
[[ "$STAGE" =~ ^/home/notchflow-deploy/incoming/[0-9]+-[0-9]+$ ]] || { echo "Invalid stage directory"; exit 1; }
[[ "$(id -un)" == "notchflow-deploy" ]] || { echo "Wrong SSH user"; exit 1; }
test -f "$STAGE/version.json"
test -f "$STAGE/version.json.sig"
test -f "$STAGE/NotchFlow-$VERSION-Setup.exe"
test -f "$BASE/version.json"
test -d "$BASE/releases"

# Recheck the remote files, reject downgrades and duplicate deployments.
python3 - "$STAGE" "$BASE" "$VERSION" <<'PY'
import hashlib
import json
from pathlib import Path
import sys

stage, base, version = map(Path, sys.argv[1:3]) if False else (Path(sys.argv[1]), Path(sys.argv[2]), sys.argv[3])
new = json.loads((stage / "version.json").read_bytes())
old = json.loads((base / "version.json").read_bytes())
def as_tuple(v):
    return tuple(int(x) for x in v.split("."))
if as_tuple(new["version"]) != as_tuple(version):
    raise SystemExit("Manifest version mismatch")
if as_tuple(new["version"]) <= as_tuple(old["version"]):
    raise SystemExit("Refusing non-incremental release")
name = f"NotchFlow-{version}-Setup.exe"
installer = stage / name
if new.get("downloadUrl") != f"https://195.78.49.209:8443/notchflow/releases/{name}":
    raise SystemExit("Unexpected download URL")
if new.get("sizeBytes") != installer.stat().st_size:
    raise SystemExit("Installer size mismatch")
if hashlib.sha256(installer.read_bytes()).hexdigest() != new.get("sha256", "").lower():
    raise SystemExit("Installer checksum mismatch")
PY

BACKUP="$HOME/notchflow-backups/$(date -u +%Y%m%dT%H%M%SZ)-before-$VERSION"
mkdir -p "$BACKUP"
cp -a "$BASE/version.json" "$BACKUP/version.json"
if [[ -f "$BASE/version.json.sig" ]]; then
  cp -a "$BASE/version.json.sig" "$BACKUP/version.json.sig"
fi

# Keep existing installers intact. Make installer available first, manifest last.
install -m 644 "$STAGE/NotchFlow-$VERSION-Setup.exe" "$BASE/releases/.NotchFlow-$VERSION-Setup.exe.new"
mv -f "$BASE/releases/.NotchFlow-$VERSION-Setup.exe.new" "$BASE/releases/NotchFlow-$VERSION-Setup.exe"
install -m 644 "$STAGE/version.json.sig" "$BASE/.version.json.sig.new"
install -m 644 "$STAGE/version.json" "$BASE/.version.json.new"
mv -f "$BASE/.version.json.sig.new" "$BASE/version.json.sig"
mv -f "$BASE/.version.json.new" "$BASE/version.json"
rm -rf -- "$STAGE"
echo "Activated signed update $VERSION (backup: $BACKUP)"
