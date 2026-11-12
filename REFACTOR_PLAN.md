# HackPDM SRP / Deduplication / Generics Refactor Plan

Status legend: `[ ]` not started · `[~]` in progress · `[x]` done

Every phase ends with the same gate:

1. `dotnet build Src/HackPDM/HackPDM.csproj -f net10.0-desktop -p:Platform=x64 -c Debug` (0 errors, no new warnings)
2. `dotnet test Src/HackPDM.Tests/HackPDM.Tests.csproj` (current: 182 passed / 37 skipped without a server; 223/223 against Odoo. The Phase 0 baseline was 146 / 23 and 173/173)
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
- `HackFile.RelativePath` means two things: `root/<dir>` from the path factories, `<dir>/<file>` from the lazy getter → still open (see Audit)

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
- [x] Unused ExtensionMethods / ExtensionsAsync / FileOperations / HackDefaults members, unused `MathUtility` members (the class stays: `AsyncHelper` uses it), empty enums and types, Shared `Help.cs`
- [x] `AsyncHelper.RenewTokenSourceAsync`

**App**
- [x] GridHelp and grid types (after the two `EmptyListItem` callers move to bound collections in Phase 7), `GridMap`, `HackLists`/`HackItemsSource`
- [x] `MessageBoxWindow`, `Configuration/GlobalSettings.cs`, `FileCheck`, `UIConstants`
- [x] Unused members of StatusDialogView, ExtensionForm, SafeHelper, DispatcherExtensions, WindowHelper, InstanceManager, IImageProvider
- [x] ProfileManagerView's private `AbleToLogin` duplicate

**Deliberately left for later phases**
- [ ] `HackFileManagerView._treeItems`: looks unused, but assigning it as `ItemsSource` puts the TreeView in items-source mode, so removing it needs a runtime check (Phase 7)
- [x] `IHackFileModel` / `IHackBaseFileModel` and `HpBaseModel.UsualIncludedFields` deleted (implemented, never used); `HpModelDictionary` replaced by the generated `HpModelDictionaryGen` (Phase 4). Still open: the `IHackDefaultBase` / `IHackDefaults` split (see Audit)
- [x] `FormHelper`, `ExtensionForm` (Phases 6–7) and the unused `ColorDrawingHelper` (the last `EZGradient`) deleted
- [x] `HpVersion.DownloadContents` blocked on `ReadAsync(...).GetAwaiter().GetResult()`: now `VersionFileService.DownloadContentsAsync` / `DownloadFileAsync` / `DownloadAllAsync`, awaited all the way up (done in the Audit)

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
- [x] Settings pages' `Window?.Close()` is null when the page is shown inline in Configuration: Submit now shows a "saved" message (Audit)

Noticed while fixing (not done):
- [x] Intermittent "An error occurred while sending the request" in the live tests (`AddonSeedDataTests` failing with search_count 0 / xml id not found): fixed. Odoo's Werkzeug server answers HTTP/1.0 and closes after each response, but SocketsHttpHandler pooled the connection anyway (even with `Connection: close` or an HTTP/1.0 request); a call that reused it before the FIN arrived got "The response ended prematurely" (about 1% of calls; Odoo never saw them). `SendJsonAsync` now routes a server that answered HTTP/1.0 without keep-alive through a non-pooling client, and `LatestException` keeps the inner exception messages (`RpcConnectionReuseTests` reproduces it offline)
- [x] `HackResultTree.ResultNode` started `ProcessDependencies()` un-awaited from its constructor (CS4014): no caller asked for it, so the constructor flag is gone and dependencies are only resolved by awaiting `ProcessDependencies` (Audit)

## Phase 3: Shared primitives (one home per concept)

Goal: each repeated rule lives in exactly one place. Pure moves, so low risk.

- [x] `PwaPaths` (Core): `Segments`, `ToRelative`, `ToAbsolute`, `FolderOf`, `LocalSegments`, `IsInside`, `ToServerName`, `ToServerDirectory`, plus the `Root` / separator constants. Replaced `ConvertToWindowsPath`, `NodePathToWindowsPath`, `WindowsToOdooPath`, `GetRelativePath`, `InPWAFolder`, `HackDefaults.DefaultPath`, `HpVersion.FolderOf`, `HpVersionRelationship.VaultWindowsName`, `EntryFileService.RootPrefixLength` and the magic `5` (`ODOO_PATH_PREFIX_LENGTH`)
  - Behaviour change: `IsInside` is boundary-aware (`/vault2/x` is no longer "inside" `/vault`), and a path outside the working folder now has no server name (null) instead of a mangled one
- [x] Split `FileOperations` into `Checksums`, `LocalFileWriter`, `ShellLauncher` (one shell-open switch). `FileOperations` keeps only the Base64 helpers (the schema generator emits calls to them) and `FilesInDirectory`
- [x] `SwDependencies.Of(file, resolver)`: a SolidWorks file's references with where each resolves locally. The 5 copies had already shrunk to 2 (HackResultTree, HpVersionRelationship) with the reference resolver; both use it now
- [x] One SolidWorks doc-type map (`SwDocumentTypes`: `Of`, `IsSolidWorks`, `IsSolidWorksExtension`) replaces `SwDocMgr.GetTypeFromString`, `SwFileReader.IsSolidWorksFile`, `Help.GetSwDmDocumentTypeFromExtension` and `OdooDefaultsConstants.DependentExt`/`DependentExtRegex`; one property-type switch (`SwDocMgr.ConvertProperty`). Deleted the unused `HpVersion.FileTypeExt`, `SolidWorksPreviewHelper` and `SldWorksUI`
- [x] One size formatter (`ByteSize.Format`, also used by `FileSizeConverter`; fixes the stray spaces before "MB"), one id-list helper (`GetIDs`; `ToArrayListIDs` removed), one model-name map (the `[OdooModel]` attribute: the generated `HpModelDictionaryGen` for transports, the attribute read by reflection for `HpBaseModel`; the two hand-written dictionaries and the unused `SetHpModel` removed), one `OdooFieldType` enum and one `IsExternalInit` (Shared's files, linked into the schema generator and Abstractions; `IsExternalInit` is now internal), one operator table (`OpExtension`, both directions)
- [x] `Help.BatchList/BatchArray` → `Enumerable.Chunk` (already gone before Phase 3)
- [x] `OdooDefaults` lookup maps: the hand-written loops → one `MapBy` helper. A duplicate key now keeps the first record; `Dictionary.Add` used to throw, which left the whole map empty
- [x] App: `IUiDispatcher` (`UiDispatcher.Current`, `DispatcherQueueUiDispatcher`) replaces `UiDispatcher.Post`'s delegate, SafeHelper, the `HackApp.DispatcherQueue` static, DispatcherExtensions and direct `DispatcherQueue.TryEnqueue` use. `Post` always queues (as before); `RunAsync` runs inline on the UI thread and propagates exceptions

Gate after Phase 3: offline 170 passed / 37 skipped; live 211/211 plus SMOKE TEST PASSED. The live count went from 216 to 211 because the path tests for the deleted converters went with them.

## Phase 4: Typed Odoo seam (generics instead of object/ArrayList/Hashtable) ✅

Done. Gate: live 223/223 + SMOKE TEST PASSED; offline 182 passed / 37 skipped. All new seam types live in `HackPDM.Infrastructure/Odoo/Rpc/` (namespace `HackPDM.Infrastructure.Odoo.Rpc`, except `OdooValueConverter`, which keeps `HackPDM.Infrastructure.Odoo` because the generated code names it).

**4a. Introduce the types**
- [x] `DomainTerm(Field, Op, Value)` and an immutable `OdooDomain : IReadOnlyList<object>` with `Where`/`And`/`IdIn`/`All` builders and `&`/`|`/`!` operators (named `OdooDomain` because `Domain` clashes with the `HackPDM.Domain` namespace)
- [x] Ids as `IReadOnlyList<int>` everywhere
- [x] One transport: `OdooRpc.CallAsync(service, method, args, timeout, token)` → `Result<JsonElement>`; `Result<T>` (`Ok`/`Error`/`ValueOr`/`Unwrap`/`Map`) replaces the static `LatestException`. Where a model method used to report through it, the instance now has `LastError`; login has `OdooDefaults.LoginError`
- [x] Typed verbs: `SearchAsync`, `SearchCountAsync`, `ReadAsync`, `SearchReadAsync`, `CreateAsync`, `CreateManyAsync`, `WriteAsync`, `UnlinkAsync`, `ExecuteAsync<T>`; rows are `OdooRow : Dictionary<string, object?>`, read with the `OdooValues` row helpers
- [x] `Repository<T> where T : HpBaseModelTransport<T>, new()` with one `FetchAsync` and one `Assign` (unknown fields → `HashedValues`)
- [x] DTO records: `RemoteEntryDto`, `SubdirectoryDto`, `LastAvailableDirectory` (in DirectoryDtos.cs). `StagedVersionPayload` and `SwProperty` were not needed: staged payloads stay `Dictionary<string, object?>`
- [x] Write payloads as `Dictionary<string, object?>` (`ComputeValues` replaces `ComputeHashtable`)

**4b. Migrate callers**
- [x] Operations, models, `EntrySearchService`, `EntryListService`, `DirectoryTreeService`, `PdmWorkflowService`, view models, smoke runner, tests
- [x] `IPdmWorkflowService` signatures take `IReadOnlyList<int>` instead of `ArrayList`
- [x] Many2one access through `OdooValues.Many2OneId/Many2OneName` (`row.RefId/RefName`); app Hashtable rows → `RemoteEntryDto`
- [x] Dropped the hand-written type → model registry (model names come from `[OdooModel]` / the generated dictionary). Connection probes moved to `OdooConnection`

**4c. Delete the old surface**
- [x] Deleted `OdooRpcCs.cs` (the wrapper matrix), `ToNativeObject`, `HashConverter`, the ArrayList/Hashtable extensions, `RecordsPopulation`/`FinalizePopulation(s)`; `HashedValues` is a `Dictionary<string, object?>`
- [x] `MultiRecord` is one abstract base; `Many2Many`/`One2Many` are sealed, implement their interfaces, and convert from `int[]`

**Bugs found and fixed on the way**
- `Many2Many`'s `int[]` conversion called itself (stack overflow), and a many2many always read as empty (Add on a null array was a no-op)
- `Many2Many`/`One2Many` didn't implement `IMany2Many`/`IOne2Many`, so casting to them failed at run time
- Entry search passed its result limit as the RPC timeout
- `EntryFileService` built its temp path with `\` on Linux
- `OdooValues.FromJson` typed every number as `double` (`? int : ? long : double` unifies to double)
- Permanent delete searched `hp.version.relationship`, which no longer exists in the addon; the old client swallowed the error as "nothing found". It now clears `hp.version.parent_ids`/`child_ids` with `(5, 0, 0)`
- Errors are no longer lost to concurrent calls overwriting the shared `LatestException`

No `ArrayList` or `Hashtable` is left in the code (the UI window parameters went in Phase 7).

## Phase 5: Split the god classes (SRP) ✅

Done. Gate: live 223/223 + SMOKE TEST PASSED; offline 182 passed / 37 skipped.

- [x] `ExtensionOdoo.cs` deleted, split into `OdooTransport` (HTTP, pooling, timeouts), `DependencyScanner` (Infrastructure/Hack: a file's or folder's SolidWorks references) and `OdooValueConverter` (one switch replacing the `TryCast*`/`From*` helper matrix; the generator calls it)
- [x] `OdooRpcCs.cs` was already gone in Phase 4 (`OdooRpc`, `OdooTransport`, `OdooConnection`)
- [x] `OdooDefaults` → `OdooConnectionSettings` (address, db, credentials) + immutable `OdooLookupCache` record built by one `LoadAsync`; `OdooDefaults` keeps only the session (user id, `LoginError`) and delegates. The lookup members of `IOdooDefaults` are get-only now
- [x] Model behaviour moved out: `VersionFileService` (find the version of a local file, preview, download, write, move, `ToHackFile`, `DownloadAll`), checkout into `PdmCheckoutOperations`, staging into `CommitStaging` (entry, version, relationships, properties). `HpEntry`/`HpVersion`/`HpVersionProperty`/`HpVersionRelationship` keep fields, derived defaults and queries
- [x] `HackFile`: `Checksums`, `LocalFileWriter`, `PwaPaths` were already split out; the version matcher went to `VersionFileService`; `GetFromPath` is the one factory (removed `GetFromFileInfo`, `GetHackFileAsync`, `FileInfoToHackFile`, `InitializeHackFromPath`, the `EntryRow` overload, `CreateFile(s)`)
- [x] `Help.cs` deleted (only an unused `FastSlice` was left)
- [x] Operations: `UnlinkMatchingAsync` (Phase 4) is the one delete-where; one `SetDeletedAsync(entries, deleted)` replaces logical delete/undelete; one checkout loop whose filters use `HpEntry.CanCheckOut`/`CanUnCheckOut`; check out and undo share the read-only toggle
- [x] `HpVersionProperty.FromSwProperty(...)` is the one property builder (the unused parameter constructor is gone); one `CommitStaging.NewVersion` behind both `PrepareVersion`/`StageVersionAsync` overloads
- [x] SchemaGenerator: one `OdooGeneratedRegistry` keyed by `typeof(model)` replaces the two `typeof(TModel).Name` switches (same-named models in two namespaces would have collided); outbound values come from a field type → emitter map

**Behaviour changes**
- A `max_batch_size` / `max_concurrency` setting that is unset or 0 now means the defaults (5 files, 2 downloads); concurrency used to drop to 1
- Float and monetary fields accept whole numbers (the JSON reader now gives them as `int`; they used to read as 0)
- A commit whose entry fails to stage no longer throws from the version step; it takes the rollback path
- Deleted entries are left out of the "N check outs" count instead of failing one by one; a logical (un)delete that the server refuses is logged

## Phase 6: ViewModel and command reuse ✅

Done. Gate: live 223/223 + SMOKE TEST PASSED; offline 182 passed / 37 skipped.

- [x] `OperationViewModel` base (`Dialogs`, `Reporter`): `RunOperationAsync(operation, completedMessage)` opens the status window, runs, then reports; `RunOnIdsAsync` adds the "nothing selected → do nothing" guard. Used by `HackFileManagerViewModel` and `SearchOdooViewModel`; directory commands share one `RunOnDirectoryAsync`
- [x] History commands share `WithHistoryVersionAsync`; check out / undo share one body in `PdmWorkflowService.ChangeCheckoutAsync`, and the new `CheckoutLatestAsync` (get latest, then check out) replaces three copies across the two view models; detail tabs are a dictionary instead of a switch
- [x] `RemoteIds` lives in the base and both view models use it
- [x] `ReplaceAll` (an `ObservableCollection` extension; `RangeObservableCollection` does it with one Reset) replaces the clear/fill loops; the detail and search-result collections are range collections
- [x] `MessageBox` is table-driven: one button layout per `MessageBoxButtons`, one `ShowAsync` (661 → 151 lines). The unused list/grid/content/tooltip layouts and their `MessageBoxType`/`MessageBoxWidth` enums are gone; `ShowAsync` is internal and every caller (view models, a few views and the updater) goes through `IDialogService`
- [x] Removed `FormHelper` and `LazyConverter`/`LazyConverterFactory`: the saved settings (`ColorSettings`, `OdooEntryColumns`, `OdooEntryColumnID`) hold plain values (same JSON, so saved configs still load); the column defaults are one table; the status brushes are plain properties over the saved colours
- [x] Group key typed: `EntryRowKeys` (was `EntryRowSorters`) has sort and group keys as `Func<EntryRow, IComparable?>`; the "Group By" menu lists its names instead of reflecting over `EntryRow`

**Behaviour changes**
- A command on a selection with no server entries no longer opens an empty status window; logical delete with nothing selected skips the "Completed!" box
- Checking out a directory gets the latest of its live entries only (it used to follow "show inactive")
- Grouping by a date groups by day as a date (sorted chronologically) rather than by its short-date text; "Group By" offers only the sortable fields

## Phase 7: Remaining pages to MVVM and a single composition root ✅

Done. Gate: live 223/223 + SMOKE TEST PASSED; offline 182 passed / 37 skipped.

- [x] Settings views bind to their view models. `HackSettingsViewModel` and `OdooSettingsViewModel` load the current values, validate, save, and raise `Saved`; the views are `x:Bind` plus one line that closes the window on `Saved`
- [x] ProfileManager: `ProfileManagerViewModel` has the login and profile commands, and `IsLoggingIn` drives the progress ring. `ProfileStore` holds the template, a snapshot of the live settings (with or without the password), `Apply` and the JSON read and write. The view is just a constructor
- [x] StatusDialog: the view model owns the session, the three logs and the progress counters. `StatusWindowReporter` calls `ResetProgress`/`Advance`/`SetProcessed`/`CompleteProgress`, which coalesce into one property-change post per batch to the UI thread. The 100 ms polling loop is gone, and the progress bar and labels are `x:Bind`
- [x] Home: `INavigationService` (`NavigationService` in the shell, which HomeView attaches to). Each menu item's `Tag` is its `NavigatePageMenu` name; `HomeView.Pages` maps each page to its type and window preset (Configuration and the not-logged-in page are routed in `HomeView.Show`). The static navigator fields and `HomeView.NavigateToPage` are gone; `ProfileManagerViewModel` and the smoke test navigate through the service (the smoke test then reads the frame from `HackApp.HomePage`)
- [x] JSON editor: `AppConfigJsonService` loads the settings file (or the sample), formats it and saves it
- [x] `IFilePickerService` (`FilePickerService`) replaces the two picker setups in ProfileManagerView
- [x] `HackFileManagerView`: the entry list, its columns and its context menu are XAML. The code only applies the saved headers, widths and row size, matching each column by `SortMemberPath`, and fills "Group By" from `EntryRowKeys.Names`
- [x] `InstanceManager`, `ViewModelEnvironment` and the `VMEnv` aliases are merged into `AppComposition`, which now holds `Navigation`, `FilePickers`, `StatusLog`, `ProfileManager`, `FileManager` and `SearchOdoo`. The page → window lookup (which window a page was opened in) moved into `WindowHelper` (`WindowOf`). `WindowConfig.Preset` falls back to the built-in preset.
  - Removed the unused `HomeViewModel`, `NotLoggedInViewModel` and `ApplicationSettingsViewModel`, along with the `CreateWindowAndPage` overloads that took `ArrayList` parameters (no caller used them).
- [x] `GlobalSerializedData` is now a plain data class; `AppConfigStore` reads and writes it (fixed path, serializer options, `Load`/`Parse`/`Save`), and `ThemeService.ReadSavedMode` uses its path.
  - `ExtensionForm` is split into `WindowExtensions` (presenter, handle, `page.Window`) and `TreeViewExtensions` (node data, path, breadcrumbs, `EnsureVisible`, `FindTreeNode`). Its unused gradient brushes and empty extension blocks are gone.
- [x] Delete GridHelp (done in Phase 1)

**Behaviour changes**
- Odoo settings: an invalid area factor now saves nothing. Before, it saved every field above it and still closed the window.
- Hack settings: a blank folder path is now rejected.
- Status window:
  - "Cancel" now cancels the running operation. Before, it set a flag nothing read.
  - Closing the window during an operation now actually waits for the confirmation. Before, the handler set `Cancel` after an `await`, so the window always closed.
  - The "Downloaded" and "Session Downloaded" labels are gone. Nothing ever wrote those counters, so they always showed 0.
- The entry list no longer throws when a saved settings file is missing a column; that column gets its default. Sorting the Size column now uses `Size` (it fell back to the header text before).
- AppConfig.json always lives in the documents folder (it already did in practice). The unread `AppConfigFilePath` core setting is no longer written.
- Loading a profile file that isn't valid JSON now shows "unable to deserialize" instead of throwing.

**Left as is**
- `HackFileManagerView._treeItems` (see Phase 1): still assigned, because it puts the TreeView in items-source mode
- `ColorDrawingHelper.EZGradient` is unused (Phase 1 note); not part of this phase

## Phase 8: Generated code ✅

Done. Gate: live 223/223 + SMOKE TEST PASSED; offline 182 passed / 37 skipped.

- [x] Asset map. The 615-line hand-written `Imaging/Settings.cs` is now `Imaging/Assets.cs` (about 60 lines). The `GenerateAssetMap` target in HackPDM.csproj writes `obj/.../AssetMap.g.cs` on every build, and only touches it when the Assets folder changed.
  - Included: images directly in Assets, every `Icons/*.svg`, and `StatusIcons` (the .svg where one exists, otherwise the .png). Each key is the file name without its extension.
  - The 8 keys that don't follow that rule stay hand-written in `Assets.cs`: jpg/jpeg/bmp/png share `gen.svg`, and four extension icons have no `Icons/` equivalent.
  - Checked: the result equals the old map, plus the 8 entries below.
- [x] SchemaGenerator follow-ups:
  - The pipeline is incremental. `ForAttributeWithMetadataName` finds the `[OdooModel]` classes and reads each into an `OdooModelGen` of plain values (`EquatableArray` for its properties), so an edit regenerates only the models it changed. Before, the whole compilation went through `Collect`, and every attributed class was re-read on every keystroke.
  - A malformed `[OdooProp]` or `[OdooModel]` now reports a warning (HPDM001/HPDM002) instead of being skipped silently. Analyzer release tracking (`AnalyzerReleases.*.md`) is added.
  - Generated code no longer depends on `using` directives: the helper calls and `nameof` are fully qualified, and the registry is sorted by type name, so its output doesn't depend on file order. Checked by diffing the generated files: those are the only changes.
  - Removed the empty `ColorCsvRow.cs`.

**Behaviour changes**
- 8 icons in `Icons/` that the old map never listed are now available: `default_file`, `default_folder`, `default_folder_opened`, `default_root_folder`, `default_root_folder_opened`, `psql`, `slddnv` and `sldtnfvt`. For .psql, .slddnv and .sldtnfvt files this means their own icon instead of the fallback.

## Audit after Phase 8 ✅

Every phase was checked against the code. Gate after the fixes: live 223/223 + SMOKE TEST PASSED; offline 182 passed / 37 skipped.

**Fixed**
- [x] Downloads are async end to end: `DownloadContentsAsync` / `DownloadFileAsync` / `DownloadAllAsync`, `IEntryFileService.DownloadVersionAsync` / `ExecuteMoveAsync`, `PdmWorkflowService.DownloadVersionsAsync`. Before, each version blocked a thread on its RPC
- [x] Folder commits: `DependencyScanner.GetHackFolderWithDependenciesAsync` awaits each file's references (it blocked on each one), and `CollectFolderCommitFilesAsync` replaces `CollectFolderCommitFiles`
- [x] Locating a "Where Used" / "Children" row split its folder on `\` only. `WinPathway` uses this OS's separators, so on Linux the double-tap never found the folder; it now uses `PwaPaths.Segments`
- [x] One composition root: the Microsoft DI container in `App.xaml.cs` is gone. `AppComposition.Settings` / `InitializeDefaults` set up the settings store and defaults, and `CreateHackSettings` / `CreateOdooSettings` / `CreateThemeEditor` build the page view models. `HackApp.Services` is removed; `MessageBox` and `HomeView` are plain `new`
- [x] Settings Submit gives feedback ("… settings saved"), which was the Phase 2 item deferred to Phase 7: inline in Configuration there is no window to close
- [x] JSON editor has a view model (`JsonEditorViewModel`: the tree, load, parse, save, remove). The view keeps the preview dialog, syntax colouring and indent
- [x] Status window: the close confirmation and cancel moved to `StatusDialogViewModel.ConfirmCloseAsync`
- [x] `RangeObservableCollection` moved from `EntryRow.cs` to `Models/Common`; the inline MB formatting in `PdmDownloadOperations` uses `ByteSize.Format`
- [x] Dead code: `IHackFileModel`, `IHackBaseFileModel`, `UsualIncludedFields`, `ColorDrawingHelper`, the commented-out `ComputeHashtable` and a commented-out `Hashtable` parameter

**Behaviour changes**
- Submitting the Odoo or Hack settings shows a "saved" message
- Opening, downloading or moving a history version no longer freezes the window while it downloads

**Still open** (larger design work, not done here)
- [ ] `HackFile.RelativePath` still means two things: `root/<dir>` from `GetFromPath`, `<dir>/<file>` from the lazy getter
- [ ] `IHackDefaultBase` / `IHackDefaults` split
- [ ] View models read the ambient `OdooDefaults.Instance` / `HackDefaults.Instance` instead of an injected service. `AppConfigStore` and `GlobalSerializedData` live under `Views/Common/Settings`, so `AppConfigJsonService` (a model) depends on the view layer
- [ ] `HackFileManagerViewModel` (≈ 610 lines) and `HackFileManagerView.xaml.cs` (≈ 490 lines) are still the largest classes. The view's locate logic polls with `AsyncHelper.WaitUntil`, and the tree load waits a fixed 500 ms
- [ ] `HackFileManagerView._treeItems` (Phase 1): still needs a runtime check before removal
- [ ] `OdooFileTypeManagerView` and `NotLoggedInView` are empty template pages ("Manage Types" opens an empty window)
- Note: the Phase 0 test list and some Phase 2 bullets name methods later phases removed or renamed (`ToNativeObject`, `HashConverter`, `StageRelationshipRecords`, `OdooClient<T>`, `SafeInvoker`, …); they record what was fixed at the time

---

## Server-side safeguards (Odoo addon) ✅

The addon no longer relies on the client to behave. Shared helpers live in `Odoo/addons/hackpdm/models/hp_guard.py`. "Admin" means superuser mode or `base.group_system`, and admins bypass the user-level guards.

- **Path-safe names**: `@api.constrains` on `hp.entry.name` and `hp.directory.name` reject:
  - empty names, `.` and `..`
  - `<>:"/\|?*` and control characters
  - a trailing space or dot
  - Windows device names (CON, NUL.txt, COM1…)
  - names over 255 characters

  This stops a stored name such as `..\..\x.dll` from making clients write outside the vault.
- **Case-insensitive uniqueness**: unique indexes on `(COALESCE(dir_id,0), lower(name))` for entries and on `(COALESCE(parent_id,0), lower(name))` for folders, created in `init()`.
  - If existing rows already collide, the index is skipped and a warning is logged.
  - The commit check (`_validate_entry_payload`) also matches with `=ilike`.
- **Checkout ownership** (`hp.entry.write`):
  - Users cannot clear, delete, rename, move or retype an entry that another user has checked out.
  - Users cannot set `checkout_user` to anyone but themselves.
  - Deleted entries cannot be checked out.
  - Releasing a checkout (`is_checked_out = False`) clears the user, node and date.
- **Bug fixed**: the checkout fields pointed in a loop. The entry's `checkout_*` were computed from `latest_version_id.checkout_*`, and those were related fields pointing back at the entry, so any read in the middle of a checkout recomputed them to empty. The fix is in "Checkout kept on the version" below. An earlier fix here stored the fields on the entry, which went against the intended design and was undone.
- **Versions**:
  - Users cannot create a version on a deleted entry, or on one checked out to someone else.
  - `entry_id` and `file_contents` of an existing version are read-only for users.
  - A version cannot be its own parent or child. Commits skip self-references.
  - `checkout_entry` reports a missing version clearly.
- **Commits**: `start_commit` and `clear_commit` run only for the commit's creator or an admin, and ignore or report missing ids.
- **Reads**:
  - `fast_read` goes through the ORM (`read()`). It used to be raw SQL that bypassed access rules, and was broken anyway.
  - `smart_read` works without `fields`.
  - `smart_read`, `related_browse` and `related_search_browse` check every step of a dotted field path for existence and read rights.
- **Maintenance jobs** (`migrate_*`, `display_all_fnames`, `cleanup_filestore`) are admin-only. They read every record and commit mid-transaction.
  - `getImageBytes` is now private (`_get_image_bytes`), and `import pdb` is gone.
  - The hardcoded WebDAV and Postgres credentials in `hp_version.py` and `hp_common_model.py` are still there. They need moving to configuration (for example `ir.config_parameter`) and rotating.
- **Tests**: `Odoo/addons/hackpdm/tests/test_safeguards.py` has 21 post_install TransactionCases, using two `base.group_user` users. The function tests below add 19 more.
  - Both `Odoo/ci/run_local_smoke.sh` and the GitHub workflow run them in a separate `<db>_tests` database: the rows they create are rolled back, but the ids they use are not, and the client needs the seeded root folder to be `hp.directory` 1.
  - Gate: addon 21/21, live 223/223, SMOKE TEST PASSED.

### Server functions checked end to end ✅

`Odoo/addons/hackpdm/tests/test_functions.py` (19 tests) drives every RPC method the client uses, with the client's own payload shapes, as a regular `base.group_user` user. It covers:

- **Commits**
  - The full commit flow: a new entry, its first version, a new version of a checked-out file, a SolidWorks reference and a custom property.
  - Commit results: summary, checksum, size, contents, check-in on commit, parent/child links.
  - Atomic failure with the reason recorded; refusal of a file checked out to someone else.
  - `clear_commit`, and `start_commit` being idempotent.
- **Checkout**: by node id or name, re-checkout, the client's un-checkout write, bad arguments.
- **Delete**: marking deleted, restoring, and the admin permanent-delete sequence.
- **Folders**: `get_entries`, `get_all_entry_ids`, `get_children_directories_by_id`, the path walk behind `last_available_directory`, and folder creation the way the client does it.
- **Dependencies**: recursive dependencies, including cycles and deleted entries.
- **Generic reads**: `related_browse` and `smart_read` with dotted fields.

Bugs these tests found and fixed:

- **Failed commits crashed for regular users.** Users may not delete `hp.record.staged` rows, so a failing commit or `clear_commit` raised an access error. The commit was then never marked as errored.
  - The staged rows of the caller's own commit are now removed with elevated rights. Ownership is checked first.
- **Commit payloads wrote through to shared records.** A version payload's `file_ext` and an entry payload's `cat_id` are related fields, so they overwrote the shared `hp.type` record. Computed fields could likewise overwrite server values.
  - `_own_fields_only` now creates each staged record from its own stored fields only.
- **`last_available_directory` matched folder names case-sensitively.** A local `parts` folder next to a server `Parts` folder would be created again, which the unique index now refuses.
  - An exact match is tried first, then a case-insensitive one.
- **`get_recursive_dependency` missed recent changes.** It is raw SQL, and could miss links or deleted flags written earlier in the same transaction. It now flushes first.

## Navigation keeps context ✅

Rule: **models outlive the page, view models live for one visit.** `AppComposition` holds the models that carry a page's context for the whole run. A page's view model is made by a `Create…` factory when the page is entered, starts from that model, writes its changes back, and is disposed in `OnNavigatedFrom`. Models hold plain data only, with no observable collections or events a page binds to, so a page that is gone is never kept alive by a long-lived object.

| Page | Kept model | Kept between visits | Not kept (reloaded when needed) |
|---|---|---|---|
| File manager | `FileManagerState` | directory tree, expanded folders, selected folder, that folder's rows (≤ `MaxCachedEntries` = 5000), selected rows, detail tab, show-deleted, grouping, sort, a pending locate request | preview images, the detail tabs' rows (history, parents, children, properties), longer entry lists |
| Profile Manager | `LoginSession` | logged in, last login errors, the account (server, database, user) | — |
| JSON editor | `JsonEditorDraft` | the editor's content, including unparsed code-view text | — |
| Settings pages | the settings store | saved values | unsaved edits (these forms show the saved values) |

- **Staleness:**
  - A view-model operation that changes the server (commit, delete, restore, …) marks the state stale through `Refresh`, even when it finishes after the page was left. The next visit then fetches the tree or rows again instead of showing the kept copy.
  - The toolbar refresh (`RefreshFromServer`) marks everything stale.
  - Logging in as another account (`LoginSession.LoggedInAs`) resets `FileManagerState`.
- **Locate requests:** the search window goes through `FileManagerState.RequestLocateAsync`. It runs at once when the file manager is shown. Otherwise it waits, and the search window switches to the file manager, which runs the waiting request once its tree is up.
- **Configurations:** the menu item is now a page (`ConfigurationView`) that is navigated to like the others. Before, it replaced the frame's content directly, so the page it replaced was never told it was left.
- **Bug fixed:** `TreeViewExtensions.FindTreeNode` returned the parent of the folder it found, and needed `RootNodes` to be a `List<TreeViewNode>`. A rebuilt tree reselected the wrong folder, or none.
- **Tests:**
  - `FileManagerStateTests`, offline: which rows are kept, the size cap, staleness, locate requests, reset.
  - Smoke step "leave and come back (context kept)": switches to the Profile Manager and back, then checks:
    - the old view model was disposed, and a new one was made;
    - the same tree object and the same row objects are shown;
    - the folder, the selected row and the History tab are back.

## Live local file changes ✅

- **Watcher:** `LocalFileWatcher` (Models/Hack) is one `FileSystemWatcher` over the whole working folder, held by `AppComposition` for the whole run.
  - `LocalChangeBatcher` collects events per path. Once a path has been quiet for 400 ms, it hands that path on once, as the single change its events add up to. For example, a save done as delete + create becomes "updated", and create then delete becomes nothing.
  - Lock and temporary files are ignored (`~$…`, `.~lock…`, `…~`, `.tmp`, `.swp`).
  - A buffer overflow reloads the listed folder.
  - If the working folder does not exist yet (nothing downloaded), the watcher creates it, so it is watched from the start.
  - A folder that appears (made with its parents, copied or moved in) is scanned for files already inside it. Without the scan, files written before inotify watched the new folder would raise no event.
- **One row, not the whole list:** a change in the listed folder rebuilds only that file's row (`IEntryListService.RefreshRowAsync`):
  - The file is hashed off the UI thread.
  - The server data the row already holds (`EntryRow.Remote`) is reused, so there is no server call.
  - The row is replaced, added or removed in place, and the other row objects are kept.
  - A selected row stays selected.
  - With more than 25 changes in the folder at once, the folder is reloaded instead.
- **Other folders:** a change in a folder that is not listed shows a short badge on that folder in the tree, or on its nearest visible parent. The badge fades in, holds, fades out and is removed. The texts are:
  - "( + ) created", in green;
  - "( . ) updated", in amber;
  - "( - ) deleted", in red.
- **While the page is not shown:** changes that touch the kept folder mark its kept rows stale (`FileManagerState.LocalFilesChanged`), so the next visit lists them again.
- **Window size bug fixed:** going to a page did not resize the window. There were two causes:
  - Uno's desktop heads leave `AppWindow.MoveAndResize` unimplemented, so the call did nothing. `WindowHelper.SetWindowConfig` now calls `Move` and `Resize` separately.
  - On X11, once a window is shown, Uno resizes the window manager's frame, and reparenting window managers (Cinnamon/Muffin, Mutter) ignore that. `X11WindowSize` also resizes the app's own client window through libX11, which the window manager honours.
- **Tests:**
  - `LocalFileWatcherTests`, offline: the merge rules, the quiet period, the noise filter, a real watcher on a temp folder, and a missing working folder with a file written right after its folders are made.
  - The smoke step "navigate to file manager" now waits for the window to reach the file manager preset size.
  - Smoke step "live file changes (one row, tree badge)" checks three things:
    - a remote row's local copy is rebuilt while the other rows stay the same objects;
    - an added file appears as one row and is removed again;
    - a file in another folder shows a tree badge.

## Cross-platform settings and credentials ✅

Before this, the app depended on Windows in three places, and two of them only worked when the app was packaged (MSIX):

| What | Was | Now |
|---|---|---|
| Server address, port, database, licence key, area factor, credential target | `ApplicationData.Current.LocalSettings`. Needs package identity on Windows, and only holds WinRT primitives, so `AreaFactor` (a `decimal`) was never saved. | `JsonFileSettingsProvider` (Core/Configuration): `settings.json` in `AppDataPaths.Config`, i.e. `%APPDATA%\HackPDM` or `~/.config/HackPDM`. Every change is written through a temporary file. |
| The login password | Windows Credential Manager (Meziantou). Elsewhere, an AES file whose key is a constant in the program. | The system keychain on each platform, through `ICredentialStore` (Infrastructure/Credentials). See below. |
| User images that replace the bundled icons | `ApplicationData.Current.LocalFolder` with `ms-appdata:///` URIs | A plain folder, `AppDataPaths.Data/Assets/Images` (`%LOCALAPPDATA%\HackPDM` or `~/.local/share/HackPDM`), with `file://` URIs |

- **Credential stores:**
  - Windows: Credential Manager, with the same targets as before, so logins already saved there keep working.
  - Linux: the Secret Service (GNOME Keyring, KWallet, KeePassXC), called directly through `libsecret-1.so.0`. Items use the schema `org.hackpdm.Credential` with a `target` attribute.
  - macOS: the login keychain (service "HackPDM", account = target).
  - Without a keychain (headless machines, CI, no Secret Service running), the login falls back to the old file, now readable by the user only. That file is still only obscured, not protected.
  - A login found in the file while a keychain is available is moved into the keychain.
- **Moving old settings over:** on first use, `AppSettingsStore` copies whatever the packaged app had in `LocalSettings` into `settings.json`.
- **Test isolation:** `HACKPDM_CONFIG_DIR`, `HACKPDM_DATA_DIR` and `HACKPDM_CREDENTIAL_STORE=file` move settings, data and the saved login out of the user's profile. The local gate and CI set them for the smoke test, so it no longer writes the test server and admin login into your own settings.
- **Tests:**
  - `JsonFileSettingsProviderTests`: types kept across runs (including `decimal`), defaults, null removes a key, importing old values, an unreadable file.
  - `CredentialStoreTests`: keychain first, moving a login from the file, falling back when the keychain is unreachable, file permissions.
  - `SecretService_RoundTrip_WhenAskedFor` uses the real Secret Service when `HACKPDM_TEST_KEYRING=1`.
- **Not verified here:** the macOS Keychain store has not run on a Mac, and the Windows build (net10.0-windows) cannot be built on Linux.

## SolidWorks assembly life cycle, end to end ✅

`SolidWorksLifecycleTests` takes one KitBot sub-assembly (a screw and two nuts) through the life cycle against the live server. Operations go through `PdmWorkflowService`, and every status is read through `EntryListService`, as the file manager shows it. It needs the gitignored KitBot samples, so CI skips it.

| Step | Status |
|---|---|
| 1. Local-only assembly and parts | Lo |
| 2. Commit. The client checks for an entry of that name in that directory; the server builds the records from 14 `hp.record.staged` rows (4 entries, 4 versions, 3 relationships, 3 properties). | Ok |
| 3. Local files moved out of the working folder | Ro |
| 4. Get latest of the assembly. Its parts are downloaded too, into the same folder, byte-identical. | Ok |
| 5. Assembly changed. Downloaded files that are not checked out are read-only by design, so this had to be cleared first. | Lm (parts Ok) |
| 6. Originals moved back. They are the latest version, so the status is Ok, not Nv. | Ok |
| 7. Downloaded parts moved back; the changed assembly stays outside | Ro (parts Ok) |
| 8. Checkout: get latest, then check out. The parts are checked out too. | Cm |
| 9. Changed assembly committed: a new version through staging (1 version, 3 relationships) | Ok (parts Ok) |
| 10. The first version put back while the server has a newer one | Nv |
| 11. Logical delete: the entry and its versions are kept | Dt (parts Ok) |
| 12. Permanent delete: the entry and both versions are removed; the local file stays | Lo |

Two bugs found and fixed:
- **Parts stayed checked out after a commit.** Checking out an assembly checks out its parts. The commit then skipped them as unchanged and never released them. Unchanged files checked out to you are now checked back in, without a new version, both when other files are committed and when nothing changed at all.
  - An unchanged file held by someone else is now a plain skip, not an error: there is nothing to commit.
  - A changed file held by someone else is still refused.
- **Logical delete took the parts with it.** `LogicalDeleteAsync` widened the selection to every entry the latest versions depend on. An assembly's parts are often shared with other assemblies (the KitBot nuts are used by about 20), so deleting one assembly deleted them for everyone. It now marks only the selected entries. Permanent delete already did this.

## Checkout kept on the version ✅

The checkout is stored on the version, as intended, and the entry shows its latest version's:

| Field | hp.version | hp.entry |
|---|---|---|
| `checkout_user`, `checkout_date`, `checkout_node` | stored | related to `latest_version_id.checkout_*`, writable (a write goes to the latest version) |
| `is_checked_out` | related to the entry | computed: the latest version has a checkout user. Setting it to False releases the checkout; it can be searched. |
| `is_checkout_version` | this version has the checkout and is the latest | – |

- **Checking out** (`hp.version.checkout_entry`) writes the checkout to the entry's latest version, whichever version id it was given. It still also sets that version's `node_id`.
- **Committing** adds a new latest version with no checkout, so the entry is checked in by itself. `_post_create_version` also clears the checkout from the previous version, so no stale checkout is left on older versions.
- **Guards** moved with the data: on a version, a user can check out only in their own name, and only the holder (or an administrator) can change or release a checkout. The entry's guards are unchanged.
- **Cache bug fixed:** `hp.entry.latest_version_id` had its `@api.depends('version_ids')` commented out. Once it was read, a commit that added a version kept seeing the old latest version, and linked a new assembly to its part's previous version. The dependency is restored.
- **Migration:** the addon is now 16.0.1.1.0. `migrations/16.0.1.1.0/post-migrate.py` copies the checkout from the old `hp_entry` columns onto each checked-out entry's latest version, so the upgrade releases nothing.
- **Tests:**
  - Server: the checkout is stored on the latest version, is searchable by user and by `is_checked_out`, and a commit clears it from the previous version. A separate test runs the migration against the old columns.
  - Client: the lifecycle test reads the fields on the entry and on the version after the checkout and after the commit.

## Folder (tree) actions and undo checkout ✅

`FolderActionsTests` uses a folder with one KitBot assembly and its parts, and a `Sub/` folder with another. It runs what the tree's menu runs: the folder's entry ids, then the workflow.

| Action | Result |
|---|---|
| Commit folder | Commits the folder's own files and the files they reference. **Not its sub-folders**: unlike the other tree actions, there is no recursive commit. |
| Get latest, folder only | Top folder Ok, `Sub/` still Ro |
| Get latest, with sub-folders | Everything Ok |
| Check out the tree | Everything Cm, and writable |
| Undo the check out of the tree | Everything Ok, and read-only again |
| Undo check out of one assembly | Releases the assembly and its parts. A local change is kept, not reverted, so the assembly shows Lm. |
| Delete the tree (logically) | Everything Dt |
| Restore | The folder's own entries only. `Sub/` needs its own restore, because the recursive restore is still "Not Implemented Yet" in the file manager. |

## Attack tests: trying to break the client and the addon ✅

`tests/test_attacks.py` (addon) and `Integration/AttackTests.cs` (client) attack the vault through the methods both programs use, as two **regular users**. The other live tests log in as the administrator, who bypasses the server's user-level guards. Each attack was first run against the unfixed code. What got through, and the fix:

**Server (addon): 14 holes**

| Attack | Before | Now |
|---|---|---|
| Commit a new version of a file **nobody** has checked out (the client refuses; an older or modified client does not) | accepted | refused: "not checked out to you". The first version of a new file still needs no checkout. |
| `hp.version.create` directly on such a file | accepted | refused (same rule) |
| Commit a version that carries `checkout_user` = another user (the file then looks checked out to them) | accepted | checkout fields are dropped from commit payloads; a direct create with them is refused |
| Stage rows into **another user's** commit (it runs with their rights), or rewrite their staged rows | accepted | refused: staged rows only go into your own commit, and only for the vault's models |
| Write another user's commit (`committed`, `errored`…) | accepted | refused |
| `clear_commit` on a finished commit (reset its history) | accepted | refused |
| Commit properties onto an **existing** version (`version_id` = any version id) | accepted | refused: only versions the commit itself creates |
| Create/change `hp.version.property` directly | accepted | refused: properties are part of what was committed |
| Clear or add a committed version's dependency links | accepted | refused (administrators only; the commit links its new version with elevated rights) |
| Change a version's `node_id` (the committing computer) | accepted | refused. **Checkout no longer writes `node_id`** either: it is the computer that created the version. |
| Check out a deleted file by writing `checkout_user` (not `is_checked_out`) | accepted | refused |
| Commit a file with no folder | accepted | refused |
| Commit a version with no file contents (the client could not read the file) | accepted, a version with no file | refused ("" — an empty file — is still fine) |
| Permanent delete by a regular user | refused, but only after the dependency links were wiped | refused at the first step, links intact |

Held already: taking, releasing, renaming or deleting a file someone else has checked out (through the entry or the version), checking out in someone else's name, unsafe names, a duplicate name, running another user's commit, deleting vault records, `smart_read` paths to passwords, and the dependency walk writing another computer's list.

**Client: 6 holes**

| Attack | Before | Now |
|---|---|---|
| **Get Latest on a checked-out file with unsaved work** | **overwrote the work** | kept. A local file whose contents are in no vault version of that file is not overwritten; Get Latest says which were kept. |
| Get Latest on a file edited without checking out (Lm) | overwrote it | kept, the same way |
| Get Latest on an older, unchanged, read-only copy (Nv) | "1 downloaded", file **unchanged**: the write failed on the read-only file and its result was ignored | read-only lifted, file updated, read-only again; a failed write is reported |
| Commit a file deleted after it was picked | committed a version **with no contents** | refused before staging ("no longer there") |
| Check out / undo the checkout of a file someone else holds | "0 check outs … Completed!" | says who holds it |
| Permanent delete by a regular user | cleared the links after the first refusal | stops at the first refused step |

Held already: committing over someone else's checkout (refused by the client, and by the server around it), a new file with an existing name, Windows-reserved and unsafe names (all or nothing: the good file in the same commit is not created), two names differing only in case. A name with no file type ("trailing dot.") is left out and now reported as skipped instead of silently. Downloads are also kept inside the working folder whatever path the server sends.

**Noticed, not changed:** searching `hp.version` on `checksum` never matches (it is related to the field's `ir.attachment`, which searches hide). The client now reads the checksums instead. `hp.entry.checksum_nonmatches` and `checksum_list_nonmatches` use that search, so they are wrong; no client calls them.

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
| 8 Generated code ✅ | low | −600 | – |

Phases 6 and 8 can run in parallel with 4/5 because they touch different projects.
