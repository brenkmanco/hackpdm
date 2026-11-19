# HackPDM Engineer Quick-Start Guide

## 1. Installation (Windows)
1. Download `HackPDM-<version>-win-x64-setup.exe` from your administrator's release link.
2. Run the installer — it installs per-user under `%LOCALAPPDATA%\Programs\HackPDM` (no local Administrator rights required) and preserves existing settings and profiles on upgrades.

## 2. First-Time Setup
1. **Configure Working Folder (PWA):**
   - Open **Settings -> Hack Settings** and select your local vault working folder (e.g., `C:\PDM\Vault`) and temporary preview/download folder.
2. **Create an Odoo API Key (Recommended):**
   - Log into the Odoo web portal -> click your profile menu -> **Preferences -> Account Security -> New API Key**.
   - Copy the generated key (treat it like a password).
3. **Connect Your Profile:**
   - Open **Profile Manager** -> **Odoo Settings**.
   - Enter the server hostname (e.g., `pdm.example.com`), port `443`, database name, your username, and your Odoo API key. Keep **Use HTTPS** enabled.
   - Click **Test Connection**, then **Login**.

---

## 3. Daily SolidWorks Workflow
1. **Get Latest:** Right-click a folder or assembly -> **Get Latest** to download the latest versions and all referenced parts. Downloaded files are marked read-only until checked out.
2. **Check Out:** Right-click the file(s) you plan to edit -> **Check Out**. This locks the file to your account and makes your local copy writable so SolidWorks can save changes.
3. **Edit in SolidWorks:** Open and save your changes normally.
4. **Commit:** Right-click the file or folder -> **Commit**. HackPDM automatically scans SolidWorks references, uploads new versions, links assembly dependencies, extracts custom properties and previews, and releases your checkout (marking local copies read-only again).
5. **Undo Check Out:** Releases your lock on the server without committing a new version; local edits are left intact on disk (marked read-only) so work is never silently lost.
6. **Dependency Graph:** Open **Dependency Graph** to inspect **Where Used** (parents) and **Dependents** (children) visually before modifying shared parts.
7. **Custom Preview Image:** For any committed file (such as STEP, PDF, or parts needing a cleaner thumbnail), click the **Upload** icon in the Preview pane (or right-click -> **Upload Preview Image**). Allowed for PDM Admins, the current checkout holder, or the version creator (PNG/JPEG/BMP/GIF/WebP, up to 4 MiB).

---

## 4. File Status Legend
| Code | Meaning |
| :--- | :--- |
| **Ok** | Local copy matches the latest server version. |
| **Cm** | Checked out to **Me** (writable; ready to edit and commit). |
| **Co** | Checked out to **Someone Else** (read-only until released). |
| **Lm** | **Locally Modified** (differs from server; check out before committing). |
| **Nv** | **Newer Version** exists on the server (run **Get Latest**). |
| **Lo** | **Local Only** (not yet committed to the vault). |
| **Ro** | **Remote Only** (exists in vault; not downloaded locally yet). |
| **Dt** | **Deleted** logically on the server (can be restored). |

---

## 5. Reporting Issues & Exporting Diagnostics
If you encounter an error:
1. Open the **Status Dialog** (or click the warning/error pill in minimal mode).
2. Click **Copy Diagnostics** to copy a redacted summary (client version, OS, server URL/DB, error count) to your clipboard.
3. Click **Logs Folder** to open `%LOCALAPPDATA%\HackPDM\logs` and attach the latest `hackpdm-YYYYMMDD.log` (or `crash-*.log`) to your support ticket. Passwords, API keys, and file contents are automatically redacted from all logs.
