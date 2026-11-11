# HackPDM SRP / Deduplication / Generics Refactor Plan

Status legend: `[ ]` not started · `[~]` in progress · `[x]` done

Every phase ends with the same gate:

1. `dotnet build Src/HackPDM/HackPDM.csproj -f net10.0-desktop -p:Platform=x64 -c Debug` (0 errors, no new warnings)
2. `dotnet test Src/HackPDM.Tests/HackPDM.Tests.csproj` (baseline: 99 passed / 22 skipped)
3. Smoke test against the CI Odoo container (once Docker access works)
4. One commit per numbered step, so any step can be reverted alone

Line counts are estimates from the survey, not measurements.

---

## Phase 0: Safety net (small)

Goal: make later phases safe to do mechanically.

- [ ] Get the Docker smoke test running locally (blocked on docker group access)
- [ ] Characterization tests for code later phases rewrite:
  - path conversion (absolute ↔ PWA-relative, the "root" prefix rule)
  - `HashConverter.AssignToClass` / `RecordsPopulation` on a sample Odoo row
  - domain building for entry search (current ArrayList output, as a golden value)
  - `HackFile` construction from a local path

## Phase 1: Delete dead code (≈ 3,500–4,000 lines, no behaviour change)

Each item was found to have zero callers; re-grep immediately before deleting.

**Infrastructure**
- [ ] Server-side XML-RPC stack in `RPC/` (XmlRpcServer, Responder, SimpleHttpRequest, SystemObject, BoxcarRequest, Logger, Context)
- [ ] Client XML path: `OdooClient.ExecuteAsync`, `OdooClient<T>.ExecuteAsync`, `SendXMLAsync`, then the remaining `RPC/*` (≈1,250 lines total in RPC/)
- [ ] `Odoo/DataEnv.cs` (whole file)
- [ ] `IHpModelConvert`, `IOdooTransport`, `Test<T>`, `HpCategory.Test`, duplicate `ExplorerItem` in HpDirectory.cs
- [ ] `Help.cs`: BatchList, BatchArray, GetResults, OdooIdBecomesKey, ConvertSWFile, ValidateDependency, GetStatusMessage, ConvertValue(Method)
- [ ] `ExtensionOdoo.cs`: OdooFieldExtension, Folder/FilePathsToHackWithDependencies, Try* variants, HasLocalVersion, TakeOutLatest, private TryNull
- [ ] Model members: HpVersion BatchDownloadFiles/DownloadFilesData/DownloadContentsAll/GetFromPaths/CreateVersion, HpVersionProperty.Create/IsNone, HpVersionRelationship.Create, HpEntry.GetEntryList/GetDependencyPaths, HpNode.UpdateNodeLatestVersions, IrAttachment.GetFileContentsB64, HpBaseModelTransport.GetAllFields/RefreshAsync/GetRelatedRecordsBySearchAsync, OdooDefaults.CreateNewVersion
- [ ] Sync `OdooClient` wrappers except the two used (PdmDeleteOperations:172, HpVersion:164 → switch those to async and delete all sync wrappers)
- [ ] `SwDocMgr.ReplaceDependencies`; `SwHelper` (never assigns `_swApp`) plus its platform test

**Core / Domain / Shared**
- [ ] Row `Clone()` boilerplate (14 classes, ≈190 lines), `IRowData`, `ICloneable<T>`
- [ ] Unused ExtensionMethods / ExtensionsAsync / FileOperations / HackDefaults members, MathUtility, empty enums and types, Shared `Help.cs`
- [ ] `AsyncHelper.RenewTokenSourceAsync`

**App**
- [ ] GridHelp and grid types (after the two `EmptyListItem` callers move to bound collections in Phase 7), `GridMap`, `HackLists`/`HackItemsSource`
- [ ] `MessageBoxWindow`, `Configuration/GlobalSettings.cs`, `FileCheck`, `UIConstants`
- [ ] Unused members of StatusDialogView, ExtensionForm, SafeHelper, DispatcherExtensions, WindowHelper, InstanceManager, IImageProvider
- [ ] ProfileManagerView's private `AbleToLogin` duplicate

## Phase 2: Fix confirmed bugs (small, high value)

Done before restructuring so later phases don't silently carry or change these.

Confirmed by reading the code:
- [ ] `FileOperations.WriteAllBytesAsync`: path is `Combine(root, DirectoryName)` with no file name, and the directory created is its parent
- [ ] `HpDirectory.CreateNew`: writes `directories[nextIndex]` every iteration; should be `[i - nextIndex]` (out of range when more than one level is created)
- [ ] `OdooDefaults.InitializeAsync`: `.ContinueWith(async …)` yields un-awaited `Task<Task>`, so lookup maps fill after init returns; `.First(...)` throws on a missing row
- [ ] `PdmCommitOperations:112,134`: `commit.ServerClear()` not awaited
- [ ] `HpVersionRelationship:119`: `stagedRecords = [...]` overwritten each loop iteration, so only the last version's relationships are staged
- [ ] `HpBaseModelTransport.GetRecordsByIdsAsync`: ignores `fields`, requests only `dir_id`
- [ ] StatusDialog: `UpdateStatusDialogLoop` is never started; `Closing` handler attached while `ParentWindow` is null

Reported by the survey; verify before fixing:
- [ ] `HackResultTree:141`: `Children.Add` bypasses `Add()`, so `Parent` is never set; `Count`/`IsReadOnly` are constant
- [ ] `OdooClient<T>.CommandAsync`: falls back to `default(T)` of the model type, not `TReturn`
- [ ] `HpBaseModelTransport:176-188`: mutates the Hashtable while enumerating it
- [ ] `HpModel.cs:130`: `implicit operator MultiRecord(int[] multi) => multi;` looks self-recursive (stack overflow)
- [ ] `SWDocMgr`: numeric properties boxed as `decimal`, possibly unboxed as `float?` elsewhere
- [ ] `ConvertJsonTable` drops data when the value is already a Hashtable
- [ ] App: ShowStatusWindow opens a new window per call; MessageBox fallback not awaited; SafeInvoker drops off-UI-thread work; settings pages' `Window?.Close()` always null; asset dictionary double extension and `Add` throwing in async void; `HackFile.Equals`/`GetHashCode` inconsistent; bare `catch` in PdmWorkflowService

## Phase 3: Shared primitives (one home per concept)

Goal: each repeated rule lives in exactly one place. Pure moves, so low risk.

- [ ] `PwaPaths` (Core): `ToRelative`, `ToAbsolute`, `ToTreeSegments`, root prefix constant. Replaces ≈8 copies and the magic `5` in 3 places
- [ ] Split `FileOperations` into `Checksums`, `LocalFileWriter`, `ShellLauncher` (one shell-open switch)
- [ ] `SwDependencies.PwaDependencyPaths(absPath)`: replaces 5 copies (ExtensionOdoo, HpEntry, HpVersionRelationship ×2, HackResultTree)
- [ ] One SolidWorks doc-type map (4 copies) and one property-type switch (2 copies in SWDocMgr)
- [ ] One size formatter, one id-list helper (`ToArrayListIDs` vs `GetIDs`), one model-name map (3 copies), one `OdooFieldType` enum, one operator mapping, one `IsExternalInit`
- [ ] `Help.BatchList/BatchArray` → `Enumerable.Chunk`
- [ ] `OdooDefaults` lookup maps: four hand-written loops → `ToDictionary`
- [ ] App: `IUiDispatcher` replaces the 5 marshalling mechanisms (`UiDispatcher.Post`, SafeHelper, `HackDispatcherQueue` static, DispatcherExtensions, direct `DispatcherQueue` use)

## Phase 4: Typed Odoo seam (generics instead of object/ArrayList/Hashtable)

The biggest and riskiest phase, so it is split. In 4a/4b old and new APIs coexist; 4c deletes the old ones.

**4a. Introduce the types**
- [ ] `record DomainTerm(string Field, string Op, object? Value)` and `Domain : IReadOnlyList<DomainTerm>` (with `&`/`|` builders)
- [ ] Ids as `IReadOnlyList<int>` everywhere
- [ ] One transport: `Task<Result<T>> CallAsync<T>(string service, string method, IReadOnlyList<object?> args, CancellationToken ct)`; `Result<T>` replaces the static `LatestException`
- [ ] Typed verbs on top: `SearchAsync`, `ReadAsync<T>`, `SearchReadAsync<T>`, `CreateAsync`, `WriteAsync`, `UnlinkAsync`
- [ ] `Repository<T> where T : HpBaseModelTransport<T>, new()` with one `FetchAsync` (replaces ≈5 fetch → populate copies) and one `Assign<T>` (replaces the 3 `HashConverter` loops)
- [ ] DTO records: `StagedVersionPayload`, `DirectoryEntriesDto`, `SubdirectoryDto`, `RemoteEntryDto`, `SwProperty`
- [ ] Write payloads as `Dictionary<string, object?>` (the generated `ToOdoo()` already returns one)

**4b. Migrate callers**
- [ ] Operations, models, `EntrySearchService`, `EntryListService`, `DirectoryTreeService`, `PdmWorkflowService`
- [ ] `IPdmWorkflowService` signatures take `IReadOnlyList<int>` instead of `ArrayList`
- [ ] `dir_id as ArrayList[1]` → `Many2One` helper; app Hashtable rows → `RemoteEntryDto`
- [ ] Drop the hand-written type → model registry in HpBaseModelTransport (use the generated `HpModelDictionaryGen`)

**4c. Delete the old surface**
- [ ] The 4× wrapper matrix in `OdooRpcCs.cs` (≈250 lines), `ToNativeObject`/`ConvertFromHt`, ArrayList extensions, `HashedValues` Hashtable, `RecordsPopulation`
- [ ] Collapse `MultiRecord`/`Many2Many`/`One2Many` operator triplication into one generic base

## Phase 5: Split the god classes (SRP)

- [ ] `ExtensionOdoo.cs` (779 lines) → `OdooTransport`, `DependencyScanner`, `OdooValueConverter`
- [ ] `OdooRpcCs.cs` → `OdooAuth`, `OdooConnectivity`, `OdooRpcClient`
- [ ] `OdooDefaults` → `OdooConnectionSettings` + immutable `OdooLookupCache` built by one loader
- [ ] Hp* models become data-only; behaviour moves to `VersionFileService` (move/download/write), `CheckoutService`, and Operations
- [ ] `HackFile` split: metadata record + `Checksums` + `LocalFileWriter` + version matcher + `PwaPaths`; one factory replaces the 5 construction copies
- [ ] `Help.cs` split by concern (or emptied by the moves above)
- [ ] Operations: `DeleteWhereAsync<T>(Domain, label, log)` replaces 3 copies; one logical (un)delete; one checkout loop that uses `HpEntry.CanCheckOut`; `HpEntry.CheckOut`/`UnCheckOut` share the read-only toggle
- [ ] `HpVersionProperty.FromSwProperty(...)` replaces the two near-identical builders; one `HpVersion.PrepareCreation`/`StageVersion`
- [ ] SchemaGenerator: shared emitter, type → emitter map instead of `t.Name` switches

## Phase 6: ViewModel and command reuse

- [ ] `RunOperationAsync(string title, Func<IOperationReporter, CancellationToken, Task<bool>>)` in a VM base: replaces the ShowWindow → op → Completed pattern (≈8 copies in HackFileManagerViewModel)
- [ ] Generic history command (6 copies), shared Checkout/UnCheckout body, detail-tab dictionary instead of a switch
- [ ] `RemoteIds` / `SelectedRemoteIds` and their guards unified
- [ ] `ReplaceAll` on collections instead of repeated clear/fill
- [ ] MessageBox table-driven (662 → ≈150 lines), all calls through `IDialogService`
- [ ] Remove the pass-through facades (FormHelper, SldWorksUI), `LazyConverter`, OdooEntryColumns literals/brushes into resources
- [ ] Group key typed (`Func<EntryRow, IComparable?>` or a `GroupKey` enum) instead of `Func<EntryRow, object>` plus reflection

## Phase 7: Remaining pages to MVVM and a single composition root

- [ ] Settings views bind to their existing (currently unused) view models
- [ ] ProfileManager: `ProfileManagerViewModel` + `ProfileStore`
- [ ] StatusDialog: view model owns the log; no polling in the view
- [ ] Home: `INavigationService`
- [ ] JSON editor: I/O moves to a service
- [ ] `IFilePickerService` replaces the duplicated pickers
- [ ] `HackFileManagerView`: columns and context menus in XAML instead of code-behind
- [ ] Merge `InstanceManager` and `ViewModelEnvironment` into `AppComposition`; `GlobalSerializedData` and `ExtensionForm` responsibilities split
- [ ] Delete GridHelp (Phase 1 leftover)

## Phase 8: Generated code

- [ ] Generate the 615-line asset map (`Imaging/Settings.cs`) from the Assets folder at build time
- [ ] SchemaGenerator follow-ups not covered in Phase 5

---

## Order and effort

| Phase | Risk | Est. net lines | Depends on |
|---|---|---|---|
| 0 Safety net | low | +300 (tests) | – |
| 1 Dead code | low | −3,500 to −4,000 | 0 |
| 2 Bug fixes | medium (behaviour changes, intended) | ±0 | 1 |
| 3 Shared primitives | low | −400 | 1 |
| 4 Typed Odoo seam | high | −1,000 | 3 |
| 5 God-class splits | medium | −500 | 4 |
| 6 VM reuse | medium | −800 | 3 (parts need 4) |
| 7 Pages to MVVM | medium | ±0 (moves) | 6 |
| 8 Generated code | low | −600 | – |

Phases 6 and 8 can run in parallel with 4/5 because they touch different projects.
