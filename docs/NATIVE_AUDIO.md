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
- ABI v11 exposure of that raw correlation to the managed timestamped gameplay
  clock;
- buffer, clock, underrun, callback-work and callback-cadence telemetry.

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
8. Per-stream callback budget, maximum callback duration, late-event interval
   and separate work/cadence miss counters.
9. Optional output-only ASIO with passive 64-bit driver discovery, exact
   driver selection, driver-owned buffer negotiation and native callback
   conversion for common integer and floating-point sample formats.

ASIO is an explicit expert backend. A selected ASIO driver never silently
falls back to WASAPI: a missing, busy, incompatible or reset-requesting driver
faults the requested session so the UI and diagnostics preserve backend truth.
The callback remains entirely native and allocation-free. ASIO sample position
is converted to speaker-presentation position using the driver-reported output
latency before it enters Yokko's playback-clock contract.

## Optional ASIO build

Yokko does not vendor the Steinberg ASIO SDK. Steinberg currently offers the
SDK under GPLv3 or a separately signed proprietary agreement, so choosing and
complying with that license remains a distribution decision rather than an
implicit repository dependency.

After obtaining the SDK under terms appropriate for the product, point the
native build at its root:

```powershell
.\scripts\build-native-audio.ps1 `
    -Configuration Release `
    -AsioSdkDir 'C:\path\to\ASIOSDK'
```

The directory must contain `common/asio.h` and `common/iasiodrv.h`. Builds
without it keep the same ABI and return `BackendUnavailable` for ASIO, while
WASAPI remains fully functional. The compiled application must still be
distributed in accordance with the selected Steinberg license.

Exclusive event-driven output submits one complete endpoint buffer on every
device event. Shared mode alone uses `GetCurrentPadding` to calculate writable
frames; sharing that path with Exclusive can leave the stream waiting forever
after its first event. A runtime callback failure now marks the core `Faulted`
and publishes the backend HRESULT/stage instead of leaving a false-running
state.

## Settings truth

`YokkoAudioSettings` is the application-owned preference source shared by the
Audio settings panel and `GameplayScreen`. The values are persisted in
`yokko.ini` and converted into an `AudioEngineStartRequest` when playback
starts:

- WASAPI Exclusive or Shared output mode;
- Windows endpoint ID, with the current Windows default represented by an
  empty ID;
- requested 64/128/256/512-frame latency profile;
- global gameplay timing offset from -200 ms to +200 ms.

Opening the settings page only enumerates endpoints. It does not open or reserve
an output stream. Changes are saved immediately and apply to the next playback
session, so an active audio clock is never silently replaced mid-song.

Next:

1. Persist per-endpoint validation results and warn before selecting a driver
   whose Exclusive event cadence fails the rhythm-game gate.
2. Add ASIO behind the same engine state, clock and telemetry contract.
3. Add long-running underrun, hot-unplug, seek and device-loss tests.

## Focused validation

Run the native core tests with:

```powershell
.\scripts\test-native-audio.ps1
```

Run the opt-in real-device matrix with:

```powershell
.\scripts\test-audio-hardware.ps1 -StabilitySeconds 12 -DeviceId '<endpoint id>'
```

Native unit tests validate priming, lifecycle, silence-on-underrun, sample
safety, counter resets, presented-position clock behavior and callback deadline
telemetry. The focused
managed hardware smoke decodes a silent WAV, enumerates active endpoints, opens
real WASAPI output and verifies the accepted device buffer, latency and live
callback timing.

On the July 28 development machine, `FiiO KA13` passed Exclusive mode at
44.1/48/96 kHz. At 48 kHz the 64-frame request was aligned by the driver to
144 frames and `GetStreamLatency` reported 3.00 ms. A 12-second run delivered
4008 callbacks with no underrun, callback-work miss or late-event cadence miss;
the maximum callback work was 0.368 ms, the maximum event interval was
4.033 ms and device-clock drift against QPC was 0.848 ms.

The internal `Senary Audio` endpoint opened Exclusive at 48 kHz/240 frames and
reported 5.00 ms, but failed the stability gate: a 1.5-second formal run
recorded 72 late-event cadence misses, a 10.679 ms maximum interval and
439.688 ms of clock drift. The endpoint must not be described as
rhythm-game-safe merely because Exclusive mode opened successfully.

These are endpoint/driver-specific stream measurements. `GetStreamLatency`
does not measure the analogue DAC, amplifier or acoustic path; a physical
loopback rig is still required for total click-to-sound latency.
