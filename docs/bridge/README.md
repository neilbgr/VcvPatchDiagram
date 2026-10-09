# VcvPatchBridge

A command-line tool that converts a `.vcv` patch between [VCV Rack](https://vcvrack.com/) (standalone) and [Cardinal](https://github.com/DISTRHO/Cardinal) (the plugin version of Rack), so a patch built in one opens correctly in the other.

## Why you need this

Cardinal runs as a plugin inside a DAW, so it can't talk to real MIDI/audio devices the way standalone Rack does. It replaces Rack's hardware-facing "Core" modules with its own DAW-facing equivalents:

| Rack (Core) | Cardinal |
|---|---|
| `MIDI-CV` **+** `CV-MIDI` | `Host MIDI` (one module) |
| `MIDI-CC-CV` **+** `CV-MIDI CC` | `Host MIDI CC` (one module) |
| `MIDI-Gate` **+** `Gate-MIDI` | `Host MIDI Gate` (one module) |
| `Audio-2` / `Audio-8` | `Host Audio 2` / `Host Audio 8` |
| `MIDI-Map` | `Host MIDI Map` |

If you open a Cardinal-saved patch in standalone Rack (or vice versa), those modules are simply missing — the patch loads broken, with dangling cables. VcvPatchBridge rewrites the patch so the right modules, cables, and settings (MIDI channel, learned CC numbers, learned gate notes...) come out the other side.

## Getting the tool

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) once, to build it:

```bash
git clone https://github.com/neilbgr/VcvPatchBridge.git
cd VcvPatchBridge
make publish-linux   # or: make publish-win / make publish-osx / make publish-all
```

This produces a single self-contained executable at `publish/<runtime>/VcvPatchBridge` (`.exe` on Windows) — copy it wherever you like, there's nothing else to install.

## Usage

The examples below assume the executable is on your `PATH` (or that you've `cd`'d into its folder and use `./VcvPatchBridge` on Linux/macOS, `VcvPatchBridge.exe` on Windows).

### Check what a patch is

```bash
VcvPatchBridge detect "My Patch.vcv"
```

```
File         : My Patch.vcv
Format       : tar+zstd archive
Origin       : Cardinal
Cardinal modules found (4):
  - HostAudio2
  - HostMIDI
  - HostMIDICC
  - HostMIDIGate
```

### Convert a patch

The direction is guessed automatically from whatever the patch contains, so most of the time you don't need `--to` at all:

```bash
# Cardinal -> Rack: writes "My Patch.rack.vcv" next to the original
VcvPatchBridge convert "My Patch.vcv"

# Rack -> Cardinal, same idea: writes "My Patch.cardinal.vcv"
VcvPatchBridge convert "My Patch (rack version).vcv"
```

Give it an explicit output name if you'd rather not use the auto-generated one:

```bash
VcvPatchBridge convert "My Patch.vcv" "My Patch for the studio DAW.vcv"
```

Force a direction (useful for a patch with no Cardinal- or Rack-only module, which the tool can't guess from):

```bash
VcvPatchBridge convert plain-patch.vcv --to cardinal
```

Cable colors are remapped by default (see below); pass `--no-cable-colors` to leave them untouched:

```bash
VcvPatchBridge convert "My Patch.vcv" --no-cable-colors
```

Re-running a conversion won't clobber a previous result — it adds `.x1`, `.x2`, etc. Pass `--force` if you actually want to overwrite:

```bash
VcvPatchBridge convert "My Patch.vcv" --force
```

### Convert a whole folder at once

```bash
for f in ~/Documents/VCVRack/*.vcv; do
    VcvPatchBridge convert "$f" --to rack
done
```

### Reading the warnings

`convert` prints a `Warnings` section whenever something couldn't be carried over 1:1 (see the mapping details below for exactly which cases these are). No warnings means the conversion is complete — every module and cable maps cleanly.

## What actually happens during a conversion

**Cable colors are translated to the other app's palette.** Rack's default cable palette (5 colors) and Cardinal's (16 colors) don't share any hex values, so left alone a cable's color would come out as an arbitrary, unrelated hue on the other side. Every cable's color — including a manually-picked custom color, not just the app's own default palette — is remapped to whichever color in the destination palette has the closest hue, so a red cable stays red, a green one stays green, etc. Pass `--no-cable-colors` to skip this and leave colors exactly as they are in the source file.

**Straight renames** — same module, same ports, just a different plugin/name: `AudioInterface2`↔`HostAudio2`, `AudioInterface`↔`HostAudio8`, `MIDI-Map`↔`HostMIDIMap`, `Notes`↔`TextEditor`, `Blank`↔`Blank`.

**MIDI I/O modules are split or merged.** Cardinal's `Host MIDI` / `Host MIDI CC` / `Host MIDI Gate` each do both directions (MIDI-in **and** MIDI-out) in one panel; Rack splits each into two separate Core modules. Converting rewires every cable to the right half automatically:

| Cardinal | Rack "in" (MIDI → CV) | Rack "out" (CV → MIDI) |
|---|---|---|
| Host MIDI (9 HP) | MIDI-CV (8 HP) | CV-MIDI (8 HP) |
| Host MIDI CC (14 HP) | MIDI-CC-CV (10 HP) | CV-MIDI CC (10 HP) |
| Host MIDI Gate (14 HP) | MIDI-Gate (10 HP) | Gate-MIDI (10 HP) |

A few things worth knowing about this split:

- **Only the half you actually use gets created.** If nothing is patched into (or out of) one direction, that Rack module is skipped entirely rather than left dangling in the rack — e.g. a `Host MIDI` used only to receive notes produces just `MIDI-CV`, not an unused `CV-MIDI` next to it.
- **Everything downstream shifts to make room, or to close the gap.** The two Rack modules are always wider than the one Cardinal module they replace (e.g. Host MIDI CC's 14 HP becomes 20 HP as two 10 HP modules) — or narrower, when only one half survives (see above). Every other module further right on the same row is moved by exactly that amount, so nothing ends up overlapping and nothing that used to sit flush against something (an expander, for instance) ends up with a gap.
- **The MIDI channel comes along.** Cardinal shows channel 0 as "All" and channels 1-16 otherwise; Rack shows -1 as "All" and 0-15 otherwise. The conversion translates between the two, in both directions.
- **Learned CC numbers and learned gate notes come along too** — whatever you assigned on the Cardinal panel (or vice versa) shows up already filled in on the other side, instead of every cell reading "Learn" again.
- **Host MIDI Gate has 18 note cells; Rack's MIDI-Gate/Gate-MIDI only have 16.** Whichever cells you actually have cabled are compacted down into Rack's 16 slots (in their original order) rather than the two highest-numbered cells being silently discarded just because of their index — you only lose cells if you're genuinely using more than 16 at once, and that's reported as a warning naming the exact cell.
- **Rack → Cardinal merges rely on the two halves being adjacent** (i.e. drawn touching each other, the same relationship VCV expanders use). If a matching pair is found next to each other, they merge into one Cardinal module; a `MIDI-CV` (or `MIDI-CC-CV`/`MIDI-Gate`) with no adjacent partner still converts, but its MIDI-out inputs are left unconnected (reported as a warning).

**Host MIDI CC's 2 extra ports** (Channel Pressure, Pitchbend — Cardinal-only, beyond the 16 regular CC cells) have no Rack equivalent; any cable on them is dropped, and reported.

**Modules with no equivalent at all** in the other application are copied through unchanged, with a warning — they simply won't load there:
- Rack-only: `AudioInterface16`
- Cardinal-only: `HostCV`, `HostParameters`, `HostParametersMap`, `HostTime`, `CardinalBlank`, `AudioToCVPitch`, `Carla`, `Ildaeil`, `AidaX`, `AudioFile`, `GlBars`, `SassyScope`, `MPV`, `ExpanderInputMIDI`, `ExpanderOutputMIDI`

**What it can't do:** pick a MIDI device for you. Cardinal receives MIDI directly from its host DAW, with no device selection at all, so there's nothing to carry over — after converting to Rack you'll need to open the new `MIDI-CV`/`MIDI-CC-CV`/`MIDI-Gate` modules and choose a MIDI input device by hand (the channel, however, is already set correctly).

## Development

```bash
dotnet build
dotnet test
```

Tests live in `tests/VcvPatchBridge.Tests` (xUnit) and are the source of truth for the exact mapping rules above — when in doubt about an edge case, that's where to look.

### Publishing manually

```bash
dotnet publish src/VcvPatchBridge -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/linux-x64
```

(Swap `linux-x64` for `win-x64` or `osx-x64`.) The project must be specified explicitly, not the solution file — otherwise `dotnet publish` also targets the test project, which fails single-file publish (`NETSDK1098`).

Or via the `Makefile`: `make publish-all` (linux-x64 + win-x64), `make publish-linux`, `make publish-win`, `make publish-osx`, `make build`, `make test`, `make clean`.
