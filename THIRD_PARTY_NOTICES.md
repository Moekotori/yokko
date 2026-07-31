# Third-party notices

## osu! and osu!framework

Yokko uses osu!framework and ports selected osu!mania legacy skinning,
gameplay, input-layout, and ruleset semantics from osu!.

- Copyright (c) ppy Pty Ltd
- Licence: MIT
- Source: <https://github.com/ppy/osu>
- Framework source: <https://github.com/ppy/osu-framework>

## SoundTouch.NET

Yokko uses `SoundTouch.Net` through
`SoundTouch.Net.NAudioSupport.Core` 2.3.2 for playback-rate and
pitch-independent tempo processing.

- Copyright (c) Olli Parviainen
- C# port Copyright (c) Olaf Woudenberg
- Licence: GNU Lesser General Public License, version 2.1 or later
- Source: <https://github.com/owoudenberg/soundtouch.net>
- Package: <https://www.nuget.org/packages/SoundTouch.Net/2.3.2>

The packages retain their own licence metadata and are distributed as separate
assemblies.

## Etterna

Yokko's Etterna compatibility includes the timing-window constants,
Judge/Justice scales, inclusive boundary checks, closest-note selection,
Wife3 scoring, combo thresholds, default life/mine/hold semantics, and the
official MinaCalc v515 MSD implementation. StepMania roll heads are preserved
as rolls and use Etterna's fixed roll-life drain/refill behaviour.

MinaCalc source is vendored under
`Yokko.MinaCalc.Native/vendor/etterna/src/Etterna/MinaCalc`. Yokko adds a small
native C ABI adapter and one standalone-build guard around Etterna's runtime XML
parameter loader; MinaCalc's default parameters and calculation are unchanged.

- Copyright (c) 2016-2023 Etterna <etternadev@gmail.com>
- Licence: MIT
- Source: <https://github.com/etternagame/etterna>
- Reference commits: `939a26ae042d3a689999a0dae630721c7701f187`,
  `b65660062ef2a23121e331c36e23c23a8f6eafaa`
- MinaCalc version: `515`

## StepMania

Yokko's StepMania mine behaviour follows the upstream mine timing window,
held-lane activation, and default life-meter penalty.

- Copyright (c) Chris Danford, the StepMania development team, et al.
- Licence: StepMania permissive licence (`Docs/Licenses.txt`)
- Source: <https://github.com/stepmania/stepmania>
- Reference commit: `21bb8dcd6c7e3782f23d5f4e01b6ee4c82cccc71`
