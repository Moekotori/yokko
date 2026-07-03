# Yokko Architecture

## Goals

- 4K and 7K vertical-scrolling rhythm gameplay.
- Frame-rate independent judgement using audio-clock time.
- Renderer designed for very high frame rates with no per-frame gameplay allocations.
- Import boundaries for osu!mania, Malody, BMS, and LR2-style BMS folders.
- Replaceable audio backends: shared WASAPI first, then WASAPI exclusive and ASIO.

## Layering

`Yokko.Game` is allowed to know about osu!framework. It should not parse chart files or talk directly to low-level audio devices.

`Yokko.Core` contains portable gameplay data and timing rules. This layer should stay free of rendering and platform dependencies.

`Yokko.Audio` owns device discovery, playback start/stop, latency reporting, and the authoritative playback clock.

`Yokko.Import` converts outside formats into `Yokko.Core` beatmaps. Format-specific quirks stay in this layer.

## Timing Contract

The gameplay path should calculate hit error as:

```text
input timestamp - audio playback time - user/device offset
```

Frame time is only for presentation. A dropped frame may look bad, but it must not shift judgement.
