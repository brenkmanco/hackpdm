# Roadmap: every HackPDM page designed in Figma and matched in the app

**Goal:** Every HackPDM page and shared control has a Figma design (light and dark) and the app matches it, with all color coming from one Material color system that the theme presets and Theme Editor control.
**Status:** App-only mode (Figma tool-call limit reached). Milestone 4, step 4.1 next; Figma steps 2.1 and 2.3b deferred. Updated 2026-11-25.

## Plan
- **Figma file:** "HackPDM — Uno Material Design" (https://www.figma.com/design/qeptbte8bdjojuGv6mc6FN), built on the Material 3 Design Kit.
- **Theme system:** each preset (Light, Dark, Navy blue) becomes a Material color scheme with light and dark; shape styles keep working; the Theme Editor edits Material colors.
- **Figma scope:** a component library page plus a frame for every page and dialog, in light and dark.
- **Checking:** launch the dev build, the user signs in, compare each page with its Figma frame. Both targets (`net10.0-desktop`, `net10.0-windows10.0.26100.0`) must build.
- **Git:** one commit to `main` per milestone; nothing pushed unless asked.
- **Per page:** Figma frames (light + dark) → XAML restyle on shared styles → both builds → on-screen check.

## Milestones
- [x] M1: Color and shape foundations — HackPDM color set in Figma (Light, Dark, Navy blue); presets, shape styles and the Theme Editor on Material colors; tests pass; File Manager work committed
- [ ] M2: Component library — Figma page plus matching shared app styles (app half done; Figma half deferred)
- [x] M3: Shell and shared pieces restyled (app; Figma frames in the backlog)
- [ ] M4: Main work pages restyled
- [ ] M5: Settings and account pages restyled
- [ ] M6: Final pass in every preset, light and dark; scorecard

## Phase 1: Foundations (reaches M1)
| Step | Size | Skill or action | Needs | Produces | Status |
|---|---|---|---|---|---|
| 1.1 | simple | write roadmap | approved plan | `ROADMAP.md` | done |
| 1.2 | standard | Figma (use_figma) | Blue LT/DT values, Navy preset colors | "HackPDM colors" collection (one group per preset) and a Colors page | done |
| 1.3 | complex | code | ThemeService, ThemePresets, ThemeSettings | presets as Material schemes; Material colors drive the remaining Windows-style brushes | done |
| 1.4 | standard | code | 1.3 | shape styles on the Material panels | done |
| 1.5 | standard | code | 1.3 | Theme Editor color list on Material keys; old overrides reset | done |
| 1.6 | standard | build, tests, on-screen check | 1.3–1.5 | both targets build, `ThemePresetsTests` pass, presets checked in the app | done |
| 1.7 | simple | git commit to main | 1.6 | milestone 1 commit (includes the File Manager restyle) | done |

## Phase 2: Component library (reaches M2)
| Step | Size | Skill or action | Needs | Produces | Status |
|---|---|---|---|---|---|
| 2.1 | complex | Figma | M1 colors | Components page: pane card, toolbar, icon buttons, chips, tabs, file table, tree row, text field, dialog, log row, navigation bar item, settings row, banner | deferred (Figma limit) |
| 2.2 | standard | code | 2.1 | `Styles/HackControls.xaml` with the matching styles, merged in `App.xaml` | done |
| 2.3 | simple | code | 2.2 | File Manager moved onto the shared styles | done |
| 2.3b | simple | Figma | 2.1 | File Manager frames rebound from the Material kit's colors to "HackPDM colors" | deferred (Figma limit) |
| 2.4 | simple | git commit | 2.2–2.3 | commit of the app half | done |

## Phase 3: Shell and shared pieces (reaches M3)
| Step | Size | Page | Status |
|---|---|---|---|
| 3.1 | standard | Shell/HomeView (navigation bar, search and status panels) || done |
| 3.2 | simple | Shell/NotLoggedInView || done |
| 3.3 | simple | Common/MessageBox || done |
| 3.4 | simple | Common/PipWindow || done |
| 3.5 | simple | Common/FolderIcon colors || done |
| 3.6 | simple | git commit || done |

## Phase 4: Main work pages (reaches M4)
| Step | Size | Page | Status |
|---|---|---|---|
| 4.1 | complex | Graph/DependencyGraphView | |
| 4.2 | standard | Search/SearchOdooView | |
| 4.3 | standard | Status/StatusDialogView | |
| 4.4 | simple | git commit | |

## Phase 5: Settings and account pages (reaches M5)
| Step | Size | Page | Status |
|---|---|---|---|
| 5.1 | standard | Login/ProfileManagerView | |
| 5.2 | simple | Settings/ConfigurationView | |
| 5.3 | simple | Settings/OdooSettingsView | |
| 5.4 | simple | Settings/HackSettingsView | |
| 5.5 | simple | Settings/ApplicationSettingsView | |
| 5.6 | standard | Settings/OdooFileTypeManagerView | |
| 5.7 | standard | Settings/StatusSettingsView | |
| 5.8 | simple | Settings/AboutUpdatesView | |
| 5.9 | complex | Themes/ThemeEditorView | |
| 5.10 | simple | git commit | |

## Phase 6: Final pass (reaches M6)
| Step | Size | Action | Status |
|---|---|---|---|
| 6.1 | standard | every page in Light, Dark and Navy blue, compared with Figma | |
| 6.2 | simple | scorecard against the plan | |
| 6.3 | simple | git commit | |

## Decisions
- 2026-11-25: Folder icons keep their origin colors (amber synced, blue server, tan local) because they carry meaning; only the badge outline follows the surface.
- 2026-11-25: The navigation rail sits on Background with a Secondary Container pill for the selected page; Uno Material's NavigationView reads `NavigationViewPaneBackground` / `NavigationViewBackground`, so those are in `MaterialRoles.AppBrushes` too.
- 2026-11-25: Continue app-only until the Figma limit resets or the plan is upgraded (user choice). Pages are restyled from the shared styles and the File Manager design; Figma frames follow later.
- 2026-11-25: Shared control colors that must follow the preset live (tabs, table headers, tree selection) are recolored by ThemeService from `MaterialRoles.AppBrushes`; styles in `HackControls.xaml` can't carry the shape style's corner radius or border thickness (set after the file loads), so pages set those on the element.
- 2026-11-25: Presets rebuilt on Material rather than dropped (user choice).
- 2026-11-25: Figma has light and dark frames for every page (user choice).
- 2026-11-25: Checks are done on screen with the user signing in (user choice).
- 2026-11-25: Commits go to `main`, one per milestone (user choice).
- 2026-11-25: Light and Dark use the Material 3 Design Kit's "Blue LT" / "Blue DT" schemes; Navy blue is a custom scheme from the preset's navy and amber colors (assumption).
- 2026-11-25: Saved Theme Editor overrides that use the old Windows-style color names are reset, not converted (assumption).
- 2026-11-25: The Figma account is on the free plan (one mode per variable collection), so "HackPDM colors" holds one variable group per preset ("Light/Primary", "Dark/Primary", "Navy blue/Primary") instead of modes. A frame for another preset rebinds to that preset's group.
- 2026-11-25: The color presets live in code (`ThemePresets` + `MaterialRoles`, values identical to the Figma variables); `Styles/ColorPaletteOverride.xaml` was removed. At startup `ThemeService` hands the scheme to Uno Material as its color override and sets the `HackSurfaceContainer*` colors; while running it recolors the role brushes and the Windows-style brushes derived from the scheme (`MaterialRoles.FluentBrushes`).
- 2026-11-25: Shape styles redefined around the Figma look: Default = 16px panel corners, 1px border (was 10px / 3px). Panels read `HackPanelCornerRadius`, `HackPanelBorderThickness`, `HackPanelStrokeBrush`.

## Open questions and assumptions
- Figma work is paused: the Starter plan's MCP tool-call limit was reached on 2026-11-25 while building the Components page (that call made no changes). Every page step below now means "restyle in the app"; its Figma frames (light + dark) are owed and listed under "Figma backlog".
- Preview summary fields from the File Manager Figma frame (File, Version, Checked out by, Path) need new view-model properties; not in scope unless asked.

## Figma backlog
- Components page (2.1): Button (Filled, Tonal, Outlined, Text), Icon button (Standard, Selected), Chip (Filter, Filter selected, Assist), Tab (Primary/Secondary × selected), Text field, Select, Checkbox, Switch, Pane card (Tinted, Outlined), Banner (Info, Warning, Error), Dialog, Log row (Info, Warning, Error), Navigation item, Settings row; icons info and warning added locally.
- File Manager frames rebound to "HackPDM colors" (2.3b).
- Light + dark frames for every page restyled app-only: Shell (rail + Search / Status drawers), Not Logged In, Message box, PiP window.

## Handoff notes
- After M3: shell, Not Logged In, message box, PiP and folder badges restyled; checked signed in (rail, both drawers, File Manager with live data). Drawer contents (Search, Status Log) are M4. A pre-existing flaky test (`PauseAndDownloadResumeTests`) is fixed on branch `claude/cool-newton-5c3635` (commit b712b0e), not merged here.
- After 2.2–2.3: `Styles/HackControls.xaml` holds HackPaneTintedStyle, HackPaneOutlinedStyle, HackToolbarStyle, HackPaneTitleStyle, HackCaptionStyle, HackBanner{Info,Warning,Error}Style, PaneIconButtonStyle, PaneIconToggleButtonStyle, AssistChipButtonStyle, GroupedBarTextButtonStyle, GroupedSortHeaderButtonStyle, and the TabView / TableView header / TreeView selection brushes. Secondary tab strips alias `HackSecondaryTabForegroundSelectedBrush`. Tests pass, both targets build; not yet checked on screen.
- After M1: Figma has a Colors page (frames 14:612 Light, 14:742 Dark, 14:872 Navy blue) and the "HackPDM colors" collection. App: presets are Material schemes; Theme Editor lists the 30 roles in 7 groups; Dark → Navy blue switches live (checked on screen, not saved). Unit tests 427 passed / 55 skipped; both targets and the smoke-test build compile. The File Manager Figma frames still bind to the Material 3 kit's variables, not "HackPDM colors" — rebind them in 2.3.
- Before M1: File Manager restyle (Figma frame "File Manager — Light" 7:3455 and "— Dark" 13:266), `Styles/ColorPaletteOverride.xaml` (Blue LT/DT), `HackSurfaceContainer*` brushes in `App.xaml`, Material + Toolkit Uno features. Built on both targets and checked on screen; uncommitted.
