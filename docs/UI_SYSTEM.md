# Yokko UI system

Yokko UI is a lightweight presentation layer on top of osu!framework. It is
an internal development tool first: pages continue to own layout, state, and
interaction, while shared theme tokens and components own reusable appearance.

## Current foundation

- `YokkoUiTheme` groups semantic colour, typography, metric, and motion tokens.
- `YokkoUiThemeStore` is cached by `YokkoGameBase`; replacing `Current` updates
  migrated components without recreating their page.
- `YokkoButton` and `YokkoCard` are the first theme-aware components.
- `YokkoText`, `YokkoDivider`, `YokkoThemeBox`, and `YokkoToggleSwitch`
  cover theme-aware typography, simple fills, separators, and bound boolean
  state without taking page behaviour away from the owning screen.
- `TestSceneYokkoUiLab` is the component gallery in the Yokko test browser. It
  shows component states and can switch between the default and a preview theme.
- The existing home, settings, song-select, and dark palettes currently forward
  to the default semantic tokens. This preserves their appearance while pages
  are migrated incrementally.

All full-screen layouts continue to target the shared `1920x1080` reference
space through `YokkoUiScalingContainer`.

## Theme-file development

Theme JSON is a strict, versioned overlay on the built-in complete theme.
Omitted properties retain their defaults, unknown properties and invalid values
are rejected, and a failed reload leaves the last valid theme active. Colours
use `#RRGGBB` or `#RRGGBBAA`.

Start the Test Browser with `YOKKO_UI_THEME_FILE` pointing to a JSON file. The
file is watched with a short debounce and migrated components update without a
page restart. The watcher exists only in `Yokko.Game.Tests`; the parser and
validated theme store live in `Yokko.Game` for later skin-package integration.

An editable starting point is available at
`docs/samples/yokko-ui-theme.json`.

## Preview workflow

Use `scripts/ui-preview.ps1` instead of assembling preview environment
variables by hand. It opens a real `1920x1080` Test Browser window for a
selected surface, applies locale/UI-scale/theme overrides only to that child
run, and restores the caller's environment afterwards.

```powershell
# Open the component gallery and keep it interactive.
.\scripts\ui-preview.ps1 -Target Lab

# Exercise the six-active-Mod scrolling state in Chinese with a live theme file.
.\scripts\ui-preview.ps1 -Target Mods -ModsState DenseActive `
  -Locale zh -ThemeFile .\docs\samples\yokko-ui-theme.json

# Capture a real renderer frame and exit automatically.
.\scripts\ui-preview.ps1 -Target Settings -SettingsPage Display `
  -Screenshot .\artifacts\ui\settings-display.png
```

Supported targets currently include the UI Lab, Mods, main menu, Song Select,
Settings, results, pause, editor, and layout editor. A requested screenshot is
rejected when the renderer returns the `1x1` placeholder used by some headless
test environments, so a green command always corresponds to a usable PNG.

## Boundaries

Theme data may control colours, typography, surface metrics, motion timing, and
eventually named image or sound assets. It must not control page navigation,
gameplay rules, settings behaviour, or execute scripts. Dynamic text remains a
code-owned layer above theme assets.

Do not introduce a JSON page language, custom data-binding framework, or visual
layout editor as part of this layer. A page may always use osu!framework
directly when a shared component does not fit.

## Migration

Migrate components when a page is already being changed or when the component
is reused by multiple pages. Preserve public behaviour and visual state, then
add the relevant component state to the UI Lab. The initial editor migration
uses `YokkoButton` for toolbar actions and `YokkoCard` for the inspector panel.

Shared state machines do not all belong in the global Presentation namespace.
`GameplayModSettingsControls` is deliberately scoped to Song Select: it shares
step and ON/OFF behaviour across Fixed Rate, Adaptive Speed, and Muted while
leaving each Mod's range and rules in its owning page. A disabled state must be
an input boundary for mouse, keyboard, and programmatic activation, not merely
a lower alpha.

The Gameplay Mods catalogue uses a single predictable column of `390x60`
cards. Family cycling, focus, and active-state feedback stay code-owned; orbit
connectors, scanners, and continuous decorative motion are intentionally not
part of the browsing pattern. The layout must retain room for all seven
conversion families without clipping.

Future user-skin work should load a validated theme definition into
`YokkoUiThemeStore`, resolve missing values and assets through the built-in
default theme, and keep arbitrary code and unrestricted layout changes out of
the package format.
