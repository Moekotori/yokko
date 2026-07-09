# Yokko

Yokko is a work-in-progress 4K/7K rhythm game prototype built on osu!framework.

The goal is a keyboard-first mania-style game with precise judgement, smooth rendering, editable charts, practical import/export tools, and a replaceable audio backend path that can grow from stable shared playback into lower-latency options later.

## Status

Yokko is actively being worked on. It is not a finished game yet.

Current focus:

- playable 4K/7K gameplay prototypes
- lightweight chart editor workspace
- osu!mania `.osu` import and export
- audio-clock-driven playtesting
- stable shared audio playback first
- cleaner timing, judgement, and offset handling

Things are expected to change quickly while the core loop is still being shaped.

## What Works

- Start a desktop build with osu!framework.
- Create new 4K or 7K draft charts.
- Toggle notes on a timeline grid.
- Import and export osu!mania `.osu` files.
- Preview chart structure with waveform support where audio can be loaded.
- Playtest charts using keyboard mappings:
  - 4K: `D F J K`
  - 7K: `S D F Space J K L`

Exports currently write to `Documents\Yokko Exports`.

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

## Audio Direction

The first backend target is stable shared playback through osu!framework/BASS/WASAPI on Windows. WASAPI exclusive and ASIO should come later behind the same `Yokko.Audio` boundary, after the gameplay timing path is measurable and repeatable.
