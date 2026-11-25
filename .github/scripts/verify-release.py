#!/usr/bin/env python3
"""Verify a locally signed NotchFlow GitHub Release before any deployment."""
import base64
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile

EXPECTED_SPKI_SHA256 = "533d5be602764ff9d94132106494543ec95380e1a54e674e8fdb53f39624f1bc"
BASE_URL = "https://195.78.49.209:8443/notchflow/releases/"


def fail(message: str) -> None:
    raise SystemExit(f"RELEASE REJECTED: {message}")


def main() -> None:
    if len(sys.argv) != 4:
        fail("usage: verify-release.py vX.Y.Z asset-directory public-key.pem")
    tag, folder_arg, key_arg = sys.argv[1:]
    if not re.fullmatch(r"v[0-9]+\.[0-9]+\.[0-9]+(\.[0-9]+)?", tag):
        fail("unexpected tag format")
    version = tag[1:]
    folder = Path(folder_arg)
    key = Path(key_arg)
    manifest_file = folder / "version.json"
    signature_file = folder / "version.json.sig"
    installer_name = f"NotchFlow-{version}-Setup.exe"
    installer_file = folder / installer_name
    for file in (key, manifest_file, signature_file, installer_file):
        if not file.is_file():
            fail(f"missing required file: {file}")

    raw = manifest_file.read_bytes()
    if len(raw) > 30000:
        fail("manifest too large")
    try:
        manifest = json.loads(raw)
    except (ValueError, UnicodeDecodeError) as exc:
        fail(f"invalid JSON: {exc}")
    expected = {
        "version": version,
        "channel": "stable",
        "platform": "win-x64",
        "downloadUrl": BASE_URL + installer_name,
    }
    if not isinstance(manifest, dict):
        fail("manifest must be a JSON object")
    for field, value in expected.items():
        if manifest.get(field) != value:
            fail(f"{field} mismatch")
    actual_size = installer_file.stat().st_size
    if not 0 < actual_size <= 512 * 1024 * 1024:
        fail("installer size out of range")
    if type(manifest.get("sizeBytes")) is not int or manifest["sizeBytes"] != actual_size:
        fail("installer size mismatch")
    actual_hash = hashlib.sha256(installer_file.read_bytes()).hexdigest()
    if not isinstance(manifest.get("sha256"), str) or manifest["sha256"].lower() != actual_hash:
        fail("installer SHA-256 mismatch")

    # The SPKI fingerprint is pinned here independently of the repository PEM file.
    result = subprocess.run(
        ["openssl", "pkey", "-pubin", "-in", str(key), "-outform", "DER"],
        capture_output=True,
        check=True,
    )
    if hashlib.sha256(result.stdout).hexdigest() != EXPECTED_SPKI_SHA256:
        fail("public key fingerprint mismatch")

    try:
        signature = base64.b64decode(signature_file.read_bytes().strip(), validate=True)
    except ValueError:
        fail("signature is not valid base64")
    if len(signature) != 384:
        fail("RSA-3072 signature must contain 384 binary bytes")
    with tempfile.TemporaryDirectory() as tmp:
        sig_path = Path(tmp) / "signature.bin"
        sig_path.write_bytes(signature)
        verify = subprocess.run(
            [
                "openssl", "dgst", "-sha256", "-verify", str(key),
                "-sigopt", "rsa_padding_mode:pss",
                "-sigopt", "rsa_pss_saltlen:-1",
                "-signature", str(sig_path), str(manifest_file),
            ],
            capture_output=True, text=True,
        )
        if verify.returncode != 0 or "Verified OK" not in verify.stdout:
            fail("RSA-PSS/SHA-256 manifest signature verification failed")
    print(f"VERIFIED: {tag}; signature, pinned key, URL, hash and size all match")


if __name__ == "__main__":
    main()
