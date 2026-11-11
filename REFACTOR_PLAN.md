# HackPDM SRP / Deduplication / Generics Refactor Plan

Status legend: `[ ]` not started · `[~]` in progress · `[x]` done

Every phase ends with the same gate:

1. `dotnet build Src/HackPDM/HackPDM.csproj -f net10.0-desktop -p:Platform=x64 -c Debug` (0 errors, no new warnings)
2. `dotnet test Src/HackPDM.Tests/HackPDM.Tests.csproj` (baseline: 146 passed / 23 skipped without a server; 173/173 against Odoo)
3. `Odoo/ci/run_local_smoke.sh`: Odoo in Docker, integration tests (the 22 skipped run), then the client smoke test
4. One commit per numbered step, so any step can be reverted alone

Line counts are estimates from the survey, not measurements.

---

## Phase 0: Safety net

Goal: make later phases safe to do mechanically.

- [x] Local smoke test: `Odoo/ci/run_local_smoke.sh` (mirrors the CI workflow; `--tests-only`, `--smoke-only`, `KEEP=1`). Passing: 168/168 tests against Odoo, client smoke 7/7 steps. Run it via `sg docker -c Odoo/ci/run_local_smoke.sh` until you log in again after joining the docker group
- [x] Characterization tests in `Src/HackPDM.Tests/Characterization/` (43 tests):
  - `PathConversionTests`: ConvertToWindowsPath, NodePathToWindowsPath, WindowsToOdooPath, GetRelativePath, InPWAFolder, HackDefaults.DefaultPath
  - `OdooRowParsingTests`: JSON → Hashtable/ArrayList (`ToNativeObject`), row → `HpEntry` (`HashConverter`)
  - `EntrySearchDomainTests`: golden search domains (EntrySearchService.cs is compiled into the test project)
  - `HackFileConstructionTests`: the constructors and `GetFromPath`
  - Tests that touch `HackDefaults.Instance` run in a non-parallel `SharedStateCollection`

Found by the first smoke run (fixed):
- Opening a directory never listed its entries: `EnsureVisible` re-raised the tree's `SelectionChanged` for the same node, and each re-raise cancelled the load in flight (a regression from the MVVM refactor; the old handler ignored selections while loading). The view now ignores a selection of the node already loading. Entry-load errors now also go to stderr, not only `Debug`

Found while writing them (pinned as current behaviour):
- `new HackFile(path)` throws `FileNotFoundException` for a missing file (reads `FileInfo.Length`) → fixed in Phase 2
- `HackFile.RelativePath` means two things: `root/<dir>` from the path factories, `<dir>/<file>` from the lazy getter → Phase 5

## Phase 1: Delete dead code — DONE (≈ 5,000 lines removed, no behaviour change)

Each item was found to have zero callers; re-grep immediately before deleting.

**Infrastructure**
- [x] Server-side XML-RPC stack in `RPC/` (XmlRpcServer, Responder, SimpleHttpRequest, SystemObject, BoxcarRequest, Logger, Context)
- [x] Client XML path: `OdooClient.ExecuteAsync`, `OdooClient<T>.ExecuteAsync`, `SendXMLAsync`, then the remaining `RPC/*` (≈1,250 lines total in RPC/)
- [x] `Odoo/DataEnv.cs` (whole file)
- [x] `IHpModelConvert`, `IOdooTransport`, `Test<T>`, `HpCategory.Test`, duplicate `ExplorerItem` in HpDirectory.cs
- [x] `Help.cs`: BatchList, BatchArray, GetResults, OdooIdBecomesKey, ConvertSWFile, ValidateDependency, GetStatusMessage, ConvertValue(Method)
- [x] `ExtensionOdoo.cs`: OdooFieldExtension, Folder/FilePathsToHackWithDependencies, Try* variants, HasLocalVersion, TakeOutLatest, private TryNull
- [x] Model members: HpVersion BatchDownloadFiles/DownloadFilesData/DownloadContentsAll/GetFromPaths/CreateVersion, HpVersionProperty.Create/IsNone, HpVersionRelationship.Create, HpEntry.GetEntryList/GetDependencyPaths, HpNode.UpdateNodeLatestVersions, IrAttachment.GetFileContentsB64, HpBaseModelTransport.GetAllFields/RefreshAsync/GetRelatedRecordsBySearchAsync, OdooDefaults.CreateNewVersion
- [x] Sync `OdooClient` wrappers except the two used (PdmDeleteOperations:172, HpVersion:164 → switch those to async and delete all sync wrappers)
- [x] `SwDocMgr.ReplaceDependencies`; `SwHelper` (never assigns `_swApp`) plus its platform test

**Core / Domain / Shared**
- [x] Row `Clone()` boilerplate (14 classes, ≈190 lines), `IRowData`, `ICloneable<T>`
- [x] Unused ExtensionMethods / ExtensionsAsync / FileOperations / HackDefaults members, MathUtility, empty enums and types, Shared `Help.cs`
- [x] `AsyncHelper.RenewTokenSourceAsync`

**App**
- [x] GridHelp and grid types (after the two `EmptyListItem` callers move to bound collections in Phase 7), `GridMap`, `HackLists`/`HackItemsSource`
- [x] `MessageBoxWindow`, `Configuration/GlobalSettings.cs`, `FileCheck`, `UIConstants`
- [x] Unused members of StatusDialogView, ExtensionForm, SafeHelper, DispatcherExtensions, WindowHelper, InstanceManager, IImageProvider
- [x] ProfileManagerView's private `AbleToLogin` duplicate

**Deliberately left for later phases**
- [ ] `HackFileManagerView._treeItems`: looks unused, but assigning it as `ItemsSource` puts the TreeView in items-source mode, so removing it needs a runtime check (Phase 7)
- [ ] `IHackFileModel` / `IHackBaseFileModel`, `IHackDefaultBase` split, `HpBaseModel.HpModelDictionary` / `UsualIncludedFields`: design leftovers, handled with the HackFile split (Phase 5) and registry cleanup (Phase 4)
- [ ] `FormHelper.EZGradient` and the gradient fields in `ExtensionForm` may now be unused (Phase 6 facade cleanup)
- Note: `HpVersion.DownloadContents` still blocks on `ReadAsync(...).GetAwaiter().GetResult()` (same as the old sync wrapper did); making it async is Phase 5 (`VersionFileService`)

## Phase 2: Fix confirmed bugs — DONE

Each fix is pinned by a test where one could reach it. Gate: 0 build errors, warnings 444 → 430 (none new), 173/173 tests against Odoo, client smoke passing.

Confirmed by reading the code:
- [x] `FileOperations.WriteAllBytesAsync`: the plan's diagnosis was off. The server path (`WinPathway`) already ends with the file name, but uses `\` separators, so on Linux every download landed in the working folder as one file named `Projects\Parts\x.txt`. The file's path was also never set, so the modified date and read-only flag were never applied. Now `LocalTargetPath` normalizes separators (and appends the name only if missing), and the file's path is set before the date and flag. `GetLatestDownloadTests` now checks each file's location, not just its contents
- [x] `HpDirectory.CreateNew`: `directories[nextIndex]` was out of range on the first directory created, so committing into any new folder threw. Also stopped mutating the caller's list
  - found by the new `DirectoryCreationTests`: every new directory was rejected by the server because `default_cat = 1` points at a category that may not exist. Set only when category 1 exists
  - `PdmCommitOperations.CreateDirectoriesAsync` split `RelativePath` on `\` only (and `RelativePath` has two meanings), so on Linux every commit went to root. Now `DirectorySegments` takes the folders from the absolute path (`CommitDirectorySegmentsTests`)
- [x] `OdooDefaults.InitializeAsync`: sequential awaits instead of un-awaited `ContinueWith(async …)`, so the lookup maps exist when init returns; a missing settings row keeps its default instead of throwing
- [x] `PdmCommitOperations`: `commit.ServerClear()` awaited (both rollbacks)
- [x] `HpVersionRelationship.StageRelationshipRecords`: accumulates every version's relationships (was overwritten per version); a null staged version is skipped instead of dereferenced
- [x] `HpBaseModelTransport.GetRecordsByIdsAsync`: the search path requests the asked-for fields (was only `dir_id`, which broke "restore deleted" rows) and no longer mutates the caller's filter list
- [x] StatusDialog: the update loop starts on Loaded and stops on Unloaded (runs until unload, so a cancelled run still shows its final counts); `ParentWindow` attaches the close-confirmation handler when set; `DialogService` sets it

Found by the Phase 0 tests:
- [x] `new HackFile(path)` for a file not on disk: no size, date or checksum instead of throwing (`FromMissingPath_BuildsWithoutSizeOrChecksum`); `ModifiedDate` no longer reports FileInfo's 1601 placeholder

Reported by the survey (all verified first):
- [x] `HackResultTree`: dependencies added through `Add()` so `Parent`/`Tree` are set; constructor children and `Insert` too; `Count`/`IsReadOnly` computed
- [x] `OdooClient<T>.CommandAsync`: an empty response gives `new TReturn()`, not a cast of the model type
- [x] `RecordsPopulation` remap: mutated the Hashtable while enumerating and stored entries under the old key; no caller passed it, so the parameter is removed
- [x] `MultiRecord(int[])`: was self-recursive (stack overflow); builds the record directly
- [x] SolidWorks number properties: boxed as `decimal`, unboxed as `float?` (InvalidCastException); converted with `Convert.ToSingle`
- [x] `ConvertJsonTable` / `ConvertSerializedDict`: accept an already-parsed Hashtable/dictionary instead of returning empty
- [x] App:
  - `ShowStatusWindow` reuses the open window
  - MessageBox awaits the UI work, so a failure reaches the native fallback instead of hanging the caller
  - `SafeInvoker` uses the UI dispatcher off the UI thread (work was dropped) and `SafeInvokerAsync(Action)` fails instead of hanging when the queue rejects work
  - asset map: no double extension, keyed like the built-ins, no throw in async void
  - `HackFile.Equals`/`GetHashCode`: identity is the normalized full path (case-insensitive on Windows), consistent with the hash (`Equality_IsThePath_AndMatchesTheHash`)
  - PdmWorkflowService: download failures are reported as failures, not "Cancelled"
- [ ] Settings pages' `Window?.Close()` is always null: confirmed but harmless (the pages are only shown inline in Configuration, so Submit just saves). Giving Submit visible feedback → Phase 7

Noticed while fixing (not done):
- [x] Intermittent "An error occurred while sending the request" in the live tests (`AddonSeedDataTests` failing with search_count 0 / xml id not found): fixed. Odoo's Werkzeug server answers HTTP/1.0 and closes after each response, but SocketsHttpHandler pooled the connection anyway (even with `Connection: close` or an HTTP/1.0 request); a call that reused it before the FIN arrived got "The response ended prematurely" (about 1% of calls; Odoo never saw them). `SendJsonAsync` now routes a server that answered HTTP/1.0 without keep-alive through a non-pooling client, and `LatestException` keeps the inner exception messages (`RpcConnectionReuseTests` reproduces it offline)
- `HackResultTree.ResultNode` starts `ProcessDependencies()` un-awaited from its constructor (CS4014) → Phase 5

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
- [x] Delete GridHelp (done in Phase 1)

## Phase 8: Generated code

- [ ] Generate the 615-line asset map (`Imaging/Settings.cs`) from the Assets folder at build time
- [ ] SchemaGenerator follow-ups not covered in Phase 5

---

## Order and effort

| Phase | Risk | Est. net lines | Depends on |
|---|---|---|---|
| 0 Safety net ✅ | low | +450 (tests) | – |
| 1 Dead code ✅ | low | ≈ −5,000 (done) | 0 |
| 2 Bug fixes | medium (behaviour changes, intended) | ±0 | 1 |
| 3 Shared primitives | low | −400 | 1 |
| 4 Typed Odoo seam | high | −1,000 | 3 |
| 5 God-class splits | medium | −500 | 4 |
| 6 VM reuse | medium | −800 | 3 (parts need 4) |
| 7 Pages to MVVM | medium | ±0 (moves) | 6 |
| 8 Generated code | low | −600 | – |

Phases 6 and 8 can run in parallel with 4/5 because they touch different projects.
