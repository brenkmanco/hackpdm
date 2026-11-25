# Roadmap: every HackPDM page designed in Figma and matched in the app

**Goal:** Every HackPDM page and shared control has a Figma design (light and dark) and the app matches it, with all color coming from one Material color system that the theme presets and Theme Editor control.
**Status:** Milestone 4 in progress: 4.1 Dependency Graph done and committed (with sync tool phase 4). Penpot ↔ code sync tool (`tools/WyrmpdmSync`, see `design/README.md`) done. Next: 4.2 Search/SearchOdooView. Updated 2026-11-25.

## Plan
- **Design file:** Penpot (local, http://localhost:9001) from here on; colors as a "HackPDM colors" token set per preset, components on a Components page, frames per page. The Figma file "HackPDM — Uno Material Design" (https://www.figma.com/design/qeptbte8bdjojuGv6mc6FN) keeps the Colors page and File Manager frames built so far.
- **Theme system:** each preset (Light, Dark, Navy blue) becomes a Material color scheme with light and dark; shape styles keep working; the Theme Editor edits Material colors.
- **Figma scope:** a component library page plus a frame for every page and dialog, in light and dark.
- **Checking:** launch the dev build, the user signs in, compare each page with its Figma frame. Both targets (`net10.0-desktop`, `net10.0-windows10.0.26100.0`) must build.
- **Git:** one commit to `main` per milestone; nothing pushed unless asked.
- **Design sync:** `tools/WyrmpdmSync` keeps Penpot and the code in step both ways (`design/README.md`); run `status` before and after design work.
- **Per page:** Penpot frames (light + dark) → XAML restyle on shared styles → mark the view (`pp:Id`) and link it under `frames` in `design/penpot-sync.json` → `wyrmpdm-sync init` and `status` (all in sync) → both builds → on-screen check.

## Milestones
- [x] M1: Color and shape foundations — HackPDM color set in Figma (Light, Dark, Navy blue); presets, shape styles and the Theme Editor on Material colors; tests pass; File Manager work committed
- [x] M2: Component library — Penpot Components page plus matching shared app styles; File Manager frames rebuilt in Penpot
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
| 2.1 | complex | Penpot (MCP) | M1 colors | Components page: pane card, toolbar, icon buttons, chips, tabs, file table, tree row, text field, dialog, log row, navigation bar item, settings row, banner | done (Penpot) |
| 2.2 | standard | code | 2.1 | `Styles/HackControls.xaml` with the matching styles, merged in `App.xaml` | done |
| 2.3 | simple | code | 2.2 | File Manager moved onto the shared styles | done |
| 2.3b | simple | Penpot (MCP) | 2.1 | File Manager frames (light + dark) rebuilt in Penpot on "HackPDM colors" | done |
| 2.4 | simple | git commit | 2.2–2.3 | commit of the app half | done |
| 2.5 | simple | git commit | 2.1, 2.3b | milestone 2 commit (roadmap, Penpot setup) | done |

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
| 4.1 | complex | Graph/DependencyGraphView | done |
| 4.2 | standard | Search/SearchOdooView | |
| 4.3 | standard | Status/StatusDialogView | |
| 4.4 | simple | git commit (4.2–4.3; 4.1 already committed) | |

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
- 2026-11-25: Step 4.1 was committed early, together with sync tool phase 4, because the graph view carries both the restyle and the sync markers (user choice); milestone 4's own commit (4.4) covers 4.2–4.3.
- 2026-11-25: Design and code are kept in step by `tools/WyrmpdmSync` (Penpot API + access token, both ways): preset colors and shape values ↔ tokens, components ↔ `HackControls.xaml` styles, views ↔ frames through `pp:Id` markers. New pages should be linked the same way (see `design/README.md`).
- 2026-11-25: The dependency graph's colors come from the active Material scheme, Theme Editor changes included (`GraphThemes.FromScheme`): canvas Surface, boxes Surface Container with an Outline Variant border, links Outline, highlight / ports / clicked box Primary, chosen files' border Tertiary. The file-kind header colors and shadows stay fixed per preset because they carry meaning.
- 2026-11-25: Graph direction (Children / Parents / Both) is a set of filter chips that act like radio buttons (`DirectionChip_Click` re-checks the chosen one).
- 2026-11-25: On-screen checks use the `net10.0-desktop` build (`Src/HackPDM/bin/x64/Debug/net10.0-desktop/HackPDM.exe`); the WinUI exe started directly, outside Visual Studio, crashes at startup (XAML 0xc000027b) before any page loads.
- 2026-11-25: Design work moves from Figma (Starter plan's MCP limit) to Penpot. The design file lives on Penpot cloud and is driven through a custom MCP connector; `docker-compose.yaml` runs a local Penpot 2.18 with its MCP server, which is kept but not used for the file. `.mcp.json` holds a personal access token, so it is git-ignored. Colors are Penpot tokens and library colors, values copied from `ThemePresets`.
- 2026-11-25: Penpot tokens are named per preset (`Light.Primary`, `Dark.Primary`, `NavyBlue.Primary`) in three always-active sets ("HackPDM colors/Light", "/Dark", "/Navy blue"), not one name switched by themes: a token set applies to the whole file, so light and dark frames could not sit side by side otherwise. A frame for another preset rebinds to that preset's tokens, as in Figma.
- 2026-11-25: Every component is one variant set with a `Theme` property (Light, Dark) plus its own properties; each set sits on a section board with the Light background behind the light row and the Dark background behind the dark row.
- 2026-11-25: Dialogs use the Default shape style's 8px overlay corners and Surface Container High (matching `Common/MessageBox`), not Material's 28px. Tree-row folders keep the synced amber (#C69749), unbound, per the folder-icon decision.
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

## Design backlog (Penpot)
- Light + dark frames for every page restyled app-only: Shell (rail + Search / Status drawers), Not Logged In, Message box, PiP window.

## Handoff notes
- After the sync tool (phases 1–5): `status` shows 106 tokens, 27 component setters and both linked views in sync, except `AssistChipButtonStyle.Height` (Penpot 34, code 32) until the Assist chip is re-laid out in the Penpot editor. The "Graph around it" labels' glyph layout was cleared by a test push and is redrawn by Penpot on the next edit. 60 tool tests pass.
- After 4.1: Penpot page "Dependency Graph" holds "— Light" and "— Dark"; File Manager frames' rail now lists the app's six pages. App: `DependencyGraphView.xaml` on the shared styles, `GraphTheme.cs` derives graph colors from the scheme (2 new tests), `ThemeService.GraphTheme` passes the applied scheme. Both targets build; 430 tests pass, 55 skipped; `PauseAndDownloadResumeTests` still fails intermittently on main too (fix on branch `claude/cool-newton-5c3635`). Checked on screen in Dark (desktop build). Seen there, not fixed: on the desktop build the Material text styles (BodyMedium, BodySmall) render noticeably heavier than the default font, app-wide.
- After M2: Penpot page "File Manager" holds "File Manager — Light" and "— Dark" (1440×900, the Balanced layout: rail, toolbar, Folders pane, Entries list with the app's 8 columns, Details inspector, Preview), built from Components-page instances and preset tokens; sample data is made up. Frames on other Penpot pages should be built the same way: instances from the Components page, preset-prefixed tokens. Keep the Penpot tab in the foreground while the plugin works; the browser suspends background tabs.
- After 2.1 (Penpot): the connected file "HackPDM -- Uno Materials Design" is on Penpot cloud (the exporter reports design.penpot.app), not the local Docker instance; the MCP connector that works is the custom one, not the `.mcp.json` entry. Pages: Colors (swatch boards for Light, Dark, Navy blue) and Components (16 variant sets, 74 variants: Button, Icon button, Chip, Tab, Text field/Select, Checkbox, Switch, Pane card, Toolbar, Banner, Dialog, Log row, Navigation item, Settings row, File table row, Tree row). Library: 90 colors under "HackPDM colors/<preset>", 10 Roboto text styles under "HackPDM type" (Material type scale), 90 tokens. Icons are Material icon paths drawn in 24px boards. Not reviewed by you yet.
- After M3: shell, Not Logged In, message box, PiP and folder badges restyled; checked signed in (rail, both drawers, File Manager with live data). Drawer contents (Search, Status Log) are M4. A pre-existing flaky test (`PauseAndDownloadResumeTests`) is fixed on branch `claude/cool-newton-5c3635` (commit b712b0e), not merged here.
- After 2.2–2.3: `Styles/HackControls.xaml` holds HackPaneTintedStyle, HackPaneOutlinedStyle, HackToolbarStyle, HackPaneTitleStyle, HackCaptionStyle, HackBanner{Info,Warning,Error}Style, PaneIconButtonStyle, PaneIconToggleButtonStyle, AssistChipButtonStyle, GroupedBarTextButtonStyle, GroupedSortHeaderButtonStyle, and the TabView / TableView header / TreeView selection brushes. Secondary tab strips alias `HackSecondaryTabForegroundSelectedBrush`. Tests pass, both targets build; not yet checked on screen.
- After M1: Figma has a Colors page (frames 14:612 Light, 14:742 Dark, 14:872 Navy blue) and the "HackPDM colors" collection. App: presets are Material schemes; Theme Editor lists the 30 roles in 7 groups; Dark → Navy blue switches live (checked on screen, not saved). Unit tests 427 passed / 55 skipped; both targets and the smoke-test build compile. The File Manager Figma frames still bind to the Material 3 kit's variables, not "HackPDM colors" — rebind them in 2.3.
- Before M1: File Manager restyle (Figma frame "File Manager — Light" 7:3455 and "— Dark" 13:266), `Styles/ColorPaletteOverride.xaml` (Blue LT/DT), `HackSurfaceContainer*` brushes in `App.xaml`, Material + Toolkit Uno features. Built on both targets and checked on screen; uncommitted.
