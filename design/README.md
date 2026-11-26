# Design sync (Penpot ↔ WyrmPDM)

The app's design lives in the Penpot file **"HackPDM -- Uno Materials Design"** on design.penpot.app.
`tools/WyrmpdmSync` (`wyrmpdm-sync`) keeps that file and this repository in step, in both directions: a change
made in Penpot can be pulled into the code, and a change made in the code can be pushed to Penpot.

## Setup (once)

1. In Penpot: **Your account → Settings → Access tokens → Generate new token**.
2. Save it in `design/.penpot-token` (git-ignored) or the `PENPOT_TOKEN` environment variable. Never commit it.
3. Check it: `dotnet run --project tools/WyrmpdmSync -- whoami`

Run every command from anywhere in the repository: `dotnet run --project tools/WyrmpdmSync -- <command>`.
The Penpot editor doesn't need to be open.

## Day to day

1. `status`: what differs, and which side changed since the last sync.
   - `<` changed in Penpot → `pull` takes it into the code.
   - `>` changed in code (or missing in Penpot) → `push` takes it to Penpot.
   - `!` changed on both sides, `?` different with no last-synced value (or gone on one side) → nothing happens
     until you choose with `--prefer penpot` or `--prefer code` (or restore the missing side).
2. `pull --dry-run` / `push --dry-run` to see exactly what would change, then run it for real.
3. After a `pull`, rebuild the app. After a `push`, open the design in Penpot once (see "Limits").
4. Commit `design/penpot-sync.json` with the code: it records what both sides agreed on.

## Commands

| Command | What it does |
|---|---|
| `whoami` | Checks the token: shows the Penpot account it signs in as. |
| `init` | Creates or refreshes the manifest: links preset colors and shape values to tokens, components to styles, and the listed view elements to their layers. Safe to rerun; never changes code or Penpot. |
| `fetch` | Downloads the Penpot file to `design/.cache/penpot-file.json` (git-ignored), for inspection. |
| `status [--all]` | Shows what differs (`--all`: everything linked). Exit code 2 when anything differs. |
| `pull` | Writes Penpot's changes into the code, and adds XAML for new layers in synced containers. |
| `push` | Sends the code's changes to Penpot, and adds layers for new marked XAML elements in synced containers. Penpot applies the whole batch or none of it. |

Options: `--dry-run` (pull, push), `--prefer penpot|code` (pull, push), `--cached` (init, status: use the last
fetched copy instead of downloading again).

## What is synced

| In code | In Penpot | Synced |
|---|---|---|
| Preset colors: `ThemePresets.Palettes` in `Src/HackPDM.Core/Theming/ThemePresets.cs` | Color tokens `<Palette>.<Role>` in the sets `HackPDM colors/<preset>`, the matching library colors, and every shape the token is applied to | both ways |
| Shape-style values: `ControlCornerRadius`, `OverlayCornerRadius`, `PanelCornerRadius`, `PanelBorderThickness` in `ThemePresets.Forms` | Tokens `<Style>.<Value>` in the sets `HackPDM shapes/<Style>` | both ways |
| Styles in `Src/HackPDM/Styles/HackControls.xaml`: the Background, BorderBrush, Padding and CornerRadius setters | The component the style stands for (Light and Dark variants, and every copy that hasn't overridden the value) | both ways |
| The same styles' Width and Height setters | The component's size | Penpot → code |
| Uno Material styles (`FilledButtonStyle`, `FilterChipStyle`…) | Button and Filter chip components | recorded only: those styles live in the Uno Material package |
| Marked view elements (`pp:Id`): Padding, Spacing, RowSpacing, ColumnSpacing, CornerRadius, Background, BorderBrush, Text (a TextBlock's text, a button's label) | The linked layer in the Light frame, and its twin in the Dark frame | both ways |
| The same elements' Width, Height and Style | The layer's size, and the component it copies | Penpot → code |
| New marked elements inside a container linked with `"children": true` | New layers inside the linked layer | both ways (added, never removed) |

Colors are compared by Material role (`SurfaceContainerLow`), padding as left,top,right,bottom, the rest as
numbers or text. Bindings (`{x:Bind …}`), commands and event handlers are wiring, not design: never synced.

## How the two sides point at each other

- **`design/penpot-sync.json`** (committed) is the manifest: the Penpot file, and every link between a Penpot
  item and its code, each with the value both sides had at the last sync.
- **Tokens** are linked by id, so renaming a token in Penpot doesn't break the link; names are matched without
  regard to case, and the manifest keeps Penpot's spelling. (The Navy blue tokens are `Navyblue.*` in Penpot,
  where shapes are bound by that name; the code's palette is `NavyBlue`.)
- **Components** are linked by name and variant (`"Pane card"`, `Kind=Tinted` → `HackPaneTintedStyle`). `init`
  adds the default pairs; add more under `components` in the manifest.
- **View elements** carry a marker in the XAML, `pp:Id="graph.toolbar"`, and a layer path in the manifest,
  `Content/Toolbar` ("#2" picks the second of same-named layers, counted in the order they show). `init` turns
  paths into layer ids, so renaming or moving layers afterwards doesn't break the link.

## Linking a new page

1. Design the page in Penpot as a "… — Light" frame (and a "… — Dark" twin with the same layers), built from
   the Components page's components.
2. In the view, declare the marker namespace and mark the elements to sync:

   ```xml
   xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
   xmlns:pp="urn:wyrmpdm:penpot-sync"
   mc:Ignorable="d pp"
   …
   <Border pp:Id="search.toolbar" Style="{StaticResource HackToolbarStyle}">
   ```

3. Add the view under `frames` in the manifest, each element with its layer path in the Light frame:

   ```json
   { "view": "Src/HackPDM/Search/SearchOdooView.xaml", "frame": "Search — Light", "darkFrame": "Search — Dark",
     "elements": [ { "anchor": "search.toolbar", "path": "Content/Toolbar" } ] }
   ```

   Add `"children": true` to a container whose children should be kept in step as a whole; every child in it
   must be marked.
4. `init`, then `status`: values both sides already share start in sync.

New XAML made by `pull` uses the closest control: a component copy becomes its linked style's control, text a
TextBlock, a flex frame a StackPanel, an icon a FontIcon. Wire up its behaviour by hand.

## Limits

- **Penpot's API stores layout but doesn't run it.** After a `push` that adds layers or changes padding, gaps or
  text, Penpot keeps the old positions and sizes until the design is next edited there; any change re-lays it out.
  Until then `status` can show a stale size (e.g. a component still at its old height).
- **Sizes and styles go Penpot → code only.** Resizing a layer through the API means recomputing its geometry;
  swapping a component means replacing the layer. Do both in Penpot, then `pull`.
- **Removals are reported, never applied**, on either side.
- **Text with several differently styled runs** isn't pushed; edit it in Penpot.
- **Pages without links** aren't touched at all.

## Troubleshooting

- **"Penpot refused … the access token is missing, expired or for another Penpot server"**: make a new token
  (Setup) and replace `design/.penpot-token`.
- **"… changed while syncing; run the command again"**: a file changed between reading and writing; rerun.
- **Penpot rejects a push (400 or 500)**: nothing was applied; the error names the layer or token. Run
  `push --dry-run` to see the batch.
- **A value keeps showing as changed on both sides**: choose with `--prefer penpot` or `--prefer code`.
- **Undoing a push**: restore an earlier version from Penpot's version history, or change the value back in code and push.

## Internals

- The code side is read from source text (`ThemePresets.cs`, `HackControls.xaml`, the views), so `status` and
  `pull` see unbuilt edits, and `pull` replaces only the values it changes: comments and layout stay as written.
- The tool talks to Penpot in its native Transit encoding (`Penpot/Transit.cs` reads, `Penpot/TransitWriter.cs`
  writes): the JSON encoding leaves out token ids and can't express the keyword values shape edits need.
- Changes go through Penpot's `update-file` with the revision just read, which Penpot uses to merge them with edits
  made in the meantime.
- Tests: `dotnet test tools/WyrmpdmSync.Tests` (no network or token needed).
