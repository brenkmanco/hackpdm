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
- Coming in later phases: components ↔ `Styles/HackControls.xaml` styles, and page frames ↔ views through
  `pp:Id` markers on XAML elements.

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
| `init` | Creates or refreshes the manifest: links every preset color to the token of the same name. Safe to rerun. |
| `fetch` | Downloads the Penpot file to `design/.cache/penpot-file.json` (git-ignored), for inspection. |
| `status [--all]` | Shows what differs and which side changed: `<` changed in Penpot (pull), `>` changed in code or missing in Penpot (push), `!` changed on both sides. Exit code 2 when anything differs. |
| `pull` | Writes values changed in Penpot into `ThemePresets.cs`. Rebuild the app afterwards. |
| `push` | Sends values changed in code to Penpot: the token (creating its set and switching it on if needed), the matching library color, and every shape the token is applied to (Penpot's API doesn't repaint those itself). All or nothing: Penpot applies the whole batch or none of it. |

Options: `--dry-run` (pull/push: show, change nothing), `--prefer penpot|code` (pull/push: settle values changed
on both sides, or that differ with no last-synced value), `--cached` (init/status: use the last fetched copy).

A typical round: `status`, then `pull` for design changes made in Penpot, `push` for changes made in code. Neither
command touches a value changed on both sides until you pick a side with `--prefer`.
