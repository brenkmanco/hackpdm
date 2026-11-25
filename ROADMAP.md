# Roadmap: every HackPDM page designed in Figma and matched in the app

**Goal:** Every HackPDM page and shared control has a Figma design (light and dark) and the app matches it, with all color coming from one Material color system that the theme presets and Theme Editor control.
**Status:** Milestone 2, step 2.1 next. Updated 2026-11-25.

## Plan
- **Figma file:** "HackPDM — Uno Material Design" (https://www.figma.com/design/qeptbte8bdjojuGv6mc6FN), built on the Material 3 Design Kit.
- **Theme system:** each preset (Light, Dark, Navy blue) becomes a Material color scheme with light and dark; shape styles keep working; the Theme Editor edits Material colors.
- **Figma scope:** a component library page plus a frame for every page and dialog, in light and dark.
- **Checking:** launch the dev build, the user signs in, compare each page with its Figma frame. Both targets (`net10.0-desktop`, `net10.0-windows10.0.26100.0`) must build.
- **Git:** one commit to `main` per milestone; nothing pushed unless asked.
- **Per page:** Figma frames (light + dark) → XAML restyle on shared styles → both builds → on-screen check.

## Milestones
- [x] M1: Color and shape foundations — HackPDM color set in Figma (Light, Dark, Navy blue); presets, shape styles and the Theme Editor on Material colors; tests pass; File Manager work committed
- [ ] M2: Component library — Figma page plus matching shared app styles
- [ ] M3: Shell and shared pieces restyled
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
| 2.1 | complex | Figma | M1 colors | Components page: pane card, toolbar, icon buttons, chips, tabs, file table, tree row, text field, dialog, log row, navigation bar item, settings row, banner | |
| 2.2 | standard | code | 2.1 | `Styles/HackControls.xaml` with the matching styles, merged in `App.xaml` | |
| 2.3 | simple | code | 2.2 | File Manager moved onto the shared styles | |
| 2.4 | simple | git commit | 2.1–2.3 | milestone 2 commit | |

## Phase 3: Shell and shared pieces (reaches M3)
| Step | Size | Page | Status |
|---|---|---|---|
| 3.1 | standard | Shell/HomeView (navigation bar, search and status panels) | |
| 3.2 | simple | Shell/NotLoggedInView | |
| 3.3 | simple | Common/MessageBox | |
| 3.4 | simple | Common/PipWindow | |
| 3.5 | simple | Common/FolderIcon colors | |
| 3.6 | simple | git commit | |

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
- Preview summary fields from the File Manager Figma frame (File, Version, Checked out by, Path) need new view-model properties; not in scope unless asked.

## Handoff notes
- After M1: Figma has a Colors page (frames 14:612 Light, 14:742 Dark, 14:872 Navy blue) and the "HackPDM colors" collection. App: presets are Material schemes; Theme Editor lists the 30 roles in 7 groups; Dark → Navy blue switches live (checked on screen, not saved). Unit tests 427 passed / 55 skipped; both targets and the smoke-test build compile. The File Manager Figma frames still bind to the Material 3 kit's variables, not "HackPDM colors" — rebind them in 2.3.
- Before M1: File Manager restyle (Figma frame "File Manager — Light" 7:3455 and "— Dark" 13:266), `Styles/ColorPaletteOverride.xaml` (Blue LT/DT), `HackSurfaceContainer*` brushes in `App.xaml`, Material + Toolkit Uno features. Built on both targets and checked on screen; uncommitted.
