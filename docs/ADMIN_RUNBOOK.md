# HackPDM Administrator Runbook

## 1. Production Architecture
```text
[HackPDM Desktop Clients (HTTPS / JSON-RPC)]
                 │  443/tcp (TLS 1.2 / 1.3)
                 ▼
   [Caddy or nginx Reverse Proxy]
   - Blocks /web/database/* (403)
   - Rate-limits /web/login
   - 160 MB max body, 1200s timeout
                 │  127.0.0.1:8069 (loopback / internal bridge)
                 ▼
        [Odoo 16.0 (hackpdm)]
        - workers >= 5, max_cron_threads = 1
        - without_demo = all, list_db = False
                 │
       ┌─────────┴──────────┐
       ▼                    ▼
[PostgreSQL 15]    [Odoo Filestore]
(metadata, ACLs)   (CAD binaries, previews)
```

> **Critical Rule:** Metadata lives in PostgreSQL while binary CAD payloads and preview images live in `data_dir/filestore/<db>`. Always back up and restore both together.

---

## 2. User Onboarding & RBAC
HackPDM defines three cumulative roles under **Settings -> Users -> HackPDM**:
- **Viewer (`group_pdm_viewer`)**: Read-only access to folders, entries, history, properties, and dependency graphs.
- **Engineer (`group_pdm_user`)**: Viewer + check-out, commit, undo check-out, logical delete/restore, and preview image replacement (for own versions or checked-out entries).
- **Administrator (`group_pdm_admin`)**: Full access, release review approvals (`hp.release.review`), permanent delete, file-type/property management, and `hp.settings` updates.

**Recommended authentication:** Have each user generate a per-workstation **Odoo API Key** (*Preferences -> Account Security -> New API Key*) rather than storing their primary password in HackPDM.

---

## 3. Managing Allowed File Types & Custom Properties
- **File Types (`hp.type`):** Register allowed extensions (e.g., `sldprt`, `sldasm`, `slddrw`, `step`, `pdf`) and map each to a category (`hp.category`). Controlled by `hp.settings.restrict_types` (`True` by default).
- **Custom Properties (`hp.property`):** Register tracked SolidWorks custom property names (e.g., `PartNum`, `Description`, `Material`). Controlled by `hp.settings.restrict_properties` (`True` by default).
- **Name Filters (`hp.entry.name.filter`):** Block temporary/lock files (e.g., `^~\$` for SolidWorks `~$*.SLD*` lock files).

---

## 4. Publishing a New HackPDM Client Release
1. Tag and push the release (`git tag v1.2.0 && git push origin v1.2.0`).
   - `.github/workflows/windows-release.yml` builds, signs (Authenticode), verifies (`signtool verify /pa`), packages `HackPDM-1.2.0-win-x64-setup.exe`, and publishes `.sha256` files.
   - `.github/workflows/desktop-release.yml` publishes Linux/macOS archives and `.sha256` files.
2. Update Odoo's `hp.settings` (`client_version`, `client_download_url`, `client_download_sha256`) using the helper script:
   ```bash
   ODOO_URL=https://pdm.example.com ODOO_DB=hackpdm_prod ODOO_USER=admin ODOO_PASSWORD=... \
     Odoo/deploy/publish_client_release.py ">=1.2.0, <2.0.0" \
     "https://github.com/<org>/<repo>/releases/download/v1.2.0/HackPDM-1.2.0-win-x64-setup.exe" \
     "<64-char-lowercase-sha256>"
   ```
   - `client_version` accepts exact versions (`1.2.0`), lower/upper bounds (`>=1.2.0`, `<=1.3.0`), combined ranges (`>=1.2.0, <2.0.0`), or hyphen ranges (`1.2.0 - 1.3.0`). Clients outside the configured range are denied access until updated.

---

## 5. Backups, Disaster Recovery & Monitoring
- **Daily Backup:** Schedule `Odoo/deploy/backup.sh` and copy `BACKUP_ROOT` off-host.
- **DR Restore Drill:** Run `Odoo/deploy/restore.sh <backup_dir> <staging_db>` on an isolated host, connect HackPDM, and verify `Get Latest` downloads files with matching SHA-1 checksums.
- **Health & TLS Alerts:** Schedule `HACKPDM_HOST=pdm.example.com Odoo/deploy/healthcheck.sh` to verify `/web/health`, HTTPS redirect, `/web/database/manager` block, TLS cert expiry (< 14 days), and disk thresholds (75% warn / 85% critical).
- **Upload Garbage Collection:** Confirm `max_cron_threads = 1` is active so `ir_cron_hp_upload_gc` reaps abandoned chunked upload spools daily.

---

## 6. Log Analysis & Rollback
- **Client Logs:** Ask users to click **Logs Folder** or **Copy Diagnostics** in HackPDM's Status Dialog (`%LOCALAPPDATA%\HackPDM\logs\hackpdm-YYYYMMDD.log` and `crash-*.log`). All secrets and base64 payloads are automatically redacted.
- **Server Logs:** `/var/log/odoo/odoo.log` (rotated daily via `/etc/logrotate.d/odoo`) or `docker logs hackpdm-prod-odoo`.
- **Client Rollback:** Revert `hp.settings.client_version` to include the previous version (e.g., `>=1.1.0, <2.0.0`) and point `client_download_url`/`client_download_sha256` to the prior installer.
