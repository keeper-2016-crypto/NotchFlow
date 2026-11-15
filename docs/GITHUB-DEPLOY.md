# NotchFlow signed release delivery (draft)

This is a **manual, production-gated** deployment pipeline. It does not build or sign an installer. It downloads a GitHub Release that was prepared and RSA-PSS/SHA-256-signed on the trusted Windows developer machine. The private signing key must never leave that machine.

## Before merging

1. Synchronize the developer's **current 0.3.6+ source** to the repository; GitHub's main branch previously had 0.3.4 code.
2. Commit **only** `NotchFlow/Assets/UpdatePublicKey.pem` (the public PEM), using a narrow `.gitignore` exception. Never commit the DPAPI private key or encrypted export.
3. Confirm the embedded public key's SHA-256 SPKI fingerprint equals `533D5BE602764FF9D94132106494543EC95380E1A54E674E8FDB53F39624F1BC`.
4. Back up and test the signing key recovery process. Finish 0.3.5 -> 0.3.6 user-data migration testing.
5. Review these scripts before merging. Do not publish 0.3.7 merely to test SSH access.

## Prepare a release on Windows

Use `pwsh` and `scripts/Prepare-UpdateRelease.ps1` from the current source with the verified installer, version, release notes file, and new output directory. Confirm files are:

```text
version.json
version.json.sig
releases/NotchFlow-X.Y.Z-Setup.exe
```

Create a GitHub Release with tag `vX.Y.Z` and upload those **three** files as top-level release assets. Example (run from the signed package directory after installing and authenticating GitHub CLI):

```powershell
gh release create vX.Y.Z ./version.json ./version.json.sig ./releases/NotchFlow-X.Y.Z-Setup.exe --repo keeper-2016-crypto/NotchFlow --title "NotchFlow X.Y.Z" --notes-file ./release-notes.md
```

Do not include the signing key or its backup. **Never publish a release whose installer was not tested.** Note that publishing a GitHub Release is independent from deploying it to the VPS.

## Server SSH setup (requires administrator)

Create `notchflow-deploy` with its own key-based SSH authentication and no root login. Store the corresponding **private SSH deployment key** as the GitHub Actions environment secret `NOTCHFLOW_SSH_KEY`. This is a separate key from the **RSA update-signing key**.

Have the server administrator explicitly prepare:

- `/home/notchflow-deploy/incoming/` writable by `notchflow-deploy`
- `/var/www/notchflow/releases/` writable by `notchflow-deploy` for atomic installer placement
- `/var/www/notchflow/` writable by `notchflow-deploy` for atomic manifest+signature placement
- a backup directory `/home/notchflow-deploy/notchflow-backups/` writable by `notchflow-deploy`

**Security warning:** granting write access to the web root gives the deploy SSH credential the power to replace public update files. Use a dedicated deployment account/key, tightly scoped SSH access, and no other services under this web root. For stricter isolation, replace the directory permissions with a root-owned audited deployment wrapper that validates an allowlisted staging directory and the pinned public-key signature.

Record the real VPS SSH host key fingerprint over an already trusted admin SSH session, then store its **verified** OpenSSH `known_hosts` line as GitHub environment secret `NOTCHFLOW_SSH_KNOWN_HOSTS`. Do not blindly trust `ssh-keyscan` output without independently comparing the fingerprint.

Configure the GitHub `production` environment with required reviewers/protection if your plan supports it. The workflow requires both the typed value `DEPLOY` and the production environment gate.

## Run

In GitHub Actions, select **Deploy signed NotchFlow update** on `main`, enter `release_tag=vX.Y.Z` and `confirm=DEPLOY`. The workflow downloads assets, validates the pinned public key, the signature, version, URL, byte size, and SHA-256, uploads to VPS over authenticated SSH, backs up the prior manifest, and activates the new release.

The server script refuses equal or older versions. The installer is published first; signature and manifest are individually atomically renamed, **not jointly transactionally swapped**. A client that observes a transient mismatch must reject/retry.

Avoid running against 0.3.6 unless deliberately testing a later version. No action happens just by merging this PR.
