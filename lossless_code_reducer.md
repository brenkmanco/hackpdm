# Lossless Code Reduction Roadmap for `main`

This guide documents concrete, **zero-reflection** refactorings the `main` branch of HackPDM can adopt to cut roughly **two-thirds of its production client C# SLOC** and significantly improve runtime speed and build times **without removing any user-facing feature, customizability, or configurability**.

Every baseline line count below was measured directly from the pre-reduction `main` baseline (`HEAD` prior to `reduce_codebase_essential`), and every architectural refactoring (items 2–8 and 11) has already been implemented and verified against the 272-test non-integration unit suite and both Release targets (`net10.0-desktop` and `net10.0-windows10.0.26100.0` x64) on this branch.

---

## 1. Measurement Methodology & Anti-Cramming Metrics

To track reduction honestly without counting comments/markup or rewarding crammed lines, track all of the following together:

| Metric | Definition |
| --- | --- |
| **Production C# SLOC** | Non-blank, non-comment C# lines in `Src/` excluding `Src/HackPDM.Tests/`, `bin/`, `obj/`, and generated files. Excludes `//`, `///` XML docs, `/* ... */` blocks, and XAML. |
| **Physical C# Lines** | Raw line count across production `.cs` files (checks comment/whitespace ratio). |
| **Line-Length Distribution** | Mean, median (`p50`), `p90`, `p95`, and `p99` line lengths (tab = 4 spaces) on SLOC lines. Healthy idiomatic C# stays around mean 35–48 chars and `p95` ≤ 115 chars. |
| **Project & File Count** | Production `.csproj` count and `.cs` / `.xaml` file counts (measures navigation & build-graph complexity). |
| **XAML Lines** | Total lines across production `.xaml` files. |
| **External Dependencies** | Direct non-SDK NuGet packages and checked-in `.dll` binaries. |
| **Runtime Reflection Sites** | Count of `System.Reflection` member lookups (`GetProperty`, `GetMethod`, `GetField`, `Activator.CreateInstance`, etc.) in production code. Target: **0** (excluding assembly version metadata). |

---

## 2. Summary Comparison

| Metric | `main` Baseline | `main` After Lossless Reduction | `reduce_codebase_essential` (This Branch) |
| --- | ---: | ---: | ---: |
| **Production C# SLOC** | **56,686** | **~19,606** *(−65.4%)* | **14,013** *(−75.3%)* |
| **Production C# Files** | 261 | ~125 *(−52%)* | 97 *(−62.8%)* |
| **Production Projects** | 7 | 2 *(−71%)* | 2 *(−71%)* |
| **XAML Files / Lines** | 20 / 5,665 | 20 / ~4,800 *(−15%)* | 13 / 1,804 *(−68.2%)* |
| **Non-SDK NuGet / DLL Refs** | 12 | 3 *(−75%)* | 3 *(−75%)* |
| **Features Preserved** | All | **100% of `main`** *(themes, JSON editor, graph, dockable workspace, columns, PiP, file types, smoke tests)* | Core PDM essentials only |

> **Why `reduce_codebase_essential` is ~5,600 SLOC smaller than lossless-reduced `main`:**
> That delta is strictly the stripped optional subsystems themselves (after lossless cleanup):
> - Dependency Node Graph (`GraphLayout` + `DependencyGraphView` + `NodeGraphView` + `GraphTheme` + `DependencyGraphLoader`): ~2,200 SLOC
> - Custom Theme Engine & Editor (`ThemeService` + `ThemeEditorView*` + `ThemePresets` + `ThemeSettings`): ~840 SLOC
> - Interactive JSON Editor (`JsonEditorView*` + `JsonNodeViewModel` + `AppConfigJsonService`): ~450 SLOC
> - Dockable/Rearrangeable Workspace Layout (`HomeView` + `WorkspaceLayoutSettings` + `MinimalStatusBar`): ~310 SLOC
> - PiP Window, File Type Manager, `TreeBranchGuides`, `NavigationTransitions`, `StatusSettings`, and configurable columns: ~1,800 SLOC

---

## 3. Lossless Reductions (Non-Overlapping Savings)

Every item below has **zero overlap** with any other item so the savings add up cleanly.

| # | Refactoring | Baseline SLOC | Projected Savings | Features Lost | Performance / Build Impact |
| --- | --- | ---: | ---: | --- | --- |
| 1 | Replace `ColorNames.cs` with algorithmic hex + ~140 CSS/WinUI names | 31,882 | **−31,730** | None | Eliminates 30k+ static table init & >1 MB assembly metadata |
| 2 | Collapse 7 projects into 2 + delete mirror `IHp*` interfaces, dead structures, unused models, & duplicate DTOs | ~2,100 | **−1,505** | None | 5 fewer MSBuild projects; removes linked-file hacks in tests |
| 3 | Replace Roslyn `SchemaGenerator` + boxing/reflection writes with `IOdooModel<TSelf>` | ~1,200 | **−350** | None | Zero reflection, zero boxing, removes Roslyn analyzer build step |
| 4 | Unify dual config stores (`settings.json` + `AppConfig.json`) into one typed `System.Text.Json` store | ~550 | **−320** | None | Removes `Newtonsoft.Json` & `ConfigurationManager`; one file for JSON editor |
| 5 | Replace `HackResultTree` + `ResultHackFile` with direct DFS `DependencyScanner` | 320 | **−205** | None | Fewer heap allocations per SolidWorks assembly scan |
| 6 | Remove `AsyncHelper.AsyncRunner` 500 ms polling loop | 92 | **−70** | None | Eliminates **0–500 ms artificial latency** on every PDM operation |
| 7 | Direct status reporting (drop `WeakReferenceMessenger`, `StatusLogSession` finalizer, `ObservableRingBuffer`) | 996 | **−400** | None | Removes GC finalizer pressure & per-log messenger allocations |
| 8 | Cache `StatusBrushes` per `(FileStatus, IsDark)` with `ThemeChanged` invalidation | 97 | **−30** | None | Eliminates per-row `LinearGradientBrush` allocations during scrolling |
| 9 | Deduplicate UI/ViewModel/Layout/Theme/Graph/JSON boilerplate | ~6,800 | **−1,730** | None | Less XAML/code-behind duplication; faster graph layout via structs |
| 10 | Move `SmokeTestRunner` + `Png` to a dedicated test harness project | 740 | **−740** | None | Keeps production client binary clean while preserving UI smoke tests |
| 11 | Remove 9 unused NuGet/DLL dependencies + dead tracked files | — | *(build/size)* | None | Smaller publish output, faster restore & JIT startup |
| | **Total Lossless Reduction** | **56,686** | **−37,080** | **Zero** | **~19,606 SLOC remaining (−65.4%)** |

---

## 4. Detailed Refactoring Playbook

### 4.1 Replace `ColorNames.cs` (Saves ~31,730 SLOC)
- **Baseline:** `Src/HackPDM.Abstractions/Structures/ColorNames.cs` is **31,882 SLOC** — 56.2% of the entire production C# codebase — consisting of tens of thousands of compiled static color entries.
- **Why it is lossless to replace:** Theme customization stores and edits RGBA / `#AARRGGBB` hex values and preset palettes. Users do not need 30,000 obscure color-name constants compiled into C# IL.
- **Zero-reflection replacement (~150 SLOC):**
  - Keep standard hex parsing/formatting (`#RGB`, `#RRGGBB`, `#AARRGGBB`).
  - Maintain a compact `static readonly (string Name, uint Argb)[]` table of the ~140 standard CSS / `Windows.UI.Colors` named colors.
  - If reverse color-name lookup is desired in the Theme Editor tooltip, compute nearest Euclidean RGB distance across those ~140 entries (takes microseconds, zero allocations). If an extended color-name catalog is ever genuinely required, ship it as a compressed `.json.gz` asset loaded on demand when the Theme Editor opens rather than 31k lines of compiled C#.

### 4.2 Collapse 7 Production Projects into 2 & Eliminate Boundary Boilerplate (Saves ~1,505 SLOC, −5 Projects)
- **Baseline:** `HackPDM.Shared` (490 SLOC), `HackPDM.Abstractions` (517 SLOC excl. `ColorNames`), `HackPDM.Domain` (812 SLOC), `HackPDM.Core` (3,326 SLOC), `HackPDM.Infrastructure` (5,332 SLOC), `HackPDM.SchemaGenerator` (257 SLOC), and `HackPDM` (14,070 SLOC).
- **Root cause of bloat:** Splitting a single desktop client across 6 library projects created artificial circular-dependency boundaries that forced:
  - **22 `IHp*` mirror interfaces** in `HackPDM.Domain/OdooModels/Models/` (339 SLOC across 22 files) plus `IHackDefaults` (12), `IOdooDefaults` (52), `HpBaseModel` (27), and empty `DataModel` (1) = **431 SLOC** whose only purpose was letting `Core` talk to `Infrastructure`.
  - **Unused data structures & polyfills**: `BiMap.cs` (169), `Except.cs` (96), `ConcurrentSet.cs` (140), `TypeRepresentative.cs` (49), `Representative.cs` (21), `MathUtility.cs` (19), `CallbackDelegations.cs` (6), `NullableAttributes.cs` (25 — built into .NET 10), `IsExternalInit.cs` (2 — built into .NET 10) = **527 SLOC**.
  - **Unused Odoo models**: `HpRelease`, `HpReleaseReview`, `HpReleaseVersionRel`, and `HpTest` (**87 SLOC** in `Infrastructure`; `HpTest` is excluded from production Odoo installs anyway).
  - **Duplicate row DTO layers**: `HackPDM.Domain/Representation/*Row.cs` (15 files, 209 SLOC) overlapped with `HackPDM/Mvvm/Models/Hack/EntryRow.cs` (55), `EntryRowKeys.cs` (38), and `DomainUI.cs` (180). Unifying all row models in `HackPDM.Core/Services/RowModels.cs` removes the duplicate `EntryRow` mapping layer (~180 SLOC net saved, 16 fewer files).
  - **Dead wrappers & helpers**: ~160 SLOC of unused helpers in `ExtensionMethods.cs` (250 SLOC), `TryHelper.cs` (40), `OdooFieldSelector.cs` (31), `Repository.cs` (51), and `BindableMetadata.cs` (45) = **~280 SLOC**.
- **Target layout (already proven on this branch):**
  - `Src/HackPDM.Core/` — pure .NET 10 library (no UI framework dependency) with clean feature folders: `Configuration/`, `Credentials/`, `Odoo/` (and `Odoo/Models/`), `Pdm/`, `Services/`, `SolidWorks/` (and `SolidWorks/Format/`), plus `Graph/` and `Theming/` on `main`.
  - `Src/HackPDM/` — Uno / WinUI presentation layer organized by feature (`Common/`, `Shell/`, `FileManager/`, `Search/`, `Login/`, `Settings/`, `Status/`, plus `Graph/`, `Themes/`, `JsonEditor/` on `main`).
  - `Src/HackPDM.Tests/` — references `HackPDM.Core` directly, eliminating the fragile `<Compile Include="..\HackPDM\..." Link="..." />` workarounds.

### 4.3 Zero-Reflection `IOdooModel<TSelf>` Instead of Roslyn `SchemaGenerator` (Saves ~350 SLOC, −1 Analyzer Project)
- **Baseline:** `HackPDM.SchemaGenerator` (257 SLOC + Roslyn packages) generated field lists for models, while `OdooValueConverter.cs` (78), `HpBaseModelTransport.cs` (137), `HpModel.cs` (108), and `OdooModelAttribute`/`OdooPropAttribute` (21) boxed values and still fell back to runtime reflection (`GetType().GetProperty(...)`) in `WriteChangedValuesAsync`.
- **Zero-reflection pattern (implemented in `Src/HackPDM.Core/Odoo/Models/HpRecord.cs`):**
  ```csharp
  public interface IOdooModel<TSelf> where TSelf : HpRecord<TSelf>, IOdooModel<TSelf>, new()
  {
      static abstract string ModelName { get; }
      static abstract string[] AllFields { get; }
  }
  ```
  Each model implements `TrySetField(string field, object? value)` via a compiler-optimized `switch` (which Roslyn compiles to a hash-jump table) and `ToOdoo()` returning its typed `Dictionary<string, object?>`. Consolidate the 11 tiny lookup models (`HpCategory`, `HpType`, `HpProperty`, `HpSetting`, `HpUser`, `HpNode`, `HpEntryNameFilter`, `HpCategoryProperty`, `HpVersionRelationship`, `HpRecordStaged`, `IrAttachment`) into a single `HpLookupModels.cs` file.
- **Win:** Zero reflection, zero boxing of typed primitives, eliminates `Microsoft.CodeAnalysis.CSharp*` analyzer compilation from every build.

### 4.4 Unify Dual Config Stores into One `System.Text.Json` `settings.json` (Saves ~320 SLOC)
- **Baseline:** Two parallel persistence systems existed simultaneously:
  1. `JsonFileSettingsProvider` + `CoreSettings` + `AppSettingsStore` writing `settings.json`.
  2. `AppConfigStore` (34) + `UIStorage` (42) + `GlobalSerializedData` (18) + `CustomSerializers` (84) + `Properties/Settings.Designer.cs` writing `AppConfig.json` via `Newtonsoft.Json` and `System.Configuration.ConfigurationManager`.
- **Lossless replacement:** Keep **every** configurable setting (`ColorSettings`, `ThemeSettings`, `WindowSettings`, `WorkspaceLayoutSettings`, `OdooEntryColumns`, `GroupByField`, status log limits, profile preferences) as strongly-typed sections in one `settings.json` powered by `System.Text.Json` (with a `[JsonSerializable]` source-generated `JsonSerializerContext` for zero-reflection serialization). Point `JsonEditorView` directly at that single unified `settings.json` — users get *better* configurability because there is only one config file to inspect or edit.

### 4.5 Replace `HackResultTree` + `ResultHackFile` with Direct DFS `DependencyScanner` (Saves ~205 SLOC)
- **Baseline:** `HackResultTree.cs` (200) + `ResultHackFile.cs` (72) + `DependencyScanner.cs` (48) = **320 SLOC** wrapping a simple SolidWorks dependency walk in a custom generic N-ary tree container.
- **Lossless replacement (implemented in `Src/HackPDM.Core/Pdm/DependencyScanner.cs`, 123 SLOC):** A direct recursive DFS using `HashSet<string> visited` and `HashSet<string> activeBranch` (for cycle detection) returning `(bool Success, List<HackFile> Files, List<BrokenDependency> Broken)`. Preserves every error case (`MissingFile`, `OutsideVault`, `CorruptDepFile`, `CircularDependency`) with a fraction of the allocations.

### 4.6 Eliminate `AsyncHelper.AsyncRunner` 500 ms Polling Loop (Saves ~70 SLOC + Latency Win)
- **Baseline:** `Src/HackPDM.Core/AsyncHelper.cs` (92 SLOC) ran background work inside a `while (!task.IsCompleted) await Task.Delay(500)` polling loop!
- **Lossless replacement:** `await Task.Run(function, token)` directly.
- **Impact:** Removes **0–500 ms of wasted wall-clock wait** from every single checkout, commit, download, delete, and search operation, and stops allocating periodic timer tasks.

### 4.7 Direct Status Reporting Without `WeakReferenceMessenger`, Finalizers, or Custom Ring Buffers (Saves ~400 SLOC)
- **Baseline:** `StatusWindowReporter` → `WeakReferenceMessenger.Default.Send(...)` → `StatusLogSession` (with a GC finalizer `~StatusLogSession()`) → `ObservableRingBuffer<T>` (166 SLOC) → `StatusDialogViewModel` (365 SLOC) → `StatusDialogView.xaml.cs` (378 SLOC).
- **Lossless replacement:**
  - Wire `StatusWindowReporter` directly to `StatusDialogViewModel` (or a thin `Action<StatusEntry>` sink) — no messenger lookup, no weak-reference indirection, and no GC finalizer queue overhead.
  - Replace the 166-line `ObservableRingBuffer<T>` with a 15-line bounded cap check on `ObservableCollection<StatusEntry>` respecting `StatusSettings.MaxEntries` (batch-trim oldest items when `Count > max`).
  - Deduplicate `StatusDialogView` filter/sort/export logic between Windows and Uno Desktop (~200 SLOC saved) while preserving full filtering, search, copy, export, and `StatusSettingsView`.

### 4.8 Cache `StatusBrushes` With `ThemeChanged` Invalidation (Saves ~30 SLOC + Scroll Performance)
- **Baseline:** `StatusColorConverters.cs` (74 SLOC) + `ThemeConverters.cs` (23 SLOC) constructed new `LinearGradientBrush`, `GradientStopCollection`, and `SolidColorBrush` instances on every cell render/recycle during `EntryTableView` / `ListView` scrolling.
- **Lossless replacement:** Cache pre-built brushes in a small array indexed by `(int)FileStatus` (for both Light and Dark themes) and clear/rebuild that small cache inside `ThemeService.ThemeChanged`. Keeps 100% live custom theme color updates with **zero per-row allocations while scrolling**.

### 4.9 Deduplicate UI, ViewModel, Layout, Theme, Graph, and JSON Editor Boilerplate (Saves ~1,730 SLOC + ~720 XAML Lines)
The top hotspots in `main` contain substantial structural duplication that can be factored out without removing a single feature:

1. **`HackFileManagerView.xaml.cs` (1,514 SLOC) & `HackFileManagerView.xaml` (1,729 lines) → ~450 SLOC + ~600 XAML saved:**
   - Currently duplicates toolbar/context-menu wiring, column persistence, selection handling, and detail-tab updates across `#if WINDOWS` (`TableView`) and `#else` (`ListView`).
   - Extract a tiny internal `IEntryListHost` adapter (4 methods: `GetSelectedEntries()`, `ApplyGrouping()`, `ApplyColumnLayout()`, `ScrollToEntry()`) with a ~60-line Windows `TableView` implementation and ~60-line Desktop `ListView` implementation so the remaining menu, keyboard, drag/drop, and detail-tab logic is written **once**.
2. **`HackFileManagerViewModel.cs` (920 SLOC) → ~200 SLOC saved:**
   - Collapse repeated `try / IsBusy / report error / refresh` blocks across 15+ commands into one `RunOperationAsync(string title, Func<CancellationToken, Task> action)` helper, and forward state reads directly to `FileManagerState` without redundant pass-through properties.
3. **`HomeView.xaml.cs` (466 SLOC) + `WorkspaceLayoutSettings.cs` (93 SLOC) → ~250 SLOC + ~120 XAML saved:**
   - Left, Right, and Bottom drawer dock/undock/popout/splitter state is copy-pasted per slot. Factor a single reusable `DockableDrawerSlot` controller parameterized by `DrawerEdge` (`Left`, `Right`, `Bottom`). Keeps 100% of rearrangeable/dockable/popout/minimal-status-bar configurability.
4. **`ProfileManagerViewModel.cs` (632 SLOC) → ~180 SLOC saved:**
   - Consolidate duplicated field-copy, validation, and dirty-check logic across Create / Edit / Duplicate / Switch profile flows into a single `ProfileDraft` value type and one `ValidateDraft()` method.
5. **`GraphLayout.cs` (1,178 SLOC) + `DependencyGraphView.xaml.cs` (595 SLOC) + `NodeGraphView.cs` (509 SLOC) → ~300 SLOC saved:**
   - Merge repeated crossing-minimization / coordinate-assignment pass helpers and replace per-iteration class allocations with `readonly struct` / pooled arrays. Reduces code size AND cuts GC pauses on large SolidWorks assembly graphs.
6. **`ThemeService.cs` (292 SLOC) + `ThemeEditorViewModel.cs` (192 SLOC) + `ThemePresets.cs` (98 SLOC) → ~200 SLOC saved:**
   - Replace 25+ manually repeated color property getters/setters/apply/reset blocks with a compile-time `enum ThemeToken` + fixed `Color[TokenCount]` array + `static readonly string[] ResourceKeys` lookup table (zero reflection!). Keeps 100% of custom themes, presets, and live color editing.
7. **`JsonNodeViewModel.cs` (340 SLOC) + `JsonEditorView*` (226 SLOC) → ~150 SLOC saved:**
   - Wrap `System.Text.Json.Nodes.JsonNode` directly instead of duplicating parallel value-type parsing/formatting code. Keeps full interactive tree editing, validation, and save/revert.

### 4.10 Move `SmokeTestRunner` & `Png` Out of the Production App (Saves 740 Production SLOC)
- **Baseline:** `Src/HackPDM/Shell/Testing/SmokeTestRunner.cs` (660 SLOC) and `Png.cs` (80 SLOC) are compiled directly into the shipping `HackPDM.exe` binary.
- **Lossless replacement:** Move them into a separate `Src/HackPDM.SmokeTests` harness project (or conditionally compile only in a `SmokeTest` build configuration). Preserves 100% of automated screenshot/UI smoke testing while removing 740 SLOC from production binaries.

### 4.11 Remove 9 Unused NuGet/DLL Dependencies & Dead Tracked Files
Confirmed unused even in `main` (`HEAD` grep verified zero C#/XAML code references):

| Dependency / File | Status on `main` | Action |
| --- | --- | --- |
| `Newtonsoft.Json` | Superseded by built-in `System.Text.Json` | Remove after item 4.4 |
| `System.Reactive` | 0 references in `Src/` | Delete immediately |
| `RestSharp` | 0 references (`OdooTransport` uses `HttpClient`) | Delete immediately |
| `XmlRpcCs.dll` | Dead legacy binary (client uses JSON-RPC) | Delete immediately |
| `Microsoft.Extensions.DependencyInjection` | `AppComposition` does fast manual DI | Delete immediately |
| `Microsoft.Graphics.Win2D` | 0 references in `.cs` or `.xaml` (`NodeGraphView` does not use Win2D) | Delete immediately |
| `CommunityToolkit.WinUI.Controls.Sizers` | 0 references (custom `PaneSplitter` is used) | Delete immediately |
| `CommunityToolkit.WinUI.Extensions` | 0 references | Delete immediately |
| `System.Configuration.ConfigurationManager` | Legacy `.NET Framework` config reader | Remove after item 4.4 |
| `Microsoft.CodeAnalysis.CSharp*` | Only used by `SchemaGenerator` + test | Remove after item 4.3 |
| `Src/HackPDM/structure.txt` (7,557 lines), `*.backup`, `Properties/Settings.*` | Stale generated/backup files | Delete immediately |

---

## 5. Recommended Incremental Adoption Order on `main`

For minimal merge risk, apply these in 4 small PRs:

1. **PR 1 — Quick Wins (~32,000 SLOC removed, < 1 day):**
   - Delete unused NuGet packages/DLLs & dead tracked files (4.11).
   - Replace `ColorNames.cs` (4.1).
   - Remove `AsyncHelper` 500 ms polling loop (4.6).
   - Cache `StatusBrushes` with `ThemeChanged` invalidation (4.8).
2. **PR 2 — Core & Odoo Consolidation (~2,060 SLOC removed, 1–2 days):**
   - Merge `Shared` / `Abstractions` / `Domain` / `Infrastructure` into `HackPDM.Core` (4.2).
   - Replace `SchemaGenerator` with `IOdooModel<TSelf>` (4.3).
   - Replace `HackResultTree` / `ResultHackFile` with direct DFS `DependencyScanner` (4.5).
   - Move `SmokeTestRunner` + `Png` to a test harness project (4.10).
3. **PR 3 — Unified Config & Direct Status Pipeline (~720 SLOC removed, 1 day):**
   - Unify `AppConfig.json` into `settings.json` via `System.Text.Json` source generation and point `JsonEditorView` at it (4.4).
   - Replace `WeakReferenceMessenger` + `StatusLogSession` + `ObservableRingBuffer` with direct callback + bounded collection (4.7).
4. **PR 4 — UI / Layout / Theme / Graph Deduplication (~1,730 SLOC + ~720 XAML removed, 2–3 days):**
   - Apply the targeted UI/ViewModel factorings in section 4.9 while keeping all features and tests green.
