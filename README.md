# VcvPatchDiagram

Turns a [VCV Rack](https://vcvrack.com/) / [Cardinal](https://github.com/DISTRHO/Cardinal) `.vcv` patch into a **macro block diagram that explains it**: who does what, where the sound comes from, what modulates it, and why each cable is there.

A dense patch is a wall of cables, knobs and lights. This tool reads the patch file and draws it the way synth manuals and teachers do (Moog manuals, *Patch & Tweak*): blocks by role, signal flowing left to right, cables typed by what they carry. It doesn't show the inside of modules: it shows how they work **together**.

It comes as:

- a **web app** (Blazor WebAssembly, runs entirely in the browser: the patch is never uploaded);
- a **command-line tool** (`vcvdiagram`) to inspect a patch or render it;
- exports to a **self-contained interactive HTML page**, **SVG**, **Graphviz** (`.dot`), **Mermaid** (`.mmd`) and **JSON**.

All three share one C# library, so they always draw the same diagram.

**Try it online: [neilbgr.github.io/VcvPatchDiagram](https://neilbgr.github.io/VcvPatchDiagram/)**. Drop a `.vcv` file on the page, or open the sample patch. The patch is read in your browser and never uploaded.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/overview-dark.png">
  <img alt="Folded overview of a patch: performance, sequencing, two voices and the mix bus, linked by typed cables" src="docs/screenshots/overview-light.png">
</picture>

*A 22-module patch, folded into its main blocks: who plays it, what sequences it, the two voices and the mix bus.*

## What the diagram shows

Here is the folded overview of a small ambient patch, exported to Mermaid (GitHub renders it):

```mermaid
flowchart LR
  subgraph b0["Performance (MIDI, host, played live)"]
    n_g_external["Played live<br/>Host MIDI, Host MIDI CC, Host MIDI Gate"]:::performance
  end
  subgraph b1["Time & sequencing"]
    n_g_time["Sequencing<br/>Clkd, ADDR-SEQ"]:::time
  end
  subgraph b2["Voices"]
    n_g_voice_1["LVCO ← ADDR-SEQ<br/>VCF #2 · +2 mod"]:::source
    n_g_voice_2["PULSE ← Host MIDI<br/>VCF #1 → Waveshaper → EQ · +4 mod"]:::source
  end
  subgraph b3["Mix & effects"]
    n_g_bus["Mix & effects<br/>Random, MIX4, Plateau, Delay Plus Stereo Fx, Audio 2"]:::mixer
  end
  n_g_voice_1 ==>|"into MIX4"| n_g_bus
  n_g_external -.->|"4 × gate / trig / clock"| n_g_voice_2
  n_g_external -->|"2 × modulation"| n_g_voice_2
  n_g_time -->|"plays the notes"| n_g_voice_1
  n_g_time -.->|"fires the envelope / new value"| n_g_voice_1
  n_g_external -->|"hand control of frequency & pre-filter gain"| n_g_voice_1
  n_g_time -.->|"fires the envelope / new value"| n_g_bus
  n_g_external -.->|"3 × gate / trig / clock"| n_g_time
  n_g_external -->|"3 × pitch"| n_g_voice_2
  n_g_voice_2 ==>|"into MIX4"| n_g_bus
  linkStyle 0 stroke:#d64545
  linkStyle 1 stroke:#2f74c4
  linkStyle 2 stroke:#2c9457
  linkStyle 3 stroke:#b97d00
  linkStyle 4 stroke:#2f74c4
  linkStyle 5 stroke:#2c9457
  linkStyle 6 stroke:#2f74c4
  linkStyle 7 stroke:#2f74c4
  linkStyle 8 stroke:#b97d00
  linkStyle 9 stroke:#d64545
  classDef time stroke:#2f74c4,stroke-width:2px
  classDef pitch stroke:#b97d00,stroke-width:2px
  classDef controller stroke:#2c9457,stroke-width:2px
  classDef source stroke:#d64545,stroke-width:2px
  classDef modifier stroke:#c2611f,stroke-width:2px
  classDef mixer stroke:#7a52b3,stroke-width:2px
  classDef effect stroke:#b0479a,stroke-width:2px
  classDef io stroke:#6b6b70,stroke-width:2px
  classDef performance stroke:#138a8a,stroke-width:2px
  classDef monitor stroke:#9a958a,stroke-width:1px,stroke-dasharray:2 3
```

The HTML page and the web app draw the same thing with more detail: orthogonal "metro map" lines, layers you switch on one by one, and boxes you unfold.

### Bands (rows), by role

From top to bottom:

| Band | Contains |
|---|---|
| Performance | what the musician plays: Host MIDI, MIDI-to-CV, on-screen pads and joysticks played with the mouse, keyboard zones and pads fed by MIDI. A pad or joystick driven by another module of the patch (a sequencer, an LFO) moves to the band of what it then does |
| Time & sequencing | clocks, sequencers, gate/trigger logic |
| Pitch | quantizers, pitch processing |
| Modulation | LFOs, envelopes, random, slew… shared by several voices |
| Voices | one lane per voice: a sound source and what shapes it before the mixer |
| Mix & effects | mixers, bus effects, audio out |

### Columns, by signal path

Each box is placed by the longest signal path that leads to it, so the patch reads **left to right**, from control to sound.

### Boxes and ports

Inspired by the diagrams of the YouTube channel *MonoTrail Tech Talk*:

- **Port tabs**: where a cable plugs into a box, a small tab in the cable's color names the port (`V/oct`, `Gate`, `Cutoff`, `CC 74`, `A2`…). You see what a cable acts on without reading anything along it. Long names are shortened; hover a tab for the full name. Off with "port names" (web app) or `--no-ports` (CLI).
- **Trunks**: cables leaving the same output share one exit, one line and their first turn, then branch towards their targets, like a mult.
- **Function icons**: each module box shows what it does with a small drawing (waveform for an oscillator, cutoff slope for a filter, ADSR outline for an envelope…), and its function in the subtitle. With "function names" (web app) or `--functions` (CLI), the function becomes the title (`FILTER #1`, `ENVELOPE #2`) and the module name goes under it.
- **VCV Library links**: a small ↗ icon on each module box opens its page on [library.vcvrack.com](https://library.vcvrack.com), to find the module in Rack or Cardinal. Hovering the icon shows the module's panel. The screenshot is fetched only then, kept for the rest of the session and cached by the browser; a module the Library doesn't have is remembered and never asked for again. Cardinal-only modules get no icon. A folded group has a panels icon instead (no link): hovering it shows each of its modules once, with a `×3` badge when there are several, up to 12 panels. Off with "library links" (web app) or `--no-links` (CLI).
- **Legend**: an exported `.svg` carries its own legend (the signals and roles it shows), so it reads on its own once shared.

### Cables, by signal type

The type comes from the patch's own cable colors, cross-checked with the real port names:

| Signal | Line | Cable color in the patch |
|---|---|---|
| Audio | thick, red | `#ff5252` |
| Pitch (1V/oct) | medium, amber | `#ffd452` |
| Modulation (CV) | thin, green | `#52ff7d`, `#a8ff52` |
| Gate / trig / clock | thin, blue | `#52beff` |

When a color contradicts the port (a red cable from an LFO into a cutoff CV input), the port wins and a diagnostic says so.

### Layers, a teaching path

The toolbar shows the patch step by step: **1 · audio** (where the sound goes), **2 · + pitch** (who plays the notes), **3 · + modulation** (what makes it move), **4 · + gate/trig/clock** (what triggers it). Each layer can also be toggled on its own.

### Folded overview first, details on demand

A complex patch gives a complex diagram, which defeats the purpose. So the first view is **folded**:

- each **voice** is one box (`PULSE ← Host MIDI`, with its chain `VCF #1 → Waveshaper → EQ · +4 mod`); identical voices merge (`LVCO ×3`);
- a modulator whose cables all go to one voice is folded into that voice;
- shared modulators, time, performance and the mix bus are one box each.

Click a box (web app) to unfold it into its modules, in a lane of its own. The CLI does the same with `--unfold`.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/unfolded-dark.png">
  <img alt="The same patch unfolded: every module in its band, one lane per voice" src="docs/screenshots/unfolded-light.png">
</picture>

*The same patch, everything unfolded: one lane per voice, signal flowing left to right.*

### What the analysis recognizes

- **Voices**: a source (oscillator, complete voice such as Lunar VCO) and everything that processes its audio before a mixer.
- **Voice processing vs bus processing**: a filter right after an oscillator belongs to that voice; a filter after the mixer is on the bus.
- **Inserts and send/returns**: an effect patched into a module's insert or aux send and returning into it is drawn stacked under its host, with a side loop.
- **Expanders**: an expander docked to its base (Venom mix expanders, MindMeld AuxSpander…) is part of it. Its cables attach to the base, ports prefixed with its name (`Mix Pan › Pan CV 2`).
- **Feedback loops**: cross-modulation and feedback are drawn turning back to their target, tagged `↺ feedback`, and hinted on folded boxes.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/feedback-dark.png">
  <img alt="Two voices with cross-modulation: the feedback cables run back under the voice to their target" src="docs/screenshots/feedback-light.png">
</picture>

- **Surge XT modulation**: Surge XT modules have "Modulation Signal n" inputs that can be assigned to any knob. The tool reads which knob each input moves and by how much (`Mod 1 → Cutoff +35%`), or reports when an input moves nothing.
- **MIDI learn**: the cells of MIDI gate and CC modules (Rack's MIDI to Gate / MIDI CC to CV and their CV to MIDI counterparts, Cardinal's Host MIDI Gate / CC) are named by the note or controller they learned in the patch: `Gate 13 · A2`, `Cell 2 · CC 74 (cutoff)`.
- **Scopes and displays** never get a box (they explain nothing about the sound). With "scopes & displays" on, they show as a 👁 badge on the box they watch.
- **Contextual roles**: controls played live (on-screen pads, joysticks, keyboard zones) belong to performance when played with the mouse or fed by Host MIDI, but become pitch/time/control utilities when another module of the patch drives them; a "visual" module with outputs is a controller.

### Intents

Every cable gets an **intent**, a short sentence for why it's there ("plays the notes", "the LFO moves the cutoff"). It is pre-filled from the ports and roles. In the web app, click a cable to write your own: intents are kept in your browser and included in exports.

### Diagnostics

`inspect` and the web app list what looks suspicious:

- stacked cables on one input (summed by Rack ≥ 2.5, but Cardinal keeps only the first one);
- cable colors that contradict the port;
- modules whose role was guessed (to add to `catalog/roles.json`);
- modules missing from the port catalog;
- Surge XT modulation inputs with no target.

## Layout algorithm

The drawing follows the **Sugiyama framework** for layered graphs (Sugiyama, Tagawa & Toda, 1981), the method behind Graphviz `dot` and ELK Layered, adapted to role bands:

1. **Cycle breaking**: feedback loops are broken with the greedy heuristic of Eades, Lin & Smyth (1993), so that as few cables as possible go backwards.
2. **Layering**: columns by longest path.
3. **Virtual nodes**: a cable spanning several columns gets a thin track in each column it passes, like a metro line, so it never runs through a box. A feedback cable gets tracks from its source's column back to its target's.
4. **Crossing reduction**: barycenter heuristic, alternating sweeps, keeping the order with the fewest crossings.
5. **Orthogonal routing**: every bend happens in the gap between columns, each cable in its own channel. Busy gaps widen, and boxes grow with their number of connections so arrowheads never overlap.

Strongly connected components (Tarjan, 1972) find the feedback loops reported by the analysis.

## Getting started

Requirements: the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/neilbgr/VcvPatchDiagram.git
cd VcvPatchDiagram
make build
make test
```

### Web app

```bash
make web
```

Open http://localhost:5016 and drop a `.vcv` file (or add `?sample=AmbientJam` to the URL for the bundled sample). From there:

- unfold/fold boxes, or use **Overview** / **Unfold all**;
- step through the layers, toggle "scopes & displays" (offered when the patch has some);
- switch port names on the box edges and function names in the titles;
- drag the background to pan, Ctrl + wheel to zoom;
- hover a box to follow its cables, click a cable to edit its intent;
- export `.html`, `.svg`, `.dot`, `.mmd` or `.json`.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/web-app-dark.png">
  <img alt="The web app: drop zone, view, layer, export and zoom controls above the diagram" src="docs/screenshots/web-app-light.png">
</picture>

`make publish-web` builds a static site in `publish/web/wwwroot`, which any static file server can host (no backend needed).

### Command line

```bash
dotnet run --project src/VcvPatchDiagram.Cli -- <command>
# or build a single-file executable: make publish-linux / make publish-win (output in publish/)
```

| Command | Does |
|---|---|
| `vcvdiagram inspect <patch.vcv>` | Text summary: modules by band, voices and their chains, inserts, groups, typed cables, loops, diagnostics |
| `vcvdiagram render <patch.vcv> [-o out.html]` | Self-contained interactive HTML page (folded overview) |
| `vcvdiagram render <patch.vcv> -f svg\|dot\|mmd\|json [-o file]` | Other formats |
| `… --unfold all` or `--unfold voice-1,bus` | Unfold everything, or the given groups (keys listed by `inspect`) |
| `… --scopes` | Show scopes and displays as badges |
| `… --functions` | Title boxes by function (`FILTER #1`) |
| `… --no-ports` | No port tabs on the box edges |
| `… --no-links` | No VCV Library links on the module boxes |
| `vcvdiagram catalog build --src <Cardinal/plugins>` | Regenerate the port catalog (see below) |

Windows paths are accepted under WSL (`"C:\Users\me\Documents\patch.vcv"` is read from `/mnt/c/...`). Patch files are only read, never modified.

Graphviz: `dot -Tsvg patch.dot > patch.svg`. Mermaid: paste the `.mmd` into any Markdown that supports it (GitHub, GitLab, Obsidian…) or https://mermaid.live.

## Port catalog and roles

`.vcv` files store cables as module id + port index. Names and roles come from three files in `catalog/`, embedded in the library:

| File | Content | Maintained |
|---|---|---|
| `ports.json` | port names of ~1,700 modules | generated by `make catalog` |
| `ports.overrides.json` | names the scanner can't work out | by hand, wins over `ports.json` |
| `roles.json` | role of a module (source, modifier, mixer…) | by hand, wins over plugin tags |

`make catalog PLUGINS=<path to Cardinal/plugins>` scans the C++ sources of every plugin bundled with Cardinal, plus Rack's own Core plugin (`src/Rack/Core.json` and `src/Rack/src/core` next to it):

- `createModel<…>("slug")`;
- `enum InputIds/OutputIds`, including `ENUMS(X, n)` with constant expressions and template arguments;
- `configInput` / `configOutput` names, including names built in loops;
- for Surge XT, `ModulationAssistant<…>` (which knob each modulation input can move).

What it can't resolve is listed in `catalog/scan-report.txt`. Without a role in `roles.json`, a module's role is guessed from its `plugin.json` tags and its name, and reported as guessed.

## Project layout

```
src/VcvPatchDiagram.Core/   all the logic, no file I/O (runs in the browser)
  Patch/      .vcv reading (zstd + tar, read without System.Formats.Tar, unsupported in WebAssembly)
  Catalog/    port catalog, source scanner, roles
  Analysis/   signal typing, voices, inserts, expanders, loops, grouping, intents
  Layout/     layered layout (positions and cable paths: the single source of truth for every renderer)
  Render/     SVG, HTML, Graphviz, Mermaid; shared CSS and JavaScript
src/VcvPatchDiagram.Cli/    vcvdiagram command-line tool
src/VcvPatchDiagram.Web/    Blazor WebAssembly app
tests/                      xUnit tests, sample patches (fixtures), golden files
catalog/                    port catalog and roles
```

## Development

- Tests: `make test`. Exporter output is pinned by golden files in `tests/golden`: after an intended change, regenerate them with `UPDATE_GOLDEN=1 dotnet test` and review the diff.
- Style: `make format` (`dotnet format`, rules in `.editorconfig`; explicit types, no `var`).
- Screenshots (`docs/screenshots`, light and dark): rendered pages captured with a headless browser, e.g. `msedge --headless=new --blink-settings=preferredColorScheme=1 --window-size=W,H --screenshot=out.png page.html` (`=0` for dark).
- Web app online: every push to `main` runs the tests and publishes it to GitHub Pages (`.github/workflows/pages.yml`).
- Contributions welcome, especially entries for `catalog/roles.json` and `catalog/ports.overrides.json` for the modules you use.

## License

[MIT](LICENSE)
