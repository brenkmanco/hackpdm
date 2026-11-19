# HackPDM Production Readiness Roadmap (Unabridged)

This document defines the complete, unabridged engineering, error-handling, logging, infrastructure, security, packaging, validation, and release-review plan required to transition **HackPDM** (the cross-platform Uno Platform client in `Src/` and the Odoo 16.0 `hackpdm` addon in `Odoo/addons/hackpdm/`) into a hardened, supportable production system.

---

## Baseline Assessment (Current State at `main` / `efe65b0`)

### Already Implemented & Verified
- **Core PDM Lifecycle:** Commit staging (`hp.pdm.commit`, `hp.record.staged`), atomic check-in/check-out, undo check-out, logical delete/restore, permanent delete (admin-only), directory tree operations, and file-system watcher live updates (`PdmWorkflowTests`, `SolidWorksLifecycleTests`, `FolderActionsTests`).
- **Large-File Chunked Transfers:** Resumable chunked uploads (`hp.upload` — 8 MiB chunk cap, 2 GiB max file size, SHA-1 verification, filestore spooling, daily GC cron `ir_cron_hp_upload_gc`) and chunked downloads (`read_contents`).
- **CAD Parsing Without SolidWorks Installed:** Cross-platform `SwFileReader` supporting both legacy OLE (SolidWorks <= 2014) and modern chunked containers (SolidWorks 2015–2026) for references, custom/evaluated properties, configuration names, and PNG preview extraction.
- **Dependency Graph & Theming UI:** `DependencyGraphView` (Children/Parents/Both layout, zoom/pan, details drawer, Where Used / Dependents / History / Properties) and `ThemeService` / `ThemeEditorView` presets (`Dark`, `NavyBlue`, `Light` × `Default`, `Fancy`, `Modern`, `Compact`).
- **Security Foundations:** Three-tier Odoo RBAC (`group_pdm_viewer`, `group_pdm_user`, `group_pdm_admin`), row-level record rules on `hp.record.staged` and `hp.upload`, server-side attack safeguards (`test_attacks.py`, `AttackTests.cs`), client-side HTTPS enforcement (`OdooEndpoint.cs`), OS keychain credential storage (`CrossPlatformCredentialManager.cs`), and SHA-256 verified auto-update (`HackUpdater.cs`).
- **CI/CD Skeleton:** Live Odoo 16.0 integration + multi-theme desktop smoke test (`.github/workflows/odoo-integration.yml`) and Windows self-contained Release + Inno Setup installer pipeline (`.github/workflows/windows-release.yml`).

### Remaining Gaps to Production
Every remaining gap across code correctness, error handling & logging, server infrastructure, packaging/signing, real-world validation, and final audit is mapped across the **6 Milestones** below.

---

## Milestone 1: Critical Code Fixes, Dependency Stabilization & Repo Hygiene

**Objective:** Eliminate all known functional bugs, remove development/test artifacts from production builds and database installs, lock down unstable compiler/package settings, and resolve incomplete user-facing workflows before hardening or deploying to any shared environment.

### 1.1 Fix Broken Release Review Workflow (`Odoo/addons/hackpdm/models/hp_release_review.py`)
1. **Fix missing translation helper import (lines 1, 85, 92):**
   - `reviewer_decision()` raises `PermissionError(_(...))` and `ValueError(_(...))` without importing `_` from `odoo`, causing an unhandled `NameError` whenever an unauthorized user or invalid parameter triggers those branches.
   - Update line 1 to `from odoo import _, api, Command, fields, models` and raise `AccessError` / `ValidationError` from `odoo.exceptions` for clean JSON-RPC fault propagation.
2. **Persist review decisions (lines 94–113):**
   - Line 112 currently executes `writing = Command.update(review.id, review_update)` and discards the return value; the `hp.release.review` record is never modified in PostgreSQL even after creating `hp.release`.
   - Replace with `review.write(review_update)` inside the same transaction and return the created `release_id` (or updated review dictionary) so clients can confirm state immediately.
3. **Guard empty search & clean up duplicate inheritance (lines 6–7, 87–91):**
   - Remove the duplicate `_inherit = 'hp.common.model'` on line 7.
   - Replace `search([('id', '=', review_id)], limit=1)[0]` (which throws `IndexError` on invalid IDs) with `review = self.browse(review_id).exists()` followed by an explicit `UserError`/`ValidationError`.
   - Prevent double-approving an already-reviewed `hp.release.review` record (`if review.reviewed: raise UserError(...)`).
4. **Add server & client test coverage:**
   - Add unit/transaction tests in `Odoo/addons/hackpdm/tests/test_functions.py` verifying: (a) admin approval writes `reviewed=True`, `accepted=True`, `release_id`, `release_reviewed`, and clears `review_deadline`; (b) admin rejection sets `reviewed=True`, `accepted=False` without creating `hp.release`; (c) Engineer/Viewer callers receive an access error; (d) non-existent or already-reviewed IDs fail cleanly.

### 1.2 Remove Test Models & Dummy Records from Production Addon Installs
1. **Isolate `hp.test` and its seed records:**
   - `Odoo/addons/hackpdm/data/hp_settings_data.xml` (lines 16–65) unconditionally inserts `hp_test_data_one` and `hp_test_data_two` into every production database, and `Odoo/addons/hackpdm/models/hp_test.py` + `access_hp_test_system` in `ir.model.access.csv` (line 44) create a live `hp_test` table in production.
   - Move `hp.test` model definitions, ACLs, and XML seed data either into a separate `hackpdm_test` addon loaded only in CI/dev, or move the test records from `data/` into `demo/` so `--without-demo=all` excludes them from production databases.
   - Update `Src/HackPDM.Tests/Integration/AddonSeedDataTests.cs` to match the separated test fixture layout.
2. **Audit `hp.user` (`Odoo/addons/hackpdm/models/hp_user.py`):**
   - Verify whether `hp.user` (which defines defaults `login_name='admin'`, `first_name='Admin'`, `last_name='User'`) is still referenced anywhere or is obsolete dead code superseded by Odoo's `res.users`. Remove or deprecate cleanly with a migration script if unused.
3. **Eliminate Odoo registry startup warnings:**
   - Add `'license': 'LGPL-3'` to `Odoo/addons/hackpdm/__manifest__.py` (fixes `Missing 'license' key in manifest for 'hackpdm'`).
   - Add `_description = 'PDM commit batch'` to `Odoo/addons/hackpdm/models/hp_pdm_commit.py` (line 18) and `_description = 'PDM staged record'` to `Odoo/addons/hackpdm/models/hp_record_staged.py` (line 14).
   - Resolve the `hp.entry: inconsistent 'compute_sudo' for computed fields: windows_complete_name, windows_complete_path` warning in `Odoo/addons/hackpdm/models/hp_entry.py` (lines 37–46) by explicitly aligning `compute_sudo` on both fields.

### 1.3 Stabilize .NET Compiler Language Version & NuGet Dependencies
1. **Pin GA NuGet packages:**
   - Upgrade `Newtonsoft.Json` from `13.0.5-beta1` to the latest stable GA release in both `Src/HackPDM/HackPDM.csproj` (line 58) and `Src/HackPDM.Core/HackPDM.Core.csproj` (line 16).
   - Replace `System.Reactive` `7.0.0-preview.1` in `Src/HackPDM/HackPDM.csproj` (line 60) with the latest stable GA release.
   - Run `dotnet list package --vulnerable --include-transitive` and `dotnet list package --deprecated` across `HackPDM.sln` and resolve any flagged packages.
2. **Remove `<LangVersion>preview</LangVersion>`:**
   - Replace `<LangVersion>preview</LangVersion>` with a fixed, supported C# version (`13` or `14`) in `Directory.Build.props` (line 3) and remove redundant per-project `<LangVersion>preview</LangVersion>` overrides in `HackPDM.Abstractions`, `HackPDM.Core`, `HackPDM.Domain`, `HackPDM.Infrastructure`, `HackPDM.Shared`, and `HackPDM.Tests`.
3. **Verify Release-mode compilation and trimming behavior:**
   - Test `dotnet publish -c Release` for both `net10.0-windows10.0.26100.0` and `net10.0-desktop` because `HackPDM.csproj` enables `<PublishReadyToRun>True</PublishReadyToRun>` and `<PublishTrimmed>True</PublishTrimmed>` in non-Debug builds (lines 249–252). Verify reflection-heavy areas (`Newtonsoft.Json`, `OdooRow` deserialization, Uno XAML bindings) do not break under IL trimming — disable trimming or add trimmer root descriptors if any reflection breakage occurs.

### 1.4 Resolve Unfinished File Manager Features & UX Traps
1. **Recursive Folder Restore:**
   - Implement recursive restore for directory tree actions (noted as unimplemented in `Src/HackPDM.Tests/Integration/FolderActionsTests.cs:218`) by passing `withSubdirectories: true, withDeleted: true` to `PdmWorkflowService.GetDirectoryEntryIdsAsync` and calling restore on all matching deleted entries; update `FolderActionsTests` to assert recursive restore works.
2. **Icon Upload (`Src/HackPDM/Mvvm/ViewModels/Hack/HackFileManagerViewModel.cs:840–846`):**
   - Either implement `UploadIconAsync()` end-to-end (file picker -> PNG/SVG validation -> upload to `hp.type` / `hp.entry`) or remove/hide the menu item in Release builds so users never encounter a `"not implemented yet"` dead-end dialog.
3. **Permanent Delete Policy (`HackFileManagerViewModel.cs:818–836`):**
   - Decide whether permanent delete should remain compiled out in Release builds (`#if DEBUG`) or be gated dynamically by checking whether the logged-in user belongs to `hackpdm.group_pdm_admin` (matching server-side enforcement in `ir.model.access.csv`).

### 1.5 Repository & Configuration Hygiene
1. **Untrack `Odoo/config/odoo.conf`:**
   - Remove `Odoo/config/odoo.conf` (contains `db_password = odoo` and `/home/moony/...`) from Git tracking (`git rm --cached Odoo/config/odoo.conf`), add `/Odoo/config/odoo.conf` to `.gitignore`, and keep `Odoo/config/odoo.conf.example` as the sole tracked template.
2. **Clean up non-production root artifacts:**
   - Move generated documentation/analysis dumps at the repo root (`ArchitectureSkeleton.txt`, `CLASS_GRAPH.html`, `class-dependency-graph.dot`, `crawler.py`) into `docs/` or `tools/` if still needed.

### Milestone 1 Verification & Exit Criteria
- `dotnet build HackPDM.sln -c Release` succeeds with **0 errors**.
- `odoo-bin -d <test_db> -i hackpdm --without-demo=all --test-enable --test-tags /hackpdm --stop-after-init` passes with **0 failures** and **0 unexpected registry warnings**.
- `dotnet test Src/HackPDM.Tests/HackPDM.Tests.csproj -c Release` passes 100% (unit + live Odoo integration tests).
- No hardcoded passwords, developer paths, or test models exist in production addon data or tracked configs.

---

## Milestone 2: Error Checking Robustness, Fault Tolerance & Structured Logging

**Objective:** Replace silent failures, swallowed exceptions, and invisible `Debug.WriteLine` / `Console.WriteLine` diagnostics (which vanish completely in Windows `WinExe` Release builds) with a unified, structured, redacted logging pipeline and strict, deterministic error propagation across both the C# client and the Odoo server.

### 2.1 Unified Client Logging Architecture (`Src/HackPDM.Core/` & `Src/HackPDM/`)
1. **Introduce a First-Class Structured Logger (`ILogger` / `HackLogger`):**
   - Today, the codebase contains **51 scattered `Debug.WriteLine`, `Console.WriteLine`, and `Console.Error.WriteLine` calls** across `Src/` (`CommitStaging.cs`, `PdmCommitOperations.cs`, `LocalFileWriter.cs`, `DirectoryTreeService.cs`, `EntryListService.cs`, `EntrySearchService.cs`, `LocalFileWatcher.cs`, `UiDispatcher.cs`, `HackFileManagerViewModel.cs`, `JsonFileSettingsProvider.cs`, `CrossPlatformCredentialManager.cs`, `Checksums.cs`, `ShellLauncher.cs`, `HackResultTree.cs`). In Windows Release builds (`<OutputType>WinExe</OutputType>`), `Debug.WriteLine` is stripped at compile time and `Console.*` writes to nowhere.
   - Standardize on `Microsoft.Extensions.Logging` (with a lightweight thread-safe rolling file provider — or Serilog/NLog — configured via DI in `AppComposition`) writing structured timestamped entries (`[UTC ISO-8601] [LEVEL] [Category] [CorrelationId] Message + Exception`) to `AppDataPaths.Data/logs/hackpdm-YYYYMMDD.log`.
   - Enforce bounded log retention (e.g., 10 MB per file, max 10 rotated files, automatic pruning on startup) so logs never fill engineering workstations.
   - Support configurable verbosity (`Information` default in Release, `Debug`/`Trace` toggleable in Settings or via `HACKPDM_LOG_LEVEL` for troubleshooting without rebuilding).
2. **Strict Secret & Payload Redaction:**
   - Guarantee that **no logger path ever emits**:
     - Odoo passwords, API keys, session cookies, or `credentials.dat` payloads
     - Raw base64 file payloads from JSON-RPC requests/responses (log byte sizes, SHA-1 hashes, and chunk offsets instead)
   - Add automated unit tests verifying secret redaction on RPC error and credential-fallback log lines.
3. **Production Global Crash Handlers (`Src/HackPDM/App.xaml.cs`):**
   - Currently, unhandled exception hooks exist only inside `SmokeTestRunner.cs` (lines 82–94), not in normal production startup (`App.xaml.cs`).
   - Register global handlers in `App.xaml.cs` for:
     - `Application.Current.UnhandledException`
     - `AppDomain.CurrentDomain.UnhandledException`
     - `TaskScheduler.UnobservedTaskException`
   - Flush fatal crash dumps (stack trace, app version `ClientUpdate.CurrentText`, OS/runtime info, active page, last RPC action) synchronously to `AppDataPaths.Data/logs/crash-YYYYMMDD-HHmmss.log` and present a clear recovery dialog with the log file path before exit.
4. **Supportability UI:**
   - Add an **"Open Logs Folder"** and **"Copy Diagnostic Summary"** action in `ApplicationSettingsView` and `StatusDialogView` (showing client version, OS, credential store backend, Odoo URL/DB, and recent error count) so engineers can attach complete diagnostics to bug reports in one click.

### 2.2 Eliminate Swallowed Exceptions & Tighten Client Error Checking
1. **Audit & Remediate Every Bare / Swallowed `catch` Block in `Src/`:**
   - Replace silent swallows with typed exception handling, structured logging, and actionable user feedback or `Result<T>` propagation across all identified sites:
     - `Src/HackPDM/Mvvm/ViewModels/Settings/ProfileManagerViewModel.cs:170` — bare `catch` in `AbleToLoginAsync()` silently returns `false` with zero error message in `OStatus`; log the full exception and surface a human-readable error message to `OStatus`.
     - `Src/HackPDM.Core/Hack/HackFile.cs:248, 270` — `catch { }` swallows file metadata/attribute failures; catch specific `IOException`/`UnauthorizedAccessException`, log at `Warning`, and surface lock/permission state.
     - `Src/HackPDM.Core/LocalFileWriter.cs:51, 121, 165` — currently prints to `Console.WriteLine` or bare `catch` on disk write/attribute errors; return structured `Result` errors distinguishing disk full, file locked by SolidWorks (`ERROR_SHARING_VIOLATION`), and access denied.
     - `Src/HackPDM.Infrastructure/Odoo/VersionFileService.cs:137` and `Src/HackPDM.Infrastructure/Operations/ContentTransfer.cs:211` — ensure temporary file cleanup errors are logged at `Debug`/`Warning` while primary transfer errors are never masked.
     - `Src/HackPDM.Infrastructure/Operations/CommitStaging.cs:180–315` and `PdmCommitOperations.cs:225` — replace `Debug.WriteLine` on failed dependency/property staging with structured logging and explicit commit-failure reporting so a commit never silently drops relationships or custom properties.
     - `Src/HackPDM/Mvvm/ViewModels/Hack/HackSettingsViewModel.cs:80`, `ThemeService.cs:130`, `WindowExtensions.cs:24`, `MessageBox.xaml.cs:123`, `ExtensionMethods.cs:152` — log non-fatal UI/config exceptions with context instead of silently swallowing.
2. **Harden Network, RPC & File-System Resilience:**
   - **Timeouts & Cancellation:** Enforce explicit per-request `HttpClient` / `CancellationToken` timeouts on all JSON-RPC calls (metadata queries vs. 8 MiB chunk transfers) and support user cancellation of long-running downloads/commits without leaving half-written files in the working folder.
   - **Transient Retry with Backoff (Idempotent Calls Only):** Add bounded exponential backoff retry (e.g., 3 attempts on `HttpRequestException` / HTTP 502/503/504 / transient socket reset) for read-only queries, `read_contents`, and idempotent `upload_chunk` calls (which `hp_upload.py` already handles safely via exact-offset checking), while **never** blindly retrying non-idempotent state mutations without verifying server state.
   - **Atomic Local File Writes:** Ensure `LocalFileWriter` / `ContentTransfer` always downloads to a sibling temporary file, verifies SHA-1 checksum against the server's `hp.version.checksum`, and atomically replaces the target file — preventing corrupt half-downloaded CAD files if power or network drops mid-transfer.
   - **File Watcher Recovery (`LocalFileWatcher.cs`):** On `FileSystemWatcher.Error` (`InternalBufferOverflowException` during bulk folder operations), log a warning and automatically trigger a debounced directory state resync instead of leaving stale file statuses in the UI.
   - **Input & Boundary Validation:** Validate all user-supplied inputs (Odoo URL/port/DB, working folder path, directory names, file filters, JSON editor payloads) up front with clear inline validation messages before issuing RPC or file-system calls.

### 2.3 Odoo Server-Side Error Checking & Structured Logging (`Odoo/addons/hackpdm/models/`)
1. **Eliminate Bare `except:` Clauses:**
   - Replace bare `except:` blocks in:
     - `Odoo/addons/hackpdm/models/hp_common_model.py:283`
     - `Odoo/addons/hackpdm/models/hp_entry.py:225`
     - `Odoo/addons/hackpdm/models/hp_version.py:609`
   - Catch only specific expected exception types (`ValueError`, `OSError`, `binascii.Error`, etc.) so `SystemExit`, `KeyboardInterrupt`, PostgreSQL `OperationalError`/`SerializationFailure`, and Odoo `AccessError`/`UserError` are never masked.
2. **Standardize Exception Types & Error Contracts:**
   - Audit all broad `except Exception as e:` handlers (`hp_common_model.py:256, 312`, `hp_directory.py:342, 379`, `hp_pdm_commit.py:180`, `hp_version.py:323, 347, 381, 491, 657`) to ensure:
     - Business rule violations raise `odoo.exceptions.UserError` or `ValidationError` with clear, user-safe messages.
     - Authorization failures always raise `odoo.exceptions.AccessError` (and log a `WARNING` via `hp_guard.py`).
     - Unexpected internal errors log full tracebacks with `_logger.exception(...)` including contextual identifiers (`uid`, `commit_id`, `entry_id`, `version_id`, `upload_id`) before failing cleanly (never returning silent empty lists or partial corruption when a DB query fails).
3. **Structured Audit & Operational Logging on Server:**
   - Ensure key PDM lifecycle events emit concise, grep-friendly `INFO` logs (with `uid`, `node`, `entry_id`, `version_id`, `commit_id`, and elapsed time):
     - Commit finalized (`hp_pdm_commit.py`) and commit failures (`errored=True`, `message_exception`)
     - Check-out, undo check-out, logical delete, restore, permanent delete (`hp_entry.py`, `hp_version.py`)
     - Chunked upload completion, checksum mismatch reset, and stale upload GC counts (`hp_upload.py`)
     - Release review submission and admin approve/reject decisions (`hp_release_review.py`)
   - Remove commented-out `#print(...)` debug leftovers in `hp_directory.py` (lines 265, 292, 341).

### Milestone 2 Verification & Exit Criteria
- **Zero** `Debug.WriteLine` or `Console.WriteLine` calls remain in production client code (`Src/` excluding `HackPDM.Tests` and `SmokeTestRunner`), replaced by the structured `ILogger` pipeline.
- **Zero** bare `except:` clauses remain in `Odoo/addons/hackpdm/models/`, and **zero** silent swallowed `catch { }` blocks remain in `Src/` without typed justification and logging.
- Fault-injection tests pass:
  - Simulated mid-download connection drop leaves no corrupted local file and logs a clear retry/error record.
  - Simulated locked file (`FileShare.None`) during `Get Latest` / `Commit` produces an explicit, actionable user error and structured log entry without crashing.
  - Simulated Odoo 500/timeout during login or directory load surfaces a clear UI error (no silent empty state) and logs the full sanitized exception.
  - Log redaction unit tests confirm passwords, API keys, and base64 file bodies never appear in log files.
- Unhandled exception handler writes a crash log cleanly when tested in a Release build.

---

## Milestone 3: Production Server Infrastructure, Security & Reliability

**Objective:** Deploy a hardened, observable, backed-up Odoo 16.0 + PostgreSQL 15 server stack behind TLS that satisfies all client security invariants and survives hardware failure, large concurrent CAD commits, and hostile network traffic.

### 3.1 TLS Reverse Proxy Architecture (Mandatory)
- Because Odoo JSON-RPC transmits credentials on every request, `Src/HackPDM.Infrastructure/Odoo/OdooEndpoint.cs` refuses plain HTTP to any non-loopback host by default.
- Deploy **nginx** or **Caddy** in front of Odoo bound strictly to `127.0.0.1:8069` (and `127.0.0.1:8072` for gevent/longpolling if the web UI is used):
  - **TLS Configuration:** Enforce TLS 1.2+ / TLS 1.3 with valid CA-signed certificates (Let's Encrypt or internal enterprise PKI trusted by all client OS trust stores). Redirect all port 80 HTTP traffic to HTTPS (301) and enable HSTS.
  - **Upload & Timeout Tuning:**
    - `client_max_body_size 160m;` (or `256m` — accommodates Odoo 16's 128 MB JSON-RPC cap plus base64 overhead; chunked `hp.upload` sends 8 MiB raw ≈ 10.7 MiB base64 per chunk).
    - `proxy_connect_timeout 60s;`, `proxy_send_timeout 1200s;`, `proxy_read_timeout 1200s;` (aligned with `limit_time_real = 1200` in `odoo.conf.example`).
    - `proxy_request_buffering on;` to protect Odoo workers from slow-client DoS attacks.
  - **Header Forwarding:** Set `X-Forwarded-Host`, `X-Forwarded-For`, `X-Forwarded-Proto https`, and `X-Real-IP` with `proxy_mode = True` in `odoo.conf`.
  - **Endpoint Hardening & Rate Limiting:**
    - Block `/web/database/*` at the proxy layer in addition to `list_db = False`.
    - Apply rate limiting (`limit_req_zone`) on `/web/login` and authentication calls to slow credential stuffing while allowing bursty `/jsonrpc` traffic for legitimate CAD commits/downloads.

### 3.2 Production Odoo & PostgreSQL Provisioning
1. **Production `odoo.conf` (derived from `Odoo/config/odoo.conf.example`):**
   - `http_interface = 127.0.0.1`, `http_port = 8069`, `proxy_mode = True`
   - `list_db = False`, `db_name = <prod_db>`, `dbfilter = ^<prod_db>$`, `without_demo = all`
   - `admin_passwd = <hashed PBKDF2 strong random secret>`
   - `db_user = odoo` (PostgreSQL role with **no** `SUPERUSER`, **no** `CREATEROLE`; revoke `CREATEDB` once the production DB is initialized)
   - `workers = <2 * CPU_cores + 1>` (minimum `5` so large file transfers never starve interactive directory browsing)
   - `max_cron_threads = 1` (ensures `ir_cron_hp_upload_gc` runs daily to clean abandoned upload spools)
   - `limit_request = 8192`, `limit_time_cpu = 600`, `limit_time_real = 1200`, `limit_memory_soft = 2147483648`, `limit_memory_hard = 2684354560`
   - Permissions: owned by dedicated non-root `odoo` service user, `chmod 600 /etc/odoo/odoo.conf`.
2. **PostgreSQL 15 Production Tuning:**
   - Configure `shared_buffers`, `effective_cache_size`, `work_mem`, and `max_connections` (sized for `workers * db_maxconn + overhead`).
   - Restrict `pg_hba.conf` to local socket / loopback with `scram-sha-256` authentication.
   - Verify autovacuum is active on high-churn tables (`hp_record_staged`, `hp_pdm_commit`, `hp_upload`, `ir_attachment`).
3. **Fix Deployment Automation (`Odoo/setup.sh` and/or Production Docker Compose):**
   - Fix `Odoo/setup.sh` so it: (a) pins compatible Python dependency versions (`numpy<2.1`, `sqlalchemy>=2,<3`, `setuptools<80`); (b) writes `workers`, `max_cron_threads`, and resource limits into `odoo.conf`; (c) runs under a dedicated `odoo` system user rather than `$USER`; (d) runs `systemctl enable --now odoo` instead of `systemctl disable odoo` (line 98).
   - Optionally provide a production `docker-compose.prod.yml` (PostgreSQL 15 + Odoo 16.0 pinned digest + Caddy/nginx + named volumes) for reproducible deployments.

### 3.3 Storage Architecture, Backups & Disaster Recovery (DR)
- **Critical Data Consistency Rule:** Metadata, relationships, properties, and commit history live in **PostgreSQL**, while binary CAD payloads and preview images live as `ir.attachment` files in **Odoo's filestore (`data_dir/filestore/<db>`)**. Both must always be backed up together.
- **Storage Requirements:** Host `data_dir` and PostgreSQL data on redundant storage (RAID-1/10, ZFS, or replicated cloud block storage) sized for immutable CAD version history growth.
- **Automated Backup Pipeline:** Daily consistent backup script (`pg_dump -Fc` + `rsync`/atomic snapshot of `data_dir/filestore/<db>`), encrypted at rest, replicated off-host with 30-day daily + 12-month monthly retention.
- **Verified Restore Drill:** Restore a backup onto an isolated staging VM, start Odoo against the restored DB + filestore, connect the HackPDM client, and verify `Get Latest` downloads files with matching SHA-1 checksums.

### 3.4 Access Control, Identity & Hardening
- Rotate the default `admin` password immediately and disable unused default Odoo accounts.
- Create named accounts for every team member assigned explicitly to `Viewer`, `Engineer`, or `Administrator` (`Odoo/addons/hackpdm/security/hackpdm_groups.xml`).
- **Least-Privilege Default Review:** `hackpdm_groups.xml` (lines 40–42) currently adds `group_pdm_user` (Engineer) to `base.default_user`. Evaluate changing new-user default to `group_pdm_viewer`.
- **API Keys:** Enable and document Odoo API keys so engineers authenticate HackPDM with revocable per-machine API keys rather than primary passwords.
- **OS Firewall:** Expose only `443/tcp` (and `80/tcp` redirect) plus restricted SSH; block external access to `5432`, `8069`, and `8072`.

### 3.5 Server Monitoring, Alerting & Log Rotation
- Poll `https://<pdm-host>/web/health` every 60s and alert on non-200 responses or TLS certificate expiry (< 14 days).
- Configure disk-space alerts on `data_dir` (> 75% warning / > 85% critical), memory/OOM alerts, and PostgreSQL connection-pool alerts.
- Configure `/etc/logrotate.d/odoo` (`daily`, `rotate 30`, `compress`, `copytruncate`) and monitor logs for `ERROR`/`CRITICAL` and `ir_cron_hp_upload_gc` failures.

### Milestone 3 Verification & Exit Criteria
- `curl -fsS https://<pdm-host>/web/health` returns `{"status": "pass"}` over valid TLS; `http://<pdm-host>` redirects to HTTPS; `/web/database/manager` returns 403/404.
- HackPDM client connects over HTTPS with OS trust store validation (zero TLS warnings, `"Use HTTPS"` enabled).
- Automated backup completes and a full restore drill recovers all entries, versions, and binary attachments with verified SHA-1 integrity.
- `ir_cron_hp_upload_gc` executes cleanly in multi-worker mode and reaps expired spool files.

---

## Milestone 4: Client Packaging, Code Signing & Auto-Update Rollout

**Objective:** Deliver trusted, signed, zero-friction client installations and verified mandatory updates across all supported desktop platforms.

### 4.1 Windows Code Signing & Release Automation
1. **Certificate Procurement & Secret Setup:**
   - Procure an Authenticode code-signing certificate (OV/EV certificate or Azure Trusted Signing) to eliminate Windows SmartScreen "Unknown Publisher" warnings.
   - Configure GitHub Actions repository secrets consumed by `.github/workflows/windows-release.yml`:
     - `WINDOWS_CERT_PFX_BASE64`
     - `WINDOWS_CERT_PASSWORD`
   - *(If using EV hardware HSM or Azure Trusted Signing instead of a `.pfx` file, update the signing steps in `windows-release.yml` lines 93–127 accordingly.)*
2. **Release Artifact Integrity:**
   - Confirm `windows-release.yml` builds self-contained `win-x64` Release binaries (`HackPDM.exe` + `HackPDM*.dll`), signs all assemblies, packages `installer/HackPDM.iss` into `HackPDM-<X.Y.Z>-win-x64-setup.exe`, signs the installer, and publishes both the installer and `HackPDM-<X.Y.Z>-win-x64.zip` alongside `.sha256` checksum files on every `vX.Y.Z` tag.
   - Verify Inno Setup per-user installation (`PrivilegesRequired=lowest`, `{localappdata}\Programs\HackPDM`) upgrades cleanly over an existing copy without requiring local Administrator privileges and preserves user config (`AppConfig.json`, profiles, saved logs, and Windows Credential Manager entries).

### 4.2 End-to-End Auto-Update Rollout Verification (`HackUpdater`)
1. **Server Settings Population (`hp.settings`):**
   - Host the signed installer at an HTTPS URL accessible from all engineering workstations (GitHub Release URL or internal HTTPS file server/SMB share).
   - Set the three `noupdate="1"` keys in Odoo (`Odoo/addons/hackpdm/data/hp_settings_data.xml` lines 71–86):
     - `client_version`: minimum allowed version (e.g., `1.1.0`)
     - `client_download_url`: HTTPS URL or UNC path to `HackPDM-<X.Y.Z>-win-x64-setup.exe`
     - `client_download_sha256`: lowercase hex SHA-256 from the `.sha256` artifact
2. **Update Flow Testing:**
   - Install version `N` on a clean Windows machine, bump `client_version` to `N+1` on staging Odoo, log in, and confirm `HackUpdater.EnsureUpdated(true)` blocks outdated login, downloads the installer, verifies SHA-256, launches the Inno Setup installer, exits cleanly, and relaunches at version `N+1` (with full update lifecycle logged by Milestone 2's logger).
   - Negative test: alter 1 character in `client_download_sha256` and confirm `ClientUpdate.DownloadInstallerAsync` refuses execution, logs the hash mismatch, and warns the user without running an untrusted binary.

### 4.3 Cross-Platform Distribution *(Scope-Dependent)*
- If Linux or macOS workstations are part of the production fleet:
  - Add a GitHub Actions workflow building `net10.0-desktop` in Release mode for `linux-x64` (AppImage or `.deb`) and `osx-arm64`/`osx-x64` (signed/notarized `.app`/`.dmg`), with `.sha256` checksums.
  - Validate OS keychain integration (`libsecret` / GNOME Keyring / KWallet on Linux, Keychain Services on macOS) and ensure Milestone 2's logger + UI notify clearly if credentials fall back to `credentials.dat` (`CrossPlatformCredentialManager.cs`).
  - Document OS-specific update instructions or extend `hp.settings` to support per-OS download URLs/hashes if mixed OS fleets rely on auto-update.

### Milestone 4 Verification & Exit Criteria
- Tagged release (`vX.Y.Z-rc1`) produces signed Windows artifacts on GitHub Releases; `signtool verify /pa` passes on both `HackPDM.exe` and `HackPDM-*-win-x64-setup.exe`.
- End-to-end auto-update upgrades a Windows test machine cleanly, preserves user state, and rejects tampered SHA-256 checksums.
- Installed Release client writes diagnostic logs cleanly to `%LOCALAPPDATA%` with Milestone 2's "Open Logs Folder" UI working as expected.

---

## Milestone 5: Pre-Production Validation, Legacy Migration & Pilot Rollout

**Objective:** Validate HackPDM under authentic engineering workloads on real Windows + SolidWorks workstations, rehearse legacy vault migration (if applicable), and prove stability with a controlled pilot group prior to full cutover.

### 5.1 Real Windows + SolidWorks Workstation Testing
1. **Real CAD Dataset Validation:**
   - Test with actual production SolidWorks assemblies (multi-level subassemblies, configurations, toolbox/library parts, drawings `.SLDDRW`, virtual components containing `^`, suppressed components, and renamed top assemblies).
   - Cross-check dependency extraction, custom property evaluation, and thumbnail previews between `SwFileReader` (cross-platform binary parser) and `SolidWorks.Interop.swdocumentmgr` (Windows COM Document Manager).
2. **Open-File & Windows Locking Semantics:**
   - Verify behavior when a user has a `.SLDASM` / `.SLDPRT` open in SolidWorks while attempting:
     - `Get Latest` (read-only flag updates, open-file sharing violations clearly reported & logged)
     - `Check Out` (clearing read-only flag so SolidWorks can save)
     - `Commit` (ignoring SolidWorks `~$*.SLD*` lock files, reading open files cleanly, restoring read-only status post-commit)
     - `Undo Check Out` (ensuring local unsaved/modified work is preserved rather than silently clobbered, matching `FolderActionsTests` expectations).
3. **Large-File & High-Latency Network Testing:**
   - Execute `PdmWorkflowTests.VeryLargeFile_CommitAndGetLatest` with `HACKPDM_BIG_FILE_MB=250` (and `500`) against the TLS-proxied staging server to exercise `hp.upload` chunked upload, resume-after-interruption, SHA-1 validation, and chunked download (`read_contents`).
   - Test mid-upload network drop / VPN reconnect to verify chunk resume recovers from `received` offset without corrupting the spooled file.

### 5.2 Multi-User Concurrency & Load Testing
- Simulate 5–10 concurrent engineering clients against staging performing simultaneous directory browsing, dependency graph queries (`hp_dependency_graph.py`), checkouts, and commits:
  - Verify database uniqueness constraints (`hp_entry_dir_name_ci_uniq`) prevent duplicate case-insensitive entries under race conditions.
  - Verify two engineers cannot simultaneously check out the same entry (`AttackTests` concurrency guarantees under multi-worker Odoo).
  - Verify Odoo worker memory stays well below `limit_memory_soft` (2 GiB) during concurrent large-file uploads/downloads and no PostgreSQL deadlocks or connection pool exhaustion occur.

### 5.3 Legacy HackPDM Data Migration Dry-Run *(if migrating an existing vault)*
1. **Staging Dry-Run:**
   - Set `hackpdm.legacy_webdav_url`, `hackpdm.legacy_webdav_user`, `hackpdm.legacy_webdav_password`, and `hackpdm.legacy_db_url` via Odoo Technical System Parameters (`Odoo/addons/hackpdm/models/hp_legacy.py`).
   - Run the migration scripts into the staging database and measure total runtime to plan the cutover window.
2. **Data Integrity Reconciliation:**
   - Compare record counts (`hp.directory`, `hp.entry`, `hp.version`, `hp.version.relationship`, `hp.version.property`) between legacy DB and Odoo DB.
   - Sample-verify binary downloads from Odoo against legacy WebDAV SHA-1 hashes and verify migrated SolidWorks preview images render in both `HackFileManagerView` and `DependencyGraphView`.
3. **Post-Migration Cleanup:**
   - Delete all `hackpdm.legacy_*` system parameters and revoke temporary legacy DB/WebDAV migration credentials once cutover is verified.

### 5.4 Controlled Pilot Group & Documentation
1. **1–2 Week Engineering Pilot:**
   - Onboard 2–3 representative engineers onto a staging/pilot vault using the signed Release installer.
   - Review collected Milestone 2 client logs and Odoo server logs weekly; track every UX friction point, error dialog, slow operation, or unexpected status transition (`Lo`, `Ro`, `Ok`, `Cm`, `Lm`, `Nv`, `Dt`) for Milestone 6 remediation.
2. **Operational Documentation:**
   - **Admin Runbook:** Server architecture diagram, user onboarding & RBAC assignment, adding allowed file types/properties (`hp.type`, `hp.property`, `restrict_types`, `restrict_properties`), releasing a new client version (`hp.settings`), backup verification, log analysis, and rollback steps.
   - **Engineer Quick-Start Guide:** Installing HackPDM, configuring the Working Folder (PWA), creating a profile with an Odoo API key, daily workflow (Get Latest -> Check Out -> Edit in SolidWorks -> Commit -> Dependency Graph), and how to export logs if an issue occurs.

### Milestone 5 Verification & Exit Criteria
- Real SolidWorks assemblies commit, check out, revise, download, and graph cleanly on Windows workstations with zero data corruption.
- `HACKPDM_BIG_FILE_MB=250` passes over the TLS reverse proxy with zero worker crashes or timeouts.
- Legacy migration dry-run (if applicable) reconciles 100% of counts and sampled checksums within the planned cutover window.
- Pilot engineers sign off on daily usability, and all discovered issues + log findings are triaged for Milestone 6.

---

## Milestone 6: Final Code Review, Production Hardening & Lingering Issue Resolution

**Objective:** Perform a comprehensive, line-by-line pre-launch review of the entire codebase and resolve every lingering defect, warning, logging gap, performance bottleneck, or operational risk before production sign-off.

### 6.1 End-to-End Security & Authorization Code Audit
1. **Odoo Addon Audit (`Odoo/addons/hackpdm/`):**
   - Audit every `sudo()` call site across the addon to confirm strict caller validation precedes privilege elevation and no cross-user data leakage is possible:
     - `hp_pdm_commit.py` (lines 97, 409, 463)
     - `hp_upload.py` (lines 118, 142, 144, 162, 187, 188, 206, 219)
     - `hp_version.py` (line 458)
     - `hp_legacy.py` (line 36)
   - Verify `ir.model.access.csv` and `hackpdm_rules.xml` enforce least privilege across `group_pdm_viewer`, `group_pdm_user`, and `group_pdm_admin` for every model (`hp.release`, `hp.release.review`, `hp.settings`, `hp.pdm.commit`, `hp.record.staged`, `hp.upload`, `hp.directory`, `hp.entry`, `hp.version`).
   - Verify all server-side path, filename, and checksum guards (`hp_guard.py`, `hp_upload.py`, `hp_entry.py`, `hp_directory.py`) block path traversal (`..`, absolute paths, Windows reserved names `CON`/`PRN`/`NUL`, case-only collisions, malformed hashes).
2. **C# Client Infrastructure & Logging Audit (`Src/HackPDM.Infrastructure/` & `Src/HackPDM.Core/`):**
   - Audit `OdooEndpoint.cs`, `OdooConnection.cs`, and JSON-RPC transport (`OdooRpc`, `HttpClient` pooling, timeouts, cancellation, retries) to confirm TLS certificate validation is never bypassed and credentials/base64 payloads are never leaked in logs or exception strings.
   - Audit `CrossPlatformCredentialManager.cs` and `ProfileStore` to confirm saved profile files never accidentally include plaintext passwords unless explicitly requested (`SaveProfileWithoutPasswordAsync` vs. keychain storage) and fallback file permissions remain `0600` on Unix.
   - Audit `PdmWorkflowService`, `CommitStaging`, `ContentTransfer`, and `LocalFileWriter` for partial-failure atomicity (network interruption mid-commit or mid-download leaves neither corrupted local files nor orphaned server checkouts).

### 6.2 Eliminate Lingering Warnings, Reflection Overhead & Technical Debt
1. **Resolve Uno XAML Binding Metadata Warnings:**
   - Annotate bindable ViewModels and row models (or configure Uno static metadata generation) to eliminate reflection fallback overhead and startup warnings for:
     - `HackPDM.Mvvm.ViewModels.Settings.ProfileManagerViewModel`
     - `HackPDM.Mvvm.ViewModels.Hack.HackFileManagerViewModel`
     - `HackPDM.Mvvm.Models.Hack.EntryRow`
     - `HackPDM.Domain.Representation.HistoryRow`, `ChildrenRow`, `ParentRow`
     - `HackPDM.Mvvm.Views.Common.Types.TreeData`
     - `HackPDM.Mvvm.ViewModels.Settings.ThemeEditorViewModel`, `ThemeColorItem`
2. **Clear Remaining Compiler, Analyzer & Code Hygiene Debt:**
   - Resolve remaining `TODO` / `FIXME` comments and dispose pattern stubs (e.g., `Src/HackPDM.Domain/Helper/ConcurrentSet.cs:211–217`).
   - Audit nullable annotations, fire-and-forget async UI handlers, and any newly introduced code from Milestones 1–5 to ensure zero silent error swallows and consistent structured logging.
   - Confirm `dotnet build HackPDM.sln -c Release` produces clean output and Odoo startup logs contain zero unexpected warnings.

### 6.3 Performance & UI Responsiveness Review
- Review `TestResults/smoke-report*.json` `MaxUiStallMs` timings (previously observed up to ~600–1100 ms during login and theme/graph transitions) and move any remaining synchronous I/O or heavy collection updates off the UI dispatcher thread.
- Profile directory listing (`EntryListService.LoadAsync`) and dependency graph loading (`hp_dependency_graph.py`) on a large synthetic directory (500–2,000 entries and 200-node assembly graphs) to verify acceptable latency and absence of N+1 Odoo RPC queries.
- Verify memory usage and disposal of ViewModels (`IsDisposed` checks, event/watcher unsubscriptions, `CancellationToken` disposal) across repeated page navigation to rule out memory or file-watcher handle leaks during all-day engineering sessions.

### 6.4 Pilot Remediation & Regression Test Hardening
- Triage and fix **100% of P0/P1 bugs**, error-handling gaps, and high-friction UX issues surfaced during Milestone 5's real-workstation validation and engineering pilot.
- Write a targeted automated regression test (xUnit unit test, Odoo `TransactionCase`, or live integration test in `Src/HackPDM.Tests/Integration/`) for every defect fixed during the review and pilot.
- Confirm `SolidWorksLifecycleTests`, `FolderActionsTests`, `PdmWorkflowTests`, `AttackTests`, `AddonSeedDataTests`, and Odoo's `test_attacks.py`, `test_safeguards.py`, `test_functions.py`, `test_transfer.py`, and `test_dependency_graph.py` all run cleanly with zero unexplained skips.

### 6.5 Final Go/No-Go Production Release Gate
Before declaring production launch, every gate below must be green on the exact release commit:
- [ ] **CI Gate 1 (`odoo-integration.yml`):** Odoo addon server tests (`TestSafeguards`, `TestServerFunctions`, `TestAttacks`, `TestTransfer`, `TestDependencyGraph`), full `HackPDM.Tests` live integration suite, and multi-theme desktop `SmokeTestRunner` (`Dark:Default`, `NavyBlue:Fancy`, `Light:Modern`, `Dark:Compact`) all pass with exit code `0` and `unhandledExceptions: []`.
- [ ] **CI Gate 2 (`windows-release.yml`):** Release build, Windows unit tests, Authenticode signing of app + installer, Inno Setup build, and `.sha256` generation succeed cleanly on the release tag (`vX.Y.Z`).
- [ ] **Error-Handling & Logging Gate:** Verified rotating client logs, secret redaction tests, crash dump capture, and structured Odoo audit logs with zero silent swallows or bare `except:` blocks.
- [ ] **Security Gate:** Zero high/critical findings open from the Milestone 6 audit; TLS, `list_db = False`, RBAC groups, and hashed `admin_passwd` verified on the production host.
- [ ] **DR & Operations Gate:** Verified backup + restore completed, monitoring & disk alerts active, `hp.settings` (`client_version`, `client_download_url`, `client_download_sha256`) published, and admin/user runbooks distributed.
- [ ] **Post-Cutover Smoke Check:** First production login, commit, check-out, get-latest, dependency graph load, log rotation, and nightly backup verified on the live production vault within 24 hours of launch.
