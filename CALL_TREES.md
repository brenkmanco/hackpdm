# HackPDM call trees

Every starting action in HackPDM, and what it calls. A starting action is a UI command or event handler, app startup, the file watcher, or an RPC method on the Odoo server. Each action has its own heading, its tree, and numbered notes underneath it.

## How to read a tree

```text
Start > FirstCall > SecondCall
                  > AnotherSecondCall        (also called by FirstCall)
      > NextFirstCall > ...                  (also called by Start)
```

- Each line is one branch. A line starting with spaces continues the line above it: its `>` sits under the call it belongs to.
- A tree goes at most two calls deep below its start.
- `Type.Method` is a call into another type; a bare `Method` is on the same class as its caller. A call through an interface names the type that implements it.
- `[rpc model.method]` marks a call that reaches the Odoo server. Look that method up in [Odoo server (RPC)](#odoo-server-rpc) to see what it does next.
- ① ② … at the end of a line point to the note with that number below the tree: when the branch runs, or how it behaves.
- Logging, progress reporting, property reads and UI-thread marshaling are left out.

## Contents
- **[App, login, search and settings](#app-login-search-and-settings)**
  - [Startup](#startup): [Program.Main](#programmain) · [HackApp (ctor)](#hackapp-ctor) · [HackApp.OnLaunched](#hackapponlaunched) · [HackApp.Window_Closed](#hackappwindow_closed) · [SmokeTestRunner.RunAsync](#smoketestrunnerrunasync)
  - [Login & profiles](#login--profiles): [ProfileManagerViewModel.LoginAsync](#profilemanagerviewmodelloginasync) · [ProfileManagerViewModel.AbleToLoginAsync](#profilemanagerviewmodelabletologinasync) · [ProfileManagerViewModel.OpenSettings](#profilemanagerviewmodelopensettings) · [ProfileManagerViewModel.LoadProfileAsync](#profilemanagerviewmodelloadprofileasync) · [ProfileManagerViewModel.SaveProfileAsync](#profilemanagerviewmodelsaveprofileasync) · [ProfileManagerViewModel.SaveProfileWithoutPasswordAsync](#profilemanagerviewmodelsaveprofilewithoutpasswordasync) · [ProfileManagerViewModel.CreateTemplateAsync](#profilemanagerviewmodelcreatetemplateasync) · [HackUpdater.EnsureUpdated](#hackupdaterensureupdated) · [HackUpdater.UpdaterProcess](#hackupdaterupdaterprocess)
  - [Search](#search): [SearchOdooViewModel.SearchAsync](#searchodooviewmodelsearchasync) · [SearchOdooViewModel.Reset](#searchodooviewmodelreset) · [SearchOdooViewModel.AddPropertyAsync](#searchodooviewmodeladdpropertyasync) · [SearchOdooViewModel.DeleteProperties](#searchodooviewmodeldeleteproperties) · [SearchOdooViewModel.ResetProperties](#searchodooviewmodelresetproperties) · [SearchOdooViewModel.CheckoutAsync](#searchodooviewmodelcheckoutasync) · [SearchOdooViewModel.UndoCheckoutAsync](#searchodooviewmodelundocheckoutasync) · [SearchOdooViewModel.CheckoutAndOpenAsync](#searchodooviewmodelcheckoutandopenasync) · [SearchOdooViewModel.OpenAsync](#searchodooviewmodelopenasync) · [SearchOdooViewModel.LocateAsync](#searchodooviewmodellocateasync) · [SearchOdooView (ctor)](#searchodooview-ctor) · [SearchOdooView.OdooCancel_Click](#searchodooviewodoocancel_click) · [SearchOdooView.OdooSearchResults_DoubleTapped](#searchodooviewodoosearchresults_doubletapped)
  - [Settings & theme](#settings--theme): [HackSettingsViewModel.SubmitAsync](#hacksettingsviewmodelsubmitasync) · [OdooSettingsViewModel.SubmitAsync](#odoosettingsviewmodelsubmitasync) · [ThemeEditorViewModel.OnSelectedModeIndexChanged](#themeeditorviewmodelonselectedmodeindexchanged) · [ThemeEditorViewModel.ResetSelected](#themeeditorviewmodelresetselected) · [ThemeEditorViewModel.ResetAll](#themeeditorviewmodelresetall) · [ThemeEditorViewModel.Save](#themeeditorviewmodelsave) · [ThemeEditorViewModel.Revert](#themeeditorviewmodelrevert) · [ThemeEditorView.Picker_ColorChanged](#themeeditorviewpicker_colorchanged) · [ThemeEditorView.ShowDialog_Click](#themeeditorviewshowdialog_click) · [ApplicationSettingsView.SettingsSections_SelectionChanged](#applicationsettingsviewsettingssections_selectionchanged)
  - [Status window](#status-window): [StatusWindowReporter.BeginOperation](#statuswindowreporterbeginoperation) · [StatusWindowReporter.Cancel](#statuswindowreportercancel) · [StatusWindowReporter.OnCancelled](#statuswindowreporteroncancelled) · [StatusWindowReporter.ShowWindow](#statuswindowreportershowwindow) · [StatusWindowReporter.Log](#statuswindowreporterlog) · [StatusWindowReporter.SetTotal / Advance / SetProcessed / Complete](#statuswindowreportersettotal--advance--setprocessed--complete) · [StatusDialogViewModel.Cancel](#statusdialogviewmodelcancel) · [StatusDialogView.CmdCloseClick](#statusdialogviewcmdcloseclick) · [StatusDialogView.StatusSettings_Click](#statusdialogviewstatussettings_click) · [StatusDialogView.AppWindow_Closing](#statusdialogviewappwindow_closing)
  - [JSON editor & tools](#json-editor--tools): [JsonEditorViewModel.Load](#jsoneditorviewmodelload) · [JsonEditorView.OnExportJsonClick](#jsoneditorviewonexportjsonclick) · [JsonEditorView.OnAddChildClick](#jsoneditorviewonaddchildclick) · [JsonEditorView.OnDeleteNodeClick](#jsoneditorviewondeletenodeclick) · [JsonEditorView.OnViewModeChanged](#jsoneditorviewonviewmodechanged) · [JsonEditorView.OnNavigatedFrom](#jsoneditorviewonnavigatedfrom)
  - [Navigation](#navigation): [HomeView (ctor)](#homeview-ctor) · [NavigationService.NavigateTo](#navigationservicenavigateto) · [HomeView.HomeNavigator_SelectionChanged](#homeviewhomenavigator_selectionchanged) · [HomeView.Navigate](#homeviewnavigate) · [HomeView.HomeNavigator_BackRequested](#homeviewhomenavigator_backrequested) · [ProfileManagerView (ctor)](#profilemanagerview-ctor) · [ConfigurationView (ctor)](#configurationview-ctor) · [OdooSettingsView (ctor)](#odoosettingsview-ctor) · [HackSettingsView (ctor)](#hacksettingsview-ctor) · [ApplicationSettingsView (ctor)](#applicationsettingsview-ctor) · [ThemeEditorView (ctor)](#themeeditorview-ctor) · [JsonEditorView (ctor)](#jsoneditorview-ctor) · [HackFileManagerView.Search_Click](#hackfilemanagerviewsearch_click) · [PipWindow (ctor)](#pipwindow-ctor) · [PipWindow.OnExitPipClicked](#pipwindowonexitpipclicked)
- **[File manager](#file-manager)**
  - [Commands — commit/checkout/get latest](#commands--commitcheckoutget-latest): [CommitSelectedCommand](#commitselectedcommand) · [CommitDirectoryCommand](#commitdirectorycommand) · [GetLatestSelectedCommand](#getlatestselectedcommand) · [GetLatestDirectoryCommand](#getlatestdirectorycommand) · [GetLatestDirectoryTopCommand](#getlatestdirectorytopcommand) · [CheckoutSelectedCommand](#checkoutselectedcommand) · [CheckoutDirectoryCommand](#checkoutdirectorycommand) · [UndoCheckoutSelectedCommand](#undocheckoutselectedcommand) · [UndoCheckoutDirectoryCommand](#undocheckoutdirectorycommand)
  - [Commands — history versions](#commands--history-versions): [DownloadHistoryCommand](#downloadhistorycommand) · [DownloadHistoryToTempCommand](#downloadhistorytotempcommand) · [DownloadHistoryOverwriteCommand](#downloadhistoryoverwritecommand) · [OpenHistoryFromTempCommand](#openhistoryfromtempcommand) · [OpenHistoryOverwriteCommand](#openhistoryoverwritecommand) · [MoveHistoryToTempCommand](#movehistorytotempcommand) · [MoveHistoryToWorkingCommand](#movehistorytoworkingcommand) · [DownloadHistoryWithDependenciesCommand](#downloadhistorywithdependenciescommand)
  - [Commands — delete/restore](#commands--deleterestore): [LogicalDeleteSelectedCommand](#logicaldeleteselectedcommand) · [LogicalDeleteDirectoryCommand](#logicaldeletedirectorycommand) · [RestoreDirectoryCommand](#restoredirectorycommand) · [RestoreDirectoryRecursiveCommand](#restoredirectoryrecursivecommand) · [PermanentDeleteSelectedCommand](#permanentdeleteselectedcommand) · [LocalDeleteSelectedCommand](#localdeleteselectedcommand) · [LocalDeleteDirectoryCommand](#localdeletedirectorycommand)
  - [Commands — navigation & view](#commands--navigation--view): [GroupByCommand](#groupbycommand) · [OpenSelectedCommand](#openselectedcommand) · [OpenSelectedLatestRemoteCommand](#openselectedlatestremotecommand) · [OpenSelectedLatestLocalCommand](#openselectedlatestlocalcommand) · [OpenSelectedFoldersCommand](#openselectedfolderscommand) · [OpenDirectoryCommand](#opendirectorycommand) · [UploadIconCommand](#uploadiconcommand)
  - [View event handlers](#view-event-handlers): [OdooDirectoryTree_SelectionChanged](#odoodirectorytree_selectionchanged) · [OdooDirectoryTree_RightTapped](#odoodirectorytree_righttapped) · [OdooDirectoryBreadcrumb_ItemClicked](#odoodirectorybreadcrumb_itemclicked) · [TreeViewItem_Loaded](#treeviewitem_loaded) · [TreeViewItem_Unloaded](#treeviewitem_unloaded) · [OdooEntryList_SelectionChanged](#odooentrylist_selectionchanged) · [VersionTabs_SelectionChanged](#versiontabs_selectionchanged) · [OdooEntryList_Sorting](#odooentrylist_sorting) · [OdooHistory_SelectionChanged](#odoohistory_selectionchanged) · [OdooParents_DoubleTapped](#odooparents_doubletapped) · [OdooChildren_DoubleTapped](#odoochildren_doubletapped) · [Refresh_Click](#refresh_click) · [Search_Click](#search_click) · [ManageTypes_Click](#managetypes_click) · [Anchor_Checked](#anchor_checked) · [OnTogglePipClicked](#ontogglepipclicked) · [OnPipWindowClosed](#onpipwindowclosed) · [OnViewModelPropertyChanged](#onviewmodelpropertychanged) · [OnRefreshRequested](#onrefreshrequested) · [OnTreeLoadedAsync](#ontreeloadedasync) · [OnFolderChanged](#onfolderchanged) · [OnRowReplaced](#onrowreplaced) · [OnGroupingChanged](#ongroupingchanged)
  - [File watcher](#file-watcher): [FileSystemWatcher.Created](#filesystemwatchercreated) · [FileSystemWatcher.Changed](#filesystemwatcherchanged) · [FileSystemWatcher.Deleted](#filesystemwatcherdeleted) · [FileSystemWatcher.Renamed](#filesystemwatcherrenamed) · [FileSystemWatcher.Error](#filesystemwatchererror) · [LocalFileWatcher.Flush](#localfilewatcherflush) · [FileManagerState.LocalFilesChanged](#filemanagerstatelocalfileschanged) · [HackFileManagerViewModel.OnLocalFilesChanged](#hackfilemanagerviewmodelonlocalfileschanged) · [HackFileManagerViewModel.ApplyLocalChangesAsync](#hackfilemanagerviewmodelapplylocalchangesasync) · [HackFileManagerViewModel.OnLocalChangesLost](#hackfilemanagerviewmodelonlocalchangeslost)
  - [Load and selection](#load-and-selection): [HackFileManagerView (ctor)](#hackfilemanagerview-ctor) · [HackFileManagerViewModel (ctor)](#hackfilemanagerviewmodel-ctor) · [OnNavigatedTo](#onnavigatedto) · [LoadHackMan](#loadhackman) · [HackFileManagerViewModel.LoadDirectoryTreeAsync](#hackfilemanagerviewmodelloaddirectorytreeasync) · [LoadAndRestoreAsync](#loadandrestoreasync) · [EntryListService.LoadAsync](#entrylistserviceloadasync) · [HackFileManagerViewModel.ShowEntryDetailsAsync](#hackfilemanagerviewmodelshowentrydetailsasync) · [HackFileManagerViewModel.PreviewVersionAsync](#hackfilemanagerviewmodelpreviewversionasync) · [OnShowInactiveChanged](#onshowinactivechanged) · [OnIsGroupingEnabledChanged](#onisgroupingenabledchanged) · [OdooDirectoryTree.Expanding](#odoodirectorytreeexpanding) · [OdooDirectoryTree.Collapsed](#odoodirectorytreecollapsed) · [OdooDirectoryTree.LostFocus](#odoodirectorytreelostfocus) · [OdooParents.SelectionChanged](#odooparentsselectionchanged) · [OdooChildren.SelectionChanged](#odoochildrenselectionchanged) · [OdooHistory.DoubleTapped](#odoohistorydoubletapped) · [EntryOpen.DoubleTapped](#entryopendoubletapped) · [EntryDelete.DoubleTapped](#entrydeletedoubletapped) · [TreeDownload.DoubleTapped](#treedownloaddoubletapped) · [TreeUndelete.DoubleTapped](#treeundeletedoubletapped) · [HistoryDownload.DoubleTapped](#historydownloaddoubletapped) · [HistoryMove.DoubleTapped](#historymovedoubletapped) · [OnNavigatedFrom](#onnavigatedfrom)
- **[Odoo server (RPC)](#odoo-server-rpc)**
  - [Commit](#commit): [hp.pdm.commit.start_commit](#hppdmcommitstart_commit) · [hp.pdm.commit.clear_commit](#hppdmcommitclear_commit) · [hp.pdm.commit.run_commit_job](#hppdmcommitrun_commit_job) · [hp.pdm.commit.handle_commit_failure](#hppdmcommithandle_commit_failure) · [hp.pdm.commit.write](#hppdmcommitwrite)
  - [Checkout](#checkout): [hp.version.checkout_entry](#hpversioncheckout_entry) · [hp.version.checkout_entries](#hpversioncheckout_entries)
  - [Entries & versions (create/write guards)](#entries--versions-createwrite-guards): [hp.entry.write](#hpentrywrite) · [hp.version.create](#hpversioncreate) · [hp.version.write](#hpversionwrite) · [hp.version.property.create](#hpversionpropertycreate) · [hp.version.property.write](#hpversionpropertywrite) · [hp.record.staged.create](#hprecordstagedcreate) · [hp.record.staged.write](#hprecordstagedwrite)
  - [Folders](#folders): [hp.directory.get_dir_id_for_filepath](#hpdirectoryget_dir_id_for_filepath) · [hp.directory.get_dir_id_for_parentpath](#hpdirectoryget_dir_id_for_parentpath) · [hp.directory.get_dir_ids_for_filepaths](#hpdirectoryget_dir_ids_for_filepaths) · [hp.directory.get_directories](#hpdirectoryget_directories) · [hp.directory.get_children_directories](#hpdirectoryget_children_directories) · [hp.directory.get_children_directories_by_id](#hpdirectoryget_children_directories_by_id) · [hp.directory.get_entries](#hpdirectoryget_entries) · [hp.directory.get_all_entry_ids](#hpdirectoryget_all_entry_ids) · [hp.directory.last_available_directory](#hpdirectorylast_available_directory) · [hp.directory.create_new_details](#hpdirectorycreate_new_details) · [hp.directory.create_new_directories](#hpdirectorycreate_new_directories)
  - [Dependencies](#dependencies): [hp.version.get_recursive_dependency](#hpversionget_recursive_dependency) · [hp.version.get_recursive_dependency_versions](#hpversionget_recursive_dependency_versions) · [hp.version.get_recursive_dependency_entries](#hpversionget_recursive_dependency_entries) · [hp.version.get_entries_from_children](#hpversionget_entries_from_children) · [hp.node.update_node_latest_versions](#hpnodeupdate_node_latest_versions)
  - [Reading helpers](#reading-helpers): [hp.common.model.smart_read](#hpcommonmodelsmart_read) · [hp.common.model.fast_read](#hpcommonmodelfast_read) · [hp.common.model.related_browse](#hpcommonmodelrelated_browse) · [hp.common.model.related_search_browse](#hpcommonmodelrelated_search_browse) · [hp.common.model.create_read](#hpcommonmodelcreate_read) · [hp.entry.checksum_nonmatches](#hpentrychecksum_nonmatches) · [hp.entry.checksum_list_nonmatches](#hpentrychecksum_list_nonmatches) · [hp.entry.first_helper](#hpentryfirst_helper)
  - [Admin & migration](#admin--migration): [hp.version.migrate_webdav_files_to_records](#hpversionmigrate_webdav_files_to_records) · [hp.common.model.migrate_webdav_files_to_records](#hpcommonmodelmigrate_webdav_files_to_records) · [hp.version.migrate_image_from_database](#hpversionmigrate_image_from_database) · [hp.version.display_all_fnames](#hpversiondisplay_all_fnames) · [hp.version.cleanup_filestore](#hpversioncleanup_filestore) · [hp.release.review.reviewer_decision](#hpreleasereviewreviewer_decision) · [hp.entry.init](#hpentryinit) · [hp.directory.init](#hpdirectoryinit)
  - [Client → server](#client--server)

## App, login, search and settings

### Startup

#### Program.Main

```text
Program.Main > UnoPlatformHostBuilder.Build              ①
             > UnoPlatformHost.Run > HackApp (ctor)
                                   > HackApp.OnLaunched
```

① app factory = new HackApp(); X11, framebuffer, macOS, Win32 hosts  

#### HackApp (ctor)

```text
HackApp (ctor) > ThemeService.ReadSavedMode > File.ReadAllText  ①
               > Setup > Directory.CreateDirectory              ②
```

① AppConfig.json Theme.Mode; sets RequestedTheme Dark/Light  
② when the temp folder does not exist  

#### HackApp.OnLaunched

```text
HackApp.OnLaunched > MainWindow (ctor) > Window.SetTitleBar
                                       > WindowHelper.ResizeWindow                   ①
                   > WindowExtensions.SetWindowType                                  ②
                   > Window.Activate
                   > AppComposition.Initialize > DispatcherQueueUiDispatcher (ctor)  ③
                   > ConfigureServices > AppComposition.Settings                     ④
                                       > AppComposition.InitializeDefaults           ⑤
                                       > HomeView (ctor)                             ⑥
                   > ThemeService.Attach > ApplyMode                                 ⑦
                   > SmokeTestRunner.Start > StartUiStallProbe                       ⑧
                                           > RunAsync                                ⑨
                                           > Finish                                  ⑩
```

① Profile Manager size  
② Overlapped  
③ becomes UiDispatcher.Current  
④ first use: CoreSettings over AppSettingsStore.Open (settings.json, imports packaged LocalSettings once)  
⑤ registers HackDefaults.Instance / OdooDefaults.Instance on that store  
⑥ becomes the root frame's content; see Navigation  
⑦ first use of AppComposition.Theme: ThemeService (ctor) loads AppConfig.json via UIStorage.GlobalData, applies saved colors  
⑧ when HACKPDM_SMOKE_TEST=1  
⑨ posted to the UI thread  
⑩ writes smoke-report.json, Environment.Exit  

#### HackApp.Window_Closed

```text
HackApp.Window_Closed > AppConfigStore.Save > File.WriteAllText  ①
```

① AppConfig.json from the in-memory UIStorage.GlobalData  

#### SmokeTestRunner.RunAsync

```text
SmokeTestRunner.RunAsync > Step "configure" > OdooDefaults (set address/port/url/db/user/pass)    ①
                                            > HackDefaults.PwaPathAbsolute (set)
                         > Step "login" > AppComposition.CreateProfileManager
                                        > ProfileManagerViewModel.AbleToLoginAsync                ②
                         > Step "navigate to file manager" > NavigationService.NavigateTo         ③
                                                           > WaitUntil                            ④
                         > Step "load directory tree" > HackFileManagerView.GetOdooDirectoryTree
                                                      > WaitUntil                                 ⑤
                         > Step "open directory (entries)" > TreeData.EnsureVisible               ⑥
                                                           > WaitStable
                         > Step "select entry (history)" > WaitUntil                              ⑦
                         > Step "leave and come back" > NavigationService.NavigateTo              ⑧
                         > Step "live file changes" > File.WriteAllTextAsync
                                                    > File.Delete                                 ⑨
```

① from HACKPDM_ODOO_* env vars  
② same path as the Login button  
③ HackFileManager  
④ page loaded and window resized to its preset  
⑤ tree loaded from the server  
⑥ selection triggers the entry load  
⑦ history rows loaded  
⑧ ProfileManager, then HackFileManager; checks state was kept  
⑨ checks the watcher rebuilds one row and badges folders  

[↑ contents](#contents)

### Login & profiles

#### ProfileManagerViewModel.LoginAsync

```text
ProfileManagerViewModel.LoginAsync > AbleToLoginAsync > OdooDefaults.InitializeAsync [rpc common.login, then lookup reads]  ①
                                                      > OdooConnection.CorrectOdooAddress                                   ②
                                                      > OdooConnection.CorrectOdooPort                                      ③
                                                      > OdooConnection.CorrectUserId
                                   > LoginSession.LoggedInAs                                                                ④
                                   > FileManagerState.Reset                                                                 ⑤
                                   > OdooDefaults.SaveCredentials > OdooConnectionSettings.SaveCredentials                  ⑥
                                   > NavigationService.NavigateTo > HomeView.Select                                         ⑦
```

① skips common.login when a user id is already set; throws on a failed login  
② ICMP ping  
③ TCP connect  
④ this and the lines below: only after a successful login  
⑤ accountChanged callback: when server/db/user differ from the last login  
⑥ OS keychain via CrossPlatformCredentialManager.WriteCredential  
⑦ HackFileManager  

#### ProfileManagerViewModel.AbleToLoginAsync

```text
ProfileManagerViewModel.AbleToLoginAsync > OdooDefaults.InitializeAsync > InitializeLoginAsync [rpc common.login]                                                                                                     ①
                                                                        > OdooLookupCache.LoadAsync [rpc search_read on hp.node, hp.directory, hp.user, hp.property, hp.type, hp.category, name filters, hp.setting]  ②
                                         > OdooConnection.CorrectOdooAddress > Ping.SendPingAsync
                                         > OdooConnection.CorrectOdooPort > TcpClient.ConnectAsync                                                                                                                    ③
                                         > OdooConnection.CorrectUserId                                                                                                                                               ④
                                         > HackUpdater.EnsureUpdated > IsCorrectOdooVersion                                                                                                                           ⑤
```

① only when OdooId is 0; a failed login throws, and AbleToLoginAsync returns false with no message  
② registers this machine as a node when missing  
③ 5 s timeout  
④ 1 = success; 0 adds "invalid odoo credentials"  
⑤ never reached: CorrectUserId always returns a status  

#### ProfileManagerViewModel.OpenSettings

```text
ProfileManagerViewModel.OpenSettings > NavigationService.NavigateTo > HomeView.Select  ①
```

① Configuration  

#### ProfileManagerViewModel.LoadProfileAsync

```text
ProfileManagerViewModel.LoadProfileAsync > FilePickerService.PickOpenFileAsync                                                  ①
                                         > ProfileStore.ReadAsync > File.ReadAllTextAsync
                                         > DialogService.ShowMessageAsync                                                       ②
                                         > ProfileStore.Apply > OdooDefaults (set address/port/db/credential target/user/pass)  ③
                                                              > HackDefaults.PwaPathAbsolute (set)                              ④
```

① .json  
② when the file is not a valid profile  
③ address/port/db saved to settings.json; user/pass in memory only  
④ settings.json  

#### ProfileManagerViewModel.SaveProfileAsync

```text
ProfileManagerViewModel.SaveProfileAsync > ProfileStore.Current                             ①
                                         > SaveAsync > FilePickerService.PickSaveFileAsync
                                                     > ProfileStore.WriteAsync              ②
```

① includePassword: true  
② password written in plain text  

#### ProfileManagerViewModel.SaveProfileWithoutPasswordAsync

```text
ProfileManagerViewModel.SaveProfileWithoutPasswordAsync > ProfileStore.Current                             ①
                                                        > SaveAsync > FilePickerService.PickSaveFileAsync
                                                                    > ProfileStore.WriteAsync
```

① includePassword: false  

#### ProfileManagerViewModel.CreateTemplateAsync

```text
ProfileManagerViewModel.CreateTemplateAsync > ProfileStore.Template
                                            > SaveAsync > FilePickerService.PickSaveFileAsync
                                                        > ProfileStore.WriteAsync
```

#### HackUpdater.EnsureUpdated

```text
HackUpdater.EnsureUpdated > CurrentVersion
                          > IsCorrectOdooVersion > OdooDefaults.HpSettings         ①
                                                 > DialogService.ShowMessageAsync  ②
                                                 > UpdaterProcess                  ③
```

① cached hp.setting rows, no rpc  
② when versions differ and showOutdatedUpdater: Yes/No/Cancel  
③ when the user answers Yes  

#### HackUpdater.UpdaterProcess

```text
HackUpdater.UpdaterProcess > DialogService.ShowMessageAsync  ①
                           > ShellLauncher.OpenUrl           ②
                           > HackApp.Exit
```

① not awaited  
② \\freedom\Engineering\hackpdm\setup.exe  

[↑ contents](#contents)

### Search

#### SearchOdooViewModel.SearchAsync

```text
SearchOdooViewModel.SearchAsync > DialogService.ShowMessageAsync                                                       ①
                                > EntrySearchService.SearchAsync > BuildDomainAsync                                    ②
                                                                 > OdooRpc.SearchReadAsync [rpc hp.entry search_read]
                                                                 > FindLocalFiles                                      ③
                                > RangeObservableCollection.ReplaceAll
                                > DialogService.ShowMessageAsync                                                       ④
```

① when MaxResults is not a number  
② one hp.version.property search_read per property filter  
③ when LocalOnly or DeletedRemotely: scans the vault  
④ "No results found." or "Finished!"  

#### SearchOdooViewModel.Reset

```text
SearchOdooViewModel.Reset > ObservableCollection.Clear  ①
```

① results and property filters; form fields reset  

#### SearchOdooViewModel.AddPropertyAsync

```text
SearchOdooViewModel.AddPropertyAsync > DialogService.ShowMessageAsync  ①
                                     > ObservableCollection.Add        ②
```

① when no property or operator is picked  
② PropertiesActive  

#### SearchOdooViewModel.DeleteProperties

```text
SearchOdooViewModel.DeleteProperties > ObservableCollection.Remove  ①
```

① selected PropertiesActive rows  

#### SearchOdooViewModel.ResetProperties

```text
SearchOdooViewModel.ResetProperties > ObservableCollection.Clear  ①
```

① PropertiesActive  

#### SearchOdooViewModel.CheckoutAsync

```text
SearchOdooViewModel.CheckoutAsync > SetCheckoutAsync > DialogService.ShowMessageAsync  ①
                                                     > RunOnIdsAsync                   ②
```

① when LocalOnly  
② runs PdmWorkflowService.CheckoutLatestAsync (get latest + checkout) in the status window; nothing when no remote ids  

#### SearchOdooViewModel.UndoCheckoutAsync

```text
SearchOdooViewModel.UndoCheckoutAsync > SetCheckoutAsync > DialogService.ShowMessageAsync  ①
                                                         > RunOnIdsAsync                   ②
```

① when LocalOnly  
② runs PdmWorkflowService.UnCheckoutAsync  

#### SearchOdooViewModel.CheckoutAndOpenAsync

```text
SearchOdooViewModel.CheckoutAndOpenAsync > SetCheckoutAsync > RunOnIdsAsync                    ①
                                         > OpenAsync > EntryFileService.OpenLatestRemoteAsync  ②
```

① PdmWorkflowService.CheckoutLatestAsync  
② when remote ids were checked out  

#### SearchOdooViewModel.OpenAsync

```text
SearchOdooViewModel.OpenAsync > EntryFileService.OpenLatestRemoteAsync > HpEntry.GetRelatedRecordByIdsAsync [rpc hp.entry related_browse]  ①
                                                                       > HpVersion.DownloadFileAsync [rpc hp.version read file_contents]   ②
                                                                       > OpenFile
                              > EntryFileService.OpenFolder                                                                                ③
```

① remote rows  
② to the system temp folder  
③ local rows  

#### SearchOdooViewModel.LocateAsync

```text
SearchOdooViewModel.LocateAsync > EntrySearchService.TreeSegmentsBelowRoot > PwaPaths.LocalSegments              ①
                                > AppComposition.LocateInFileManagerAsync > FileManagerState.RequestLocateAsync
                                                                          > NavigationService.NavigateTo         ②
```

① local rows only  
② HackFileManager  

#### SearchOdooView (ctor)

```text
SearchOdooView (ctor) > AppComposition.SearchOdoo                                                     ①
                      > SearchOdooViewModel.LoadLookups > EntrySearchService.GetSearchableProperties  ②
```

① first use builds the run-wide SearchOdooViewModel  
② from cached OdooDefaults.IdToProp, no rpc  

#### SearchOdooView.OdooCancel_Click

```text
SearchOdooView.OdooCancel_Click > Frame.GoBack  ①
                                > Window.Close  ②
```

① when the frame can go back  
② otherwise: the search window  

#### SearchOdooView.OdooSearchResults_DoubleTapped

```text
SearchOdooView.OdooSearchResults_DoubleTapped > SearchOdooViewModel.LocateCommand > LocateAsync
```

[↑ contents](#contents)

### Settings & theme

#### HackSettingsViewModel.SubmitAsync

```text
HackSettingsViewModel.SubmitAsync > Validate > TryCreateDirectory                                      ①
                                             > TryCreateDirectory                                      ②
                                  > DialogService.ShowMessageAsync                                     ③
                                  > HackDefaults.PwaPathAbsolute (set) > JsonFileSettingsProvider.Set  ④
                                  > HackDefaults.PwaPathRelative (set)
                                  > StorageBox.TemporaryPath (set)                                     ⑤
                                  > DialogService.ShowMessageAsync                                     ⑥
                                  > Saved > Window.Close                                               ⑦
```

① PWA folder; created even when the other path fails  
② temp folder  
③ when a folder can't be created: nothing saved  
④ settings.json  
⑤ in memory only, not persisted  
⑥ "Hack settings saved"  
⑦ HackSettingsView: only when the page has its own window  

#### OdooSettingsViewModel.SubmitAsync

```text
OdooSettingsViewModel.SubmitAsync > DialogService.ShowMessageAsync                                                        ①
                                  > OdooDefaults (set address/port/db/sw key/area factor) > JsonFileSettingsProvider.Set  ②
                                  > OdooDefaults (set user/pass/url)                                                      ③
                                  > DialogService.ShowMessageAsync                                                        ④
                                  > Saved > Window.Close                                                                  ⑤
```

① when the area factor is not a decimal: nothing saved  
② settings.json  
③ in memory only; keychain gets user/pass on the next successful login; OdooId is not reset, so that Login skips common.login  
④ "Odoo settings saved"  
⑤ OdooSettingsView: only when the page has its own window  

#### ThemeEditorViewModel.OnSelectedModeIndexChanged

```text
ThemeEditorViewModel.OnSelectedModeIndexChanged > ThemeService.SetMode > ApplyMode  ①
                                                > Refresh
                                                > PublishSelected
```

① mode combo box; every attached window root  

#### ThemeEditorViewModel.ResetSelected

```text
ThemeEditorViewModel.ResetSelected > ThemeService.ResetColor > Restore
                                                             > RefreshOwnedBackgrounds
                                   > Refresh > ThemeService.GetColor
                                             > ThemeService.IsOverridden
                                   > PublishSelected > ThemeEditorView.ShowInPicker     ①
```

① SelectedColorValueChanged  

#### ThemeEditorViewModel.ResetAll

```text
ThemeEditorViewModel.ResetAll > ThemeService.ResetAll > Restore                  ①
                                                      > RefreshOwnedBackgrounds
                              > Refresh
                              > PublishSelected
```

① each overridden key  

#### ThemeEditorViewModel.Save

```text
ThemeEditorViewModel.Save > ThemeService.Save > AppConfigStore.Save  ①
```

① AppConfig.json; restart needed when the mode changed  

#### ThemeEditorViewModel.Revert

```text
ThemeEditorViewModel.Revert > ThemeService.Revert > Restore
                                                  > AppConfigStore.Load
                                                  > ApplyOverrides
                                                  > ApplyMode
                            > Refresh
                            > PublishSelected
```

#### ThemeEditorView.Picker_ColorChanged

```text
ThemeEditorView.Picker_ColorChanged > ThemeEditorViewModel.SetSelectedColor > ThemeService.SetColor  ①
                                                                            > Refresh
```

① ignored while the view model drives the picker; live brush recolor; no-op when a restart is pending  

#### ThemeEditorView.ShowDialog_Click

```text
ThemeEditorView.ShowDialog_Click > ContentDialog.ShowAsync  ①
```

① preview of the dialog surface only  

#### ApplicationSettingsView.SettingsSections_SelectionChanged

```text
ApplicationSettingsView.SettingsSections_SelectionChanged > ShowSection > Frame.Navigate  ①
```

① ThemeEditorView (the only section)  

[↑ contents](#contents)

### Status window

#### StatusWindowReporter.BeginOperation

```text
StatusWindowReporter.BeginOperation > CancellationTokenSource (ctor)            ①
                                    > CancellationToken.Register > OnCancelled  ②
```

① only when the previous one was cancelled  
② callback; also sets StatusDialogViewModel.IsInProcess = true first  

#### StatusWindowReporter.Cancel

```text
StatusWindowReporter.Cancel > CancellationTokenSource.Cancel > OnCancelled  ①
```

① registered callback  

#### StatusWindowReporter.OnCancelled

```text
StatusWindowReporter.OnCancelled > DialogService.ShowMessageAsync  ①
```

① "Cancelled Operation"; IsInProcess = false first  

#### StatusWindowReporter.ShowWindow

```text
StatusWindowReporter.ShowWindow > DialogService.ShowStatusWindow > Window.Activate                                     ①
                                                                 > WindowHelper.CreateWindowAndPage<StatusDialogView>  ②
```

① when already open  
② otherwise  

#### StatusWindowReporter.Log

```text
StatusWindowReporter.Log > StatusLogSession.Log > WeakReferenceMessenger.Send  ①
```

① to StatusDialogViewModel.Receive > AddLog  

#### StatusWindowReporter.SetTotal / Advance / SetProcessed / Complete

```text
StatusWindowReporter.SetTotal / Advance / SetProcessed / Complete > StatusDialogViewModel.ResetProgress / Advance / SetProcessed / CompleteProgress > Update  ①
```

① one coalesced UI refresh  

#### StatusDialogViewModel.Cancel

```text
StatusDialogViewModel.Cancel > StatusWindowReporter.Cancel > CancellationTokenSource.Cancel  ①
```

① via AppComposition's cancelOperation callback  

#### StatusDialogView.CmdCloseClick

```text
StatusDialogView.CmdCloseClick > Window.Close > AppWindow_Closing  ①
```

① Closing event  

#### StatusDialogView.StatusSettings_Click

```text
StatusDialogView.StatusSettings_Click > WindowHelper.CreateWindowPage<ApplicationSettingsView> > CreateWindowAndPage
```

#### StatusDialogView.AppWindow_Closing

```text
StatusDialogView.AppWindow_Closing > StatusDialogViewModel.ConfirmCloseAsync > DialogService.ShowMessageAsync  ①
                                                                             > StatusWindowReporter.Cancel     ②
                                   > Window.Close                                                              ③
```

① only while IsInProcess; the close is held; OK/Cancel  
② when the user confirms  
③ when confirmed  

[↑ contents](#contents)

### JSON editor & tools

#### JsonEditorViewModel.Load

```text
JsonEditorViewModel.Load > AppConfigJsonService.Load > File.ReadAllText  ①
                         > Show > JsonNodeViewModel (ctor)               ②
```

① AppConfig.json; sample JSON when missing  
② one per root key  

#### JsonEditorView.OnExportJsonClick

```text
JsonEditorView.OnExportJsonClick > JsonEditorViewModel.ToJson > JsonNodeViewModel.ToJsonNode
                                                              > AppConfigJsonService.Format
                                 > ContentDialog.ShowAsync
                                 > JsonEditorViewModel.Save > AppConfigJsonService.Save       ①
```

① when Save is pressed; writes AppConfig.json; UIStorage.GlobalData not updated, so Window_Closed overwrites it on exit  

#### JsonEditorView.OnAddChildClick

```text
JsonEditorView.OnAddChildClick > JsonNodeViewModel.AddChildProperty > JsonNodeViewModel (ctor)  ①
```

① objects and arrays only  

#### JsonEditorView.OnDeleteNodeClick

```text
JsonEditorView.OnDeleteNodeClick > JsonEditorViewModel.Remove > JsonNodeViewModel.DeleteSelf  ①
                                                              > ObservableCollection.Remove   ②
```

① child node  
② root node  

#### JsonEditorView.OnViewModeChanged

```text
JsonEditorView.OnViewModeChanged > SyncTreeToCodeView > JsonEditorViewModel.ToJson    ①
                                                      > ApplyJsonSyntaxHighlighting
                                 > SyncCodeViewToTree > RichEditBox.Document.GetText  ②
                                                      > JsonEditorViewModel.LoadText  ③
```

① Code tab  
② Tree tab  
③ parse error: InfoBar shown and Code tab reselected  

#### JsonEditorView.OnNavigatedFrom

```text
JsonEditorView.OnNavigatedFrom > RichEditBox.Document.GetText       ①
                               > JsonEditorViewModel.Keep > ToJson  ②
```

① when the code view is shown  
② kept in AppComposition.JsonDraft for the next visit  

[↑ contents](#contents)

### Navigation

#### HomeView (ctor)

```text
HomeView (ctor) > NavigationService.Attach
                > HomeNavigator.SelectedItem = ProfileManager > HomeNavigator_SelectionChanged
```

#### NavigationService.NavigateTo

```text
NavigationService.NavigateTo > HomeView.Select > HomeNavigator_SelectionChanged  ①
```

① via SelectedItem; Settings selects the settings item  

#### HomeView.HomeNavigator_SelectionChanged

```text
HomeView.HomeNavigator_SelectionChanged > Show > ShowConfiguration  ①
                                               > Navigate           ②
```

① Configuration: navigates to ConfigurationView  
② NotLoggedInView when HackFileManager is picked while logged out; else the page from the Pages table  

#### HomeView.Navigate

```text
HomeView.Navigate > Frame.Navigate                                                 ①
                  > WindowHelper.SetWindowConfig > WindowConfig.Preset
                                                 > WindowExtensions.SetWindowType
                                                 > AppWindow.Move / Resize
                                                 > X11WindowSize.TryResize
```

① creates the page; the page builds its view model  

#### HomeView.HomeNavigator_BackRequested

```text
HomeView.HomeNavigator_BackRequested > Frame.GoBack  ①
```

① when the frame can go back  

#### ProfileManagerView (ctor)

```text
ProfileManagerView (ctor) > AppComposition.CreateProfileManager > ProfileManagerViewModel (ctor)  ①
```

① OStatus seeded from LoginSession.LastErrors; no server call  

#### ConfigurationView (ctor)

```text
ConfigurationView (ctor) > Frame.Navigate > OdooSettingsView (ctor)
                         > Frame.Navigate > HackSettingsView (ctor)
```

#### OdooSettingsView (ctor)

```text
OdooSettingsView (ctor) > AppComposition.CreateOdooSettings > OdooSettingsViewModel (ctor)  ①
```

① reads OdooDefaults; user/pass from the OS keychain on first read  

#### HackSettingsView (ctor)

```text
HackSettingsView (ctor) > AppComposition.CreateHackSettings > HackSettingsViewModel (ctor)  ①
```

① current PWA and temp folders, or defaults  

#### ApplicationSettingsView (ctor)

```text
ApplicationSettingsView (ctor) > ShowSection > Frame.Navigate  ①
```

① on Loaded; ThemeEditorView  

#### ThemeEditorView (ctor)

```text
ThemeEditorView (ctor) > AppComposition.CreateThemeEditor > ThemeEditorViewModel (ctor)  ①
                       > ShowInPicker
                       > FillSamples
```

① fills Colors from ThemeColorCatalog, then Refresh  

#### JsonEditorView (ctor)

```text
JsonEditorView (ctor) > JsonEditorViewModel (ctor) > LoadText  ①
```

① when AppComposition.JsonDraft kept text from the last visit  

#### HackFileManagerView.Search_Click

```text
HackFileManagerView.Search_Click > WindowHelper.CreateWindowAndPage<SearchOdooView> > SearchOdooView (ctor)  ①
                                                                                    > ThemeService.Attach
                                                                                    > SetWindowConfig
                                 > SearchOdooView.StoreWindowInstance
```

① opens the search window  

#### PipWindow (ctor)

```text
PipWindow (ctor) > ConfigureCompactOverlay > AppWindow.SetPresenter  ①
```

① CompactOverlay  

#### PipWindow.OnExitPipClicked

```text
PipWindow.OnExitPipClicked > ReturnRequested > HackFileManagerView.ReturnPreviewToPage  ①
```

① the only subscriber  

[↑ contents](#contents)

## File manager

Pass-through plumbing is folded into the call it forwards to: OperationViewModel.RunOperationAsync / RunOnIdsAsync, RunOnDirectoryAsync, WithHistoryVersionAsync, AsyncHelper.AsyncRunner, UiDispatcher.Post, and one-line private forwards (PdmWorkflowService.CheckoutAsync/ChangeCheckoutAsync, CommitFilesAsync, DownloadLatestAsync). `HackFileManagerView.OnX  # via Event` = raising that view-model event.

### Commands — commit/checkout/get latest

#### CommitSelectedCommand

```text
CommitSelectedCommand > PdmWorkflowService.CollectCommitFilesAsync > DependencyScanner.GetHackFileWithDependencies  ①
                                                                   > HackFile.GetFromPath                           ②
                      > PdmWorkflowService.CommitAsync > PdmCommitOperations.FilterCommitHackFilesAsync             ③
                                                       > CheckInUnchangedAsync                                      ④
                                                       > HpPDMCommit.CreateCommitAsync [rpc hp.pdm.commit.create]
                                                       > PdmCommitOperations.CommitRecordAsync                      ⑤
                                                       > HpPDMCommit.ServerCommit [rpc hp.pdm.commit.start_commit]
                                                       > CheckInUnchangedAsync                                      ⑥
```

① SolidWorks rows: the file plus everything it references  
② other rows whose file exists  
③ when any files were collected  
④ nothing new or changed, only checked-out unchanged files: checks them in, no commit  
⑤ per new or changed file; stops at the first failure  
⑥ after the server commit succeeded  

#### CommitDirectoryCommand

```text
CommitDirectoryCommand > PdmWorkflowService.CollectFolderCommitFilesAsync > DependencyScanner.GetHackFolderWithDependenciesAsync  ①
                       > PdmWorkflowService.CommitAsync > PdmCommitOperations.FilterCommitHackFilesAsync                          ②
                                                        > CheckInUnchangedAsync                                                   ③
                                                        > HpPDMCommit.CreateCommitAsync [rpc hp.pdm.commit.create]
                                                        > PdmCommitOperations.CommitRecordAsync                                   ④
                                                        > HpPDMCommit.ServerCommit [rpc hp.pdm.commit.start_commit]
                                                        > CheckInUnchangedAsync                                                   ⑤
```

① not for a remote-only folder  
② when any files were collected  
③ nothing new or changed, only checked-out unchanged files  
④ per new or changed file; stops at the first failure  
⑤ after the server commit succeeded  

#### GetLatestSelectedCommand

```text
GetLatestSelectedCommand > PdmWorkflowService.GetLatestAsync > ExpandWithDependenciesAsync                                                 ①
                                                             > PdmDownloadOperations.GetLatestVersionsAsync [rpc hp.entry.related_browse]
                                                             > PdmDownloadOperations.ProcessDownloadsAsync
```

① only when a selected row has a server id  

#### GetLatestDirectoryCommand

```text
GetLatestDirectoryCommand > Dialogs.ShowMessageAsync                                                                                                   ①
                          > PdmWorkflowService.GetDirectoryEntryIdsAsync > HpDirectory.GetDirectoryEntryIDsAsync [rpc hp.directory.get_all_entry_ids]  ②
                          > PdmWorkflowService.GetLatestAsync > ExpandWithDependenciesAsync
                                                              > PdmDownloadOperations.GetLatestVersionsAsync [rpc hp.entry.related_browse]
                                                              > PdmDownloadOperations.ProcessDownloadsAsync
```

① no directory selected  
② with subfolders; deleted too while ShowInactive  

#### GetLatestDirectoryTopCommand

```text
GetLatestDirectoryTopCommand > Dialogs.ShowMessageAsync                                                                                                   ①
                             > PdmWorkflowService.GetDirectoryEntryIdsAsync > HpDirectory.GetDirectoryEntryIDsAsync [rpc hp.directory.get_all_entry_ids]  ②
                             > PdmWorkflowService.GetLatestAsync > ExpandWithDependenciesAsync
                                                                 > PdmDownloadOperations.GetLatestVersionsAsync [rpc hp.entry.related_browse]
                                                                 > PdmDownloadOperations.ProcessDownloadsAsync
```

① no directory selected  
② this folder only; deleted too while ShowInactive  

#### CheckoutSelectedCommand

```text
CheckoutSelectedCommand > PdmWorkflowService.CheckoutLatestAsync > GetLatestAsync                                                              ①
                                                                 > ExpandWithDependenciesAsync                                                 ②
                                                                 > HpEntry.GetRecordsByIdsAsync [rpc hp.entry.read]
                                                                 > PdmCheckoutOperations.CheckOutEntriesAsync [rpc hp.version.checkout_entry]
```

① rows nobody has checked out  
② CheckoutAsync, after the download finished  

#### CheckoutDirectoryCommand

```text
CheckoutDirectoryCommand > Dialogs.ShowMessageAsync                                                                                                   ①
                         > PdmWorkflowService.GetDirectoryEntryIdsAsync > HpDirectory.GetDirectoryEntryIDsAsync [rpc hp.directory.get_all_entry_ids]  ②
                         > PdmWorkflowService.CheckoutLatestAsync > GetLatestAsync
                                                                  > ExpandWithDependenciesAsync                                                       ③
                                                                  > HpEntry.GetRecordsByIdsAsync [rpc hp.entry.read]
                                                                  > PdmCheckoutOperations.CheckOutEntriesAsync [rpc hp.version.checkout_entry]
```

① no directory selected  
② with subfolders  
③ CheckoutAsync, after the download finished  

#### UndoCheckoutSelectedCommand

```text
UndoCheckoutSelectedCommand > PdmWorkflowService.UnCheckoutAsync > ExpandWithDependenciesAsync                                        ①
                                                                 > HpEntry.GetRecordsByIdsAsync [rpc hp.entry.search_read]            ②
                                                                 > HpEntry.GetRecordsByIdsAsync [rpc hp.entry.search_read]            ③
                                                                 > PdmCheckoutOperations.UnCheckOutEntriesAsync [rpc hp.entry.write]
```

① checked-out rows only  
② the entries checked out to this user  
③ selected entries held by others: logged, left alone  

#### UndoCheckoutDirectoryCommand

```text
UndoCheckoutDirectoryCommand > Dialogs.ShowMessageAsync                                                                                                   ①
                             > PdmWorkflowService.GetDirectoryEntryIdsAsync > HpDirectory.GetDirectoryEntryIDsAsync [rpc hp.directory.get_all_entry_ids]  ②
                             > PdmWorkflowService.UnCheckoutAsync > ExpandWithDependenciesAsync
                                                                  > HpEntry.GetRecordsByIdsAsync [rpc hp.entry.search_read]                               ③
                                                                  > HpEntry.GetRecordsByIdsAsync [rpc hp.entry.search_read]                               ④
                                                                  > PdmCheckoutOperations.UnCheckOutEntriesAsync [rpc hp.entry.write]
```

① no directory selected  
② with subfolders  
③ the entries checked out to this user  
④ selected entries held by others: logged, left alone  

[↑ contents](#contents)

### Commands — history versions

#### DownloadHistoryCommand

```text
DownloadHistoryCommand > EntryFileService.GetVersionAsync > HpVersion.GetRecordByIdAsync [rpc hp.version.read]
                       > EntryFileService.HasConflictingLocalCopy > Checksums.Matches                                        ①
                       > EntryFileService.DownloadVersionAsync > VersionFileService.DownloadFileAsync [rpc hp.version.read]  ②
                       > Dialogs.ShowMessageAsync                                                                            ③
                       > EntryFileService.DownloadVersionAsync > VersionFileService.DownloadFileAsync [rpc hp.version.read]  ④
```

① when a history row is selected  
② no conflicting local copy: into the working folder  
③ a different local version exists: Retry = temp, Ignore = overwrite, Abort  
④ after Retry or Ignore  

#### DownloadHistoryToTempCommand

```text
DownloadHistoryToTempCommand > EntryFileService.GetVersionAsync > HpVersion.GetRecordByIdAsync [rpc hp.version.read]
                             > EntryFileService.DownloadVersionAsync > VersionFileService.DownloadFileAsync [rpc hp.version.read]  ①
```

① into the temp folder  

#### DownloadHistoryOverwriteCommand

```text
DownloadHistoryOverwriteCommand > EntryFileService.GetVersionAsync > HpVersion.GetRecordByIdAsync [rpc hp.version.read]
                                > EntryFileService.DownloadVersionAsync > VersionFileService.DownloadFileAsync [rpc hp.version.read]  ①
```

① into the working folder, overwriting  

#### OpenHistoryFromTempCommand

```text
OpenHistoryFromTempCommand > EntryFileService.GetVersionAsync > HpVersion.GetRecordByIdAsync [rpc hp.version.read]
                           > OpenHistoryAsync > EntryFileService.DownloadVersionAsync                               ①
                                              > EntryFileService.OpenFile
```

① into the temp folder  

#### OpenHistoryOverwriteCommand

```text
OpenHistoryOverwriteCommand > EntryFileService.GetVersionAsync > HpVersion.GetRecordByIdAsync [rpc hp.version.read]
                            > OpenHistoryAsync > EntryFileService.DownloadVersionAsync                               ①
                                               > EntryFileService.OpenFile
```

① into the working folder, overwriting  

#### MoveHistoryToTempCommand

```text
MoveHistoryToTempCommand > EntryFileService.GetVersionAsync > HpVersion.GetRecordByIdAsync [rpc hp.version.read]
                         > MoveHistoryAsync > EntryFileService.PlanMove
                                            > Dialogs.ConfirmAsync
                                            > EntryFileService.ExecuteMoveAsync                                   ①
                                            > Refresh                                                             ②
```

① after ConfirmAsync: moves the file, or downloads it when missing  
② always, confirmed or not  

#### MoveHistoryToWorkingCommand

```text
MoveHistoryToWorkingCommand > EntryFileService.GetVersionAsync > HpVersion.GetRecordByIdAsync [rpc hp.version.read]
                            > MoveHistoryAsync > EntryFileService.PlanMove
                                               > Dialogs.ConfirmAsync
                                               > EntryFileService.ExecuteMoveAsync                                   ①
                                               > Refresh                                                             ②
```

① after ConfirmAsync: moves the file, or downloads it when missing  
② always, confirmed or not  

#### DownloadHistoryWithDependenciesCommand

```text
DownloadHistoryWithDependenciesCommand > PdmWorkflowService.PlanVersionDownloadAsync > HpVersion.GetRecordsByIdsAsync [rpc hp.version.read]                                          ①
                                                                                     > HpEntry.GetRecordByIdAsync [rpc hp.entry.read]
                                                                                     > PdmDownloadOperations.GetVersionListAsync [rpc hp.version.get_recursive_dependency_versions]
                                       > Dialogs.ShowMessageAsync                                                                                                                    ②
                                       > PdmWorkflowService.DownloadVersionsAsync > HpVersion.GetRecordsByIdsAsync [rpc hp.version.read]                                             ③
                                                                                  > VersionFileService.DownloadAllAsync
                                       > Dialogs.ShowMessageAsync                                                                                                                    ④
```

① when a history row is selected  
② Yes/No/Cancel: lists the version ids to download  
③ after Yes  
④ some versions failed to download  

[↑ contents](#contents)

### Commands — delete/restore

#### LogicalDeleteSelectedCommand

```text
LogicalDeleteSelectedCommand > PdmWorkflowService.LogicalDeleteAsync > HpEntry.GetRecordsByIdsAsync [rpc hp.entry.read]          ①
                                                                     > PdmDeleteOperations.SetDeletedAsync [rpc hp.entry.write]
                             > Refresh > FileManagerState.MarkStale
                                       > HackFileManagerView.OnRefreshRequested                                                  ②
```

① only when a selected row has a server id  
② via RefreshRequested; reloads tree and rows  

#### LogicalDeleteDirectoryCommand

```text
LogicalDeleteDirectoryCommand > Dialogs.ShowMessageAsync                                                                                                   ①
                              > PdmWorkflowService.GetDirectoryEntryIdsAsync > HpDirectory.GetDirectoryEntryIDsAsync [rpc hp.directory.get_all_entry_ids]  ②
                              > PdmWorkflowService.LogicalDeleteAsync > HpEntry.GetRecordsByIdsAsync [rpc hp.entry.read]
                                                                      > PdmDeleteOperations.SetDeletedAsync [rpc hp.entry.write]
                              > Refresh > FileManagerState.MarkStale                                                                                       ③
                                        > HackFileManagerView.OnRefreshRequested                                                                           ④
```

① no directory selected  
② with subfolders  
③ runs even when no directory was selected  
④ via RefreshRequested  

#### RestoreDirectoryCommand

```text
RestoreDirectoryCommand > PdmWorkflowService.RestoreDeletedInDirectoryAsync > HpEntry.GetRecordsByIdsAsync [rpc hp.entry.search_read]   ①
                                                                            > PdmDeleteOperations.SetDeletedAsync [rpc hp.entry.write]
                        > Refresh > FileManagerState.MarkStale
                                  > HackFileManagerView.OnRefreshRequested                                                              ②
```

① deleted entries directly in the folder  
② via RefreshRequested  

#### RestoreDirectoryRecursiveCommand

```text
RestoreDirectoryRecursiveCommand > Dialogs.ShowMessageAsync  ①
```

① "Not Implemented Yet"  

#### PermanentDeleteSelectedCommand

```text
PermanentDeleteSelectedCommand > Dialogs.ShowMessageAsync                                                                                                                                     ①
                               > Dialogs.ConfirmAsync                                                                                                                                         ②
                               > PdmWorkflowService.PermanentDeleteAsync > PdmDeleteOperations.PermanentDeleteEntry [rpc unlink on hp.version.property, ir.attachment, hp.version, hp.entry]  ③
                               > Dialogs.ShowMessageAsync                                                                                                                                     ④
                               > Refresh > FileManagerState.MarkStale                                                                                                                         ⑤
                                         > HackFileManagerView.OnRefreshRequested                                                                                                             ⑥
```

① Release builds: "only available in debug builds"; DEBUG: when no row has a server id  
② DEBUG builds only  
③ after ConfirmAsync  
④ "Was unable to delete entries" or "Completed!"  
⑤ when the delete succeeded  
⑥ via RefreshRequested  

#### LocalDeleteSelectedCommand

```text
LocalDeleteSelectedCommand > EntryFileService.FindLocalFiles
                           > Dialogs.ConfirmAsync                              ①
                           > EntryFileService.DeleteFiles > FileInfo.Delete    ②
                           > Refresh > FileManagerState.MarkStale              ③
                                     > HackFileManagerView.OnRefreshRequested  ④
```

① when any selected row has a local file  
② after ConfirmAsync  
③ confirmed or not  
④ via RefreshRequested; rows only  

#### LocalDeleteDirectoryCommand

```text
LocalDeleteDirectoryCommand > EntryFileService.CountFiles
                            > Dialogs.ConfirmAsync                                 ①
                            > EntryFileService.DeleteDirectory > Directory.Delete  ②
```

① when the local folder exists  
② after ConfirmAsync; server not touched; recursive  

[↑ contents](#contents)

### Commands — navigation & view

#### GroupByCommand

```text
GroupByCommand > EntryRowKeys.TryGetGroupKey
               > FileManagerState.GroupField                                      ①
               > HackFileManagerView.OnGroupingChanged > EntryListGrouping.Apply  ②
```

① set; remembered for later visits  
② via GroupingChanged  

#### OpenSelectedCommand

```text
OpenSelectedCommand > EntryFileService.OpenEntriesAsync > FileStatusPolicy.ForOpen  ①
                                                        > OpenFile                  ②
                                                        > OpenLatestRemoteAsync     ③
                    > Dialogs.ShowMessageAsync                                      ④
```

① picks local copy, latest remote, or nothing per row  
② local copy  
③ remote only: downloads the latest to temp and opens it  
④ some rows could not be opened  

#### OpenSelectedLatestRemoteCommand

```text
OpenSelectedLatestRemoteCommand > EntryFileService.OpenEntriesAsync > FileStatusPolicy.ForLatestRemote
                                                                    > OpenLatestRemoteAsync             ①
                                > Dialogs.ShowMessageAsync                                              ②
```

① rows with a server id  
② local-only rows: "can't open local only file remotely"  

#### OpenSelectedLatestLocalCommand

```text
OpenSelectedLatestLocalCommand > EntryFileService.OpenEntriesAsync > FileStatusPolicy.ForLatestLocal
                                                                   > OpenFile                         ①
                               > Dialogs.ShowMessageAsync                                             ②
```

① rows with a local copy  
② remote-only rows: "can't open remote only file locally"  

#### OpenSelectedFoldersCommand

```text
OpenSelectedFoldersCommand > EntryFileService.OpenContainingFolders > ShellLauncher.OpenFolder  ①
```

① once per folder holding an existing file  

#### OpenDirectoryCommand

```text
OpenDirectoryCommand > EntryFileService.OpenFolder > ShellLauncher.OpenFolder  ①
```

① when the local folder exists  

#### UploadIconCommand

```text
UploadIconCommand > FileStatusPolicy.CanUploadIcon
                  > Dialogs.ShowMessageAsync        ①
```

① exactly one eligible row: "not implemented yet"  

[↑ contents](#contents)

### View event handlers

#### OdooDirectoryTree_SelectionChanged

```text
OdooDirectoryTree_SelectionChanged > SelectDirectory > HackFileManagerViewModel.SelectedDirectory  ①
                                                     > TreeViewNode.UpdateBreadCrumbCollection
                                   > TreeData.EnsureVisible
                                   > LoadSelectedEntriesAsync > LoadAndRestoreAsync                ②
```

① skipped when the node is the one already loading; set: FileManagerState.SelectedDirectory and SelectedPath  
② cancels an older load; runs HackFileManagerViewModel.LoadEntriesAsync  

#### OdooDirectoryTree_RightTapped

```text
OdooDirectoryTree_RightTapped > OdooDirectoryTree.SelectedNode                                ①
                              > SelectDirectory > HackFileManagerViewModel.SelectedDirectory  ②
                                                > TreeViewNode.UpdateBreadCrumbCollection
```

① set: raises OdooDirectoryTree_SelectionChanged  
② set  

#### OdooDirectoryBreadcrumb_ItemClicked

```text
OdooDirectoryBreadcrumb_ItemClicked > OdooDirectoryTree.SelectedNode                                ①
                                    > TreeData.EnsureVisible
                                    > SelectDirectory > HackFileManagerViewModel.SelectedDirectory  ②
                                                      > TreeViewNode.UpdateBreadCrumbCollection
```

① set: raises OdooDirectoryTree_SelectionChanged  
② set  

#### TreeViewItem_Loaded

```text
TreeViewItem_Loaded > RemoveBadges  ①
```

① a recycled container drops another folder's change badge  

#### TreeViewItem_Unloaded

```text
TreeViewItem_Unloaded > ItemToContainerMap.Remove
```

#### OdooEntryList_SelectionChanged

```text
OdooEntryList_SelectionChanged > HackFileManagerViewModel.SelectedEntries > FileManagerState.SelectedEntryNames                                   ①
                               > HackFileManagerViewModel.ShowEntryDetailsAsync > ClearDetails                                                    ②
                                                                                > LoadDetailTabAsync
                                                                                > EntryDetailsService.GetPreviewImageAsync [rpc hp.version.read]
                                                                                > EntryDetailsService.GetLocalPreviewImageAsync                   ③
```

① set; once the list is loaded  
② exactly one row selected  
③ no server preview and a local file exists  

#### VersionTabs_SelectionChanged

```text
VersionTabs_SelectionChanged > HackFileManagerViewModel.SelectedDetailTab                                                                       ①
                             > HackFileManagerViewModel.ShowEntryDetailsAsync > ClearDetails                                                    ②
                                                                              > LoadDetailTabAsync
                                                                              > EntryDetailsService.GetPreviewImageAsync [rpc hp.version.read]
                                                                              > EntryDetailsService.GetLocalPreviewImageAsync                   ③
```

① set: FileManagerState.SelectedDetailTab  
② when a row is selected  
③ no server preview and a local file exists  

#### OdooEntryList_Sorting

```text
OdooEntryList_Sorting > HackFileManagerViewModel.SortEntries > EntryRowKeys.TryGetSortKey
                                                             > Entries.Sort
                                                             > FileManagerState.Sort                  ①
                                                             > HackFileManagerView.OnGroupingChanged  ②
```

① set; remembered for later visits  
② via GroupingChanged  

#### OdooHistory_SelectionChanged

```text
OdooHistory_SelectionChanged > HackFileManagerViewModel.SelectedHistory                       ①
                             > PreviewVersion > HackFileManagerViewModel.PreviewVersionAsync
```

① set; the target of the history commands  

#### OdooParents_DoubleTapped

```text
OdooParents_DoubleTapped > FindSearchSelectionAsync > OnLocateRequestedAsync  ①
```

① exactly one parent row selected  

#### OdooChildren_DoubleTapped

```text
OdooChildren_DoubleTapped > FindSearchSelectionAsync > OnLocateRequestedAsync  ①
```

① exactly one child row selected  

#### Refresh_Click

```text
Refresh_Click > HackFileManagerViewModel.RefreshFromServer > FileManagerState.MarkStale              ①
                                                           > HackFileManagerView.OnRefreshRequested  ②
```

① All  
② via RefreshRequested (Tree); the rebuilt tree reselects the folder and relists it  

#### Search_Click

```text
Search_Click > WindowHelper.CreateWindowAndPage<SearchOdooView>
             > SearchOdooView.StoreWindowInstance
```

#### ManageTypes_Click

```text
ManageTypes_Click > WindowHelper.CreateWindowPage<OdooFileTypeManagerView>
```

#### Anchor_Checked

```text
Anchor_Checked > OverlappedPresenter.IsAlwaysOnTop  ①
```

① set  

#### OnTogglePipClicked

```text
OnTogglePipClicked > ReturnPreviewToPage > PipWindow.ReleaseComponent  ①
                                         > PipWindow.Close
                   > PipWindow.HostComponent                           ②
                   > PipWindow.Activate
```

① PiP window already open  
② otherwise: moves the preview image into a new PiP window  

#### OnPipWindowClosed

```text
OnPipWindowClosed > ImageSlot.Children.Add  ①
```

① posted to the UI thread: puts the preview image back  

#### OnViewModelPropertyChanged

```text
OnViewModelPropertyChanged > ShowPreviewAsync > PreviewImageLoader.FromBytesAsync     ①
                                              > PreviewImageLoader.FromFileIconAsync  ②
```

① when Preview changed; image bytes  
② local file without an image  

#### OnRefreshRequested

```text
OnRefreshRequested > HackFileManagerViewModel.LoadDirectoryTreeAsync  ①
                   > ReloadEntriesAsync > LoadSelectedEntriesAsync    ②
```

① Tree flag  
② Entries flag, when a folder is selected  

#### OnTreeLoadedAsync

```text
OnTreeLoadedAsync > DirectoryTreeBuilder.Build
                  > TreeData.EnsureVisible                                        ①
                  > SelectDirectory > HackFileManagerViewModel.SelectedDirectory  ②
```

① the previously selected folder  
② set; selecting the node also raises OdooDirectoryTree_SelectionChanged  

#### OnFolderChanged

```text
OnFolderChanged > VisibleNodeFor
                > RemoveBadges
                > Storyboard.Begin  ①
```

① fades a created/updated/deleted badge in and out on the folder  

#### OnRowReplaced

```text
OnRowReplaced > OdooEntryList.SelectedItem  ①
```

① set when the replaced row was selected: reselects the new row  

#### OnGroupingChanged

```text
OnGroupingChanged > EntryListGrouping.Apply
```

[↑ contents](#contents)

### File watcher

#### FileSystemWatcher.Created

```text
FileSystemWatcher.Created > Add > LocalChangeBatcher.Add
                          > AddContentsOfNewFolder > Add  ①
```

① a new folder: each entry inside it, up to 1000  

#### FileSystemWatcher.Changed

```text
FileSystemWatcher.Changed > Add > LocalChangeBatcher.Add  ①
```

① files only, as Updated  

#### FileSystemWatcher.Deleted

```text
FileSystemWatcher.Deleted > Add > LocalChangeBatcher.Add
```

#### FileSystemWatcher.Renamed

```text
FileSystemWatcher.Renamed > Add > LocalChangeBatcher.Add  ①
                          > Add > LocalChangeBatcher.Add  ②
```

① old path as Deleted  
② new path as Created  

#### FileSystemWatcher.Error

```text
FileSystemWatcher.Error > LocalFileWatcher.Overflowed > FileManagerState.LocalFilesChanged           ①
                                                      > HackFileManagerViewModel.OnLocalChangesLost  ②
```

① event; AppComposition subscriber, posted to the UI thread: marks the kept rows stale  
② while a file manager page is shown  

#### LocalFileWatcher.Flush

```text
LocalFileWatcher.Flush > LocalChangeBatcher.TakeDue                                               ①
                       > LocalFileWatcher.Changed > FileManagerState.LocalFilesChanged            ②
                                                  > HackFileManagerViewModel.OnLocalFilesChanged  ③
```

① timer tick, 200 ms; changes quiet for 400 ms  
② event, when any changes are due; AppComposition subscriber, posted to the UI thread  
③ while a file manager page is shown  

#### FileManagerState.LocalFilesChanged

```text
FileManagerState.LocalFilesChanged > MarkStale  ①
```

① Entries; only when no page is live and the kept folder changed (null = any)  

#### HackFileManagerViewModel.OnLocalFilesChanged

```text
HackFileManagerViewModel.OnLocalFilesChanged > ApplyLocalChangesAsync  ①
```

① posted to the UI thread  

#### HackFileManagerViewModel.ApplyLocalChangesAsync

```text
HackFileManagerViewModel.ApplyLocalChangesAsync > HackFileManagerView.OnFolderChanged                  ①
                                                > Refresh > FileManagerState.MarkStale                 ②
                                                          > HackFileManagerView.OnRefreshRequested     ③
                                                > RefreshRowAsync > EntryListService.RefreshRowAsync   ④
                                                                  > HackFileManagerView.OnRowReplaced  ⑤
                                                > KeepListedRows > FileManagerState.RememberEntries    ⑥
                                                                 > UpdateEntryLabels
```

① via FolderChanged, once per changed folder other than the listed one  
② more than 25 files of the listed folder changed  
③ via RefreshRequested (Entries)  
④ per changed file of the listed folder  
⑤ via RowReplaced  
⑥ when any row changed  

#### HackFileManagerViewModel.OnLocalChangesLost

```text
HackFileManagerViewModel.OnLocalChangesLost > Refresh > FileManagerState.MarkStale              ①
                                                      > HackFileManagerView.OnRefreshRequested  ②
```

① posted to the UI thread  
② via RefreshRequested (Entries)  

[↑ contents](#contents)

### Load and selection

#### HackFileManagerView (ctor)

```text
HackFileManagerView (ctor) > AppComposition.CreateFileManager > HackFileManagerViewModel (ctor)  ①
                           > ApplyColumnSettings > UIStorage.Columns
                                                 > EntryGroupBy.Items.Add                        ②
                           > LoadHackMan                                                         ③
```

① ViewModel field initializer, runs first  
② a GroupByCommand item per EntryRowKeys.Names  
③ on Loaded  

#### HackFileManagerViewModel (ctor)

```text
HackFileManagerViewModel (ctor) > EntryRowKeys.TryGetGroupKey  ①
```

① restores ShowInactive, grouping, group field and selection from FileManagerState, change hooks muted  

#### OnNavigatedTo

```text
OnNavigatedTo > LoadHackMan  ①
```

① once per page  

#### LoadHackMan

```text
LoadHackMan > HackFileManagerViewModel.Initialize > PdmWorkflowService.PreloadMetadata    ①
            > OnGroupingChanged > EntryListGrouping.Apply
            > HackFileManagerViewModel.Activate > LocalFileWatcher.Watch                  ②
                                                > FileManagerState.TakePendingLocate
            > LoadTreeAfterLayoutAsync > HackFileManagerViewModel.LoadDirectoryTreeAsync  ③
                                       > OnLocateRequestedAsync                           ④
```

① reads the OdooDefaults lookup tables (nodes, settings, root dir, types, users) on first access  
② after subscribing to the view model and control events; first call: subscribes Changed/Overflowed, watches the vault folder  
③ after 50 ms (kept tree) or 500 ms  
④ a search asked to locate a file while the page was not shown  

#### HackFileManagerViewModel.LoadDirectoryTreeAsync

```text
HackFileManagerViewModel.LoadDirectoryTreeAsync > DirectoryTreeService.LoadAsync > HpDirectory.GetSubdirectoriesAsync [rpc hp.directory.get_children_directories_by_id]  ①
                                                                                 > DirectoryTreeMerger.AddLocalDirectories
                                                > FileManagerState.TreeRefreshed                                                                                         ②
                                                > HackFileManagerView.OnTreeLoadedAsync > DirectoryTreeBuilder.Build                                                     ③
                                                                                        > SelectDirectory
```

① unless a kept, non-stale tree exists  
② a fresh tree  
③ via TreeLoaded  

#### LoadAndRestoreAsync

```text
LoadAndRestoreAsync > HackFileManagerViewModel.LoadEntriesAsync > FileManagerState.TryGetEntries
                                                                > EntryListService.LoadAsync             ①
                                                                > FileManagerState.RememberEntries
                                                                > Entries.Sort                           ②
                                                                > HackFileManagerView.OnGroupingChanged  ③
                    > RestoreSortArrow                                                                   ④
                    > HackFileManagerViewModel.TakeSelectionToRestore                                    ⑤
```

① kept rows missing or stale  
② by the kept sort  
③ via GroupingChanged  
④ first listing of the visit only  
⑤ first listing of the visit only; reselects the rows kept in FileManagerState  

#### EntryListService.LoadAsync

```text
EntryListService.LoadAsync > HpDirectory.GetEntriesAsync [rpc hp.directory.get_entries]  ①
                           > HashLocalCopiesAsync                                        ②
                           > CreateRemoteRow > EntryStatusResolver.ResolveRemote
                           > FindLocalOnlyFilesAsync > FileOperations.FilesInDirectory
                           > CreateLocalRow > EntryStatusResolver.ResolveLocal
```

① remote folder  
② hashes the local copies of remote entries  

#### HackFileManagerViewModel.ShowEntryDetailsAsync

```text
HackFileManagerViewModel.ShowEntryDetailsAsync > ClearDetails                                                                                     ①
                                               > LoadDetailTabAsync > EntryDetailsService.GetHistoryAsync [rpc hp.entry.related_browse]           ②
                                                                    > EntryDetailsService.GetParentsAsync [rpc hp.version.related_browse]         ③
                                                                    > EntryDetailsService.GetChildrenAsync [rpc hp.version.related_browse]        ④
                                                                    > EntryDetailsService.GetPropertiesAsync                                      ⑤
                                                                    > EntryDetailsService.GetVersionInfoAsync [rpc hp.version.read]               ⑥
                                               > EntryDetailsService.GetPreviewImageAsync > HpVersion.GetRecordsByIdsAsync [rpc hp.version.read]  ⑦
                                                                                          > VersionFileService.GetPreviewImage                    ⑧
                                               > EntryDetailsService.GetLocalPreviewImageAsync > SolidWorksUtil.DocMgr.GetPreviewPng              ⑨
```

① cancels the previous details load first  
② only the visible tab; History tab  
③ Parents tab  
④ Children tab  
⑤ Properties tab  
⑥ Info tab  
⑦ when the row has a latest version  
⑧ preview_image not returned  
⑨ no server preview and a local file exists; SolidWorks files only  

#### HackFileManagerViewModel.PreviewVersionAsync

```text
HackFileManagerViewModel.PreviewVersionAsync > EntryDetailsService.GetPreviewImageAsync > HpVersion.GetRecordsByIdsAsync [rpc hp.version.read]  ①
                                                                                        > VersionFileService.GetPreviewImage                    ②
```

① version id set  
② preview_image not returned  

#### OnShowInactiveChanged

```text
OnShowInactiveChanged > FileManagerState.ShowInactive                                ①
                      > Entries.Clear
                      > HackFileManagerView.OnRefreshRequested > ReloadEntriesAsync  ②
```

① set; skipped while the constructor restores state  
② via RefreshRequested (Entries)  

#### OnIsGroupingEnabledChanged

```text
OnIsGroupingEnabledChanged > FileManagerState.IsGroupingEnabled                               ①
                           > HackFileManagerView.OnGroupingChanged > EntryListGrouping.Apply  ②
```

① set; skipped while the constructor restores state  
② via GroupingChanged  

#### OdooDirectoryTree.Expanding

```text
OdooDirectoryTree.Expanding > HackFileManagerViewModel.SetExpanded > FileManagerState.ExpandedPaths.Add  ①
```

① lambda in LoadHackMan  

#### OdooDirectoryTree.Collapsed

```text
OdooDirectoryTree.Collapsed > HackFileManagerViewModel.SetExpanded > FileManagerState.ExpandedPaths.Remove  ①
```

① lambda in LoadHackMan  

#### OdooDirectoryTree.LostFocus

```text
OdooDirectoryTree.LostFocus > SelectDirectory > HackFileManagerViewModel.SelectedDirectory  ①
```

① lambda in LoadHackMan, when a node is selected; set  

#### OdooParents.SelectionChanged

```text
OdooParents.SelectionChanged > PreviewVersion > HackFileManagerViewModel.PreviewVersionAsync  ①
```

① lambda in LoadHackMan  

#### OdooChildren.SelectionChanged

```text
OdooChildren.SelectionChanged > PreviewVersion > HackFileManagerViewModel.PreviewVersionAsync  ①
```

① lambda in LoadHackMan  

#### OdooHistory.DoubleTapped

```text
OdooHistory.DoubleTapped > DownloadHistoryWithDependenciesCommand  ①
```

① lambda in LoadHackMan  

#### EntryOpen.DoubleTapped

```text
EntryOpen.DoubleTapped > OpenSelectedCommand  ①
```

① submenu header default action  

#### EntryDelete.DoubleTapped

```text
EntryDelete.DoubleTapped > LocalDeleteSelectedCommand  ①
```

① submenu header default action  

#### TreeDownload.DoubleTapped

```text
TreeDownload.DoubleTapped > GetLatestDirectoryCommand  ①
```

① submenu header default action  

#### TreeUndelete.DoubleTapped

```text
TreeUndelete.DoubleTapped > RestoreDirectoryCommand  ①
```

① submenu header default action  

#### HistoryDownload.DoubleTapped

```text
HistoryDownload.DoubleTapped > DownloadHistoryCommand  ①
```

① submenu header default action  

#### HistoryMove.DoubleTapped

```text
HistoryMove.DoubleTapped > MoveHistoryToTempCommand  ①
```

① submenu header default action  

#### OnNavigatedFrom

```text
OnNavigatedFrom > Leave > HackFileManagerViewModel.Dispose  ①
                        > PipWindow.Close
```

① unsubscribes the watcher and locate events, cancels page loads  

[↑ contents](#contents)

## Odoo server (RPC)

### Commit

#### hp.pdm.commit.start_commit

```text
hp.pdm.commit.start_commit > browse(commit_ids).exists
                           > _check_commit_owner > hp_guard.is_pdm_admin
                           > cr.savepoint                                      ①
                           > write > _check_commit_owner                       ②
                           > run_commit_job > _create_all_records_atomically
                                            > write                            ③
                           > handle_commit_failure > write                     ④
                                                   > _rollback_records_staged
                           > read                                              ⑤
```

① per commit; skipped when already committed or nothing staged  
② committed_at  
③ committed=True  
④ on any exception: errored + message_exception  
⑤ one result row per commit  

#### hp.pdm.commit.clear_commit

```text
hp.pdm.commit.clear_commit > browse(rec_id).exists                                      ①
                           > _check_commit_owner > hp_guard.is_pdm_admin
                           > _rollback_records_staged > hp.record.staged.sudo().unlink  ②
                           > write > _check_commit_owner                                ③
```

① raises UserError when missing  
② only when rows are staged; UserError before this when already committed  
③ only when rows are staged; resets committed/errored/summary/dates  

#### hp.pdm.commit.run_commit_job

```text
hp.pdm.commit.run_commit_job > _create_all_records_atomically > _create_records  ①
                                                              > write            ②
                             > write > _check_commit_owner                       ③
```

① per model, in model_order  
② commit_summary  
③ committed=True, commit_finished_at  

#### hp.pdm.commit.handle_commit_failure

```text
hp.pdm.commit.handle_commit_failure > write > _check_commit_owner                                ①
                                    > _rollback_records_staged > hp.record.staged.sudo().unlink
```

① errored=True, message_exception  

#### hp.pdm.commit.write

```text
hp.pdm.commit.write > _check_commit_owner > hp_guard.is_pdm_admin  ①
                    > super().write
```

① raises AccessError when not the creator  

[↑ contents](#contents)

### Checkout

#### hp.version.checkout_entry

```text
hp.version.checkout_entry > hp.node.browse.exists                     ①
                          > hp.node.search                            ②
                          > hp.version.browse.exists                  ③
                          > hp.version.write > _check_checkout_write  ④
                                             > super().write
```

① node given as id  
② node given as name; raises UserError when not found  
③ raises UserError when missing  
④ regular users only  

#### hp.version.checkout_entries

```text
hp.version.checkout_entries > checkout_entry > hp.node.search    ①
                                             > hp.version.write
```

① per version id; first failure aborts the whole call  

[↑ contents](#contents)

### Entries & versions (create/write guards)

#### hp.entry.write

```text
hp.entry.write > hp_guard.is_pdm_admin
               > _check_user_write                        ①
               > super().write > hp.version.write         ②
                               > _inverse_is_checked_out  ③
                               > _check_name_path_safe    ④
```

① regular users only; AccessError/UserError on someone else's checkout or a deleted file  
② checkout fields are related to the latest version  
③ is_checked_out=False  
④ name changed; ValidationError on unsafe name  

#### hp.version.create

```text
hp.version.create > hp_guard.is_pdm_admin
                  > _check_entry_accepts_version > hp.entry.browse.exists  ①
                  > super().create
```

① regular users only; AccessError first when checkout fields are given  

#### hp.version.write

```text
hp.version.write > hp_guard.is_pdm_admin
                 > _check_checkout_write                      ①
                 > super().write > _check_not_own_dependency  ②
```

① regular users writing checkout fields; AccessError first on immutable fields  
② child_ids/parent_ids changed  

#### hp.version.property.create

```text
hp.version.property.create > hp_guard.is_pdm_admin  ①
                           > super().create
```

① raises AccessError for regular users  

#### hp.version.property.write

```text
hp.version.property.write > hp_guard.is_pdm_admin  ①
                          > super().write
```

① raises AccessError for regular users  

#### hp.record.staged.create

```text
hp.record.staged.create > hp_guard.is_pdm_admin
                        > _check_own_commit > hp.pdm.commit.browse.exists  ①
                        > super().create
```

① regular users only; UserError when target_model is not stageable  

#### hp.record.staged.write

```text
hp.record.staged.write > hp_guard.is_pdm_admin
                       > _check_own_commit > hp.pdm.commit.browse.exists  ①
                       > super().write
```

① regular users: each row's commit and a new committing_id  

[↑ contents](#contents)

### Folders

#### hp.directory.get_dir_id_for_filepath

```text
hp.directory.get_dir_id_for_filepath > _get_record_for_filepath > hp.directory.search  ①
```

① parent_path; IndexError when none  

#### hp.directory.get_dir_id_for_parentpath

```text
hp.directory.get_dir_id_for_parentpath > _get_record_for_filepath > hp.directory.search  ①
```

① complete_name; IndexError when none  

#### hp.directory.get_dir_ids_for_filepaths

```text
hp.directory.get_dir_ids_for_filepaths > get_dir_id_for_filepath > _get_record_for_filepath  ①
```

① per path  

#### hp.directory.get_directories

```text
hp.directory.get_directories  ①
```

① no-op placeholder  

#### hp.directory.get_children_directories

```text
hp.directory.get_children_directories > _get_record_for_filepath                     ①
                                      > _recurse_directories > _recurse_directories
                                                             > _get_entry_dict
```

① always raises TypeError (missing is_parent_path arg)  

#### hp.directory.get_children_directories_by_id

```text
hp.directory.get_children_directories_by_id > hp.directory.search                          ①
                                            > _recurse_directories > _recurse_directories  ②
                                                                   > _get_entry_dict       ③
```

① by id; IndexError when none  
② per child folder  
③ withEntries only  

#### hp.directory.get_entries

```text
hp.directory.get_entries > hp.directory.search  ①
                         > _get_entry_dict      ②
```

① by id; errors logged, {} returned  
② deleted entries only when show_deleted  

#### hp.directory.get_all_entry_ids

```text
hp.directory.get_all_entry_ids > hp.directory.search                      ①
                               > _get_all_entry_ids > _get_all_entry_ids  ②
```

① by id; errors logged, [] returned  
② withSubEntries only; per sub-folder  

#### hp.directory.last_available_directory

```text
hp.directory.last_available_directory > hp.directory.search                                          ①
                                      > _recurse_directories_finding > _recurse_directories_finding  ②
```

① root (id 1); UserError first when paths is empty  
② exact name, then case-insensitive  

#### hp.directory.create_new_details

```text
hp.directory.create_new_details > hp.directory.search                                          ①
                                > _recurse_directories_finding > _recurse_directories_finding  ②
```

① root (id 1)  
② result discarded; returns None  

#### hp.directory.create_new_directories

```text
hp.directory.create_new_directories  ①
```

① no-op placeholder  

[↑ contents](#contents)

### Dependencies

#### hp.version.get_recursive_dependency

```text
hp.version.get_recursive_dependency > env.flush_all
                                    > cr.execute (recursive CTE)  ①
```

① hp_version_relationship, skips deleted entries; [] on error  

#### hp.version.get_recursive_dependency_versions

```text
hp.version.get_recursive_dependency_versions > get_recursive_dependency > env.flush_all
                                                                        > cr.execute (recursive CTE)
```

#### hp.version.get_recursive_dependency_entries

```text
hp.version.get_recursive_dependency_entries > get_recursive_dependency_versions > get_recursive_dependency
                                            > hp.node.write                                                 ①
                                            > hp.version.browse.mapped(entry_id)
```

① update and node_id only; admins only, otherwise [] returned  

#### hp.version.get_entries_from_children

```text
hp.version.get_entries_from_children > hp.entry.search         ①
                                     > _iterative_add_entries  ②
```

① IndexError when none  
② AttributeError when the version has dependencies  

#### hp.node.update_node_latest_versions

```text
hp.node.update_node_latest_versions > write  ①
```

① no-op over RPC (@api.model runs on an empty recordset)  

[↑ contents](#contents)

### Reading helpers

#### hp.common.model.smart_read

```text
hp.common.model.smart_read > _check_field_path > check_access_rights        ①
                                               > check_field_access_rights
                           > browse / search                                ②
                           > _resolve_field > _help_gather                  ③
                                            > _resolve_field                ④
```

① per field path; UserError on unknown field  
② ids, domain, or both  
③ last path part  
④ dotted path, per related record  

#### hp.common.model.fast_read

```text
hp.common.model.fast_read > browse(ids).read
```

#### hp.common.model.related_browse

```text
hp.common.model.related_browse > _check_field_path > check_access_rights
                                                   > check_field_access_rights
                               > browse(ids).mapped(field_name)
                               > read
```

#### hp.common.model.related_search_browse

```text
hp.common.model.related_search_browse > _check_field_path > check_access_rights
                                                          > check_field_access_rights
                                      > search
                                      > mapped(field_name).read
```

#### hp.common.model.create_read

```text
hp.common.model.create_read > create  ①
                            > read    ②
```

① the model's own create override applies  
② all fields when none given  

#### hp.entry.checksum_nonmatches

```text
hp.entry.checksum_nonmatches > hp.version.search  ①
```

① checksum; never matches, so always True  

#### hp.entry.checksum_list_nonmatches

```text
hp.entry.checksum_list_nonmatches > hp.version.search  ①
```

① returns None (result list never returned)  

#### hp.entry.first_helper

```text
hp.entry.first_helper  ①
```

① takes a Python function; not usable over RPC  

[↑ contents](#contents)

### Admin & migration

#### hp.version.migrate_webdav_files_to_records

```text
hp.version.migrate_webdav_files_to_records > hp_guard.require_admin > hp_guard.is_pdm_admin  ①
                                           > search                                          ②
                                           > _get_attachments > ir.attachment.search         ③
                                           > WebDav.get_file_from_webdav
                                           > _create_attachment > ir.attachment.create
                                           > cr.commit                                       ④
```

① AccessError for non-admins  
② every version  
③ skips versions that already have a file  
④ after every version  

#### hp.common.model.migrate_webdav_files_to_records

```text
hp.common.model.migrate_webdav_files_to_records > hp_guard.require_admin > hp_guard.is_pdm_admin  ①
                                                > Database.start_session
                                                > _import_versions_image > _get_attachments
                                                                         > _get_image_bytes
                                                                         > _create_attachment
                                                                         > cr.commit              ②
```

① other hp.* models; hp.version overrides it  
② per record  

#### hp.version.migrate_image_from_database

```text
hp.version.migrate_image_from_database > hp_guard.require_admin > hp_guard.is_pdm_admin
                                       > Database.start_session
                                       > _get_attachments > ir.attachment.search                        ①
                                       > _get_image_bytes > connection.execute (raw SQL on the old db)
                                       > _create_attachment > ir.attachment.create
                                       > cr.commit                                                      ②
```

① skips versions that already have a preview  
② after every version  

#### hp.version.display_all_fnames

```text
hp.version.display_all_fnames > hp_guard.require_admin > hp_guard.is_pdm_admin
                              > ir.attachment.search                            ①
```

① logs each attachment  

#### hp.version.cleanup_filestore

```text
hp.version.cleanup_filestore > hp_guard.require_admin > hp_guard.is_pdm_admin  ①
```

① placeholder; does nothing else  

#### hp.release.review.reviewer_decision

```text
hp.release.review.reviewer_decision > env.user.has_group        ①
                                    > hp.release.review.search  ②
                                    > hp.release.create         ③
```

① non-admin: raises NameError, since _ is not imported  
② IndexError when none  
③ accepted only; the review itself is never written  

#### hp.entry.init

```text
hp.entry.init > hp_guard.create_case_insensitive_unique_index > cr.execute               ①
                                                              > sql.create_unique_index  ②
```

① install/upgrade hook; duplicate (dir_id, lower(name)) check  
② only when no duplicates  

#### hp.directory.init

```text
hp.directory.init > hp_guard.create_case_insensitive_unique_index > cr.execute               ①
                                                                  > sql.create_unique_index  ②
```

① install/upgrade hook; duplicate (parent_id, lower(name)) check  
② only when no duplicates  

[↑ contents](#contents)

### Client → server

| Server method | Reached from (client call path) |
|---|---|
| `hp.pdm.commit.start_commit` | `HpPDMCommit.ServerCommit`<br>`PdmWorkflowService.CommitFilesAsync` → `HpPDMCommit.ServerCommit` |
| `hp.pdm.commit.clear_commit` | `HpPDMCommit.ServerClear`<br>`PdmCommitOperations.CommitRecordAsync` → `HpPDMCommit.ServerClear` (3 failure paths) |
| `hp.record.staged.create` | `CommitStaging.StageEntryAsync` → `HpBaseModelTransport.StageRecAsync`<br>`CommitStaging.StageAsync` → `HpBaseModelTransport.StageRecAsync`<br>`CommitStaging.StagePropertiesAsync` → `HpBaseModelTransport.MultiStageAsync` |
| `hp.version.checkout_entry` | `PdmCheckoutOperations.CheckOutEntryAsync`<br>`PdmWorkflowTests.FileCheckedOutBySomeoneElse_CannotBeTakenReleasedOrCommitted` (test) |
| `hp.entry.write` | `PdmCheckoutOperations.UnCheckOutEntryAsync` → `HpBaseModelTransport.WriteChangedValuesAsync`<br>`PdmDeleteOperations.SetDeletedAsync` → `HpBaseModelTransport.WriteChangedValuesAsync` |
| `hp.version.write` | `PdmDeleteOperations.PermanentDeleteVersionRelationships` |
| `hp.version.get_recursive_dependency_entries` | `PdmDownloadOperations.GetAllEntriesAndDependenciesListAsync`<br>`PdmWorkflowService.ExpandWithDependenciesAsync` → `PdmDownloadOperations.GetAllEntriesAndDependenciesListAsync` |
| `hp.version.get_recursive_dependency_versions` | `PdmDownloadOperations.GetVersionListAsync`<br>`PdmWorkflowService.PlanVersionDownloadAsync` → `PdmDownloadOperations.GetVersionListAsync` |
| `hp.directory.last_available_directory` | `HpDirectory.LastAvailableDirectory` |
| `hp.directory.get_children_directories_by_id` | `HpDirectory.GetSubdirectoriesAsync` |
| `hp.directory.get_entries` | `HpDirectory.GetEntriesAsync` |
| `hp.directory.get_all_entry_ids` | `HpDirectory.GetDirectoryEntryIDsAsync` |
| `hp.common.model.smart_read` | `Repository.SmartReadAsync` (on any hp.* model)<br>`HpBaseModelTransport.GetRecordsBySmartSearchAsync` → `Repository.SmartReadAsync` |
| `hp.common.model.related_browse` | `Repository.RelatedAsync`<br>`HpBaseModelTransport.GetRelatedRecordByIdsAsync` → `Repository.RelatedAsync` |

No client caller found for: hp.pdm.commit.run_commit_job/handle_commit_failure/write, hp.version.create, hp.version.checkout_entries, hp.version.property.create/write, hp.record.staged.write, hp.version.get_recursive_dependency, get_entries_from_children, hp.node.update_node_latest_versions, fast_read, related_search_browse, create_read, the other hp.directory get_*/create_* methods, checksum_*, first_helper, reviewer_decision, migration/admin jobs, init.
