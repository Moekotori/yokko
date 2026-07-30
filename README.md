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
- Import osu!mania `.osu` / `.osz`, Quaver `.qua` / `.qp`, Malody Key `.mc` / `.mcz`, Etterna/StepMania
  `.sm` / `.ssc` / `.zip` / `.smzip`, and BMS `.bms` / `.bme` / `.bml` charts.
- Preserve Quaver normalized and legacy BPM-relative SV, including initial,
  zero, negative, timing-group, and scroll-speed-factor changes.
- Export editable charts as osu!mania `.osu` files.
- Preserve osu! timing points, including BPM changes and inherited timing data.
- Preview chart structure with waveform support where audio can be loaded.
- Judge hold-note heads and releases as separate gameplay events.
- Playtest charts using keyboard mappings:
  - 4K: `D F J K`
  - 7K: `S D F Space J K L`

Exports currently write to `Documents\Yokko Exports`.

Import currently targets playable 4K/7K tap, hold, offset, and BPM semantics.
Unsupported source features such as scratch lanes, BGA, warps, scroll effects
outside the supported Quaver SV model, or runtime keysound mixing are reported
as import warnings instead of being silently discarded.

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

## Windows Playtest Package

Double-click `打包Yokko测试包.bat` to build a complete Windows x64 playtest
package. The script builds the native audio library, publishes a self-contained
.NET 8 desktop build, copies the playtest documents, creates a ZIP, verifies its
required files, and writes a SHA-256 checksum under `artifacts\packages`.

The package includes both WASAPI and ASIO and statically links the MSVC runtime.
The script resolves the ASIO SDK from `YOKKO_ASIO_SDK_DIR` or the existing
native-audio CMake cache. If no valid SDK is available, packaging fails instead
of silently producing a build with disabled features. The SDK path can also be
provided explicitly:

```powershell
.\scripts\package-playtest.ps1 `
    -AsioSdkDir "D:\path\to\asio-sdk" `
    -OpenOutputFolder
```

## Crash Reports

The desktop build writes a timestamped diagnostic report to the `crashes`
directory in Yokko's user storage when a fatal managed exception reaches the
process boundary. Each report includes the complete exception chain and stack
trace, application/runtime/system details, thread context, and the location of
the related osu!framework logs.

## Timing Direction

Gameplay judgement should be driven by audio time and input timestamps, not frame time.

The intended judgement path is:

```text
input gameplay time = presented audio time - input event age + user offset
hit error = input gameplay time - object time
```

The audio clock already reports presented position, so device latency is
diagnostic data and is not subtracted a second time. Frame time is only for
presentation. A dropped frame may look bad, but it should not shift judgement.

## osu!mania skins

Yokko can load legacy osu!mania skins directly. No
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

Legacy `[Mania]` geometry, colours, stage pieces, upscroll and split-stage
settings are honoured for every key count defined by the osu! skin format.
This includes animated keys, notes, long-note parts, stage lights, hit lighting, hitbursts, combo
digits, combo bursts, warning arrows, key reminders, custom hit sounds and the
vertical mania scorebar health display. Custom paths, case-insensitive names,
`.png`/`.jpg`/`.jpeg`, `@2x` assets, numbered animation frames and oversized
long-note bodies are supported. Missing or invalid resources fall back to
Yokko's built-in rendering instead of preventing gameplay.

Editor rows are derived from the active uninherited timing point and beat divisor. They are not fixed-duration rows, so BPM changes remain aligned to the song grid.

## Audio Direction

The first backend target is stable shared playback through osu!framework/BASS/WASAPI on Windows. WASAPI exclusive and ASIO should come later behind the same `Yokko.Audio` boundary, after the gameplay timing path is measurable and repeatable.
