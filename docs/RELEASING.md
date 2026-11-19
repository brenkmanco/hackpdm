# Publishing & Automatic Update Rollout Guide

HackPDM uses a single version source (`Directory.Build.props` / git tags) and SHA-256 verified installers so releases can be rolled out to engineering workstations automatically or on demand.

---

## 1. Versioning

- Default development version lives in `Directory.Build.props` (`<HackPdmVersion>1.1.0</HackPdmVersion>`).
- Release workflows automatically derive `X.Y.Z` from git tags matching `vX.Y.Z` (or `vX.Y.Z-rc1` for prereleases) and stamp it onto:
  - `HackPDM.exe` and all `HackPDM.*.dll` assembly/file versions & Windows Explorer Details
  - The Inno Setup installer (`HackPDM-X.Y.Z-win-x64-setup.exe`)
  - Linux/macOS archives (`HackPDM-X.Y.Z-<rid>.tar.gz`)

---

## 2. Cutting a Release

```bash
git tag v1.2.0
git push origin v1.2.0
```

This triggers two GitHub Actions workflows:

1. **`.github/workflows/windows-release.yml`**:
   - Runs Windows unit tests.
   - Publishes a self-contained `win-x64` Release build.
   - Signs `HackPDM.exe`, `HackPDM*.dll`, and the Inno Setup installer with Authenticode (when signing secrets are set).
   - Generates `.sha256` checksum files and uploads `HackPDM-1.2.0-win-x64-setup.exe`, `HackPDM-1.2.0-win-x64.zip`, and their `.sha256` files to the GitHub Release.
   - Optionally runs the **`rollout-odoo`** job (for stable tags without `-`) to update Odoo's `hp.settings` automatically.
2. **`.github/workflows/desktop-release.yml`**:
   - Runs unit tests and publishes self-contained `net10.0-desktop` builds for `linux-x64`, `osx-arm64`, and `osx-x64` (including `hackpdm.desktop` and `install-desktop-entry.sh` for Linux) with `.sha256` checksums, attaching them to the same GitHub Release.

---

## 3. Repository Secrets & Variables

Configure these under **GitHub Repository -> Settings -> Secrets and variables -> Actions**:

### Code Signing (Optional, Recommended for Windows)
| Secret | Purpose |
| :--- | :--- |
| `WINDOWS_CERT_PFX_BASE64` | Base64-encoded Authenticode `.pfx` certificate |
| `WINDOWS_CERT_PASSWORD` | Password for the `.pfx` certificate |

### Automatic Odoo Update Rollout (Optional)
| Secret / Variable | Type | Purpose |
| :--- | :--- | :--- |
| `ODOO_URL` | Secret | Production Odoo URL (e.g. `https://pdm.example.com`) |
| `ODOO_DB` | Secret | Odoo database name |
| `ODOO_USER` | Secret | Odoo admin username |
| `ODOO_PASSWORD` | Secret | Odoo admin password or API key |
| `CLIENT_VERSION_RULE` | Variable | Optional rollout policy on tag push: `soft` (default), `min`, `exact`, or a custom range (e.g. `>=1.2.0, <2.0.0`) |

If the four `ODOO_*` secrets are not set, the `rollout-odoo` job skips cleanly and the GitHub Release still succeeds.

---

## 4. Soft vs. Mandatory Update Rollouts

Odoo's `hp.settings` controls how clients respond to a new release:

| Setting | Behavior |
| :--- | :--- |
| `client_version` | **Hard compatibility policy.** Clients outside this range (e.g. `>=1.2.0, <2.0.0`) are denied login and prompted to install the update immediately. |
| `client_latest_version` | **Soft/recommended version** (e.g. `1.2.0`). Clients that still satisfy `client_version` are allowed to log in, and receive a non-blocking prompt once per session offering to update. |
| `client_download_url` | `https://` URL or UNC network share (`\\server\share\...`) pointing to the installer. |
| `client_download_sha256` | Required 64-character lowercase SHA-256 hex digest. The client deletes and refuses to run any download whose hash does not match. |

Engineers can also check for updates anytime via **Settings -> About & Updates -> Check for Updates** (checks Odoo first, then falls back to GitHub Releases with `.sha256` verification).

---

## 5. Manual Rollout or Rollback

### Option A: GitHub Actions UI (Recommended)
Run **Actions -> Roll out client update to Odoo -> Run workflow** (`.github/workflows/rollout-update.yml`):
- **`tag`**: Any published release tag (e.g. `v1.2.0` to roll forward, or `v1.1.0` to roll back).
- **`rollout_mode`**: `soft`, `mandatory`, `exact`, or `custom`.
- **`custom_version_rule`**: Used when `rollout_mode` is `custom` (e.g. `>=1.1.0, <2.0.0`).

### Option B: CLI Script
```bash
ODOO_URL=https://pdm.example.com ODOO_DB=hackpdm_prod ODOO_USER=admin ODOO_PASSWORD=... \
  python3 Odoo/deploy/publish_client_release.py \
  ">=1.1.0, <2.0.0" \
  "https://github.com/moonxbears/wyrmpdm/releases/download/v1.2.0/HackPDM-1.2.0-win-x64-setup.exe" \
  "<64-char-lowercase-sha256>" \
  --latest-version "1.2.0"
```
