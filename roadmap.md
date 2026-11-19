# WyrmPDM / HackPDM Feature & Architecture Roadmap

This roadmap details implementation plans for the 12 prioritized enhancements across workflows, cross-view discovery, large-vault scalability, and UI/diagnostic polish (`1.2`, `1.3`, `1.4`, `2.2`, `2.3`, `2.4`, `3.1`, `3.2`, `3.3`, `4.1`, `4.2`, `4.3`).

Each section lists **verified existing touchpoints** in the repository separately from **proposed additions** so implementation work stays grounded in the current Uno Platform / MVVM architecture (`HackPDM`, `HackPDM.Domain`, `HackPDM.Infrastructure`, `HackPDM.Tests`).

---

## Status Legend

Aligned with [`REFACTOR_PLAN.md`](REFACTOR_PLAN.md):

- `[ ]` Not started
- `[~]` In progress
- `[x]` Done

---

## Overview & Recommended Execution Order

To avoid rework and maximize perceived speed early, execute in four dependency waves:

| Wave | Roadmap ID | Title | Priority | Effort | Key Unlocks / Dependencies | Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Wave 0 — Quick Wins** | **4.1** | Register Missing Row Types in `BindableMetadata` | High | Low | Eliminates reflection/trimming warnings before adding new views/rows | `[ ]` |
| **Wave 0 — Quick Wins** | **4.3** | Richer Minimal Previews | Medium | Low–Medium | Immediate UX polish for CAD/non-CAD files lacking bitmaps | `[ ]` |
| **Wave 1 — Scale Foundation** | **3.2** | Persistent Local Metadata & Checksum Index | High | Medium–High | Eliminates redundant hashing; accelerates `3.1`, `1.2`, `1.4`, `3.3` | `[ ]` |
| **Wave 1 — Scale Foundation** | **3.1** | Incremental Tree & Entry Hydration | High | High | Fixes multi-second startup/folder load stalls on large vaults (`3.2`) | `[ ]` |
| **Wave 1 — Scale Foundation** | **3.3** | Search Debouncing & Incremental Results | Medium | Low–Medium | Instant local matches + clean cancellation in minimal Search Odoo bar | `[ ]` |
| **Wave 2 — Safe PDM Workflows** | **1.2** | Pre-Commit Staging & Reference Checklist | High | Medium–High | Safe check-in pre-flight; faster with `3.2` | `[ ]` |
| **Wave 2 — Safe PDM Workflows** | **1.4** | Offline Queue & Conflict Resolution Dialog | High | Medium–High | Safe overwrite/backup UX and disconnect/reconnect resilience (`3.2`) | `[ ]` |
| **Wave 2 — Safe PDM Workflows** | **1.3** | Version & Property Diffing | Medium–High | Medium | Side-by-side history/local delta inspection (`4.1`) | `[ ]` |
| **Wave 2 — Safe PDM Workflows** | **4.2** | Inline Actionable Error Links | Medium | Medium | Clickable jump/retry actions in `StatusDialogView` / `HoverPopupCard` | `[ ]` |
| **Wave 3 — Cross-View Discovery** | **2.4** | File Manager, Search & Dependency Graph Cross-Highlighting | Medium | Medium | Unified entry locate/graph handoff; reused by `4.2` & `2.3` | `[ ]` |
| **Wave 3 — Cross-View Discovery** | **2.2** | Saved Searches & Quick-Filter Chips | Medium | Medium | Reusable `EntrySearchQuery` presets & inline `Entries` filter chips | `[ ]` |
| **Wave 3 — Cross-View Discovery** | **2.3** | Unified Command Palette (`Ctrl+K`) | Medium | Medium | Shell-wide keyboard launcher surfacing `2.2`, `2.4`, and vault actions | `[ ]` |

---

## Shared Architecture & Verification Gate

All changes should preserve existing cross-platform constraints (Skia Desktop on Linux/Windows + WinUI/Windows App SDK parity) and keep business/diff/index/queue logic in pure C# services so unit tests execute headless in `HackPDM.Tests` without UI hosting.

Run the shared verification gate after completing any item:

```bash
# 1. Desktop build (must complete cleanly with 0 warnings / 0 errors)
dotnet build Src/HackPDM/HackPDM.csproj -f net10.0-desktop -p:Platform=x64 -c Debug -v minimal -nologo

# 2. Headless unit tests
dotnet test Src/HackPDM.Tests/HackPDM.Tests.csproj --logger "console;verbosity=minimal"

# 3. Live Odoo end-to-end smoke checks (for workflow/RPC-touching items)
Odoo/ci/run_local_smoke.sh
```

---

## Area 1 — Core PDM Safety & Workflows

### 1.2 Pre-Commit Staging & Reference Checklist `[ ]`

- **Goal**: Replace single-shot directory/file commits with an explicit pre-commit staging checklist displaying SolidWorks assembly dependencies, missing/out-of-vault references, server-newer conflicts, per-file inclusion checkboxes, and commit notes before any Odoo RPC mutation runs.
- **Existing Touchpoints**:
  - `Src/HackPDM/Mvvm/ViewModels/Hack/HackFileManagerViewModel.cs` (`CommitDirectoryAsync`, `ChangesTabHeader`, `LocalChanges`, `RemoteChanges`, `LocalChangesHeader`, `RemoteChangesHeader`)
  - `Src/HackPDM/Mvvm/Views/Hack/HackFileManagerView.xaml`
  - `Src/HackPDM/Mvvm/Models/Hack/PdmWorkflowService.cs` (`IPdmWorkflowService.CommitAsync`, `CollectCommitFilesAsync`, `CollectFolderCommitFilesAsync`)
  - `Src/HackPDM.Infrastructure/Operations/CommitStaging.cs` (`CommitStaging`, `NewEntry`, `FindRemoteEntryAsync`, `StageEntryAsync`, `PrepareVersion`, `StageVersionAsync`)
  - `Src/HackPDM.Infrastructure/Operations/PdmCommitOperations.cs` (`CreateDirectoriesAsync`, `FilesNotInOdooSegmentedAsync`)
  - `Src/HackPDM.Infrastructure/SldWrks/SwFileReader.cs` (`ISwDocumentReader.GetDependencies`)
- **Proposed Additions & UX Design**:
  - Introduce `CommitStagingPlan` and `CommitCandidateRow` models classifying candidate files returned by `CollectCommitFilesAsync` / `CollectFolderCommitFilesAsync` and `FilesNotInOdooSegmentedAsync` (`LocalOnly`, `RemoteExisting`, `UnchangedCheckedOut`).
  - Run `ISwDocumentReader.GetDependencies` across staged SolidWorks assemblies/drawings to detect:
    - Dirty or untracked child components not yet selected for commit (with one-click *"Include Referenced Children"*).
    - Missing references on disk or paths outside the vault working root (blocking or flagged with an explicit acknowledgment toggle).
    - Checked-out conflict / server-newer conditions detected against Odoo entry/version state.
  - Render a structured Pre-Commit Staging panel/dialog (linked from `ChangesTabHeader` and `CommitDirectoryAsync`) with per-row checkboxes, status badges, group filters (*Modified / New / Unchanged / Missing Refs*), and a commit summary/message box.
- **Implementation Tasks**:
  - [ ] Build pure staging-analysis service aggregating `FilesNotInOdooSegmentedAsync` and `ISwDocumentReader.GetDependencies` without performing RPC writes.
  - [ ] Add cycle-safe recursive reference traversal for nested assemblies (`SW` sub-assemblies) so missing-reference checks terminate cleanly.
  - [ ] Wire per-file inclusion filtering into `IPdmWorkflowService.CommitAsync` so unchecked items are cleanly excluded (including directory creation pruning for empty excluded folders).
  - [ ] Add Pre-Commit Staging dialog/drawer UI inside `HackFileManagerView` / `HackFileManagerViewModel` reusing existing status badges (`StatusIcon`, `StatusDescription`, `FileSizeFormatter`).
  - [ ] Ensure staging analysis shows cancellable busy progress for large folders rather than blocking the UI thread.
- **Edge Cases & Constraints**:
  - Unchecked parent assembly with checked child parts vs checked assembly with missing/virtual components must clearly distinguish hard blockers from informational warnings.
  - Non-CAD files bypass `ISwDocumentReader.GetDependencies` quickly.
- **Acceptance Criteria & Verification**:
  - Unit tests verifying classification of `LocalOnly`, `RemoteExisting`, `UnchangedCheckedOut`, circular assembly references, and exclusion filtering when items are unchecked.
  - Live Odoo smoke test committing a subset of staged files and verifying deselected files remain uncommitted.

---

### 1.3 Version & Property Diffing `[ ]`

- **Goal**: Let users compare two remote versions (or the local working file vs latest server version) side by side to inspect custom property (`HpVersionProperty`) deltas, assembly dependency changes, file size/checksum changes, and optional visual thumbnails.
- **Existing Touchpoints**:
  - `Src/HackPDM/Mvvm/ViewModels/Hack/HackFileManagerViewModel.cs` (`ShowEntryDetailsAsync`, `LoadDetailTabAsync`, `PreviewVersionAsync`)
  - `Src/HackPDM/Mvvm/Models/Hack/EntryDetailsService.cs` (`IEntryDetailsService`: `GetHistoryAsync`, `GetParentsAsync`, `GetChildrenAsync`, `GetPropertiesAsync`, `GetVersionInfoAsync`, `GetPreviewImageAsync`, `GetLocalPreviewImageAsync`)
  - `Src/HackPDM.Domain/Representation/HistoryRow.cs`, `PropertiesRow.cs`, `VersionRow.cs`, `ChildrenRow.cs`, `ParentRow.cs`
  - `Src/HackPDM.Infrastructure/SldWrks/SwFileReader.cs` (local dependency inspection for *Local vs Server* mode)
- **Proposed Additions & UX Design**:
  - Create a `VersionDiffService` comparing `VersionSnapshot` `(A, B)` pairs and emitting normalized diff rows (`Added`, `Removed`, `Modified`, `Unchanged`) across:
    - Core version metadata (revision/version ID, author, timestamp, size, checksum).
    - Custom properties (`PropertiesRow` keyed cleanly by property name + configuration).
    - Child dependencies (`ChildrenRow` / local `ISwDocumentReader.GetDependencies` snapshot).
  - Add *"Compare with Previous"*, *"Compare Selected Versions"* (2 selected `HistoryRow` items), and *"Compare Local vs Latest"* actions in `HackFileManagerView`'s `History` tab.
  - Display a split/compact diff drawer or sub-view with a *"Show Changed Only"* toggle and side-by-side preview thumbnails when `GetPreviewImageAsync` / `GetLocalPreviewImageAsync` returns images.
- **Implementation Tasks**:
  - [ ] Ensure `HistoryRow`, `PropertiesRow`, `VersionRow`, `ChildrenRow`, and `ParentRow` are bindable via **4.1**.
  - [ ] Implement `VersionDiffService` in pure C# comparing properties, children, and version metadata case-sensitively/configuration-safely.
  - [ ] Support fetching two version detail sets concurrently in `HackFileManagerViewModel` via `IEntryDetailsService` with cancellation if the user switches entries mid-load.
  - [ ] Build the Version Diff UI panel with status-tinted badges (`Added`, `Removed`, `Modified`) and a *"Changed Only"* filter switch.
- **Edge Cases & Constraints**:
  - Handle missing custom properties or absent bitmap previews gracefully (`null` previews collapse cleanly to metadata-only diff).
  - Preserve existing single-version `PreviewVersionAsync` and temp-copy commands (`DownloadHistoryToTempAsync`, `OpenHistoryFromTempAsync`, etc.).
- **Acceptance Criteria & Verification**:
  - Unit tests covering identical versions, added/removed/changed properties, configuration-scoped property collisions, and child reference additions/removals.

---

### 1.4 Offline Queue & Conflict Resolution Dialog `[ ]`

- **Goal**: Give engineers explicit interactive choices when local edits collide with newer server versions—preventing silent data loss—and track offline disk edits in a durable queue until connectivity returns.
- **Existing Touchpoints**:
  - `Src/HackPDM.Infrastructure/Operations/PdmDownloadOperations.cs` (`GetAllEntriesAndDependenciesListAsync`, `GetVersionListAsync`, `GetLatestVersionsAsync`, `DownloadSummary` including `KeptLocalChanges`, `ProcessDownloadsAsync`)
  - `Src/HackPDM/Mvvm/Models/Hack/PdmWorkflowService.cs` (`VersionDownloadPlan`, `GetLatestAsync`, `CheckoutLatestAsync`, `UnCheckoutAsync`)
  - `Src/HackPDM/Mvvm/Models/Hack/LocalFileWatcher.cs` (`LocalFileChange`, `LocalChangeKind`: `Created`, `Updated`, `Deleted`, `LocalChangeBatcher`)
  - `Src/HackPDM/Mvvm/ViewModels/Hack/HackFileManagerViewModel.cs` (temp/backup workflow helpers like `MoveHistoryToTempAsync`)
  - `ProfileManagerViewModel` / `LoginSession` (connection/login state transitions)
- **Proposed Additions & UX Design**:
  - Define a `FileConflictItem` model (entry identity, local path, local mtime/size/checksum, remote version/author/timestamp, local checkout ownership, recommended default resolution).
  - Introduce a **Conflict Resolution Dialog** triggered when `GetLatestAsync` / `CheckoutLatestAsync` / folder sync encounters locally modified files that differ from incoming remote versions, offering per-file and *"Apply to all remaining"* choices:
    - **Keep Local** (retain workspace copy, record in `KeptLocalChanges`)
    - **Take Server (Backup Local to Temp)** (safely copy/move local edits to temp before overwriting)
    - **Take Server (Overwrite)**
    - **Skip**
  - Add a persistent per-profile/per-vault **Offline Change Queue** subscribing to `LocalChangeBatcher` when the session is disconnected/offline, coalescing redundant events (`Created` + `Updated` -> `Created`, `Created` + `Deleted` -> removed).
  - Surface a subtle banner/status notification upon reconnect in `ProfileManagerView` / `HackFileManagerView` allowing users to open the queued edits into Pre-Commit Staging (`1.2`) or dismiss stale items.
- **Implementation Tasks**:
  - [ ] Add structured conflict detection hook in `VersionDownloadPlan` / `PdmDownloadOperations.ProcessDownloadsAsync` (or a pre-flight check before overwriting/skipping).
  - [ ] Build headless conflict-resolution policy engine supporting per-file overrides and batch *"Apply to remaining"* semantics.
  - [ ] Implement persistent JSON/atomic-write offline queue store scoped by profile + workspace path (`LocalChangeBatcher` coalescing).
  - [ ] Build the Conflict Resolution modal UI (accessible cleanly from `HackFileManagerViewModel`) and reconnect queue banner.
  - [ ] Emit actionable conflict/resolution summaries via `BasicStatusMessage` (`4.2`).
- **Edge Cases & Constraints**:
  - Never overwrite an unbacked-up `Lm` local CAD file unless the user explicitly selects **Take Server (Overwrite)**.
  - Handle read-only file attribute toggles safely across Linux and Windows when backing up or replacing local files.
- **Acceptance Criteria & Verification**:
  - Unit tests covering conflict resolution decisions, batch policy application, temp backup creation before overwrite, and offline queue event coalescing + persistence round-trip.
  - Live smoke test exercising `GetLatest` on a locally modified file without unprompted loss of local changes.

---

## Area 2 — Discovery & Cross-View Navigation

### 2.2 Saved Searches & Quick-Filter Chips `[ ]`

- **Goal**: Speed up repetitive PDM triage by persisting reusable `EntrySearchQuery` presets in `SearchOdooView` and adding instant client-side filter chips + filename filter above `Entries` in `HackFileManagerView`.
- **Existing Touchpoints**:
  - `Src/HackPDM/Mvvm/Models/Odoo/EntrySearchService.cs` (`EntrySearchQuery`, `IEntrySearchService`, `GetSearchableProperties`)
  - `Src/HackPDM/Mvvm/ViewModels/Odoo/SearchOdooViewModel.cs` (`SearchResultsList`, `LocateCommand`)
  - `Src/HackPDM/Mvvm/Views/Odoo/SearchOdooView.xaml` & `.xaml.cs` (`MinimalSearchRoot`, `MiniResultsCombo`)
  - `Src/HackPDM/Mvvm/ViewModels/Hack/HackFileManagerViewModel.cs`
  - `Src/HackPDM/Mvvm/Views/Hack/HackFileManagerView.xaml`
  - `Src/HackPDM/Mvvm/Models/Hack/FileManagerState.cs` (`ShowInactive`, `IsGroupingEnabled`, `GroupField`, `Sort`)
- **Proposed Additions & UX Design**:
  - Add a `SavedSearchPreset` store (name, pinned flag, `EntrySearchQuery` payload, last-used UTC) persisted per profile.
  - Add Save / Pin / Rename / Delete preset controls to full `SearchOdooView` and display pinned presets as one-click chips inside both full `SearchOdooView` and `MinimalSearchRoot`'s options flyout.
  - Upgrade `MiniResultsCombo` rows with compact inline quick-actions (*Locate*, *Get Latest*, *Check Out*, *Open in Graph*) via `IPdmWorkflowService` / `2.4` intent.
  - Add a compact inline filter bar above the file `TableView` in `HackFileManagerView`: live filename/extension substring filter box + toggleable status chips (*All*, *Modified*, *Checked Out*, *Remote Only*, *Local Only*, *Inactive*), saved in `FileManagerState`.
- **Implementation Tasks**:
  - [ ] Implement serializable `SavedSearchPreset` persistence store + CRUD methods in `SearchOdooViewModel`.
  - [ ] Expose pinned search chips in `SearchOdooView` (both full and `MinimalSearchRoot` modes) and inline action buttons on `MiniResultsCombo`.
  - [ ] Add active filter chip state + filename filter predicate to `FileManagerState` / `HackFileManagerViewModel` layered cleanly on top of grouping and `Sort`.
  - [ ] Ensure clearing filters restores full folder view without triggering unnecessary remote RPC reloads.
- **Edge Cases & Constraints**:
  - Grouping (`IsGroupingEnabled`, `GroupField`) must continue rendering empty-group-free sections when quick filters narrow `EntryRow` items.
  - Saved presets referencing properties or file types removed on a different server profile degrade gracefully with a status notice.
- **Acceptance Criteria & Verification**:
  - Unit tests for preset serialization, filename + multi-chip filter evaluation across `EntryRow` statuses, and state preservation across directory switches.

---

### 2.3 Unified Command Palette (`Ctrl+K`) `[ ]`

- **Goal**: Provide a single keyboard-first `Ctrl+K` launcher across the shell (`HomeView`) for switching pages, jumping to recent folders/files, executing saved searches, toggling docked/minimal Search & Status drawers, and running common vault actions.
- **Existing Touchpoints**:
  - `Src/HackPDM/Mvvm/Views/Hack/HomeView.xaml` & `HomeView.xaml.cs` (`Pages`, `NavFrame`, `DockedSearchDrawer`, `DockedStatusDrawer`, `ShellOverlayCanvas`, existing shortcuts `Ctrl+1..6`, `Ctrl+F`, `Alt+S`, `Alt+Left`)
  - `Src/HackPDM/Mvvm/Models/Hack/FileManagerState.cs` (`RememberEntries`, `SelectedPath`, `RequestLocateAsync`)
  - `Src/HackPDM/Mvvm/Models/Odoo/EntrySearchService.cs` (`IEntrySearchService`)
  - `Src/HackPDM/Mvvm/ViewModels/Settings/StatusDialogViewModel.cs`
- **Proposed Additions & UX Design**:
  - Build a `CommandPaletteService` / `CommandPaletteProvider` registering static and context-aware commands:
    - Navigation (`HomeView.Pages` views, toggle docked/floating Search & Status extensions, switch Minimal vs Full mode).
    - Recent directories & recently viewed entries tracked in `FileManagerState`.
    - Saved searches from `2.2` + fallback *"Search Odoo for `<query>`"* command.
    - Contextual PDM actions when vault session is active (*Refresh Folder*, *Get Latest*, *Check Out*, *Commit Directory*).
  - Support lightweight prefix scopes (`>` commands, `@` recent files/folders, `?` Odoo search) plus deterministic fuzzy/substring scoring.
  - Render the modal palette over `HomeView`'s shell overlay with `Up`/`Down` selection, `Enter` execute, `Escape` dismiss, and focus restoration to active content.
- **Implementation Tasks**:
  - [ ] Create pure C# `CommandPaletteItem` & deterministic ranking/filtering engine in `HackPDM` Models/ViewModels (unit-testable without XAML).
  - [ ] Track recent folders and selected entries in `FileManagerState` bounded to a concise LRU list.
  - [ ] Wire `Ctrl+K` keyboard accelerator in `HomeView` without conflicting with `Ctrl+1..6`, `Ctrl+F`, `Alt+S`, or text input shortcuts inside dialogs.
  - [ ] Bind shell-level and active-page commands safely using `CanExecute` / login state checks.
- **Edge Cases & Constraints**:
  - Avoid trapping focus or stealing `Enter`/`Escape` when another modal dialog (`ContentDialog`) or text box is actively capturing overlay input.
- **Acceptance Criteria & Verification**:
  - Unit tests verifying query ranking, prefix scoping (`>`, `@`, `?`), recent LRU deduping, and auth/context gating of commands.

---

### 2.4 Cross-Highlighting Between File Manager, Search & Dependency Graph `[ ]`

- **Goal**: Establish two-way contextual navigation so any entry selected in `HackFileManagerView` or `SearchOdooView` can open `DependencyGraphView` pre-focused on that assembly/part, and any node in `DependencyGraphView` can locate back into `HackFileManagerView` or trigger PDM actions.
- **Existing Touchpoints**:
  - `Src/HackPDM/Mvvm/Views/Hack/DependencyGraphView.xaml` & `DependencyGraphView.xaml.cs` (`GraphEntryItem`, `FolderTree`, `FolderBreadcrumb`, `DependencyGraph`, `GraphNode`, `_roots`, `_entries`, `_details` (`IEntryDetailsService`), `_previews` (`IPreviewImageLoader`), `HistoryRows`, `ParentRows`, `ChildRows`, `PropertyRows`, `VersionRows`)
  - `Src/HackPDM/Mvvm/Models/Hack/FileManagerState.cs` (`SelectedDirectory`, `SelectedPath`, `SelectedEntryNames`, `RequestLocateAsync`)
  - `Src/HackPDM/Mvvm/ViewModels/Odoo/SearchOdooViewModel.cs` (`LocateCommand`)
  - `Src/HackPDM/Mvvm/ViewModels/Hack/HackFileManagerViewModel.cs`
  - `Src/HackPDM/Mvvm/Views/Hack/HomeView.xaml.cs` (`Pages`, `NavFrame`)
- **Proposed Additions & UX Design**:
  - Introduce a unified navigation/selection request model (e.g. extending `FileManagerState` or a shell `VaultNavigationCoordinator`) holding pending target `{ PageKey, DirectoryPath, EntryId, EntryName }`.
  - Add *"Open in Dependency Graph"* action to `HackFileManagerView` entry context menu / toolbar and `SearchOdooView` (`SearchResultsList` + `MiniResultsCombo`).
  - Update `DependencyGraphView` activation (`OnNavigatedTo` / pending request check) to expand `FolderTree` / `FolderBreadcrumb` along `DirectoryPath`, select the matching `GraphEntryItem`, render `DependencyGraph`, and visually distinguish upstream (`ParentRows`) vs downstream (`ChildRows`) `GraphNode` edges.
  - Add *"Locate in File Manager"* plus quick *"Get Latest"* / *"Check Out"* context actions on selected `GraphNode` / `GraphEntryItem` entries inside `DependencyGraphView`.
- **Implementation Tasks**:
  - [ ] Extract `DependencyGraphView`'s navigation/target state cleanly so pending targets consume once on arrival (no stale re-triggers on back navigation).
  - [ ] Wire *"Open in Dependency Graph"* from `HackFileManagerView` and `SearchOdooView` (`LocateCommand` companion).
  - [ ] Wire *"Locate in File Manager"* from `DependencyGraphView` using `FileManagerState.RequestLocateAsync`.
  - [ ] Add upstream vs downstream node highlighting styles and focus-on-selected-node behavior in `DependencyGraphView`.
- **Edge Cases & Constraints**:
  - Handle entries that exist only locally or are newly checked out without remote children yet (render single root node cleanly with empty-state guidance).
- **Acceptance Criteria & Verification**:
  - Unit tests for navigation-intent consume-once semantics and directory path segment matching.

---

## Area 3 — Performance & Responsiveness at Scale

### 3.1 Incremental Tree & Entry Hydration `[ ]`

- **Goal**: Cut login-to-usable-file-manager latency on large vaults (15s+ cold loads) to near-instant interactive shell + progressive folder rendering via lazy tree expansion, phased entry hydration, and cancellable batch hashing.
- **Existing Touchpoints**:
  - `Src/HackPDM/Mvvm/Models/Hack/DirectoryTreeService.cs` (`IDirectoryTreeService.LoadAsync`, `HpDirectory.GetSubdirectoriesAsync(false)`, `LocalTreePathSet`, `DirectoryTreeMerger.AddLocalDirectories`)
  - `Src/HackPDM/Mvvm/Models/Hack/EntryListService.cs` (`IEntryListService`, `EntryLoadProgress`, `HashConcurrency`, `LoadAsync`, `ScanLocalDirectory`, `toHash`, `RefreshRowAsync`)
  - `Src/HackPDM/Mvvm/Models/Hack/FileManagerState.cs` (`Tree`, `ExpandedPaths`, `MaxCachedEntries = 5000`, `RememberEntries`)
  - `Src/HackPDM.Infrastructure/Odoo/Models/HpDirectory.cs` (`GetSubdirectoriesAsync`, `GetEntriesAsync`, `GetDirectoryEntryIDsAsync`)
  - `Src/HackPDM/Mvvm/ViewModels/Hack/HackFileManagerViewModel.cs`
- **Proposed Additions & UX Design**:
  - **On-Demand Tree Expansion**: Hydrate top-level roots plus `ExpandedPaths` / `SelectedPath` ancestors on login/first open instead of eagerly building every deep branch of `LocalTreePathSet(pwa)` upfront; expand child directories lazily as the user opens nodes (or incrementally in background idle slices).
  - **Two-Phase Progressive Entry Hydration**:
    - **Phase 1 (Immediate)**: Return lightweight rows from `ScanLocalDirectory` + remote `HpDirectory.GetEntriesAsync` (plus cached index hits from `3.2`) immediately so names, types, sizes, and dates appear within ~100–300 ms.
    - **Phase 2 (Streaming)**: Compute remaining uncached hashes in bounded batches (`HashConcurrency`) with `IProgress<EntryLoadProgress>` and `CancellationToken`, updating matching `EntryRow` statuses in-place in coalesced UI ticks.
  - **Coalesced Collection Updates & Bounded Cache**: Replace single-item add storms with batch/keyed updates and enforce `FileManagerState.MaxCachedEntries` (`5000`) LRU eviction cleanly, invalidated selectively by `LocalFileWatcher`.
- **Implementation Tasks**:
  - [ ] Refactor `DirectoryTreeService` to support shallow initial root/ancestor hydration + `ExpandNodeAsync(node, token)` lazy child merge (`FromRemote` + local subdirectory probe for that single folder).
  - [ ] Refactor `EntryListService.LoadAsync` into progressive Phase 1 (metadata + index `3.2` hits) -> Phase 2 (cancellable batched hashing callbacks) emitting coalesced updates.
  - [ ] Enforce clean `CancellationTokenSource` replacement in `HackFileManagerViewModel` when users click rapidly between folders so abandoned folder loads cancel immediately.
  - [ ] Implement deterministic LRU eviction in `FileManagerState` respecting `MaxCachedEntries`.
- **Edge Cases & Constraints**:
  - Ensure tree search/locate (`FileManagerState.RequestLocateAsync`) still materializes ancestor nodes on demand even if intermediate branches have not been manually expanded yet.
  - Keep `EntryLoadProgress` status banners/loading indicators responsive without flickering on fast cached folders.
- **Acceptance Criteria & Verification**:
  - Unit tests simulating 5,000+ entries confirming: Phase 1 returns before hashing completes, folder-switch cancellation stops pending `toHash` work promptly, and `FileManagerState` cache count never exceeds `MaxCachedEntries`.

---

### 3.2 Persistent Local Metadata & Checksum Index `[ ]`

- **Goal**: Eliminate repeated disk hashing across sessions and folder switches by persisting `(length, lastWriteUtc) -> checksum` metadata indexed per profile and vault path, updated incrementally on file watcher and PDM workflow events.
- **Existing Touchpoints**:
  - `Src/HackPDM/Mvvm/Models/Hack/EntryListService.cs` (`IEntryListService.LoadAsync`, `ScanLocalDirectory`, `toHash`, `RefreshRowAsync`)
  - `Src/HackPDM/Mvvm/Models/Hack/LocalFileWatcher.cs` (`LocalFileChange`, `LocalChangeKind`, `LocalChangeBatcher`)
  - `Src/HackPDM/Mvvm/Models/Hack/PdmWorkflowService.cs` (`GetLatestAsync`, `CheckoutAsync`, `CommitAsync`)
  - `Src/HackPDM.Infrastructure/Operations/PdmDownloadOperations.cs` & `CommitStaging.cs`
- **Proposed Additions & UX Design**:
  - Introduce `ILocalMetadataIndex` (e.g. `LocalMetadataIndexService`) persisted atomically in user app-data storage segmented by profile/vault identifier.
  - Record schema per normalized vault-relative path: `FileLengthBytes`, `LastWriteTimeUtcTicks`, `Checksum`, `LastKnownRemoteVersionId`, `IndexedAtUtc`.
  - **Cache Lookup Path**: Before queueing a file into `toHash` in `EntryListService.LoadAsync` / `RefreshRowAsync`, verify current disk `(Length, LastWriteTimeUtc.Ticks)`:
    - Exact match -> reuse cached `Checksum` immediately (zero file read).
    - Mismatch or missing -> re-hash and upsert index entry.
  - **Write-Through Invalidation**: Hook `LocalChangeBatcher` (`Created`, `Updated`, `Deleted`), `PdmDownloadOperations.ProcessDownloadsAsync`, and `CommitStaging` completions directly into index invalidation/upsert.
  - Add a *"Rebuild Local File Index"* utility action in Settings / Profile Manager for recovery or timestamp-granularity resets.
- **Implementation Tasks**:
  - [ ] Design `ILocalMetadataIndex` interface and atomic disk persistence format (crash-safe temp-file write + rename, corrupted-index fallback).
  - [ ] Integrate index lookup and batch flush into `EntryListService` (`LoadAsync` and `RefreshRowAsync`).
  - [ ] Subscribe index updates/deletions to `LocalChangeBatcher` and download/commit completion hooks.
  - [ ] Add pruning for files deleted outside the running app and expose manual index rebuild/clear command.
- **Edge Cases & Constraints**:
  - Handle coarse filesystem timestamp granularity cleanly (e.g. compare exact `LastWriteTimeUtc.Ticks` + file size; never trust cached checksum if file is actively locked or modified within the current write).
  - Thread-safe concurrent reads/writes across `HashConcurrency` workers and UI/watcher threads.
- **Acceptance Criteria & Verification**:
  - Unit tests verifying cache hit skips hash delegate execution, size or mtime mutation triggers re-hash, deleted entries prune cleanly, and corrupt index files recover gracefully without failing folder load.

---

### 3.3 Search Debouncing & Incremental Results `[ ]`

- **Goal**: Make minimal and full `SearchOdooView` feel responsive while typing by adding debounced search, in-flight cancellation, and two-stage results combining instant local cached matches with asynchronous Odoo RPC results.
- **Existing Touchpoints**:
  - `Src/HackPDM/Mvvm/ViewModels/Odoo/SearchOdooViewModel.cs` (`SearchResultsList`, `QuickResultsPlaceholder`, `SearchCompleted`, `LocateCommand`)
  - `Src/HackPDM/Mvvm/Views/Odoo/SearchOdooView.xaml` & `.xaml.cs` (`MinimalSearchRoot`, `MiniResultsCombo`)
  - `Src/HackPDM/Mvvm/Models/Odoo/EntrySearchService.cs` (`IEntrySearchService`, `EntrySearchQuery`, `ResultFields`, `OdooRpc.SearchReadAsync`)
  - `Src/HackPDM/Mvvm/Models/Hack/FileManagerState.cs` (`RememberEntries`) & `3.2` local index
- **Proposed Additions & UX Design**:
  - Add a `350 ms` debounce pipeline on minimal search input (`MinimalSearchRoot`), triggerable above a configurable minimum query length (default 2–3 chars) with an option toggle in the minimal options flyout to switch between live debounced search and `Enter`-only search.
  - Cancel superseded search `CancellationTokenSource` instances immediately so out-of-order RPC replies cannot overwrite newer `SearchResultsList` / `MiniResultsCombo` state.
  - **Two-Tier Merging**: Immediately match remembered `FileManagerState` entries (and `3.2` indexed paths if enabled) as `Cached / Local` quick hits, then merge and deduplicate authoritative Odoo `OdooRpc.SearchReadAsync` results as they arrive.
- **Implementation Tasks**:
  - [ ] Introduce cancellable debounce coordinator inside `SearchOdooViewModel` cleanly testable via injectable time/delay or `CancellationToken`.
  - [ ] Separate local-index pre-matching from remote `IEntrySearchService.SearchAsync` RPC and deduplicate results by entry ID / normalized vault path.
  - [ ] Show subtle in-flight indicator in `MinimalSearchRoot` without clearing local matches mid-typing.
- **Edge Cases & Constraints**:
  - When user isn't logged in (`!IsLoggedIn`), clearly skip remote RPC while optionally showing a connect prompt or local-only feedback rather than spamming error popups on every keystroke.
- **Acceptance Criteria & Verification**:
  - Unit tests verifying 5 rapid query edits produce a single debounced RPC request, stale slow RPC replies are ignored after cancellation, and duplicate local + remote entries merge cleanly.

---

## Area 4 — UI Polish, Diagnostics & Rich Metadata

### 4.1 Register Missing Row Types in `BindableMetadata` `[ ]`

- **Goal**: Eliminate runtime reflection/trimming warnings and safeguard NativeAOT/WinUI compiled bindings by registering all missing `HackPDM.Domain.Representation` row models in `BindableMetadata.cs`.
- **Existing Touchpoints**:
  - `Src/HackPDM/Shell/BindableMetadata.cs` (currently registers `EntryRow`, `TreeData`, view models, `ThemeColorItem`, `JsonNodeViewModel`)
  - `Src/HackPDM.Domain/Representation/` (`BasicStatusMessage`, `DataGridData`, `HistoryRow`, `ChildrenRow`, `ParentRow`, `FileTypeRow`, `FileTypeEntryFilterRow`, `FileTypeLocDatRow`, `FileTypeLocRow`, `OperatorsRow`, `PropertiesRow`, `SearchPropertiesRow`, `SearchRow`, `VersionRow`, `TypeRepresentative`)
- **Proposed Additions & UX Design**:
  - Audit every XAML `{Binding}` and `x:Bind` item template across views (`HackFileManagerView`, `DependencyGraphView`, `SearchOdooView`, `StatusDialogView`, `ProfileManagerView`, settings/file-type views) to confirm every bound representation class.
  - Add `[Bindable]` declarations in `Src/HackPDM/Shell/BindableMetadata.cs` for `BasicStatusMessage`, `HistoryRow`, `ChildrenRow`, `ParentRow`, `FileTypeRow`, `OperatorsRow`, `SearchRow`, `PropertiesRow`, `VersionRow`, `SearchPropertiesRow`, `FileTypeEntryFilterRow`, `FileTypeLocDatRow`, `FileTypeLocRow`, `DataGridData`, and `TypeRepresentative` (plus any nested sub-row types referenced in UI bindings).
  - Add a reflection unit guard test in `HackPDM.Tests` asserting that newly added public row classes in `HackPDM.Domain.Representation` are registered in `BindableMetadata.cs` so future features (`1.2`, `1.3`, `2.2`) never regress.
- **Implementation Tasks**:
  - [ ] Audit all XAML views and `HackPDM.Domain.Representation` types for unbound/reflection-bound row models.
  - [ ] Register all missing row classes in `Src/HackPDM/Shell/BindableMetadata.cs`.
  - [ ] Add automated completeness test in `HackPDM.Tests` verifying `BindableMetadata` coverage against `HackPDM.Domain.Representation`.
- **Edge Cases & Constraints**:
  - Verify compatibility under both `net10.0-desktop` (Skia) and Windows App SDK target frameworks without introducing ambiguity or duplicate partial declarations.
- **Acceptance Criteria & Verification**:
  - `dotnet build` succeeds with `0 warnings, 0 errors`.
  - `BindableMetadata` completeness test passes in `dotnet test`.
  - Navigating Status Dialog, Search Odoo, File Manager detail tabs (`History`, `Properties`, `Parents`, `Children`, `Version`), and File Types produces zero bindable metadata warnings in runtime logs.

---

### 4.2 Inline Actionable Error Links `[ ]`

- **Goal**: Transform `StatusDialogView` and its minimal `HoverPopupCard` / `ProfileManagerView` diagnostics from passive text logs into actionable remediation links (*Jump to File*, *Open Settings*, *Copy Details*, *Retry*).
- **Existing Touchpoints**:
  - `Src/HackPDM.Domain/Representation/Representative.cs` (`BasicStatusMessage`)
  - `Src/HackPDM/Mvvm/ViewModels/Settings/StatusDialogViewModel.cs` (`OStatus`, `OInfo`, `OError`, `OWarnings`, `OErrorsOnly`)
  - `Src/HackPDM/Mvvm/Views/Settings/StatusDialogView.xaml` & `.xaml.cs` (`MinimalStatusRoot`, `HoverPopupCard`, `StatusHoverPopup`)
  - `Src/HackPDM/Mvvm/Models/Hack/FileManagerState.cs` (`RequestLocateAsync`)
  - `Src/HackPDM/Mvvm/Views/Hack/HomeView.xaml.cs` (`Pages`, `NavFrame`)
  - `ProfileManagerViewModel` (`TestConnectionCommand`, `CheckStatusCommand`)
- **Proposed Additions & UX Design**:
  - Extend `BasicStatusMessage` with optional non-breaking metadata fields: `TargetVaultPath`, `TargetEntryName`, `TargetEntryId`, `TargetPageKey`, `ActionKind` (`None`, `LocateEntry`, `NavigatePage`, `RetryAction`), and optional structured detail text for clipboard copy.
  - Attach structured file targets in key workflow emitters (`PdmDownloadOperations`, `PdmCommitOperations`, `EntryListService`, `ProfileManagerViewModel` login/connection diagnostics).
  - Add compact inline action links/buttons on full `StatusDialogView` rows and `HoverPopupCard` summary items (*Locate*, *Settings*, *Copy*, *Retry*) while preserving click-on-card -> open full Status Dialog behavior when clicking the card body.
- **Implementation Tasks**:
  - [ ] Extend `BasicStatusMessage` (ensuring `BindableMetadata` stays updated from `4.1`) with optional target/action properties and `HasAction` helper properties.
  - [ ] Update file download/commit/hashing and profile connection message producers to populate `TargetVaultPath` / `TargetPageKey` where known.
  - [ ] Add `ExecuteStatusActionCommand` & `CopyStatusDetailsCommand` in `StatusDialogViewModel` routing locate actions via `HomeView` / `FileManagerState.RequestLocateAsync`.
  - [ ] Add compact inline buttons inside `StatusDialogView` log templates and `HoverPopupCard` ensuring click bubbling does not trigger contradictory dual actions.
- **Edge Cases & Constraints**:
  - If a referenced file or folder was deleted before the user clicks *Locate*, navigate safely to the closest existing ancestor directory and emit an informative status notice.
- **Acceptance Criteria & Verification**:
  - Unit tests verifying `BasicStatusMessage` backward compatibility, missing-path safe fallback behavior, and action dispatch routing.

---

### 4.3 Richer Minimal Previews `[ ]`

- **Goal**: Upgrade the `HackFileManagerView` Preview pane when no bitmap preview image exists (or alongside it) with a clean, structured CAD/file metadata summary card sourced from `EntryDetailsService` and `SwFileReader`.
- **Existing Touchpoints**:
  - `Src/HackPDM/Mvvm/ViewModels/Hack/HackFileManagerViewModel.cs` (`Preview`, `IsPreviewLoading`, `IsPreviewVisible`, `PreviewStatusText`, `ShowEntryDetailsAsync`)
  - `Src/HackPDM/Mvvm/Views/Hack/HackFileManagerView.xaml` (`TypeIcon`, `StatusDescription`, `FileSizeFormatter`, `LocalDateFormatter`)
  - `Src/HackPDM/Mvvm/Models/Hack/EntryDetailsService.cs` (`GetPropertiesAsync`, `GetVersionInfoAsync`, `GetPreviewImageAsync`, `GetLocalPreviewImageAsync`, `GetChildrenAsync`, `GetParentsAsync`)
  - `Src/HackPDM.Infrastructure/SldWrks/SwFileReader.cs` (`ISwDocumentReader.GetDependencies`, `SwStreamStore.Open`, `SwDocContentMgr/SwDocContentMgrInfo`, `swXmlContents/COMPINSTANCETREE`)
- **Proposed Additions & UX Design**:
  - Add a structured `EntryPreviewSummary` model rendered inside the Preview panel whenever an entry is selected:
    - Header strip: `TypeIcon`, extension/CAD classification, `StatusDescription`, active/latest version indicator.
    - Key metrics grid: formatted size (`FileSizeFormatter`), local modified timestamp (`LocalDateFormatter`), checkout owner, direct parent/child reference count.
    - Highlighted CAD attributes: top custom properties (e.g., *Description*, *Part Number*, *Material*, *Revision*, *Mass* if available in `PropertiesRow` or local `SwStreamStore` streams) and configuration summary.
  - Keep `GetPreviewImageAsync` / `GetLocalPreviewImageAsync` thumbnails prominently centered when present, cleanly collapsing image chrome (no awkward blank space or unhelpful placeholder icon) when unavailable so the structured metadata card fills the preview pane naturally.
- **Implementation Tasks**:
  - [ ] Define `EntryPreviewSummary` projection model built from `EntryRow` + `IEntryDetailsService` + lightweight local `SwFileReader` stream inspection when local-only.
  - [ ] Bind `EntryPreviewSummary` into `HackFileManagerView`'s Preview panel with clean responsive wrapping for narrow docked layouts.
  - [ ] Cancel in-flight metadata extraction cleanly when cursoring rapidly through `Entries` in `HackFileManagerViewModel.ShowEntryDetailsAsync`.
  - [ ] Avoid duplicating RPC calls when the user already has `Properties` / `Version` detail tabs cached for the selected `EntryRow`.
- **Edge Cases & Constraints**:
  - Corrupt or password-protected OLE/CAD streams must fail softly without throwing or overwriting `PreviewStatusText` with noisy stack traces; fall back cleanly to filesystem + entry row attributes.
  - Non-CAD files (PDF, STEP, text, images) display clean generic file/entry summary cards without CAD-specific empty rows.
- **Acceptance Criteria & Verification**:
  - Unit tests covering summary generation across: server entry with bitmap, server entry without bitmap, local-only SolidWorks file, corrupt CAD stream fallback, and cancellation on rapid selection changes.
