# Yokko Native Audio

Yokko's production gameplay audio path is moving towards a native,
low-latency engine owned by `Yokko.Audio`.

Gameplay audio is owned by `Yokko.Audio`; it no longer uses the
osu!framework/BASS audio types. Compressed files are decoded on a managed
worker, while device output, PCM buffering, MMCSS scheduling and the playback
clock remain native-owned. WASAPI shared mode is the safe fallback when the
requested endpoint rejects exclusive mode.

## Real-time invariants

The output callback must:

- perform no dynamic allocation;
- acquire no blocking lock;
- perform no file or network I/O;
- emit no logs;
- never call managed code;
- fill missing PCM with silence and increment an underrun counter;
- replace non-finite samples with silence and clamp float PCM to `[-1, 1]`.

Stop/reset is coordinated without taking a real-time lock: the control thread
first prevents new submissions and publishes the idle state, then waits for
already-running producer/callback calls to leave before resetting the ring.

Decoded interleaved float PCM is transferred from a worker thread to the output
callback through a preallocated single-producer/single-consumer ring buffer.

## Clock truth

The playback clock is based on the frame that the output endpoint reports as
currently presented:

```text
playback milliseconds = presented frames * 1000 / sample rate
```

WASAPI's `IAudioClock::GetPosition` already identifies the sample currently
playing through the speakers, so `GetStreamLatency` is exposed as telemetry and
is not subtracted a second time. The correlated QPC timestamp returned by
`GetPosition` is used to interpolate between device callbacks, then clamped to
frames already submitted to the endpoint. This avoids a buffer-sized staircase
in the gameplay clock while preserving a monotonic result.

The callback frame count is only a provisional monotonic clock until a backend
reports a hardware position. Gameplay uses one clock for the whole session and
must not fall back to a renderer clock after playback starts.

## Native ABI

`Yokko.Audio.Native/include/yokko_audio.h` is the stable C boundary between the
.NET application and the native engine. ABI structs start with `struct_size`
and status reports the ABI version so incompatible binaries fail explicitly.

The P0 ABI covers:

- engine creation and destruction;
- deterministic idle, primed, running and paused states;
- pre-roll enforcement before playback can start;
- interleaved float PCM submission and callback rendering;
- presented-position/QPC correlation and output-latency reporting;
- buffer, clock, underrun and callback-deadline telemetry.

The audio callback entry point exists for native output backends. It is not a
managed callback and must not be called from the gameplay update thread.

## Backend state

Implemented:

1. Event-driven WASAPI Exclusive and shared output.
2. Native endpoint enumeration and endpoint-id selection.
3. `Pro Audio` MMCSS registration on the device thread.
4. Float/PCM32/24-in-32/PCM16 format negotiation without callback allocation.
5. `IAudioClock` position and `GetStreamLatency` reporting.
6. Worker-thread MP3/WAV/OGG decoding into the native PCM ring.
7. Gameplay and editor waveform paths independent of osu!framework audio.
8. Per-stream callback budget, maximum callback duration and deadline-miss
   counters.

Exclusive event-driven output submits one complete endpoint buffer on every
device event. Shared mode alone uses `GetCurrentPadding` to calculate writable
frames; sharing that path with Exclusive can leave the stream waiting forever
after its first event. A runtime callback failure now marks the core `Faulted`
and publishes the backend HRESULT/stage instead of leaving a false-running
state.

Next:

1. Validate 64/128/256-frame profiles per real device and persist the smallest
   stable profile.
2. Add ASIO behind the same engine state, clock and telemetry contract.
3. Add long-running underrun, hot-unplug, seek and device-loss tests.

## Focused validation

Run the native core tests with:

```powershell
.\scripts\test-native-audio.ps1
```

Native unit tests validate priming, lifecycle, silence-on-underrun, sample
safety, counter resets, presented-position clock behavior and callback deadline
telemetry. The focused
managed hardware smoke decodes a silent WAV, enumerates active endpoints, opens
real WASAPI output and verifies the accepted device buffer, latency and live
callback timing.

On the July 28 development machine, the smoke opened `Senary Audio` in
exclusive mode at 48 kHz with a 240-frame buffer and a reported 5.00 ms stream
latency. This is device-specific evidence, not a universal latency guarantee.
