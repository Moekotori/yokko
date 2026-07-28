# Yokko Native Audio

Yokko's production gameplay audio path is moving towards a native,
low-latency engine owned by `Yokko.Audio`.

The existing osu!framework/BASS backend remains the safe fallback while the
native engine is built and validated. Gameplay must not switch to the native
path until the selected output backend has successfully opened, primed its
buffer, and exposed a monotonic device-backed clock.

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

The playback clock is based on frames observed by the output device, corrected
by the backend-reported device latency:

```text
presented frames = device frame position - device latency frames
playback milliseconds = presented frames * 1000 / sample rate
```

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
- hardware position and device-latency reporting;
- buffer, clock and underrun telemetry.

The audio callback entry point exists for native output backends. It is not a
managed callback and must not be called from the gameplay update thread.

## Backend sequence

1. Implement event-driven WASAPI Exclusive output and endpoint enumeration.
2. Report the accepted sample rate, buffer frames, device latency and hardware
   clock rather than echoing requested values.
3. Validate 64/128/256-frame profiles on real devices and choose the smallest
   stable profile.
4. Wire decoded chart audio into the native ring buffer.
5. Add ASIO behind the same engine state, clock and telemetry contract.
6. Switch gameplay only after focused native, integration and real-device
   tests pass.

## Focused validation

Run the native core tests with:

```powershell
.\scripts\test-native-audio.ps1
```

Native unit tests validate priming, lifecycle, silence-on-underrun, sample
safety, counter resets and latency-adjusted device clock behavior. Real
WASAPI/ASIO validation is a separate required gate once those backends exist.
