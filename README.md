# Yokko

Yokko is a work-in-progress 4K/7K rhythm game prototype built on osu!framework.

The goal is a keyboard-first mania-style game with precise judgement, smooth rendering, editable charts, practical import/export tools, and a replaceable audio backend path that can grow from stable shared playback into lower-latency options later.

## Status

Yokko is actively being worked on. It is not a finished game yet.

Current focus:

- playable 4K/7K gameplay prototypes
- lightweight chart editor workspace
- osu!mania, Quaver, Malody Key, Etterna/StepMania, and BMS-family import
- osu!mania `.osu` export
- audio-clock-driven playtesting
- stable shared audio playback first
- cleaner timing, judgement, and offset handling

Things are expected to change quickly while the core loop is still being shaped.

## What Works

- Start a desktop build with osu!framework.
- Create new 4K or 7K draft charts.
- Toggle notes on a timeline grid.
- Import osu!mania `.osu` / `.osz`, Quaver `.qua`, Malody Key `.mc` / `.mcz`, Etterna/StepMania
  `.sm` / `.ssc` / `.zip` / `.smzip`, and BMS `.bms` / `.bme` / `.bml` charts.
- Export editable charts as osu!mania `.osu` files.
- Preserve osu! timing points, including BPM changes and inherited timing data.
- Preview chart structure with waveform support where audio can be loaded.
- Judge hold-note heads and releases as separate gameplay events.
- Playtest charts using keyboard mappings:
  - 4K: `D F J K`
  - 7K: `S D F Space J K L`

Exports currently write to `Documents\Yokko Exports`.

Import currently targets playable 4K/7K tap, hold, offset, and BPM semantics.
Unsupported source features such as scratch lanes, BGA, warps, special scroll
effects, or runtime keysound mixing are reported as import warnings instead of
being silently discarded.

## Project Layout

- `Yokko.Desktop` launches the desktop host.
- `Yokko.Game` owns screens, visuals, editor flow, gameplay flow, and osu!framework integration.
- `Yokko.Core` owns timing, judgement, editing models, and internal chart data.
- `Yokko.Audio` defines the replaceable audio engine boundary.
- `Yokko.Import` converts external chart formats into Yokko chart models.
- `Yokko.Game.Tests` contains core and visual tests.

## Development

```powershell
dotnet restore .\Yokko.Desktop.slnf
dotnet build .\Yokko.Desktop.slnf
dotnet test .\Yokko.Desktop.slnf
dotnet run --project .\Yokko.Desktop\Yokko.Desktop.csproj
```

Use `Yokko.Desktop.slnf` for fast desktop iteration. The full solution also contains the template iOS project for later platform work.

## Timing Direction

Gameplay judgement should be driven by audio time and input timestamps, not frame time.

The intended judgement path is:

```text
input timestamp - audio playback time - user/device offset
```

Frame time is only for presentation. A dropped frame may look bad, but it should not shift judgement.

## osu!mania skin preview

Yokko can load an initial subset of osu!stable mania skins directly. No
Yokko-specific skin conversion is performed. Drop an `.osk` package, a
`skin.ini`, or an extracted skin folder anywhere in the Yokko window to import
and enable it. Installed skins can be selected or removed from
`Settings > Skins`; the active selection persists across restarts.

For development overrides, an extracted skin can still be placed under:

```text
Skins/Current/
```

Alternatively, place a packaged skin at `Skins/current.osk`, or set
`YOKKO_OSU_MANIA_SKIN` to an absolute extracted-folder or `.osk` path before
launching Yokko.

The current compatibility slice supports 4K/7K repeated `[Mania]` sections,
column widths/spacing/colours, hit position, key-up/key-down textures, tap notes,
long-note heads/bodies/tails, custom asset paths, `.png`/`.jpg`/`.jpeg`, `@2x`
assets, and first-frame fallback for animated assets. Arrow and LN-focused skins
also honour `UpsideDown`, per-element flip settings, `KeysUnderNotes`,
`WidthForNoteHeightScale`, and the common stretch/top-crop/bottom-crop
`NoteBodyStyle` modes. This includes very tall body textures used by Chinese
community “投皮” variants. Missing or invalid resources fall back to Yokko's
built-in rendering instead of preventing gameplay.

Animation playback, hit bursts, skin sounds, stage splitting, and rare extended
long-note body modes are still in progress.

Editor rows are derived from the active uninherited timing point and beat divisor. They are not fixed-duration rows, so BPM changes remain aligned to the song grid.

## Audio Direction

The first backend target is stable shared playback through osu!framework/BASS/WASAPI on Windows. WASAPI exclusive and ASIO should come later behind the same `Yokko.Audio` boundary, after the gameplay timing path is measurable and repeatable.
