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
- The tool reads the file in Penpot's native Transit encoding (decoded in `Penpot/Transit.cs`); the plain JSON
  encoding leaves out the token ids that writing back needs.
- Coming in later phases: shape values ↔ shape styles, components ↔ `Styles/HackControls.xaml` styles, and page
  frames ↔ views through `pp:Id` markers on XAML elements.

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
| `status [--all]` | Shows what differs and which side changed: `<` changed in Penpot (pull), `>` changed in code (push), `!` changed on both sides. Exit code 2 when anything differs. |

Add `--cached` to `init` or `status` to use the last fetched copy instead of downloading again.
`pull` (Penpot → code) and `push` (code → Penpot) arrive in phase 2.
