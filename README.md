# Yokko

Yokko is an osu!framework rhythm game prototype focused on 4K/7K play, precise timing, smooth rendering, low-latency audio backends, and chart/skin import compatibility.

## Current Shape

- `Yokko.Desktop` launches the Windows/Linux/macOS desktop host.
- `Yokko.Game` owns screens, visuals, and player-facing flow.
- `Yokko.Core` owns timing, judgement, and internal chart models.
- `Yokko.Audio` defines the replaceable audio engine boundary for shared WASAPI, WASAPI exclusive, and ASIO.
- `Yokko.Import` defines chart importer contracts for osu!mania, Malody, BMS, and LR2-oriented BMS libraries.

Yokko now starts in a lightweight editor workspace. The first editor pass supports new 4K/7K draft charts, click-to-toggle grid notes, live note/length stats, osu!mania `.osu` import/export, and playtesting the current draft chart in the existing gameplay screen. 4K playtest uses `D F J K`; 7K uses `S D F Space J K L`.

Export currently writes to `Documents\Yokko Exports`. Import/export support covers osu!mania tap notes and long notes for 4K/7K charts.

## Development

```powershell
dotnet restore .\Yokko.Desktop.slnf
dotnet build .\Yokko.Desktop.slnf
dotnet run --project .\Yokko.Desktop\Yokko.Desktop.csproj
```

Use `Yokko.Desktop.slnf` for fast desktop iteration. The full solution also contains the template iOS project for later platform work.

## Architecture Direction

Gameplay judgement must be driven by audio time and input timestamps, not frame time. The render loop can run as fast as the machine allows, while judgement uses the audio clock plus device/user offsets.

The first audio backend target is stable shared playback. WASAPI exclusive and ASIO should be added behind `Yokko.Audio` once the gameplay timing path is measurable and repeatable.
