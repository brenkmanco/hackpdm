<p align="center">
  <img src="./Src/HackPDM/Assets/Square150x150Logo.scale-200.png" alt="HackPDM" width="96" />
</p>

<h1 align="center">HackPDM</h1>

<p align="center">
  <strong>Open-source, local-first Product Data Management (PDM) for CAD &amp; Odoo.</strong>
</p>

---

## Why HackPDM?

- **Focused PDM, not bloated PLM:** Fast check-in, check-out, versioning, and assembly dependency tracking without enterprise overhead.
- **Self-hosted on Linux + Odoo:** Runs on your own Odoo 16 + PostgreSQL server with full control over security, backups, and customization.
- **Cross-platform .NET 10 desktop client:** Native WinUI 3 on Windows and Uno Platform Skia Desktop on Windows, Linux, and macOS.
- **No per-seat licensing:** Licensed under GNU GPL-3.0.

## Key Features

- **Vault File Manager:** Tree navigation, grouped/flat views, dockable panes, customizable themes, real-time local file watcher, and status color coding.
- **SolidWorks & CAD Dependency Tracking:** Scans SolidWorks references via `SWDocumentMgr`, stages atomic commits, and resolves recursive dependencies on **Get Latest** and **Check Out**.
- **Interactive Dependency Graph:** Inspect **Where Used** (parents) and **Dependents** (children) visually before modifying shared parts.
- **Previews & Custom Thumbnails:** Inline image previews, detachable Picture-in-Picture (PiP) preview window, and custom preview uploads (PNG/JPEG/BMP/GIF/WebP).
- **Verified Automatic Updates:** Server-enforced compatibility ranges (`client_version`), soft/recommended update prompts (`client_latest_version`), SHA-256 verification before launching any installer, and on-demand checks in **Settings -> About & Updates**.
- **Production Operations Ready:** HTTPS JSON-RPC client, Odoo API key authentication, RBAC roles (Viewer / Engineer / Admin), redacted diagnostics, and crash dumps.

---

## Getting Started

### 1. Install the Client (End Users)

Download the latest release from [GitHub Releases](https://github.com/moonxbears/wyrmpdm/releases):

- **Windows:** Run `HackPDM-<version>-win-x64-setup.exe` (installs per-user under `%LOCALAPPDATA%\Programs\HackPDM`; no admin rights required).
- **Linux (`x64`):** Extract `HackPDM-<version>-linux-x64.tar.gz` and run `./install-desktop-entry.sh` to add HackPDM to your application menu.
- **macOS (`arm64` / `x64`):** Extract `HackPDM-<version>-osx-<arch>.tar.gz` and launch `HackPDM`.

See the [Engineer Quick-Start Guide](docs/ENGINEER_QUICKSTART.md) for first-time setup and daily CAD workflows.

### 2. Deploy the Odoo Backend

Mount and install `Odoo/addons/hackpdm` on Odoo 16.0 behind TLS (Caddy or nginx):

```bash
cd Odoo/deploy
cp .env.example .env
# Edit .env with your domain, DB name, and strong PostgreSQL password
docker compose -f docker-compose.prod.yml up -d --build
```

See [Odoo/deploy/README.md](Odoo/deploy/README.md) and the [Administrator Runbook](docs/ADMIN_RUNBOOK.md) for hardening, RBAC, and backups.

### 3. Build from Source (Developers)

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (pinned in `global.json`).

```bash
# Cross-platform Uno Skia Desktop (Windows / Linux / macOS)
dotnet build Src/HackPDM/HackPDM.csproj -f net10.0-desktop

# Native Windows WinUI 3 (Windows only)
dotnet build Src/HackPDM/HackPDM.csproj -f net10.0-windows10.0.26100.0 -p:Platform=x64

# Run unit tests
dotnet test Src/HackPDM.Tests/HackPDM.Tests.csproj
```

---

## Client Status Codes

| Icon | Code | Meaning |
| :---: | :---: | :--- |
| <img src="./Src/HackPDM/Assets/StatusIcons/cm.png" width="20" alt="cm" /> | **cm** | Checked out to **Me** (writable; ready to edit and commit) |
| <img src="./Src/HackPDM/Assets/StatusIcons/co.png" width="20" alt="co" /> | **co** | Checked out to **Other** (read-only until released) |
| <img src="./Src/HackPDM/Assets/StatusIcons/lm.png" width="20" alt="lm" /> | **lm** | **Local Modification** (differs from server; check out before committing) |
| <img src="./Src/HackPDM/Assets/StatusIcons/nv.png" width="20" alt="nv" /> | **nv** | **Newer Remote Version** available (run **Get Latest**) |
| <img src="./Src/HackPDM/Assets/StatusIcons/lo.png" width="20" alt="lo" /> | **lo** | **Local Only** (not yet committed to the vault) |
| <img src="./Src/HackPDM/Assets/StatusIcons/ro.png" width="20" alt="ro" /> | **ro** | **Remote Only** (exists in vault; not downloaded locally) |
| <img src="./Src/HackPDM/Assets/StatusIcons/if.png" width="20" alt="if" /> | **if** | **Ignore Filter** matched (excluded from commits) |
| <img src="./Src/HackPDM/Assets/StatusIcons/ft.png" width="20" alt="ft" /> | **ft** | **No Remote File Type** registered on the server |
| <img src="./Src/HackPDM/Assets/StatusIcons/dt.png" width="20" alt="dt" /> | **dt** | **Deleted** logically on the server (can be restored) |
| <img src="./Src/HackPDM/Assets/StatusIcons/ds.png" width="20" alt="ds" /> | **ds** | **Destroyed** permanently |

---

## Automatic Releases & Update Rollouts

Pushing a version tag (`vX.Y.Z`) builds and packages Windows, Linux, and macOS artifacts with SHA-256 checksums, publishes them to GitHub Releases, and (when Odoo secrets are configured) automatically updates Odoo's `hp.settings` so connected clients receive verified update prompts.

See the [Release & Update Rollout Guide](docs/RELEASING.md) for full details.

---

## Documentation

| Document | Audience | Contents |
| :--- | :--- | :--- |
| [docs/ENGINEER_QUICKSTART.md](docs/ENGINEER_QUICKSTART.md) | Engineers | Installation, first-time setup, daily SolidWorks workflow, updates, diagnostics |
| [docs/ADMIN_RUNBOOK.md](docs/ADMIN_RUNBOOK.md) | Administrators | Production architecture, RBAC, file types/properties, backups, rollback |
| [docs/RELEASING.md](docs/RELEASING.md) | Maintainers & Admins | Tagging, code signing, automatic Odoo rollout, manual rollout/rollback |
| [Odoo/deploy/README.md](Odoo/deploy/README.md) | DevOps / Admins | Docker Compose & bare-metal Odoo deployment, TLS, security hardening |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Developers | Layer map, commit/download/checkout flows, where to change things |
| [API.md](API.md) | Developers | Core C# types, Odoo models, and extension points |

## License

HackPDM is licensed under the [GNU General Public License v3.0](LICENSE).
