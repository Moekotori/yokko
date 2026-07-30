# Yokko Latency Lab

Yokko latency claims must use an external electrical reference. Software clocks
are diagnostic evidence, not an end-to-end input-to-sound or input-to-photon
measurement.

## Audio qualification

Run the endpoint buffer probe before an end-to-end session:

```powershell
.\scripts\test-audio-hardware.ps1 `
    -StabilitySeconds 30 `
    -DeviceId '<endpoint id>'
```

The script probes 64, 128, 256 and 512 requested frames, records the accepted
device buffer and callback telemetry, then runs the lowest-latency accepted
profile through the stability gate. It writes a versioned JSON report under
`artifacts/latency` unless `-ReportPath` is supplied. A report is valid only
when the command exits successfully.

The recommendation is endpoint-specific. A requested 64-frame profile may be
aligned to a larger device period and must never be displayed or persisted as
an actual 64-frame result.

## End-to-end A/B fixture

Use a microcontroller which emits the following from the same hardware timer:

1. A USB HID keyboard edge delivered to the game.
2. A GPIO reference pulse recorded by an audio interface.

Record game audio on another input channel to measure input-to-sound. For
input-to-photon, place a photodiode over a dedicated full-white response patch
and record it together with the GPIO reference.

Each capture row must contain:

```text
client,client_version,run_id,event_id,display_hz,frame_mode,
audio_backend,device_id,accepted_buffer_frames,reference_ms,
audio_onset_ms,photon_onset_ms
```

Keep raw rows. Report P50, P95, P99 and maximum separately for
`audio_onset_ms - reference_ms` and `photon_onset_ms - reference_ms`.

Compare Yokko and osu!lazer on the same machine in three groups:

1. Identical frame limiter and audio endpoint.
2. Fresh-install defaults.
3. Best stable configuration available to each client.

Use at least 1,000 edges per client/configuration and repeat after a cold boot.
Do not combine different endpoints, refresh rates or accepted buffer sizes in
one percentile distribution.
