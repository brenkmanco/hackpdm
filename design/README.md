# Design sync (Penpot ↔ WyrmPDM)

The app's design lives in the Penpot file **"HackPDM -- Uno Materials Design"** on design.penpot.app.
`tools/WyrmpdmSync` (`wyrmpdm-sync`) keeps that file and this repository in step, in both directions.

## How the two sides point at each other

- **`design/penpot-sync.json`** (committed) is the manifest. It names the Penpot file and holds every link between
  a Penpot item and the code it belongs to. Each link also keeps the value both sides had at the last sync, so the
  tool can tell which side changed since, or that both did (a conflict, which it never resolves on its own).
- **Colors:** each preset color in `ThemePresets.Palettes` (`Src/HackPDM.Core/Theming/ThemePresets.cs`) ↔ a Penpot
  color token named `<Palette>.<Role>` (`Light.Primary`, `NavyBlue.SurfaceContainer`) in the token set
  `HackPDM colors/<preset>`. Links are kept by token id, so renaming a token in Penpot doesn't break them.
  Names are matched without regard to case and the manifest keeps Penpot's spelling: the Navy blue tokens are
  `Navyblue.*` in the design file (shapes are bound by that name), while the code's palette is `NavyBlue`.
- **Shape values:** each shape style's `ControlCornerRadius`, `OverlayCornerRadius`, `PanelCornerRadius` and
  `PanelBorderThickness` in `ThemePresets.Forms` ↔ a Penpot token `<Style>.<Value>` (`Default.PanelCornerRadius`,
  type border-radius or stroke-width) in the token set `HackPDM shapes/<Style>`.
- The code side is read from the text of `ThemePresets.cs`, so `status` and `pull` see unbuilt edits too, and
  `pull` replaces only the literal it changes (comments and layout stay).
- The tool talks to Penpot in its native Transit encoding (`Penpot/Transit.cs` reads, `Penpot/TransitWriter.cs`
  writes): the JSON encoding leaves out token ids and can't express the keyword values shape edits need.
- **Components:** each Penpot component variant ↔ the XAML style it stands for, e.g. "Pane card" (Kind=Tinted) ↔
  `HackPaneTintedStyle` in `Src/HackPDM/Styles/HackControls.xaml`. The synced setters are the ones the style has
  among Background, BorderBrush (compared by color role), Padding, CornerRadius, Width and Height. The Light variant
  is compared with the code; `push` writes the Light and Dark variants (each with its own theme's color token) and
  every copy of them that hasn't overridden that property in Penpot. Components drawn from Uno Material styles
  (Button → `FilledButtonStyle`, Filter chip → `FilterChipStyle`…) are linked for reference only: those styles
  live in the Uno Material package, not this repository. Edit `components` in the manifest to add pairs.
- Width and Height are pull-only: resize a component in Penpot, then `pull`. Penpot's API stores layout but doesn't
  re-run it, so after a `push` that changes padding, a hug-content component keeps its old stored size until the
  design is next edited in Penpot (any change to the component re-lays it out).
- **Views:** a XAML view ↔ its Penpot frames (`frames` in the manifest: the view, the Light frame compared with
  the code, and the Dark frame written alongside). Elements are linked by a marker in the XAML,
  `pp:Id="graph.toolbar"`, and a layer path in the manifest, `Content/Toolbar` ("#2" picks the second of
  same-named layers), which `init` turns into layer ids. The view declares
  `xmlns:pp="urn:wyrmpdm:penpot-sync"` and lists `pp` in `mc:Ignorable`, so the compilers skip the markers.
  Synced per element, where both sides define them: Padding, Spacing/RowSpacing/ColumnSpacing, CornerRadius,
  Background, BorderBrush, Text (a TextBlock's text, a button's label), and pull-only Width, Height and Style
  (the component a layer copies). Bindings (`{x:Bind …}`) are wiring and never synced.
- **New elements:** an element linked with `"children": true` keeps its children in step as a whole (every child
  must be marked). A layer added inside it in Penpot becomes a marked XAML element on `pull` (a component copy →
  the linked style's control, text → TextBlock, flex frame → StackPanel, icon → FontIcon); a marked XAML element
  added inside it becomes a layer on `push`, in the Light and Dark frames (a component copy for linked styles,
  text based on an existing text layer, frames for panels). Wire up behaviour (commands, bindings) by hand.
  Removals on either side are reported, never applied.

## Setup

1. In Penpot: **Your account → Settings → Access tokens → Generate new token**.
2. Save the token in `design/.penpot-token` (git-ignored) or the `PENPOT_TOKEN` environment variable.
   Never commit it.
3. Check it: `dotnet run --project tools/WyrmpdmSync -- whoami`

## Commands

Run from anywhere in the repository: `dotnet run --project tools/WyrmpdmSync -- <command>`.

| Command | What it does |
|---|---|
| `whoami` | Checks the token: shows the Penpot account it signs in as. |
| `init` | Creates or refreshes the manifest: links every preset color and shape value to its token, the default components to their styles, and the listed view elements to their layers. Safe to rerun. |
| `fetch` | Downloads the Penpot file to `design/.cache/penpot-file.json` (git-ignored), for inspection. |
| `status [--all]` | Shows what differs and which side changed: `<` changed in Penpot (pull), `>` changed in code or missing in Penpot (push), `!` changed on both sides. Exit code 2 when anything differs. |
| `pull` | Writes values changed in Penpot into `ThemePresets.cs`, the linked XAML styles and views, and adds XAML for new layers in synced containers. Rebuild the app afterwards. |
| `push` | Sends values changed in code to Penpot: the token (creating its set and switching it on if needed), the matching library color, and every shape the token is applied to (Penpot's API doesn't repaint those itself); changed style setters to their components and copies; changed view values to their layers; and new marked elements as layers. All or nothing: Penpot applies the whole batch or none of it. |

Options: `--dry-run` (pull/push: show, change nothing), `--prefer penpot|code` (pull/push: settle values changed
on both sides, or that differ with no last-synced value), `--cached` (init/status: use the last fetched copy).

A typical round: `status`, then `pull` for design changes made in Penpot, `push` for changes made in code. Neither
command touches a value changed on both sides until you pick a side with `--prefer`.

## Linking a view

1. Mark the elements in the view: `pp:Id="area.name"` (and declare the `pp` namespace as above).
2. Add the view under `frames` in the manifest, each element with the layer path in its Light frame:
   `{ "anchor": "graph.toolbar", "path": "Content/Toolbar" }`. Add `"children": true` to containers whose children
   should be kept in step as a whole.
3. Run `init`, then `status`: values both sides already share start in sync.

Penpot's API stores layout but doesn't run it: after a push that adds layers or changes padding, gaps or text,
Penpot shows the final arrangement once the design is next edited there.
