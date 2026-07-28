# Yokko Architecture

## Goals

- 4K and 7K vertical-scrolling rhythm gameplay.
- Frame-rate independent judgement using audio-clock time.
- Renderer designed for very high frame rates with no per-frame gameplay allocations.
- Import boundaries for osu!mania, Malody, BMS, and LR2-style BMS folders.
- Yokko-owned audio backends: event-driven WASAPI exclusive with shared fallback,
  followed by ASIO.

## Layering

`Yokko.Game` is allowed to know about osu!framework. It should not parse chart files or talk directly to low-level audio devices.

`Yokko.Core` contains portable gameplay data and timing rules. This layer should stay free of rendering and platform dependencies.

`Yokko.Audio` owns device discovery, playback start/stop, latency reporting, and the authoritative playback clock.
It may use codec libraries on decoder worker threads, but output, buffering,
device access, and clock truth must not depend on osu!framework.

The production low-latency path is implemented behind a stable native C ABI.
The native output callback owns the hardware-facing clock and never calls
managed code. See [Native Audio](NATIVE_AUDIO.md) for real-time invariants,
clock truth, fallback policy, and backend rollout gates.

`Yokko.Import` converts outside formats into `Yokko.Core` beatmaps. Format-specific quirks stay in this layer.

## Beat Timing

`YokkoTimingPoint` preserves the timing-point fields required for osu!mania round trips, including inherited points. `BeatTimingMap` uses uninherited positive beat lengths as the authoritative beat grid and converts editor rows to song time through the active timing segment.

The editor must not assume a fixed number of milliseconds per row. New notes are placed using the active timing point and the selected beat divisor.

## Hold Judgement

Hold notes have two scored phases:

- the head is judged on key press;
- the tail is judged on key release.

The hold remains active between those phases. Releasing outside the tail window or holding beyond it resolves the tail as a miss. A hold is complete only after both phases resolve.

## Timing Contract

The gameplay path should calculate hit error as:

```text
input timestamp - audio playback time - user/device offset
```

Frame time is only for presentation. A dropped frame may look bad, but it must not shift judgement.
