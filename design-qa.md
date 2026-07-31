> Current layout policy (2026-07-31): all new full-screen UI and visual QA use
> the shared 1920 x 1080 reference. Earlier 1280 x 720 and 1600 x 900 entries in
> this file are historical evidence or explicitly retained internal artboards,
> not the current application-wide baseline.

# Pause overlay dynamic-content verification (2026-07-29, QA5)

## Evidence

- User-reported source state:
  `C:\Users\nyafa\AppData\Local\Temp\codex-clipboard-4de8f446-73e4-4c16-91fd-bffef68720a6.png`
- Source normalized from 2048 x 1152 to 1366 x 768:
  `D:\YOKKO\.artifacts\pause-ui-qa5\before-user-1366x768.png`
- Revised implementation, matching Comfortable 90% state:
  `D:\YOKKO\.artifacts\pause-ui-qa5\comfortable-start.png`
- Scrolled-title state:
  `D:\YOKKO\.artifacts\pause-ui-qa5\comfortable-scrolled.png`
- Full-view comparison, reported state left / revised state right:
  `D:\YOKKO\.artifacts\pause-ui-qa5\matched-before-left-after-right.png`
- Focused comparisons, reported state left / revised state right:
  - Song header:
    `D:\YOKKO\.artifacts\pause-ui-qa5\focused-header-before-left-after-right.png`
  - Accuracy, rank, score, and combo:
    `D:\YOKKO\.artifacts\pause-ui-qa5\focused-metrics-before-left-after-right.png`
- Responsive captures:
  - Large:
    `D:\YOKKO\.artifacts\pause-ui-qa5\large-final.png`
  - Comfortable:
    `D:\YOKKO\.artifacts\pause-ui-qa5\comfortable-start.png`
  - Compact:
    `D:\YOKKO\.artifacts\pause-ui-qa5\compact-final.png`

## Viewport and state

- Source pixels: 2048 x 1152.
- Implementation pixels: 1366 x 768.
- Density normalization: source downsampled to 1366 x 768 before comparison.
- UI scale: Comfortable 90%, matching the user's captured sheet bounds.
- Song: Eternal Ending (aran Remix) / Kobaryo / 4K / AT /
  00:05 of 04:28.
- Performance: 100.00%, rank SS, score 11,342, combo 41 / 41,
  judgments 41 / 0 / 0 / 0 / 0 / 0.

## Comparison history

1. Reported state
   - P1: the fixed percentage position overlapped the final accuracy digits.
   - P1: the two-letter rank overflowed the circular rank stamp.
   - P2: the fixed combo suffix position overlapped a two-digit combo.
   - P2: the truncated song title ended too close to the mode block and could
     not reveal the full title.
2. Fixes
   - Accuracy and combo now use content-sized horizontal flows.
   - Rank typography scales for one-, two-, and longer rank strings.
   - The title uses a 235-unit masked lane with a ping-pong marquee and a
     larger protected gap before the mode block.
3. Post-fix evidence
   - `100.00 %`, `SS`, and `41 / 41` remain fully legible with no collisions.
   - The two title captures show the same text at different horizontal
     positions, confirming that the full title scrolls through the lane.
   - Large, Comfortable, and Compact preserve all controls and data.

## Required fidelity surfaces

- Fonts and typography: production Yokko fonts retained; dynamic values scale
  without changing their visual hierarchy.
- Spacing and layout rhythm: the title lane and mode block have a protected
  gap; score, combo, and rank remain aligned to the existing grid.
- Colors and tokens: existing navy, cyan, ivory, and judgment colors retained.
- Image quality and assets: existing logo, mascot, music icon, and decorative
  assets are unchanged and remain sharp.
- Copy and content: the full song title is preserved by scrolling instead of
  destructive truncation.

## Verification

- Build: passed with 0 warnings and 0 errors.
- Focused pause/resume test:
  `TestPauseOverlayStopsAndResumesAudio`
  - Passed: 1 / 1.

## Findings

- No remaining actionable P0, P1, or P2 findings.

final result: passed

---

# Song Select 1920 x 1080 layout pass QA (2026-07-31)

## Evidence

- Selected layout reference: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png`.
- Native Direct3D 11 implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\yokko-song-select-layout-final.png` (1920 x 1080).
- Same-viewport comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-comparison-final.png`.
- State: English Song Select, 7K filter active, two expanded packages, one collapsed package, long selected title, global ranking visible.

## Required fidelity review

- Typography and hierarchy: the selected song, ranking scores, package names, difficulty pills, and Play action retain clear priority using Yokko's existing display fonts.
- Major layout: the screen now follows the reference's navy global navigation, left details and ranking rail, right search/filter/browser rail, and ivory bottom action bar on the shared 1920 x 1080 stage.
- Spacing: the right-side search, star range, sort/group/collection controls, package headers, and difficulty rows remain separated with no clipping. The footer controls have distinct hit areas and no overlap.
- Colors and assets: the existing Yokko logo, mascot, avatars, sticker textures, chart artwork, navy, cyan, pink, yellow, and ivory palette are retained. No science-fiction visual treatment was introduced.
- Interaction: search, key filters, Sort, Group, Random, Mods, Back, and Play retain production actions. Sort and Group now also update their visible values after activation. Show Converts, Collection, and Options are intentionally visual-only in this layout pass.

## Findings

- P0: none.
- P1: none after separating the star-range and browse-control rows and widening the footer tools.
- P2: none after correcting the Collection label/value collision.
- P3: the implementation retains production beatmap art and the current details-card component; the reference's hand-drawn integrated metadata treatment and denser decorative stickers are deferred to a later polish pass.

## Verification

- `Yokko.Game.Tests` isolated build: passed with 0 warnings and 0 errors.
- Native Direct3D 11 preview captured at 1920 x 1080 with a null Yokko preview-audio engine.
- Reference and implementation were inspected in one combined comparison image.
- `git diff --check` passed for the touched Song Select and visual-preview files.

final result: passed

---

# Home signal-snake background toy QA (2026-07-31)

## Evidence

- Selected visual target: `C:\Users\nyafa\.codex\generated_images\019fb866-37b8-77a3-a853-c6c642e2fbf4\exec-438752d2-13d7-4ab0-87b9-bc78ec6b1213.png`.
- Native Direct3D 11 implementation after arrow-key input: `D:\YOKKO\artifacts\signal-snake-arrow-active-1920x1080.png` (1920 x 1080).
- Same-viewport comparison, selected target left and implementation right: `D:\YOKKO\artifacts\signal-snake-mock-vs-arrow-active.png`.
- State: Chinese home screen, wide layout, signal snake available; implementation captured after an Up-arrow input.
- Density normalization: the 2048 x 1152 target was resampled to 1920 x 1080 without cropping; the implementation remained at native 1x output.

## Comparison history

1. The selected target established a tiny route-and-pip toy embedded in the cyan background, without a card, tutorial copy, or mascot interaction.
2. The production component is slightly smaller and more transparent than the target so it reads as ambient motion rather than another primary UI region.
3. The final input path accepts both D/F/J/K and arrow keys. Compact layouts hide the toy and return Left/Right to the existing music shortcuts.

## Required fidelity review

- Fonts and typography: no new copy or typography was introduced; all existing home typography remains unchanged.
- Spacing and layout: the toy occupies unused centre-left cyan space and does not overlap the main navigation, speech sticker, key-test pad, music player, or mascot.
- Colors and tokens: cyan-white route points, yellow/pink pips, and the navy/cyan/pink arrow reuse the existing home palette at low opacity.
- Image quality and assets: the mascot and all existing artwork are unchanged; the tiny signal geometry remains sharp at native 1920 x 1080.
- Copy and content: no HUD, score card, instructional label, or extra chrome was added.
- Interaction: collecting pips grows the trail; leaving bounds or crossing the trail resets it with a small burst; key repeats are consumed without unintended rapid movement.

## Findings

- P0: none.
- P1: none.
- P2: none in the final same-viewport comparison.
- P3: the production trail is deliberately subtler than the selected target, matching the request to keep the UI change small.

## Verification

- Clean isolated build of the exact three-file feature patch: passed.
- Focused `TestSceneMainScreen` suite: 11 passed, 0 failed, including D/F/J/K and arrow-key signal movement.
- Native Direct3D 11 preview captured and compared at 1920 x 1080 after actual arrow-key input.
- `git diff --check`: passed for all feature files.

final result: passed

---

# Gameplay pause interaction and sharpness QA (2026-07-31)

## Evidence

- Source visual truth: `C:\Users\nyafa\AppData\Local\Temp\codex-clipboard-4b8b466d-0fd1-40a5-b75a-d919db463417.png` (2559 x 1439).
- Native Direct3D 11 implementation: `D:\YOKKO\artifacts\pause-interactive-preview\implementation-final.png` (1920 x 1080).
- Full-view comparison, source left and implementation right: `D:\YOKKO\artifacts\pause-interactive-preview\source-left-implementation-right.png`.
- Focused performance comparison, source left and implementation right: `D:\YOKKO\artifacts\pause-interactive-preview\metrics-source-left-implementation-right.png`.
- Viewport/state: 1920 x 1080, Comfortable UI scale, Chinese pause overlay, long-title fixture, 100.00% accuracy, SS rank, score 11,342, combo 41 / 41, pauses 03.
- Density normalization: the 2559 x 1439 source was resampled to 1920 x 1080 without cropping; the implementation remained at native 1x output.

## Comparison history

1. The prior performance rail was readable but static, and the smallest muted labels were too close to decorative contrast.
2. Accuracy used a late runtime scale when the horizontal flow exceeded its width, which could soften the largest value even though the selected font was correct.
3. Accuracy, rank, score, combo, and pause count now expose hover feedback and click-to-pin states. Active values shift to cyan, receive a pale-cyan focus field and stronger underline, and retain a compact `PINNED` state after click.
4. Accuracy now renders directly at a natural integer display size with no runtime scale. Muted and faint navy text gained contrast, while the crisp `SmoothPath` centre divider and the verified transparent logo remain unchanged.
5. The first interaction capture placed `CLICK TO PIN` too close to the judgment heading. The final pass moves metric state labels into each cell's top-right corner and places one global discoverability hint above the PAUSES cell.

## Required fidelity review

- Fonts and typography: Roboto Bold display text remains at natural proportions; accuracy no longer receives a fractional scale; small labels have stronger contrast without becoming primary content.
- Spacing and layout rhythm: the five interactive metric hit targets do not cross the column rules, mascot, judgment heading, or song header. Interaction labels occupy existing negative space.
- Colors and tokens: hover, pinned, rule, and focus states use the existing Yokko navy, cyan, pale-cyan, pink, and ivory palette.
- Image quality and assets: the real transparent Yokko logo and mascot remain sharp. No raster asset was replaced by code-native approximation; the centre line remains a vector path.
- Copy and content: all live score data, judgment values, pause count, song metadata, and pause actions remain present. The new English microcopy matches the screen's existing technical-label language.
- Interaction and accessibility: five large metric regions provide hover and click feedback; the state does not move data or reduce the existing keyboard navigation target area.

## Findings

- P0: none.
- P1: none.
- P2: none after moving the per-cell interaction labels away from the judgment heading.
- P3: the overlay retains its documented legacy 1600 x 900 internal artboard and is uniformly fitted to the shared 1920 x 1080 viewport.

## Verification

- `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors.
- Native Direct3D 11 preview: captured successfully at 1920 x 1080.
- Full-view and focused source/implementation comparisons were opened in combined images.
- Primary interaction implementation checked: hover highlight, click-to-pin toggle, pinned visual retention, and click-again release for accuracy, rank, score, combo, and pauses.

final result: passed

---

# Gameplay settings typography QA (2026-07-31)

## Evidence

- Source visual truth: `C:\Users\nyafa\AppData\Local\Temp\codex-clipboard-16d82cfe-9a81-46f1-9824-673301517d57.png` (2559 x 1439).
- Revised native implementation: `C:\Users\nyafa\AppData\Local\Temp\yokko-gameplay-typography-after.png` (1920 x 1080, Comfortable UI scale).
- Additional timing-section capture: `C:\Users\nyafa\AppData\Local\Temp\yokko-gameplay-typography-timing.png` (1920 x 1080).
- State: Chinese Gameplay settings, Input section, 4K profile. Key values differ because the captures used different persisted bindings; this is not a typography finding.

## Comparison history

1. The reported screen mixed 9, 13, 14, 15, and 16-unit text across controls with equivalent importance. Tool actions and lane-card descriptions were visually subordinate to the large empty surfaces that contained them.
2. The hierarchy was consolidated to 20-24 for section/status titles, 16-18 for actions, and 15-17 for supporting copy. Four-key lane labels and actions now use 16, while key values use 30.
3. The first timing capture showed the scroll-mode chip still lagging behind the revised hierarchy. Its text was raised from 11 to 13 and its container from 20 to 22 logical pixels.

## Fidelity review

- Fonts and typography: existing Yokko display/body families and weights remain; sizes now form a consistent three-level hierarchy with no visible truncation.
- Spacing and layout: card, tab, and button bounds are unchanged; enlarged text remains centred and does not collide with icons or borders.
- Colors and tokens: unchanged.
- Image quality and assets: logo, icons, sticker frame, and decorations are unchanged and remain sharp.
- Copy and content: unchanged apart from persisted key bindings in the verification fixture.

## Findings

- P0: none.
- P1: none after removing the inconsistent small-type tiers.
- P2: none in the Input and Timing native captures.
- P3: dense multi-row key modes retain smaller 10-11-unit support labels so 12+ lanes continue to fit; their primary key values remain 19.

## Verification

- Current-tree `Yokko.Game` isolated build: 0 warnings, 0 errors.
- Clean isolated `Yokko.Game.Tests` build: 0 warnings, 0 errors.
- Focused settings typography and gameplay interaction tests: 2 passed, 0 failed.
- Before/after Input captures were opened in one comparison input; the Timing capture was inspected separately for overflow.

final result: passed

---

# Song Select redesign verification (2026-07-31)

## Evidence

- Source visual truth: `C:\Users\mochi\.codex\generated_images\019fb5dd-52c5-7062-91bc-86e305437bdc\call_bVGxWmV4UrXTKxwGw3LXOidW.png`
- Native implementation: `D:\yokko\artifacts\song-select-redesign\implementation-06.png`
- Full comparison, source left and implementation right: `D:\yokko\artifacts\song-select-redesign\comparison-final.png`
- Focused song-browser comparison: `D:\yokko\artifacts\song-select-redesign\comparison-right-final.png`
- Play transition smoke: `D:\yokko\artifacts\song-select-redesign\play-transition-smoke.png`

## Viewport and state

- Source and implementation pixels: 1600 x 1000.
- CSS/stage size: 1600 x 1000; density normalization was not required.
- State: English Song Select, 7K filter active, first and second packs expanded, third pack collapsed, long title selected, global ranking visible.
- The screenshot fixture intentionally has no playable notes, so difficulty values render as `--`; production imported charts continue to display their computed numeric difficulty in the retained left-side rating block.

## Comparison history

1. First native pass
   - P1: chart artwork overpowered the controls on bright backgrounds.
   - P1: the blue logo disappeared into the background and the search/filter palette was inverted from the selected target.
   - P2: the song card, ranking rows, footer and pack headers were too compressed.
   - P2: long package titles truncated to one line.
2. Second pass
   - Increased the constant navy isolation layer, switched to the real white logo asset, darkened the search/filter surfaces, and matched the target's major-region proportions.
   - Expanded the selected chart card, added reliable two-line title layout, seven relaxed score ribbons with avatars, 120px art-backed pack headers, and a taller footer.
3. User-reported row-layout pass
   - P1: compact difficulty rows duplicated the rating at left and lower-right, causing `STAR 7.05` to collide with the mode pill and disappear on an ivory selected row.
   - Removed the duplicate compact-row rating. The left numeric rating block remains the single difficulty source; standalone rows retain one adaptive rating label.
4. Post-fix evidence
   - The implementation has no clipped controls, duplicate difficulty labels, or long-title collisions.
   - The automated Play smoke crossed Song Select, Gameplay and the empty-fixture result path without a deadlock; gameplay construction now runs off the UI update thread while preview audio stops.

## Required fidelity surfaces

- Fonts and typography: Yokko display/body fonts preserve the selected target's hierarchy; selected-song and package titles use controlled two-line layout, while difficulty rows truncate safely.
- Spacing and layout rhythm: 32px left frame, 15px right frame, 793px detail/ranking rail, 730px browser rail, seven 56px score rows, and the 146px footer match the relaxed composition without clipping.
- Colors and tokens: navy isolation, cyan outlines, pink difficulty pills, yellow primary action, and ivory selected surfaces remain stable across arbitrary beatmap backgrounds.
- Image quality and assets: the real Yokko logo, mascot, avatars, tape decorations, and actual beatmap artwork are loaded as raster assets; dynamic text and interaction state remain code-owned.
- Copy and content: long song and package names remain readable; numeric difficulty is retained once per row; account, ranking, mode, mapper, score and chart metadata remain present.

## Findings

- P0: none.
- P1: none after the duplicate-rating and Play-transition fixes.
- P2: none after the same-viewport comparison.
- P3: generated reference art is richer than the deterministic preview fixtures; production intentionally uses each beatmap's own artwork instead of shipping reference-specific illustrations.

## Verification

- `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors.
- Native Direct3D 11 Song Select capture: passed at 1600 x 1000.
- Automated Play transition smoke: passed; Song Select entered Gameplay in the same logged second.
- The broad visual-test filter was stopped after exceeding the focused time budget; it was not used as pass evidence.

final result: passed

---

# Gameplay Mods overall polish QA (2026-07-31)

## Source visual truth

- Baseline native implementation:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb72c-4c90-73c0-8b44-35807879ec2f\mods-footer-option1-compact-grouped-final.png`
- Polished native implementation:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb72c-4c90-73c0-8b44-35807879ec2f\mods-overall-polish-pass1.png`
- Full-view comparison:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb72c-4c90-73c0-8b44-35807879ec2f\mods-overall-polish-comparison-full.png`
- Focused orbit and control-panel comparison:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb72c-4c90-73c0-8b44-35807879ec2f\mods-overall-polish-comparison-details.png`

## Viewport and state

- Both captures are native Direct3D 11 frames at 1600 x 1000 and 1x density.
- State: Chinese locale, Difficulty Reduction page, Half Time focused,
  Half Time and Hidden active, playback rate 0.75x.
- No crop or density normalization was required for the full-view comparison.
- Focused crops use the same pixel bounds in both captures: orbit
  x=330..1145/y=145..790 and right panel x=1145..1600/y=145..790.

## Findings and comparison history

1. [P2 fixed] The baseline right panel claimed five available Mod slots but
   rendered only four rows. The polished version renders all five slots in the
   available height using a 48px row and 53px rhythm.
2. [P2 fixed] Repeated dashed containers made the empty state visually louder
   than active Mods. Empty slots now use one quiet guide line, a centred plus,
   and the same full-row hit target.
3. [P2 fixed] Tiny headings and telemetry competed with the orbit rather than
   supporting it. Section labels, rate endpoints, node labels, and meaningful
   telemetry were increased while nonessential rings, ticks, texture, and
   ambient decoration were reduced.
4. [P2 fixed] The header logo and central acronym dominated the page. Both were
   reduced slightly, while the page title, focused Mod name, and primary rate
   value gained clearer typographic hierarchy.
5. [P3 retained] Small technical labels remain intentionally secondary. They
   are decorative status texture and do not carry required interaction copy.

## Required fidelity surfaces

- Typography: key headings and values are larger and more consistent; body
  copy remains readable without competing with the selected Mod acronym.
- Spacing and layout: the three-column structure is unchanged. The right rail
  now uses a consistent five-row system and the footer retains its compact
  grouped actions.
- Colors and tokens: only Yokko's existing ivory, navy, cyan, pink, yellow,
  and pale-cyan tokens are used.
- Image quality and assets: the existing logo, paper texture, and waveform
  assets remain native and unscaled beyond their intended bounds. No generated
  placeholder or custom SVG asset was introduced.
- Copy and content: all localized player-facing copy is preserved. Technical
  English remains decorative and subordinate.
- Interaction and accessibility: active rows, remove controls, empty slots,
  rate presets, slider, category rail, reset, back, and done retain their live
  actions and pointer targets. Focused tests cover their core behaviors.

## Verification

- `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors.
- Focused Gameplay Mods tests: 17 passed, 0 failed.
- Native Direct3D 11 preview: captured successfully at 1600 x 1000.
- Full and focused before/after comparisons contain no remaining actionable
  P0, P1, or P2 issue.

final result: passed

---

# Gameplay layout resize editor verification (2026-07-31)

## Evidence

- Source visual truth:
  `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-a5c5267a-a0ea-4053-af5a-ffacf969554c.png`
- Native Direct3D 11 implementation:
  `D:\yokko\artifacts\gameplay-layout-editor\layout-editor-resize-implementation.png`
- Full-view comparison, source left and implementation right:
  `D:\yokko\artifacts\gameplay-layout-editor\source-left-implementation-right.png`
- Focused timing-bar comparison, source left and implementation right:
  `D:\yokko\artifacts\gameplay-layout-editor\timing-bar-source-left-implementation-right.png`

## Viewport, density, and state

- Source: 3200 x 2000 at 2x density.
- Implementation: 1600 x 1000 at 1x density.
- The source was downsampled to 1600 x 1000 without cropping before the
  full-view comparison.
- State: paused four-key gameplay with layout editor active, Chinese locale,
  timing bar moved upward/right and resized from its bottom-right handle.
- Primary interactions checked: pause-menu entry, three draggable targets,
  24 resize handles, timing-bar move/resize, reset, save/back, and 16:9
  full-page overview synchronization.

## Comparison history

1. First native capture
   - P1: the HUD top handles occupied the same region as reset/save.
   - Fix: moved editor actions into a dedicated left-side toolbar region and
     raised its input layer.
2. Second native capture
   - P1: the full-width toolbar obscured the playfield's top resize handles
     when the playfield touched the viewport edge.
   - Fix: reduced the toolbar to the action buttons only, leaving the rest of
     the top edge available for direct manipulation.
3. Final native capture
   - All playfield, HUD, and timing-bar edge/corner handles are visible and
     separable.
   - The timing bar remains readable while moved and non-uniformly resized.
   - The full-page overview reflects the transformed timing-bar bounds.

## Required fidelity surfaces

- Fonts and typography: existing Yokko gameplay fonts and weights are
  preserved; editor labels use the established compact UI type treatment.
- Spacing and layout rhythm: target frames track the exact rendered bounds;
  action buttons, target handles, covers, and preview no longer overlap.
- Colors and visual tokens: existing cyan selection, lime action, navy
  background, and gameplay judgment-window colors are retained.
- Image quality and assets: no raster assets were replaced or approximated;
  the change uses standard editor geometry over the real native gameplay.
- Copy and content: controls are Chinese and explicitly describe drag/edge
  resize behavior; reset and save/back remain directly clickable.

## Findings

- P0: none.
- P1: none after the two fixes above.
- P2: none.
- P3: the compact labels are intentionally subtle so they do not cover notes
  or timing information during layout work.

## Verification

- Focused tests: 24 passed, 0 failed.
- Isolated build: passed.
- Shared-worktree build was temporarily blocked by unrelated concurrent
  SongSelect edits, so final verification used the current HEAD in an
  isolated worktree with only the layout-editor files overlaid.

final result: passed

# Yokko Song Select whole-page polish QA (2026-07-31)

## Evidence

- Selected visual direction:
  `D:\yokko\docs\design\song-select\yokko-song-select-final-cute-ranking-account.png`
- Final native implementation:
  `D:\yokko\artifacts\song-select\song-select-rework-qa-02.png`
- Same-input full-view comparison:
  `D:\yokko\artifacts\song-select\song-select-target-vs-qa-02.png`

## Viewport, density, and state

- Source pixels: 1672 x 941.
- Implementation pixels: 1600 x 1000, native Direct3D 11 capture.
- The comparison fits the source into a 1600 x 1000 cell without cropping and
  places the native implementation beside it at 1:1.
- State: `Cold Sweat`, 4K PACK, global ranking, six visible score records,
  account summary present, Mods closed, song library scrolled near the selected
  chart.
- The beatmap artwork is intentionally different from the source. Production
  always uses the selected beatmap background and applies code-native
  readability isolation instead of relying on one authored waterfall image.

## Comparison history

1. The previous implementation left the song information and ranking cluster
   too close to the page centre. The final pass moved the shared details host
   left while preserving the stagger between the smaller song card and wider
   ranking table.
2. Search and filter controls previously had weak inactive contrast and a
   cramped escape hint. Their ivory surfaces, navy copy, radii, spacing, and
   hover treatment now match the rest of the screen.
3. The right library could lose separation on arbitrary bright or detailed
   beatmap art. A restrained navy isolation gradient now sits behind the
   library only; individual rows remain code-native ivory surfaces.
4. Ranking values were visually lighter than the player names and grades.
   Score, accuracy, and combo now use a compact bold display treatment while
   keeping six avatar rows visible.
5. The Mods marker is now the real transparent Yokko diamond asset. Stable
   panels, rows, dividers, and dynamic text remain code-drawn rather than
   flattened raster cutouts.

## Final review

- Typography and long content: title, artist, mapper, difficulty, stats, pack
  headers, and library rows retain a clear hierarchy. Long titles use the
  existing bounded multi-line treatment and no visible text overlaps the song
  card or library metadata.
- Spacing and layout: the left cluster now begins close to the logo edge, the
  ranking card is aligned underneath it, and the dense library has a distinct
  right rail. Header, content, and footer keep separate visual bands.
- Colors and surfaces: ivory, navy, cyan, pink, and yellow remain the shared
  Yokko palette. The background isolation is deliberately low-opacity, so
  beatmap artwork remains recognizable without controlling component colors.
- Image quality: logo, avatars, mascot, tape, star, and diamond are real
  textures. Dynamic panels and interactive states are not screenshots or
  whole-panel cutouts.
- Information density: six leaderboard entries with avatars and six scoring
  columns remain visible, the account card exposes PP, accuracy, global rank,
  level, mode, and online state, and the compact song library preserves more
  simultaneous charts than the concept.
- Interactions and states: search, filters, ranking tabs, chart selection,
  Mods, Back, and Play remain separate code-native controls. Selected ranking
  and song states have distinct pink/yellow emphasis.

## Findings

- P0: none.
- P1: none.
- P2: none.
- P3: the selected concept uses a bright waterfall background and larger
  library rows. Production intentionally uses arbitrary beatmap artwork and
  denser fixed-height rows to satisfy the background-agnostic and
  more-information requirements.

## Verification

- `dotnet build Yokko.Desktop.slnf --no-restore`: passed with 0 warnings and
  0 errors.
- Focused Song Select score-store, long-text layout, and artwork-policy tests:
  14 passed, 0 failed.
- Final native preview was captured with Yokko foregrounded and without
  keyboard or pointer input.

final result: passed

# Song Select background-agnostic redesign QA

## Evidence

- Selected visual:
  `D:\yokko\docs\design\song-select\yokko-song-select-final-cute-ranking-account.png`
- Current implementation capture supplied from the native Yokko renderer:
  `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-4a58e8ad-fa8a-406d-9cea-062455c896e9.png`
- Normalized implementation evidence:
  `D:\yokko\artifacts\song-select\implementation-current-1600x900.png`
- Same-viewport side-by-side comparison:
  `D:\yokko\artifacts\song-select\comparison-reference-vs-current.png`

## Viewport and state

- Source dimensions: 1676 x 943.
- Native implementation capture: 3840 x 2160.
- Comparison normalization: both images resized to 1600 x 900 and placed
  side by side without cropping.
- Authored layout space: 1600 x 900.
- Selected chart state: Waterfall / VA / 4K PACK.
- Background comparison state: the selected visual uses a bright illustrated
  beatmap background while the native implementation uses the chart's dark
  geometric artwork. This intentionally validates the shared UI system across
  opposite background luminance and visual density.

## Comparison history

1. The first implementation retained the old dark list treatment and left
   insufficient room for rankings and account context.
2. The selected cute direction introduced warm paper surfaces, a six-row
   avatar ranking, Global and My History tabs, account summary, compact Mods,
   and the yellow Play action.
3. A runtime border/masking fault in song rows was fixed by moving the selected
   border onto the masked paper card.
4. The final density pass restored Best Score in the song card and made the
   song browser height follow the available area above the footer.
5. Background handling was made chart-owned: every non-empty artwork path is
   preserved, no Waterfall/title-specific branch exists, and a fixed neutral
   navy isolation layer is used instead of sampling or adapting UI colors to
   the artwork.

## Final review

- Fonts and typography: the navy hierarchy, cyan metadata, pink difficulty
  labels, yellow stars, and compact numeric columns remain readable on both
  bright and dark chart art.
- Spacing and layout: song details, six ranking rows, right-side chart browser,
  account card, Mods, Back, and Play preserve the selected composition while
  fitting more list information than the concept image.
- Long content: title layout supports two lines, CJK and no-space strings, and
  applies ellipsis when the available title region is exhausted. Pack headers
  remain separate from chart titles.
- Colors and surfaces: all information-bearing surfaces use fixed ivory,
  navy, cyan, pink, and yellow Yokko tokens. The UI does not inherit colors
  from the selected background.
- Background policy: selected chart artwork always remains the visual
  background. Only missing artwork uses the generic fallback texture. A fixed
  18% deep-navy isolation veil stabilizes contrast across arbitrary artwork.
- States and interactions: search, key-mode filters, pack expansion, chart
  selection, ranking/history tabs, Mods, Back, and Play remain active.
- Responsive behavior: the song browser is masked and dynamically bounded
  above the footer, preventing selected rows from drawing underneath account
  and Play controls.

## Findings

- P0: none.
- P1: none.
- P2: none after the final information-density and footer-bounds pass.
- P3: the generated concept has richer paper grain and hand-painted edge
  texture. The implementation deliberately retains crisp production surfaces
  so dynamic text and arbitrary beatmap art stay clear at different
  resolutions.

## Verification

- `dotnet build Yokko.Desktop\Yokko.Desktop.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- Focused Song Select persistence, long-title, and background-policy tests
  passed: 14 tests, 0 failures.
- The exact comparison input above was reviewed at a shared 1600 x 900
  viewport. The fixed surface hierarchy remains legible on both the bright
  reference artwork and the dark native beatmap artwork.
- Computer Use was stopped by the user before a post-patch recapture; the
  supplied native capture remains the visual evidence, while the final
  footer-bound and Best Score adjustments are covered by the successful build
  and focused tests.

final result: passed

---

# Gameplay Mods orbit workspace QA (2026-07-31)

## Evidence

- Source visual truth:
  `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-fd661d85-dd8d-4cba-b5ef-047e5e1a6fc5.png`
- Native implementation:
  `D:\yokko\artifacts\mods\orbit-polish-empty.png`
- Active-state implementation:
  `D:\yokko\artifacts\mods\orbit-polish-active.png`
- Side-by-side comparison:
  `D:\yokko\artifacts\mods\orbit-polish-comparison.png`
- Refined connector-state implementation:
  `D:\yokko\artifacts\mods\connector-polish-active.png`
- Refined source/implementation comparison:
  `D:\yokko\artifacts\mods\connector-polish-comparison.png`
- Post-transition regression capture:
  `D:\yokko\artifacts\mods\transition-fixed-scroll.png`
- Interaction-polish implementation:
  `D:\yokko\artifacts\mods\interaction-polish-active.png`
- Interaction-polish full comparison:
  `D:\yokko\artifacts\mods\interaction-polish-comparison.png`
- Interaction-polish focused orbit/right-panel comparison:
  `D:\yokko\artifacts\mods\interaction-polish-focused-comparison.png`
- Decoration-density implementation:
  `D:\yokko\artifacts\mods\decoration-density-active.png`
- Decoration-density full comparison:
  `D:\yokko\artifacts\mods\decoration-density-comparison.png`
- Decoration-density focused comparison:
  `D:\yokko\artifacts\mods\decoration-density-focused-comparison.png`
- Micro-polish active-state implementation:
  `D:\yokko\artifacts\mods\micro-polish-active.png`
- Micro-polish source/implementation comparison:
  `D:\yokko\artifacts\mods\micro-polish-comparison.png`
- Interaction audit baseline:
  `D:\yokko\artifacts\mods\interaction-audit-before.png`
- Interaction-polish implementation:
  `D:\yokko\artifacts\mods\interaction-polish-after.png`
- Authored viewport: 1600 x 900, verified through the native 3168 x 1785
  renderer capture used by the 3200 x 2000 desktop.
- States: empty Difficulty Down page and active Difficulty Up page with
  Hard Rock selected.

## Review

- Structure: the numbered category rail, central orbit, selected Mod hero,
  global rate controls, active slots, and cyan footer follow the source grid.
- Typography and colour: the production Yokko logo and existing navy, cyan,
  pink, yellow, ivory, and pale-cyan tokens preserve the source hierarchy.
  Secondary labels, descriptions, slider ticks, page counters, and active-row
  copy are sized for comfortable reading on the verified 2K-class viewport.
- Assets: the workspace uses a transparent production logo, a dedicated paper
  texture, and the source-matched waveform texture rather than placeholder
  geometry.
- Interaction: category rows and rail arrows are clickable; mouse wheel and
  Tab move between pages; orbit nodes support hover, click, and keyboard focus;
  the central hero toggles the focused Mod; the rate slider supports continuous
  drag and +/- nudging; active rows remove Mods; Back, Reset, and Done are
  functional. Hover and press states now animate position, scale, track weight,
  underline, colour, and removal affordances without delaying state changes.
- Motion and decoration: the selected category diamond and active-node halo
  breathe gently; source-derived waveform and point fields pulse at independent
  rhythms; orbit signal markers and a restrained footer scan line add technical
  motion while preserving copy readability.
- Connector system: straight centre-to-centre strokes were replaced by trimmed
  anti-aliased curves with a soft signal glow, a parallel telemetry rail,
  rotated data ticks, outlined joints, and a travelling signal pulse. Focused
  paths turn pink, active paths gain cyan energy, and idle paths remain quiet.
  The orbit also includes compact `SYNC // MOD MATRIX` and `SIGNAL 06`
  telemetry labels to strengthen the authored technical-instrument feel.
- Behaviour: all 1K-10K conversion Mods remain hidden. Real Mod compatibility,
  mutual exclusion, preferences, configuration state, score multiplier, and
  commit-on-handoff behaviour remain owned by `GameplayModsScreen`.
- Responsive behaviour: the authored workspace remains centred on the shared
  1600 x 900 layout and is scaled by Yokko's global UI-size container. The
  enlarged text hierarchy was checked in both empty and active states without
  clipping or node-label collisions.
- Page-transition stability: the orbit animation now offsets from its authored
  X=335 resting position and always returns there. Previously it animated to
  absolute X=0, permanently moving the central workspace over the category
  rail after the first page change.
- Quick interactions: the right panel now provides 0.75x, 1.00x, and 1.50x
  rate presets with selected, hover, and pressed states. Empty active slots
  add the currently focused Mod and reveal an explicit `ADD FOCUSED MOD`
  affordance on hover. Active orbit nodes show a check badge; focused inactive
  nodes show a plus badge, and both receive tactile press feedback.
- Responsive decoration: the orbit adds a slowly rotating, focus-coloured
  scanner and a live `FOCUS / ACTIVE` telemetry readout. These stay subordinate
  to the hero copy and use the existing cyan/pink/yellow token system.
- Density refinement: the central matrix now has 36 radial scale marks,
  cardinal index labels, and six node identifiers. The right panel adds an
  animated level meter, live five-step Mod-bus capacity, and edge scale; the
  canvas and footer add input-route, refresh-rate, input-ready, and session-bus
  readouts. All additions use low-opacity production tokens and preserve the
  primary copy and control hit targets.
- Material and feedback refinement: orbit nodes now use a restrained offset
  depth layer that strengthens focus without reading as a card shadow. Active
  rows gain compact status lamps and a one-shot hover scan; empty slots reuse
  the same scan language. Rate presets keep a persistent pink selection rail,
  while the speed panel uses sparse cyan/pink corner brackets to group its
  controls without adding another filled surface.
- Interaction audit and repair:
  1. Global wheel input now accumulates precision deltas and uses a 430 ms
     gesture lock, so one physical wheel/trackpad gesture moves exactly one
     category page instead of leaking momentum into a second page.
  2. Boundary wheel input is ignored without showing a false transition hint.
     Successful page gestures show a concise previous/next-page confirmation.
  3. The orbit rate slider's pointer target grows from 28 px to 44 px while
     keeping the same visual track. Hover and drag expose a marker-anchored
     exact-rate readout, and release still commits only during page handoff.

## Findings

- P0: none.
- P1: none.
- P2: none.
- P3: none.

## Verification

- Isolated `dotnet build Yokko.Game.Tests\Yokko.Game.Tests.csproj
  --no-restore --artifacts-path D:\yokko\artifacts\mods\polish-qa-build`:
  passed with 0 warnings and 0 errors.
- Focused `TestSceneGameplayModsScreen` suite: passed, 8/8, including the
  central-hero activation path.
- Connector refinement isolated build: passed with 0 warnings and 0 errors.
- Connector refinement focused `TestSceneGameplayModsScreen` suite: passed,
  8/8.
- Category-transition regression assertions now verify the orbit returns to
  X=335 after wheel, Tab, and Shift+Tab navigation; focused suite passed 8/8.
- Interaction-polish build: passed with 0 warnings and 0 errors.
- Focused suite: passed 9/9, including empty-slot activation and the 1.50x
  rate-preset path.
- Full and focused comparisons show no actionable P0, P1, or P2 visual
  regressions. The preset row remains clear of the multiplier and slider, node
  badges do not obscure labels, and telemetry stays outside the hero copy.
- Decoration-density build: passed with 0 warnings and 0 errors.
- Focused Gameplay Mods suite after density changes: passed 9/9.
- Micro-polish build: passed with 0 warnings and 0 errors.
- Focused Gameplay Mods suite after micro-polish: passed 9/9. This includes
  page transitions, global wheel paging, slider drag/commit, orbit hero,
  active-slot activation, and rate presets.
- Native Direct3D 11 micro-polish preview exited with code 0. The matching
  authored-density comparison shows the new depth and status accents remain
  subordinate to labels and do not introduce clipping or overlap.
- Interaction-polish verification was run in an isolated clean worktree because
  unrelated concurrent result/footer API edits blocked the shared-tree build.
  The isolated build passed with 0 warnings and 0 errors; the focused
  Gameplay Mods suite passed 9/9, including the new residual-wheel rejection
  and 44 px slider-target assertions.
- The post-change native Direct3D 11 capture exited with code 0. Static layout
  remains unchanged outside the deliberately more forgiving slider hit area.
- Full and focused density comparisons show no text collisions, clipped
  controls, or decoration over primary hit targets. No actionable P0, P1, or
  P2 findings remain.
- Native Direct3D 11 empty-state and active-state previews: exited with code 0.
- Final source/implementation comparison and 2K readability captures were
  reviewed at matching authored density.

final result: passed

---

# Yokko Home mascot sticker-bubble fidelity QA (2026-07-30)

## Evidence

- Selected visual target:
  `D:\YOKKO\artifacts\home-message-bubble\option-3-reference.png`
- Native implementation:
  `D:\YOKKO\artifacts\home-message-bubble\implementation-raster-final.png`
- Same-state, same-viewport focused comparison:
  `D:\YOKKO\artifacts\home-message-bubble\comparison-raster-focused.png`
- Normalized viewport: 1365 x 768.
- State: Home idle, first mascot line `开始吧！`.

## Findings and correction

- P1 resolved: the previous implementation was a regular rounded card with
  a diamond tail and pill-shaped pink accent, so its silhouette did not match
  the selected comic sticker.
- Replaced the approximated shape stack with a project-owned transparent
  raster asset derived from the selected visual direction. The main panel is
  now crooked, the ivory/white backing follows the perimeter, the speech tail
  is integrated, and the pink accent is a real lightning shape.
- Preserved live localized text and added length-aware display sizing so the
  short callout retains the source's impact while longer English, Chinese,
  and Japanese lines stay inside the panel.
- The focused comparison shows matching placement, footprint, palette,
  star cluster, layered outline, tail direction, and pink signal accent.

## Remaining polish

- P3: the generated project asset has a slightly thicker white sticker edge
  and smoother cyan fill than the concept crop. This does not change the
  component hierarchy or readability.

## Verification

- `dotnet build Yokko.Game\Yokko.Game.csproj --no-restore
  --artifacts-path D:\YOKKO\artifacts\home-message-bubble\build-raster`
  passed with 0 warnings and 0 errors.
- Native 1365 x 768 renderer capture completed successfully.
- The bubble remains above the mascot's hand and does not overlap the
  bottom-right music player.

final result: passed

# Pause overlay true-target verification (2026-07-29, QA4)

## Evidence

- Authoritative source:
  `C:\Users\nyafa\AppData\Local\Temp\codex-clipboard-d7e0b945-f981-4006-b3da-e8c84920934b.png`
- Source normalized to the native 16:9 capture:
  `D:\YOKKO\.artifacts\pause-ui-qa4\target-1366x768.png`
- Verified native implementation:
  `D:\YOKKO\.artifacts\pause-ui-qa4\verified-large.png`
- Same-state comparison, source left / implementation right:
  `D:\YOKKO\.artifacts\pause-ui-qa4\verified-comparison-source-left.png`
- Long-title regression capture:
  `D:\YOKKO\.artifacts\pause-ui-qa4\verified-long-title.png`
- Responsive captures:
  - Large 100%:
    `D:\YOKKO\.artifacts\pause-ui-qa4\verified-large.png`
  - Comfortable 90%:
    `D:\YOKKO\.artifacts\pause-ui-qa4\verified-comfortable.png`
  - Compact 80%:
    `D:\YOKKO\.artifacts\pause-ui-qa4\verified-compact.png`

## Viewport and state

- Source pixels: 1672 x 941.
- Native implementation pixels: 1366 x 768.
- Authored logical viewport: 1600 x 900.
- UI scale used for source matching: Large 100%.
- Locale: Chinese.
- Song state: Labyrinth / :Spiral_Eyes: / 4K / NM /
  02:14 of 03:48.
- Performance state: 97.18%, rank S, score 1,071,630,
  combo 3 / 414, judgments 287 / 18 / 2 / 0 / 0 / 2.
- Interaction state: paused, resume selected, no pointer hover.

## Findings and fixes

1. QA3 superseded
   - QA3 used a different visual as its source of truth. It is retained only
     as historical evidence and must not be used to judge this implementation.
2. True-target geometry
   - Restored the full-size report-sheet composition, large performance
     numerals, split-colour combo, judgment ledger, tall YOKKO wordmark, and
     the large lower-right mascot.
   - Matched the primary action, secondary action row, angled divider,
     song header, progress rule, footer barcode, dotted fields, rank stamp,
     and mascot bubble against the normalized source.
3. Runtime detail regressions
   - Long song titles now truncate inside a fixed title lane and cannot cover
     the mode, timer, or progress region.
   - The angled divider is rendered behind the action cards, so it cannot cut
     through the exit control or its border.
4. Responsive result
   - P0/P1/P2: pass. No clipping, overlap, unreadable text, or unreachable
     controls in Large, Comfortable, or Compact.
   - P3: the runtime rank glyph uses Yokko's production font and a subtle
     dotted distress treatment; raster texture differs slightly from the
     painted mockup but preserves its weight and hierarchy.

## Functional verification

- `dotnet build Yokko.Game.Tests\Yokko.Game.Tests.csproj --no-restore
  --artifacts-path D:\YOKKO\.artifacts\pause-ui-build`
  - Passed with 0 warnings and 0 errors.
- `dotnet test Yokko.Game.Tests\Yokko.Game.Tests.csproj --no-restore
  --artifacts-path D:\YOKKO\.artifacts\pause-ui-test
  --filter FullyQualifiedName~TestPauseOverlayStopsAndResumesAudio`
  - Passed: 1 / 1.

# Pause overlay pixel-fidelity recheck (2026-07-29, QA3)

## Evidence

- Source visual truth:
  `C:\Users\nyafa\.codex\attachments\5954dffe-5b60-4658-a0d8-f5b7c36937b9\image-1.png`
- Native implementation:
  `D:\YOKKO\.artifacts\pause-ui-qa3\implementation-final-comfortable.png`
- Density-normalized implementation:
  `D:\YOKKO\.artifacts\pause-ui-qa3\implementation-final-comfortable-2560x1440.png`
- Full-view comparison, source left / implementation right:
  `D:\YOKKO\.artifacts\pause-ui-qa3\comparison-final-source-left-implementation-right.png`
- Focused comparisons, source left / implementation right:
  - Left copy and controls:
    `D:\YOKKO\.artifacts\pause-ui-qa3\focused-left-final.png`
  - Performance, rank, judgments, and mascot:
    `D:\YOKKO\.artifacts\pause-ui-qa3\focused-right-final.png`
  - Mascot and bubble:
    `D:\YOKKO\.artifacts\pause-ui-qa3\focused-mascot-final.png`
  - Logo:
    `D:\YOKKO\.artifacts\pause-ui-qa3\focused-logo-final.png`
- Responsive captures:
  - Large 100%:
    `D:\YOKKO\.artifacts\pause-ui-qa3\implementation-final-large.png`
  - Comfortable 90%:
    `D:\YOKKO\.artifacts\pause-ui-qa3\implementation-final-comfortable.png`
  - Compact 80%:
    `D:\YOKKO\.artifacts\pause-ui-qa3\implementation-final-compact.png`

## Viewport and state

- Source pixels: 2560 x 1440.
- Native implementation pixels: 1366 x 768.
- Authored logical viewport: 1600 x 900.
- Density normalization: the native 16:9 implementation capture was resized
  to 2560 x 1440 before comparison. Upsampling softness was excluded from
  typography and asset-quality findings.
- UI scale: Comfortable 90% for source matching; Large 100% and Compact 80%
  checked for clipping and overlap.
- Locale: Chinese.
- Song state: Eternal Ending (aran Remix) / Kobaryo / 4K / NM /
  01:08 of 04:28.
- Performance state: 96.89%, rank S, score 224,758, combo 843 / 843,
  judgments 533 / 261 / 36 / 8 / 5 / 0.
- Interaction state: paused, resume selected, no pointer hover.

## Comparison history

1. Rejected QA2 handoff
   - P2: the mascot's visible color bounds were 124 x 146 px while the
     source was 143 x 145 px, making the character visibly too narrow and
     weak in the lower-right composition.
   - P2: the rank letter was 80 x 106 px instead of 67 x 89 px; judgment
     values were roughly 25% too large; score/combo values were roughly 18%
     too small; the resume title was 130 px wide instead of 113 px.
   - Fix: remeasured the normalized source and implementation by region,
     corrected the mascot display box, rank typography, metric typography,
     judgment values, pause copy, and primary/secondary action text.
   - Earlier evidence:
     `D:\YOKKO\.artifacts\pause-ui-qa2\source-left-implementation-right-final.png`
2. Same-state pixel pass
   - P2: the first correction matched the mascot width but left its color
     bounds 7 px high; several labels and metric baselines still differed by
     1-3 px.
   - Fix: corrected mascot vertical size/position, rank-label optical scale,
     percentage position, score/combo baseline, judgment value baseline,
     and action-row alignment.
   - Intermediate evidence:
     `D:\YOKKO\.artifacts\pause-ui-qa3\comparison-pass2-source-left.png`
3. Final comparison
   - Mascot source bounds: 144 x 145 px at x=2047, y=1114.
   - Mascot implementation bounds: 144 x 145 px at x=2048, y=1114,
     within one normalized pixel.
   - Accuracy numeral, rank circle and letter, score/combo values,
     judgment labels/values, pause title/subtitle, primary action title/hint,
     bubble, rules, and decorative fields now match their source bounding
     boxes within the 1-3 px antialiasing tolerance.
   - Post-fix evidence:
     `D:\YOKKO\.artifacts\pause-ui-qa3\comparison-final-source-left-implementation-right.png`

## Final review

- Fonts and typography: the pause title, subtitle, song metadata, accuracy,
  rank, score/combo, judgment labels/values, and action copy use the intended
  display/body hierarchy. The earlier oversized rank and judgment typography
  and undersized metric typography are corrected. Small residual edge
  differences are antialiasing from density normalization.
- Spacing and layout rhythm: the 1600 x 900 sheet, angled divider, header,
  action panels, data columns, rank stamp, double rules, judgment grid,
  bubble, mascot, barcode, and dot fields align with the source. Large,
  Comfortable, and Compact show no clipping, overlap, or reflow drift.
- Colors and tokens: navy, cyan, yellow, pink, green, orange, ivory,
  pale-cyan surfaces, dividers, shadows, and selected-state accents use
  Yokko's existing tokens and preserve source contrast.
- Image quality and asset fidelity: the production high-resolution YOKKO
  logo and mascot are used; the mascot display box now matches the source's
  visible 144 x 145 px footprint. Existing FontAwesome icons are retained,
  with no placeholder or handcrafted SVG assets.
- Copy and content: app-owned text and dynamic gameplay data match the source.
  The operating-system IME toolbar outside the source sheet is intentionally
  not reproduced.
- States, interaction, and accessibility: resume remains selected by default;
  mouse and keyboard selection, custom pause binding, audio pause, and audio
  resume still work. Text and selected-state contrast remain readable at all
  three UI scales.

## Findings

- P0: none.
- P1: none.
- P2: none.
- P3: the production YOKKO logo remains intentionally taller than the raster
  mock because the user explicitly requested that it not look horizontally
  flattened. Its width and left alignment still match the source.

## Verification

- `dotnet build Yokko.Game.Tests\Yokko.Game.Tests.csproj --no-restore
  --artifacts-path D:\YOKKO\.artifacts\pause-ui-build`
  passed with 0 warnings and 0 errors.
- `dotnet test Yokko.Game.Tests\Yokko.Game.Tests.csproj --no-build
  --no-restore --artifacts-path D:\YOKKO\.artifacts\pause-ui-build
  --filter FullyQualifiedName~TestPauseOverlayStopsAndResumesAudio`
  passed: 1 test, 0 failures.
- Large, Comfortable, and Compact native screenshot runs exited with code 0.

final result: passed

---

# Yokko Gameplay Mods Studio Index design QA

final result: passed

---

# Pause overlay target-fidelity correction QA (2026-07-29)

## Evidence

- Source visual truth:
  `C:\Users\nyafa\.codex\attachments\5954dffe-5b60-4658-a0d8-f5b7c36937b9\image-1.png`
- Native implementation screenshot:
  `D:\YOKKO\.artifacts\pause-ui-qa2\implementation-final-comfortable.png`
- Density-normalized implementation:
  `D:\YOKKO\.artifacts\pause-ui-qa2\implementation-final-comfortable-2560x1440.png`
- Full-view comparison, source left / implementation right:
  `D:\YOKKO\.artifacts\pause-ui-qa2\source-left-implementation-right-final.png`
- Focused comparisons, source left / implementation right:
  - Logo:
    `D:\YOKKO\.artifacts\pause-ui-qa2\focused-logo-source-left-implementation-right.png`
  - Pause controls:
    `D:\YOKKO\.artifacts\pause-ui-qa2\focused-controls-source-left-implementation-right.png`
  - Performance data:
    `D:\YOKKO\.artifacts\pause-ui-qa2\focused-performance-source-left-implementation-right.png`
- Responsive captures:
  - Large 100%:
    `D:\YOKKO\.artifacts\pause-ui-qa2\implementation-final-large.png`
  - Comfortable 90%:
    `D:\YOKKO\.artifacts\pause-ui-qa2\implementation-final-comfortable.png`
  - Compact 80%:
    `D:\YOKKO\.artifacts\pause-ui-qa2\implementation-final-compact.png`

## Viewport and state

- Source pixels: 2560 x 1440.
- Implementation pixels: 1366 x 768 native osu!framework capture.
- Authored logical viewport: 1600 x 900.
- Density normalization: the native implementation was resized to
  2560 x 1440 before comparison; the app-owned viewport and 16:9 state match.
  The slight softness in normalized close-ups is from upsampling and is not
  present in the native capture.
- UI scale: Comfortable 90% for target comparison; Large 100% and Compact
  80% checked separately for resilience.
- Locale: Chinese.
- Song state: Eternal Ending (aran Remix) / Kobaryo / 4K / NM /
  01:08 of 04:28.
- Performance state: 96.89%, rank S, score 224,758, combo 843 / 843,
  judgments 533 / 261 / 36 / 8 / 5 / 0.
- Interaction state: paused, resume selected, no pointer hover.

## Comparison history

1. Initial target correction
   - P1: the earlier implementation used a much larger accuracy treatment,
     flanking rank stars, oversized mascot, and denser judgment block than
     this source. Its dynamic song state also differed, so the first pass was
     used only for structural comparison.
   - Fix: matched the source state in the native preview and rebuilt the
     internal proportions without changing the 1600 x 900 sheet frame.
   - Evidence:
     `D:\YOKKO\.artifacts\pause-ui-qa2\new-target-vs-current-comfortable.png`
2. Same-state geometry pass
   - P2: the rank circle was too low, the mascot was too large and low, the
     percentage spacing drifted, and the pause separator length did not match.
   - Fix: aligned the rank stamp and stars, resized/repositioned the mascot,
     corrected percentage placement, restored the exact separator length,
     and matched score/combo optical sizes.
   - Intermediate evidence:
     `D:\YOKKO\.artifacts\pause-ui-qa2\implementation-pass1.png`
   - Post-fix evidence:
     `D:\YOKKO\.artifacts\pause-ui-qa2\source-left-implementation-right-final.png`
3. Logo aspect correction
   - P2: the YOKKO asset was displayed in a horizontally flattened box.
   - Fix: retained the production asset and changed its render slot to the
     original approximately 2.94:1 aspect ratio, with vertical compensation
     so the left-side rhythm remains aligned.
   - Post-fix evidence:
     `D:\YOKKO\.artifacts\pause-ui-qa2\focused-logo-source-left-implementation-right.png`

## Final review

- Fonts and typography: title, metadata, accuracy, rank, metrics, judgment
  values, and Chinese actions match the source hierarchy and optical sizes.
  The YOKKO mark now uses its natural aspect ratio rather than horizontal
  stretching, as explicitly requested.
- Spacing and layout rhythm: the paper frame, angled divider, left action
  column, song header, accuracy block, rank stamp, metric row, double rules,
  judgment ledger, bubble, and mascot align in the normalized comparison.
  No clipping or overlap appears at Large, Comfortable, or Compact.
- Colors and tokens: navy, cyan, pink, yellow, judgment colors, ivory paper,
  pale-cyan controls, rule opacity, and shadows use Yokko's existing tokens
  and match the source balance.
- Image quality and assets: the production high-resolution YOKKO logo and
  mascot texture are reused with correct aspect handling. Existing
  FontAwesome icons are used for controls and rank stars; there are no
  placeholders, custom SVG substitutes, or code-drawn image assets.
- Copy and content: all app-owned static copy and dynamic pause data match
  the source state. The operating-system IME toolbar visible outside the
  source sheet is correctly excluded from the app UI.
- States, interaction, and accessibility: resume remains selected by default;
  mouse and keyboard actions remain reachable, and the custom pause binding
  still pauses and resumes audio. Text and selected-state contrast remain
  readable across the three size settings.

## Findings

- P0: none.
- P1: none.
- P2: none.
- P3: the natural-aspect production YOKKO logo is intentionally a little
  taller than the raster mock's mark. This is the user's latest requested
  correction and preserves the real asset instead of stretching it.

## Verification

- `dotnet build Yokko.Game.Tests\Yokko.Game.Tests.csproj --no-restore
  --artifacts-path D:\YOKKO\.artifacts\pause-ui-build`
  passed with 0 warnings and 0 errors.
- `dotnet test Yokko.Game.Tests\Yokko.Game.Tests.csproj --no-build
  --no-restore --artifacts-path D:\YOKKO\.artifacts\pause-ui-build
  --filter FullyQualifiedName~TestPauseOverlayStopsAndResumesAudio`
  passed: 1 test, 0 failures.
- Large, Comfortable, and Compact native screenshot runs exited with code 0.

final result: passed

## Evidence

- Source visual truth: `C:\Users\mochi\.codex\attachments\022453ae-9d23-490b-8efa-75ab6faaef9f\image-1.png`
- Saved concept: `D:\yokko\docs\design\mods\yokko-gameplay-mods-studio-index-concept.png`
- Final 100% responsive screenshot: `D:\yokko\artifacts\mods\responsive-after\01-large-100.png`
- Final 90% responsive screenshot: `D:\yokko\artifacts\mods\responsive-after\02-comfortable-90.png`
- Final 80% responsive screenshot: `D:\yokko\artifacts\mods\responsive-after\03-compact-80.png`
- Pre-polish interaction audit: `D:\yokko\artifacts\mods\interaction-audit-before\interaction-audit.md`
- Full normalized comparison: `D:\yokko\artifacts\mods\gameplay-mods-comparison-v8.png`
- Focused list comparison: `D:\yokko\artifacts\mods\gameplay-mods-focus-list-v8.png`
- Focused detail comparison: `D:\yokko\artifacts\mods\gameplay-mods-focus-right-v8.png`
- Source pixels: 1664 x 936.
- Implementation viewport: the same 3200 px-wide native Windows renderer at 200% DPI for all three captures.
- Density normalization: the page consumes the complete logical viewport exposed by the global 100%, 90%, and 80% UI-size setting. Controls retain their authored size while the browser grows from two to three or four columns, the inspector remains right-aligned, and the footer spans the live viewport.
- State: Difficulty Down selected, Half Time selected and active with Hidden, speed 0.75x, music pitch off.

## Full-view comparison

The implementation follows the selected Studio Index composition: a logo/title header, five-category left rail, responsive Mod index, right selected-Mod inspector, and the homepage-style cyan action footer. It no longer places a fixed 1600 x 900 page inside the larger logical workspace exposed by 90% and 80%. The stage now fills the available viewport, distributes the body between the header and footer, adds useful browser columns, and keeps the detail panel against the right edge.

## Focused comparison

- Mod acronyms are nested inside fixed 40 x 40 badges; they no longer fall below or outside their outlines.
- Difficulty Down and Difficulty Up retain the same column-major order and section hierarchy while gaining columns only when the available browser width supports them.
- The selected Half Time card, speed slider, and Active Mods list share the reference baselines without overlap.
- Fixed-rate Mods use a light, page-native configuration surface. The slider and pitch row are interactive and display the actual supported range.
- Configurable Mods exclusively own the compact Settings row, preventing the shortcut label and settings title from colliding.
- Plain Mods move their empty-settings message below the shortcut, preserving the same vertical rhythm as fixed-rate Mods.
- Hover no longer shifts complete list rows horizontally, so text and badge columns remain stable.

## Required fidelity surfaces

- Fonts and typography: existing Yokko display/body families and uppercase letter spacing are retained; headings, labels, acronyms, descriptions, and footer actions preserve the reference hierarchy.
- Spacing and layout rhythm: the 1280 x 720 authored minimum remains the narrow-layout floor. Above it, the header stays at the top, the main workspace is vertically balanced, the inspector and divider track the right edge, and the 110 px footer tracks the live bottom edge.
- Colors and visual tokens: ivory, deep navy, cyan, pink, and yellow are reused from Yokko's existing design system.
- Image quality and asset fidelity: the existing Yokko logo and framework icon set are reused; no placeholder raster or fabricated brand asset was introduced.
- Copy and content: concept-only labels such as `Difficulty Calculator`, `No Recover`, and `Classic Omission` are intentionally replaced by the real selectable Yokko entries `Daycore`, `No Release`, and `Cover`.

## Findings

- No actionable P0, P1, or P2 visual issue remains in the inspected state.
- The 100%, 90%, and 80% responsive captures contain no clipped persistent control, footer drift, text collision, stretched typography, or fixed-stage side margins.
- P3 accepted difference: real gameplay descriptions are longer and are ellipsized at the same fixed text boundary rather than replaced with the concept's short placeholder copy.
- P3 accepted difference: Half Time displays its truthful 0.50x-0.99x range and a functional pitch toggle; the concept showed generic 0.25x-2.00x ticks.
- P3 expected rendering variance: framework font rasterization is slightly lighter than the generated concept at 200% Windows DPI.

## Comparison history

1. Initial implementation:
   - P1: acronym text used an item-level anchor and rendered below the badge at the tested DPI.
   - P1: fixed-rate configuration inherited a dark generic panel that did not match the selected light concept.
   - P2: category rows and the right detail sections used different vertical density from the source.
2. Alignment correction:
   - Nested acronyms inside fixed badges and removed row-level hover translation.
   - Reordered difficulty entries into the concept's column-major rhythm while retaining their real gameplay identities.
   - Added the light fixed-rate slider and pitch control, and repositioned the Settings and Active Mods sections.
3. Post-fix comparison:
   - The full normalized comparison confirmed the page shell, columns, footer, and right inspector.
   - Focused list and detail comparisons confirmed stable badge baselines, no text collision, and no panel overlap.
4. Global 1600 x 900 adaptation:
   - P1: the page still rendered at its old 1280 x 720 size after the global reference changed, creating a visibly undersized centred island.
   - The first correction mapped the authored stage to a fixed 1600 x 900 surface, which solved 100% but still left a centred island at 90% and 80%.
   - P2: configurable detail pages let `SPACE TO TOGGLE` collide with `SETTINGS`; the configuration surface now owns that row and plain/fixed-rate states use the lower settings baseline.
5. True responsive adaptation and interaction polish:
   - Removed the fixed inner scale and made the page stage consume the complete logical viewport produced by the global UI-size system.
   - Browser columns now adapt from two to four; the inspector, divider, footer actions, and decorations use edge-aware positioning.
   - Added visible keyboard focus, arrow and Tab navigation, Enter/Space activation, page-level wheel navigation, precise keyboard and slider rate changes, explicit active/preview configuration labels, transient interaction feedback, and a disabled Reset state.
   - Follow-up performance audit found that every slider tick notified the suspended Song Select screen, restarting preview and rebuilding scores, details, and the song list. Gameplay Mods now keeps edits local and commits once during page handoff.
   - Slider dragging updates its own marker immediately, activates an inactive fixed-rate Mod on first interaction, ignores unchanged values, and avoids repeated focus/opacity transforms. Responsive layout only recalculates when the viewport changes, and list hover no longer rebuilds the inspector.
   - Native renderer captures across all three size modes show stable layout and no clipping.

## Primary interactions and verification

- Category selection, Mod selection, activation/removal, Reset, Done, and Back remain functional.
- The global wheel and Tab change category pages; arrow keys move the focused Mod inside the current page; Enter/Space toggles the focused Mod.
- `H` toggles Half Time; `P` toggles pitch when supported; plus/minus and slider dragging adjust rate precisely.
- Isolated `Yokko.Game` and `Yokko.Game.Tests` builds: passed with 0 warnings and 0 errors.
- Focused Gameplay Mods, Song Select integration, and display-scale tests: passed, 30/30.
- Native Windows visual inspection at 200% DPI and 100%/90%/80% UI size: passed.

---

# Yokko Song Select search redesign design QA

final result: passed

## Evidence

- Source visual truth: `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-6658cec6-d3ce-4f2c-b2e1-1543e6eb919a.png`
- Final implementation, query state: `D:\yokko\artifacts\product-design\song-select-search-redesign-final.png`
- Final implementation, first-Esc cleared state: `D:\yokko\artifacts\product-design\song-select-search-redesign-cleared-final.png`
- Focused before/after comparison: `D:\yokko\artifacts\product-design\song-select-search-redesign-comparison.png`
- Source pixels: 1990 x 700.
- Implementation pixels and viewport: 1600 x 952 native Windows visual-test capture.
- Density normalization: the source search region was cropped to 900 x 180 and scaled to 1200 x 240; the implementation search region was cropped to 500 x 140 and scaled to 858 x 240. Both are displayed together in the 2058 x 240 comparison.
- State: search focused with `43` entered; a separate capture records the first-Esc cleared state.

## Full-view comparison

The full implementation capture confirms that the redesigned control remains anchored to the upper-right song-browser column, does not overlap the artwork, filters, or sliders button, and preserves the existing Yokko stage and footer. The filter row now begins on the same visual axis as the search control instead of being stranded beneath an oversized field.

## Focused comparison

The combined comparison shows the supplied state on the left and the implemented redesign on the right. The old field is excessively long and visually empty; the new 360 x 44 logical-pixel control uses a compact cyan icon well, stronger focus rail, localized placeholder, visible `ESC` affordance, and a matching 44 x 44 sliders button. The narrower width preserves enough room for normal song queries while restoring hierarchy to the right column.

## Required fidelity surfaces

- Fonts and typography: the control reuses `HomeTypography.Body` and `HomeTypography.Display`; entered text, placeholder, and the small `ESC` hint remain legible without competing with the filters.
- Spacing and layout rhythm: search and sliders use the same 44 px height and 7 px corner radius; the filter row aligns to the search start with an 8 px inter-control gap.
- Colors and visual tokens: deep navy, navy, cyan, pale cyan, and ivory all come from `SongSelectTheme`; focus raises the cyan border and bottom rail without introducing a new palette.
- Image quality and asset fidelity: no raster asset was added or replaced. The search and sliders icons remain Font Awesome assets already used by Yokko.
- Copy and content: the placeholder now uses the existing localized `song_select.search` string, while `ESC` communicates the new keyboard behavior.

## Findings

- No actionable P0, P1, or P2 issue remains.
- The search control, sliders action, and filter row are visually balanced in the captured desktop viewport.
- Typing `43` works, the first Esc clears the query while staying on Song Select, and the second Esc returns to the previous screen.
- P3 accepted constraint: the visual-test browser adds a test navigation rail outside the app stage; it does not affect the captured app layout.

## Comparison history

1. Source state:
   - P1: the search field occupied most of the right header and read as an unfinished debug input.
   - P2: search and sliders used mismatched sizing and weak grouping.
   - P2: no visible affordance explained the requested two-stage Esc behavior.
2. Implementation:
   - Reduced the field to 360 x 44 logical pixels.
   - Added a contained search icon, localized placeholder, visible `ESC` hint, cyan focus rail, and matched 44 x 44 sliders control.
   - Realigned the filter row and implemented clear-first, return-second Esc handling in both the focused text box and screen-level key path.
3. Post-fix evidence:
   - Native query and cleared-state captures show no clipping, overlap, or unreadable text.
   - The focused before/after comparison has no remaining P0/P1/P2 issue.

## Primary interactions and verification

- Native Windows interaction: entered `43`; first Esc cleared it without leaving Song Select.
- Automated screen-stack interaction: first Esc cleared the query and kept Song Select current; second Esc returned to the previous screen.
- `Yokko.Game` focused build: passed with 0 warnings and 0 errors.
- `Yokko.Game.Tests` focused build: passed with 0 warnings and 0 errors.
- Focused `TestSceneSongSelectScreen` suite: passed, 5/5.
- Native visual test: no runtime exception observed.

---

# Yokko Song Select full-screen reference-match design QA

final result: passed

## Evidence

- Source visual truth: `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-e17b6d95-27c3-4162-a66e-d18d823cfc73.png`
- Final implementation, closed state: `D:\yokko\artifacts\product-design\song-select-fullscreen-deep-final.jpg`
- Final implementation, Mods open state: `D:\yokko\artifacts\product-design\song-select-fullscreen-deep-final-mods-open.jpg`
- Full-screen normalized comparison: `D:\yokko\artifacts\product-design\song-select-fullscreen-deep-final-comparison.png`
- Source pixels: 1672 x 941
- Implementation viewport: 1282 x 749 native Windows capture, including the 30 px title bar; app content is 1282 x 719.
- Density normalization: the source app content below its 29 px title bar was normalized from 1672 x 912 to the implementation's 1282 x 719 content viewport. The source footer from y=804 and implementation footer from y=640 were each normalized to 1282 x 109 for the focused comparison.
- State: Waterfall selected, Global Ranking visible, footer closed; Mods open state additionally inspected.

## Full-view comparison

The implementation now matches the full selected reference rather than only its footer. The ivory stage uses the same shallow diagonal edge, the logo and left detail column share the reference origin, the ranking surface is restored to a readable 320 px visual width, and the search/filter/browser column uses the same right-aligned start and compact list density. The homepage-style cyan footer, looping mascot, Back card, single Mods tile, and Play action remain pixel-aligned from the previous pass.

## Focused footer comparison

- Footer height and top edge align with the reference after viewport normalization.
- Back reproduces the white raised card, ESC keycap, navy label, pink chevron, cyan outline/shadow, and yellow corner diamond.
- Mods is represented by one centered sliders tile with a separate `MODS` label; individual mod acronyms only appear in the functional popover.
- Play reuses the homepage primary-action language: navy card, white play tile, small spaced `SONG SELECT`, large `PLAY`, yellow chevron, cyan outline, pink underline, and dot texture.
- The user-provided GIF loops at the lower-left and remains visually connected to the footer without covering ranking data.
- Sparse white plus marks, dot texture, and the diagonal divider preserve the selected visual rhythm without competing with controls.
- Measured white-surface alignment after normalization:
  - Back target `x=221..355, y=658..710`; implementation `x=219..355, y=657..709`.
  - ESC target `x=231..263, y=669..700`; implementation `x=230..262, y=667..699`.
  - Mods target `x=600..658, y=657..713`; implementation `x=601..657, y=657..714`.
  - Play icon tile target `x=932..988, y=664..721`; implementation `x=932..987, y=665..721`.

## Findings

- No actionable P0, P1, or P2 issue remains.
- Typography uses Yokko's existing display family and preserves the reference hierarchy at the native viewport.
- No left-side text, ranking control, footer control, or mascot is clipped. The selected Waterfall row intentionally continues a few pixels below the footer edge, matching the supplied reference while keeping its title, key mode, and rating visible.
- The Mods popover opens above its tile, keeps all mod choices readable, and leaves the leaderboard/browser usable.
- P3 expected dynamic variance: song-list scroll position and the visible GIF animation frame differ between captures.

## Comparison history

1. First implementation pass:
   - P2: footer was too shallow.
   - P2: mascot was undersized and sat too low.
   - P2: Back and Play cards did not match the selected proportions.
2. Correction:
   - Increased the logical footer height to 136.
   - Resized and raised the looping mascot.
   - Rebalanced Back width/position and Play scale/position.
   - Kept the browser height bound above the footer.
3. Post-fix:
   - The first combined comparison still showed actionable pixel drift: mascot about 20% oversized and left/up, Back about 10 px too low and too short, Mods about 11 px too far right and undersized, the Play icon tile undersized, and a missing centre-right dot field.
4. Pixel correction:
   - Matched the mascot's transformed visible bounds with a 251 logical-pixel GIF box at `(39, -60)`.
   - Raised and deepened Back, enlarged its keycap, and aligned its diamond to the footer edge.
   - Enlarged and shifted Mods left, moved the popover with it, and matched the target label rhythm.
   - Added footer-specific icon-tile sizing/offsets to the shared homepage Play action without changing its default home appearance.
   - Repositioned the divider/pluses and restored the missing dot field.
   - Sampled the target footer cyan and matched the footer fill.
5. Final precision pass:
   - The second native comparison found only 1-5 px surface drift. Back depth, Mods surface, Play icon position, and copy origin were corrected once more.
   - Final full-view and focused evidence is `song-select-footer-final-pixel-comparison.png`; all major control surfaces now align within 0-2 px after normalization.
6. Full-screen deep pass:
   - Reduced the ivory-panel slant from 8 degrees to 3 degrees and moved its bottom intersection to the reference boundary.
   - Shifted and widened the left detail/ranking column; the ranking surfaces now match the reference's 400 logical-pixel width and lower vertical position.
   - Tightened the search box and filters, moved the song browser start to the reference x-coordinate, and preserved the right edge.
   - Rebalanced normal and selected song-row heights so the same three compilation rows, package header, seven package songs, and selected Waterfall continuation are visible at the native viewport.

## Primary interactions

- Mods tile opens and closes its panel.
- Mod family exclusivity and visibility-family behavior remain intact.
- Play transfers selected mods into Gameplay.
- Ranking remains above the footer at the 16:9 stage.

## Verification

- `dotnet build .\Yokko.Desktop\Yokko.Desktop.csproj --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test .\Yokko.Game.Tests\Yokko.Game.Tests.csproj --no-build --filter "FullyQualifiedName~TestSceneSongSelectScreen" --logger "console;verbosity=minimal"`: passed, 3/3.
- Native Windows visual inspection: passed at 1282 x 749.
- Desktop runtime: no exception observed while opening Song Select and toggling Mods.

---

# Yokko Song Select design QA

final result: passed

## Evidence

- Reference visual: `C:\Users\nyafa\AppData\Local\Temp\codex-clipboard-c8ee9a51-8c45-4121-a170-3157ae072f09.png`
- User-reported broken implementation: `C:\Users\nyafa\AppData\Local\Temp\codex-clipboard-4df5ee61-f358-4985-9b7c-1a8efb877111.png`
- Final implementation screenshot: `D:\YOKKO\artifacts\song-select-implementation-v5.png`
- Side-by-side comparison: `D:\YOKKO\artifacts\song-select-comparison-v5.png`
- State inspected: Blue Signal selected, Global Ranking selected, five leaderboard rows visible.

## Full-view comparison

The final screen keeps the reference's strong left-detail/right-browser composition, fixed bottom action bar, chart-driven full-page background, navy glass panels, cyan/pink/yellow accents, and compact rhythm-game typography. The implementation deliberately uses a five-row leaderboard and moves Length/BPM beside the difficulty summary to preserve vertical space.

## Focused region comparison

- Leaderboard: all five positions are fully visible above the footer. Avatar, rank, player name, grade emblem, score, accuracy, mods, and current-player treatment remain readable without collisions.
- Selected-song summary: title, artist, mapper, key mode, difficulty, stars, numeric rating, length, and BPM form one compact hierarchy.
- Mascot/footer: the mascot is reduced and placed below the leaderboard, no longer obscuring the fourth/fifth rows or song statistics.
- Browser: filter/search controls and five chart rows are visible. Demo rows use the selected chart fallback background; imported charts are expected to provide their own beatmap background.

## Findings

- No P0, P1, or P2 issue remains in the inspected state.
- P3 accepted deviation: the implementation uses background strips rather than album-art thumbnails because the current data model is chart-background driven.
- P3 accepted deviation: the selected chart remains at the top of the list rather than being vertically centered; selection, filtering, and keyboard movement are functional.

## Comparison history

1. Broken state:
   - P1: leaderboard rows collided with the mascot and Length/BPM block.
   - P1: fourth and fifth positions were partially hidden.
   - P2: grade letters read like unstyled placeholder text.
2. Fix:
   - Moved Length/BPM above the leaderboard.
   - Reduced leaderboard row/avatar typography and kept five rows.
   - Replaced grade letters with bordered grade emblems.
   - Reduced and lowered the mascot.
3. Post-fix:
   - Side-by-side visual comparison shows no overlap, clipping, or unreadable hierarchy.

## Verification

- `dotnet build .\Yokko.Desktop.slnf --no-restore -p:AllowUnsafeBlocks=true`: passed with 0 warnings and 0 errors.
- Focused Song Select and localisation tests: passed, 10/10.
- Covered interactions: song selection, 7K filter, search/no-results recovery, global/personal ranking switch, and pushing Gameplay from Play.

---

# Yokko Performance Readout design QA

final result: passed

## Evidence

- Source visual truth: `C:\Users\nyafa\.codex\generated_images\019fa8f4-d7ca-7302-9e14-612ecfca6704\call_fMsB0dovJHy4DkIOqOS9pLvt.png`
- Implementation screenshot: `C:\Users\nyafa\.codex\visualizations\2026\07\28\019fa8f4-d7ca-7302-9e14-612ecfca6704\yokko-performance-readout-implementation.png`
- Main-screen context screenshot: `C:\Users\nyafa\.codex\visualizations\2026\07\28\019fa8f4-d7ca-7302-9e14-612ecfca6704\yokko-performance-readout-main-screen.png`
- Focused comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\28\019fa8f4-d7ca-7302-9e14-612ecfca6704\yokko-performance-readout-comparison.png`
- Source pixels: 1716 x 917
- Implementation pixels: 913 x 543
- Native component size: 118 x 22 logical pixels
- Density normalization: the 200 x 45 source crop was compared with a 100 x 25 implementation crop scaled 2x with nearest-neighbour sampling.
- State: source shows `194 FPS | 5.1 ms`; the visual test shows live test data `480 FPS | 2.1 ms`.

## Full-view comparison

The selected concept and implementation both keep the readout as a quiet, single-line utility rather than a separate card. The native main-screen capture confirms the surrounding Yokko palette and bottom-right context. Windows Graphics Capture returned a 1707 x 960 logical-pixel view of the 2560 x 1440 high-DPI window and clipped the physical right edge, so placement fidelity was verified from the unchanged bottom-right anchor and the focused component scene rather than inferred from the clipped edge.

## Focused region comparison

The focused side-by-side comparison shows the selected concept on the left and the native rendered component on the right. Both use an ivory rail, cyan top rule, pink square, navy values and labels, a single divider, compact radius, and no heavy border, dot field, diamond, graph, or shadow.

## Findings

- No actionable P0, P1, or P2 differences remain.
- Fonts and typography: the existing Yokko Roboto display family is retained; value weight, compact label scale, and single-line hierarchy match the concept. Dynamic values remain readable in the focused render.
- Spacing and layout rhythm: 118 x 22 logical pixels, 3 px radius, 4-7 px internal offsets, and one 12 px divider reproduce the compact status-rail proportions without clipping.
- Colors and visual tokens: the implementation reuses `HomeControlColours` for ivory, navy, cyan, and pink; no new off-brand colors or gradients were introduced.
- Image quality and asset fidelity: the component contains no raster imagery or non-standard icons, so no image asset substitution was needed.
- Copy and content: the rail presents live `{value} FPS | {value} ms` content. Omitting `FRAME` is intentional and matches the selected concept's reduced information density.

## Comparison history

- Pass 1: no P0/P1/P2 mismatch was found in the focused rendered comparison, so no post-comparison visual fix was required.

## Follow-up polish

No blocking polish remains. A future live 2560 x 1440 hardware capture could document the final high-DPI corner placement more clearly than the logical-pixel Windows capture.

---

# Yokko Home Multiplayer design QA

final result: passed

## Evidence

- Source visual truth: `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-8b1aa213-32b9-4b36-bfcb-3b84b8dd32a4.png`
- Rendered implementation: `D:\yokko\artifacts\home-multiplayer-implementation.png`
- Click feedback: `D:\yokko\artifacts\home-multiplayer-click-feedback.png`
- Side-by-side comparison: `D:\yokko\artifacts\home-multiplayer-comparison.png`
- Source pixels: 1638 x 960
- Implementation pixels and viewport: 1280 x 750 native desktop window capture
- Density normalization: the source was scaled to 1280 x 750 for the side-by-side comparison; aspect ratios differ by less than 0.1%.
- State: Chinese locale, no online friends, Multiplayer idle and clicked states.

## Full-view comparison

The implementation preserves the current Yokko home shell, mascot art, ivory/cyan split, navy primary Play action, pink/yellow accents, and existing Editor/Settings hierarchy. Multiplayer occupies the requested full-width third row and remains visually secondary to Play. The current production home has additional utility controls and a music player that were not present in the generated concept; these were intentionally preserved rather than removed to force a literal mock match.

## Focused region comparison

A separate crop was not needed because the complete Multiplayer row is readable at native size in the 2560 x 750 side-by-side comparison. The icon, title baseline, outline, corner treatment, chevron, hover rail, and spacing relative to Editor/Settings are visible without enlargement.

## Required fidelity surfaces

- Fonts and typography: the new label reuses Yokko's existing `HomeTypography.Display` treatment, weight, and optical scale. It remains readable in English, Chinese, and Japanese without introducing another font.
- Spacing and layout rhythm: the two compact secondary actions remain paired above one 520 x 82 Multiplayer action. No overlap or clipping appears at the 1280 x 750 desktop viewport.
- Colors and visual tokens: the control uses the existing ivory, navy, cyan, pink, and yellow tokens; no off-brand color or gradient was introduced.
- Image quality and asset fidelity: the existing mascot and logo assets are unchanged. The player icon comes from the same Font Awesome set as the other home controls. No placeholder or generated avatar is rendered in the empty-friends state.
- Copy and content: with zero friends, neither `Friends online` nor an avatar strip is present. Clicking Multiplayer changes the mascot bubble to the localized coming-soon message and emits the existing sparkle feedback.

## Findings

- No actionable P0, P1, or P2 issue remains.
- P3 accepted deviation: the rendered label is slightly quieter than the generated concept because it follows the smaller typography already used by the live Editor and Settings controls.

## Comparison history

1. First rendered comparison found no overlap, clipping, unreadable text, or hierarchy regression.
2. No visual fix loop was required.

## Verification

- Focused visual test: passed, 2/2.
- Native Windows capture: passed at 1280 x 750.
- Primary interaction: Multiplayer click feedback verified.
- Empty state: `Friends online` and avatar strip are absent when no friend textures are supplied.

---

# Yokko native-resolution interface scaling design QA

final result: passed

## Evidence

- Source visual truth, Home 100% maximised:
  `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-3919f73f-3bba-4876-8063-0b1fa665e627.png`
- Source visual truth, Settings 100% maximised:
  `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-ebdf7339-3222-4e67-bbda-b86facaf9fbe.png`
- Source visual truth, Home 80% maximised:
  `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-f79dd367-fb9b-494a-871b-891b0b0823ba.png`
- Final Home implementation:
  `D:\yokko\.artifacts\ui-scale-resolution-test\main-100-native-3200x2000.png`
- Final Settings implementation:
  `D:\yokko\.artifacts\ui-scale-settings-responsive-test\settings-100-responsive-3200x2000.png`
- Final Song Select implementation:
  `D:\yokko\.artifacts\ui-scale-resolution-test\songselect-100-native-3200x2000.png`
- Combined Home comparison:
  `D:\yokko\.artifacts\ui-scale-resolution-test\qa-main-side-by-side.png`
- Combined Settings comparison:
  `D:\yokko\.artifacts\ui-scale-settings-responsive-test\settings-responsive-comparison.png`
- Current-code Home 100% / 90% / 80% comparison:
  `D:\yokko\.artifacts\ui-scale-compact-responsive-test\main-100-90-80-current.png`
- Source pixels: Home 3200 x 1898; Settings 3196 x 1870.
- Implementation pixels and client viewport: 3200 x 2000.
- Density normalization: each source and implementation was normalized
  to 1600 px width and padded to 1000 px height before horizontal
  comparison. Window-title and crop-height differences were excluded from
  density findings.
- State: Chinese locale, maximised desktop source captures, interface
  sizes 100%, 90%, and 80%; current-code comparison at one identical viewport.

## Full-view comparison

The broken source used the earlier 1.5x desktop cap, leaving Home,
Settings, and Song Select at an undersized 1920x1080-equivalent density.
The final captures use the native viewport with a shared 1600x900 layout
space. At 3200x2000 the 100% scale resolves to 2.0x, fills the useful
desktop area, and preserves the responsive Home and Song Select regions.
Settings now migrates its authored 1280x720 stage into that same shared
space instead of leaving it as a smaller centred island.
When 90% or 80% exposes a larger logical viewport, Home now expands its
responsive stage to that complete viewport instead of capping itself back
to 1600x900.

## Focused comparison

The combined comparisons keep the same overall state and show the intended
density correction without typography, icon, image, or control clipping.
Settings' sidebar, content panel, and footer now use the full shared
1600x900 reference stage. The same-code Home comparison proves that 90%
and 80% progressively reduce fixed UI density while redistributing the
left controls, right mascot, utility controls, and player across the newly
available logical space.
No additional crop was needed because labels, icon alignment, and control
boundaries remain readable in the combined images.

## Required fidelity surfaces

- Fonts and typography: the existing Yokko font family, weights, wrapping,
  and hierarchy are unchanged; native scaling improves readability.
- Spacing and layout rhythm: Home remains balanced across both halves,
  Settings fills the reference stage while preserving safe outer margins,
  and Song Select keeps its footer and header within the viewport.
- Colors and visual tokens: no palette, opacity, border, radius, or state
  token changed.
- Image quality and asset fidelity: all existing logo, mascot, background,
  and icon assets remain native framework drawables; no replacement asset
  or rasterized interface was introduced.
- Copy and content: Chinese UI copy is unchanged and remains fully visible.

## Findings

- No actionable P0, P1, or P2 issue remains in the inspected 3200x2000
  100% state or current-code 100%/90%/80% comparison.

## Comparison history

1. Initial implementation:
   - P1: the 1.5x desktop cap made 100% visibly smaller than its label.
   - P2: the global 1280x720 reference conflicted with Song Select's
     1600x900 layout density.
2. Correction:
   - Removed the raw desktop-resolution cap.
   - Replaced the legacy 1280x720 global reference with a shared
     1600x900 layout space.
   - Kept 90% and 80% as proportional reductions from the native
     resolution-derived 100% scale.
3. Settings follow-up:
   - P1: Settings still retained its fixed 1280x720 authored stage inside
     the new 1600x900 reference, so it remained visibly undersized.
   - Scaled that local stage by 1.25 so it maps exactly to 1600x900 while
     continuing to inherit the global 100%, 90%, and 80% scale.
4. Compact-scale follow-up:
   - P1: Home capped its own responsive stage at 1600x900, cancelling the
     extra layout space exposed by 90% and 80% and leaving a centred island.
   - Removed that second cap; the stage now consumes the complete logical
     viewport while retaining the authored 1280x720 minimum.
5. Post-fix evidence:
   - Native Home, Settings, and Song Select captures show no clipping,
     overlap, or unreadable persistent controls.
   - Same-build 100%/90%/80% Home captures visibly differ and the smaller
     layouts use the complete viewport rather than retaining the 100%
     footprint.
- Focused scaling tests pass 15/15.

---

# Yokko Home player progression card design QA

final result: passed

## Evidence

- Source visual truth:
  `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-192c553d-6054-4a43-8ca9-c507acae51ee.png`
- User-reported crowded state:
  `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-fa889d69-9f07-46c5-ae4f-da63499faf25.png`
- Final native tall-window implementation:
  `C:\Users\mochi\.codex\visualizations\2026\07\29\019faccd-026e-73a0-a8de-4d77ab5f5555\yokko-player-card-spacing-tall-final.png`
- Final focused control-stack capture:
  `C:\Users\mochi\.codex\visualizations\2026\07\29\019faccd-026e-73a0-a8de-4d77ab5f5555\yokko-player-card-spacing-tall-final-detail.png`
- Final focused player-card capture:
  `C:\Users\mochi\.codex\visualizations\2026\07\29\019faccd-026e-73a0-a8de-4d77ab5f5555\yokko-player-card-spacing-card-final.png`
- Full normalized comparison:
  `C:\Users\mochi\.codex\visualizations\2026\07\29\019faccd-026e-73a0-a8de-4d77ab5f5555\yokko-player-card-comparison.png`
- Source pixels: 1586 x 992.
- Implementation capture: 2560 x 4000 native Windows window at 200% DPI. The intentionally tall off-screen viewport exposes the complete authored vertical stack for focused inspection.
- Density normalization: the full comparison scales the 2560 x 2000 implementation pass to the source's 992 px height. The focused control and player-card captures retain native pixels.
- State: Chinese locale, Home idle state, full player-card layout.

## Full-view comparison

The implementation preserves the selected game-first hierarchy: Play remains the dominant action, Editor and Settings remain paired secondary actions, Multiplayer remains a full-width game entry, and the player progression card closes the left stack. The production screen keeps Yokko's existing live utility controls, audio status, mascot interaction, and music player instead of removing them to force a literal generated-mock match.

## Focused comparison

- The user-reported state had only 2-8 logical pixels between adjacent control surfaces, so shadows and yellow corner diamonds visually merged.
- The final full layout uses 12 px between Play and the secondary row, 12 px between the secondary row and Multiplayer, and 16 px between Multiplayer and the player card.
- The compact 720 px layout keeps 8 px, 6 px, and 10 px gaps respectively, while switching the player card to a 74 px summary and hiding the non-essential audio footer.
- The full player card is 148 px tall. Avatar, identity, level, experience, combo, and played-song rows no longer collide or touch the bottom border.

## Required fidelity surfaces

- Fonts and typography: the card reuses Yokko's display and body bitmap fonts. New Chinese/Japanese glyphs were regenerated into the existing font atlases.
- Spacing and layout rhythm: the selected hierarchy is retained, but the control groups now have explicit gaps and the player card has enough internal height for its two information rows.
- Colors and visual tokens: ivory, navy, cyan, pink, pale cyan, and yellow all reuse the Home control palette.
- Image quality and asset fidelity: the circular avatar reuses the existing Yokko mascot texture with framework masking; no placeholder or fabricated illustration was added.
- Copy and content: `YOKKO_PLAYER`, `RANK 07`, `LV. 24`, `72%`, `1,284`, and `36` match the selected concept. Supporting labels are localized in English, Chinese, and Japanese.

## Findings

- No actionable P0, P1, or P2 visual issue remains in the inspected full layout.
- P3 product follow-up: the current progression values are a single presentation model matching the selected concept. A future player-progression store can replace that model without rebuilding the card.

## Comparison history

1. Initial implementation:
   - P1: Play, secondary actions, Multiplayer, and the player card visually merged into one dense block.
   - P1: the player's combo and song-count rows crowded the card border.
2. Spacing correction:
   - Added explicit full and compact layout positions for all four control groups.
   - Increased the full player card from 126 px to 148 px and moved its experience and stat rows onto stable baselines.
   - Moved the audio status below the full card and hid it in the compact fallback.
3. Post-fix evidence:
   - Native focused captures show separated control surfaces, intact shadows and diamonds, readable card copy, and no internal overlap.
   - Layout invariants verify that both full and compact stacks retain positive gaps and fit their intended stages.

## Primary interactions and verification

- Play, Editor, Settings, and Multiplayer actions retain their existing behavior.
- Player-card full/compact switching follows the available logical height and does not create a new route or fake interaction.
- Isolated focused build plus `TestSceneMainScreen` and localisation/font coverage: passed, 8/8.
- Native Windows visual inspection: passed.

---

# Yokko Home player card bottom-clipping design QA

final result: passed

## Evidence

- Source visual truth:
  `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-42505d70-c2d0-43ab-9e37-25b345c07b68.png`
- Final native implementation:
  `D:\yokko\artifacts\home-player-card-clipping-fixed-native.png`
- Focused before/after comparison:
  `D:\yokko\artifacts\home-player-card-clipping-comparison.png`
- Source pixels: 1108 x 376.
- Implementation pixels and viewport: 1600 x 1000 native desktop window.
- Density normalization: the 522 x 146 logical-pixel implementation card
  crop was enlarged 2x and compared with the matching 1043 x 290 source
  crop.
- State: Chinese locale, Home idle state, full player-card layout.

## Full-view and focused comparison

The native full-window capture preserves the existing Home composition and
control hierarchy. The focused comparison confirms that `1,284` and `36`
now render completely above the card's bottom border; the avatar, identity,
level, experience rail, labels, divider, shadows, and corner diamond retain
their previous alignment and styling.

## Required fidelity surfaces

- Fonts and typography: existing Yokko display/body fonts, sizes, weights,
  and copy are unchanged; only the stat row baseline moved upward.
- Spacing and layout rhythm: both bottom stat groups moved upward by 6
  logical pixels, restoring bottom padding without changing the 148 px card
  height or adjacent Home layout.
- Colors and visual tokens: no color, opacity, border, shadow, or radius
  changed.
- Image quality and asset fidelity: the existing mascot avatar and framework
  icons are unchanged.
- Copy and content: `最高连击 1,284` and `游玩曲目 36` remain unchanged and
  are now fully visible.

## Findings

- No actionable P0, P1, or P2 issue remains in the inspected native state.

## Comparison history

1. User-reported state:
   - P1: both stat values crossed the white card surface and were clipped by
     the bottom edge.
2. Fix:
   - Moved the stat row and divider from y=112 to y=106.
   - Corrected the existing layout invariant to the actual 12 px and 16 px
     full-layout gaps.
3. Post-fix:
   - Native focused comparison shows both values fully inside the card with
     visible bottom padding and no new overlap.

## Verification

- `dotnet build .\Yokko.Desktop.slnf --no-restore
  -p:AllowUnsafeBlocks=true --verbosity:minimal`: passed with 0 warnings and
  0 errors.
- Focused `TestSceneMainScreen`: passed, 2/2.
- Native Windows visual inspection: passed at 1600 x 1000.

---

# Yokko Home highest-combo alignment design QA

final result: passed

## Evidence

- Source visual truth:
  `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-4be529e8-b0f6-4c61-8f96-dc5de4013676.png`
- Final native implementation:
  `D:\yokko\artifacts\home-highest-combo-shifted-more-native.png`
- Focused comparison:
  `D:\yokko\artifacts\home-highest-combo-shifted-more-comparison.png`
- Implementation viewport: 1600 x 1000.
- State: Chinese locale, Home idle state, full player-card layout.

## Findings

- The complete highest-combo group is shifted 32 logical pixels from its
  original position, including a further 20 logical pixels (about 40 screen
  pixels) after the first adjustment. Its icon, label, and value remain
  aligned with each other and keep clear separation from the centre divider.
- No typography, colors, imagery, copy, or adjacent layout changed.
- No actionable P0, P1, or P2 issue remains.

## Verification

- Desktop build: passed with 0 warnings and 0 errors.
- Focused MainScreen tests: passed, 2/2.
- Native Windows visual inspection: passed at 1600 x 1000.

---

# Yokko Home card polish and bottom-anchored player design QA

final result: passed

## Evidence

- Source close-up:
  `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-5de2e159-2ebd-462f-9392-9e1c411ab3c8.png`
- Source full view:
  `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-d906eef5-b5ab-4398-877f-5d9d0d4a270b.png`
- Final native implementation:
  `D:\yokko\artifacts\home-layout-player-lowered-native.png`
- Full before/after comparison:
  `D:\yokko\artifacts\home-layout-player-lowered-comparison.png`
- Source pixels: 3200 x 2000.
- Implementation pixels and viewport: 1600 x 1000 native desktop window.
- Density normalization: the source is an exact 2x-density capture of the
  implementation viewport and was downsampled to 1600 x 1000 for comparison.
- State: Chinese locale, Home idle state, no imported track selected.

## Full-view and focused comparison

The player is now bottom-anchored with a 12 px safe margin after the right
stage offset is applied. In the 1600 x 1000 state it sits completely below
the mascot rather than covering the character's feet and clothing. The Home
player card no longer has a third oversized animated heartbeat hanging from
its lower divider; the remaining progress and combo hearts keep the intended
visual language without making the card edge feel crowded.

## Required fidelity surfaces

- Fonts and typography: no typography, wrapping, truncation, or copy changed.
- Spacing and layout rhythm: the music player derives its y position from the
  responsive stage height and keeps 12 px bottom clearance. The player card
  and audio status now read as two separate surfaces.
- Colors and visual tokens: existing ivory, cyan, navy, pink, and yellow
  tokens are unchanged.
- Image quality and asset fidelity: mascot and avatar textures remain
  untouched and unobscured; no replacement assets were introduced.
- Copy and content: player metadata, card labels, clock, status, and actions
  remain unchanged.

## Findings

- No actionable P0, P1, or P2 issue remains in the inspected native state.

## Comparison history

1. User-reported state:
   - P1: the music player covered the mascot's lower body.
   - P2: the large animated heartbeat immediately below the player card made
     the lower edge feel visually crowded.
2. Fix:
   - Replaced the fixed player y position with a responsive bottom anchor.
   - Removed the redundant status-divider heartbeat while retaining the
     progress and combo heart icons.
3. Post-fix:
   - The normalized before/after comparison shows the complete mascot above
     the player and a cleaner separation below the progression card.

## Verification

- `dotnet build .\Yokko.Desktop.slnf --no-restore
  -p:AllowUnsafeBlocks=true --verbosity:minimal`: passed with 0 warnings and
  0 errors.
- Focused display-settings and MainScreen tests: passed, 21/21.
- Native Windows visual inspection: passed at 1600 x 1000.

## Gameplay Mods interaction polish (2026-07-29)

- Added persistent keyboard and wheel navigation guidance, page-level wheel
  traversal, reverse category traversal with `Shift+Tab`, and explicit
  feedback for unavailable mods.
- Wheel, Tab, and category-button navigation now use a directional page
  transition: the current browser and inspector leave together, the next page
  enters from the matching direction, repeated input is locked during the
  transition, and the first/last page gives bounded edge feedback.
- Enlarged the fixed-rate slider target and added hover/press feedback; an
  inactive rate mod now explains that dragging or pressing Space enables it.
- Plain mods no longer show irrelevant rate controls, including after pending
  hide animations complete.
- Native captures passed for default, wheel focus, configurable, and inactive
  rate states at Large UI scale.
- Focused interaction and display tests: passed, 30/30.

---

# Gameplay Mods redesign QA

## Evidence

- Source visual truth: `C:\Users\nyafa\.codex\generated_images\019fadd2-f94c-7d92-b4aa-e88843fa5928\call_2ZoM9XyaiFZYLw1ujWEsRsuL.png`
- Source pixels: 1672 x 941
- Implementation screenshot: `D:\YOKKO\artifacts\design-preview\mods-1600-large-final4.png`
- Implementation pixels: 1366 x 768
- Authored viewport: 1600 x 900 at Yokko UI scale `Large`
- Density normalization: source resized to 1366 x 768; implementation is a native 1366 x 768 capture of the 1600 x 900 authored workspace at a 0.85375 content scale
- State: All category, Half Time and Hidden active, Half Time selected
- Full-view comparison: `D:\YOKKO\artifacts\design-preview\mods-design-comparison-final4.png`
- Focused inspector comparison: `D:\YOKKO\artifacts\design-preview\mods-inspector-comparison-final4.png`
- Responsive captures:
  - `D:\YOKKO\artifacts\design-preview\mods-comfortable-final4.png`
  - `D:\YOKKO\artifacts\design-preview\mods-compact-final4.png`

## Findings

No actionable P0, P1, or P2 differences remain.

- Fonts and typography: Yokko display/body fonts preserve the source hierarchy. The final pass increased catalogue and inspector optical sizes so names, descriptions, settings, and shortcuts remain readable at the normalized viewport.
- Spacing and layout rhythm: the implementation follows the shared 1600 x 900 authored grid. Loadout, search, category chips, two-column featured catalogue, inspector, and footer align with the source. Comfortable and Compact expose additional logical space without moving the primary workspace down; Compact uses three catalogue columns.
- Colors and tokens: navy, cyan, pink, yellow, ivory, pale-cyan surfaces, selected states, dividers, and compatibility green use Yokko's existing tokens.
- Image quality and assets: the existing high-resolution Yokko logo is reused. Visible icons use the project's Font Awesome set; no placeholder, CSS-art, or improvised image asset was introduced.
- Copy and content: section labels, Mod names, settings, compatibility, and shortcuts match the design intent. Dynamic product truth intentionally overrides two mock values: the active loadout computes `0.3x`, and Half Time is constrained to `0.50x鈥?.99x`.
- Interaction and accessibility: search, Escape-to-clear, category selection, keyboard Mod navigation/toggle, reset, rate drag, pitch toggle, selected indicators, and commit behavior are active. Focused tests cover the principal states and configuration paths.

## Comparison history

1. Initial comparison
   - P1: the page still used a 1280 x 720 authored baseline, creating a large empty header gap and four catalogue columns at Compact.
   - Fix: bound the screen to `YokkoDisplaySettings.ReferenceLayoutSize` and rebuilt the layout at 1600 x 900.
   - Evidence after fix: `D:\YOKKO\artifacts\design-preview\mods-1600-large-pass3.png`
2. Structural comparison
   - P1: the catalogue composition and inspector controls did not match the selected visual.
   - Fix: added the featured two-column composition, responsive three-column Compact variant, segmented pitch control, compatibility card, and shortcut area.
   - Evidence after fix: `D:\YOKKO\artifacts\design-preview\mods-1600-large-pass5.png`
3. Detail comparison
   - P2: selected states and small typography were weaker than the source.
   - Fix: added full-row selection outlines, rate/check indicators, uppercase inspector title, larger optical text, and source-aligned footer spacing.
   - Post-fix evidence: `D:\YOKKO\artifacts\design-preview\mods-design-comparison-final4.png`

## Follow-up polish

- P3: the implementation keeps a small live shortcut/status line above Settings that is not present in the static mock.
- P3: score multiplier and Half Time slider maximum differ from the generated mock because the implementation displays real gameplay constraints.

## Verification

- `dotnet build Yokko.Game.Tests\Yokko.Game.Tests.csproj --no-restore --artifacts-path D:\YOKKO\artifacts\design-preview`
- `dotnet test Yokko.Game.Tests\Yokko.Game.Tests.csproj --no-build --no-restore --artifacts-path D:\YOKKO\artifacts\design-preview --filter "FullyQualifiedName~TestSceneGameplayModsScreen"`
- Result: build passed with 0 warnings and 0 errors; 7 focused tests passed.

final result: passed

---

# Pause overlay design QA

## Evidence

- Source visual:
  `C:\Users\nyafa\.codex\generated_images\019fae03-2de4-71e3-8fbb-720cbe3bfbe0\call_qUkMSPS8d2kOEGYI3Xfspmv0.png`
- Native implementation screenshot:
  `D:\YOKKO\.artifacts\pause-ui-qa\implementation-final.png`
- Full normalized comparison:
  `D:\YOKKO\.artifacts\pause-ui-qa\comparison-final.png`
- Focused control comparison:
  `D:\YOKKO\.artifacts\pause-ui-qa\comparison-left-controls-final.png`
- Focused performance comparison:
  `D:\YOKKO\.artifacts\pause-ui-qa\comparison-performance-final.png`
- Responsive captures:
  `D:\YOKKO\.artifacts\pause-ui-qa\implementation-comfortable.png`
  and
  `D:\YOKKO\.artifacts\pause-ui-qa\implementation-compact.png`

## Viewport and state

- Source dimensions: 1672 x 941.
- Implementation capture: 1366 x 768 native osu!framework renderer.
- Comparison normalization: source resized to 1366 x 768, then placed
  beside the implementation at the same density.
- Authored layout space: Yokko's 1600 x 900 reference size.
- UI scale captures: Large 100%, Comfortable 90%, Compact 80%.
- Locale: Chinese.
- Song state: Labyrinth / :Spiral_Eyes: / 4K / NM / 02:14 of 03:48.
- Performance state: 97.18%, rank S, score 1,071,630,
  combo 3 / 414, judgments 287 / 18 / 2 / 0 / 0 / 2.

## Comparison history

1. Initial native pass exposed density drift in the left action column,
   an undersized mascot, an unframed accuracy label, and misplaced rank
   ornaments.
2. Intermediate passes aligned the paper frame, angled divider, song header,
   left controls, performance columns, judgment ledger, and mascot to the
   normalized source.
3. Final pass corrected the progress rule, accuracy numeral proportions,
   percentage spacing, vertical rules, score divider, and responsive scaling.

## Final review

- Fonts and typography: hierarchy, weight, tracking, and numeric emphasis
  match the source. Chinese labels remain legible at all three UI scales.
- Spacing and layout: the report sheet, diagonal split, action column,
  song header, performance columns, rank stamp, judgment ledger, and mascot
  align with the normalized source. No clipping or overlap was observed.
- Colors and surfaces: navy, cyan, pink, yellow, green, orange, ivory,
  borders, shadows, and dotted accents follow the selected visual and Yokko's
  existing tokens.
- Image and icon fidelity: the production Yokko logo and mascot textures are
  used. Controls use the existing FontAwesome icon source and remain optically
  aligned.
- Copy and data: song information, progress, score, accuracy, combo, rank,
  mods, and every judgment count are populated from a pause-time gameplay
  snapshot. Static copy matches the selected visual.
- States and interactions: resume, restart, settings, exit, mouse hover,
  keyboard selection, custom pause binding, audio pause, and audio resume
  remain functional.
- Accessibility: keyboard reachability and selected-state contrast are
  retained. Text remains readable at Large, Comfortable, and Compact scales.
- Runtime: native preview exited with code 0. No fatal renderer errors were
  observed. The only runtime notices were expected large-texture atlas
  performance messages for existing production assets.

## Findings

- P0: none.
- P1: none.
- P2: none.
- P3: the generated source uses a distressed ink texture inside the rank
  letter and four-point decorative sparkles. The native implementation uses
  Yokko's crisp production type and the closest existing FontAwesome star,
  avoiding a new fake raster or handcrafted icon asset. This does not alter
  hierarchy, readability, or interaction.

## Verification

- `dotnet build Yokko.Game.Tests\Yokko.Game.Tests.csproj --no-restore
  --artifacts-path D:\YOKKO\.artifacts\pause-ui-build`
  passed with 0 warnings and 0 errors.
- Focused visual/gameplay test
  `TestPauseOverlayStopsAndResumesAudio`
  passed: 1 test, 0 failures.
- Large, Comfortable, and Compact native screenshot runs all exited with
  code 0.

final result: passed

# Gameplay result overlay redesign (2026-07-31, QA1)

## Evidence

- Selected Product Design source:
  `D:\yokko\docs\design\gameplay\yokko-result-v2-selected.png`
- Native osu!framework implementation capture:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-v2-final.png`

## Viewport and state

- Source: 1680 x 945.
- Implementation: 3168 x 1785.
- Both captures use the same 16:9 composition and were compared at normalized
  density.
- Song: Afterimage [Insane].
- Result: rank B, score 0537761, 82.51% accuracy, max combo 20.
- Mods: HD, DT, 1.50x.
- Judgments: 8 / 12 / 0 / 0 / 0 / 4.

## Comparison history

1. The first native pass kept the old expanding stage model. At 1600 x 900,
   content stayed at 1280 x 720 density and appeared undersized on the left.
2. The result overlay now scales a fixed 1280 x 720 authored canvas
   proportionally to the available viewport. Cards, controls, decorations,
   and mascot therefore retain the selected composition at every density.
3. The final pass aligned the diagonal split, moved the production mascot
   upward, tuned the production logo scale, and added a dedicated fixed-rate
   chip so DT visibly reports 1.50x.

## Final review

- Typography and hierarchy: logo, RESULT heading, song title, rank, score,
  metrics, judgments, and actions match the selected order and emphasis.
- Spacing and layout: the score hero, summary rail, judgment baseline, primary
  action, and secondary actions align to the same left grid without clipping.
- Colors and surfaces: Yokko navy, cyan, ivory, pink, and yellow tokens are
  reused throughout. Borders, shadows, dotted fields, and signal decorations
  remain subordinate to the result data.
- Image fidelity: the production `home-logo-light.png` and cropped
  `yokko.png` mascot are used directly; the generated concept is not shipped
  as a flattened interface.
- Dynamic data: score, accuracy, combo, rank, every judgment, localized
  labels, and MOD chips remain code-driven.
- Interactions: retry, replay, and song-select callbacks and keyboard labels
  remain present. The entrance transition and subtle mascot motion are
  preserved.

## Findings

- P0: none.
- P1: none.
- P2: none.
- P3: the selected concept uses a clapperboard replay icon while the
  implementation retains Yokko's existing replay glyph. Meaning, hierarchy,
  and hit target are unchanged.

## Verification

- Isolated `Yokko.Game.Tests` build passed with 0 warnings and 0 errors.
  Isolation was required because unrelated concurrent Song Select constructor
  work temporarily blocked the shared-tree build.
- Focused result overlay and responsive-scale tests passed: 3 / 3.
- Native Direct3D 11 preview exited normally and produced the implementation
  capture above.

final result: passed

# Gameplay result broadcast redesign (2026-07-31, QA2)

## Evidence

- Source visual truth:
  `D:\yokko\docs\design\gameplay\yokko-result-broadcast-selected.png`
- Final native implementation:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-option2-final-en.png`
- Equal-size full-view comparison:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-option2-comparison-final.png`
- Focused mascot comparison:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-option2-focus-mascot.png`
- Focused result-data and action comparison:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-option2-focus-results-final.png`

## Viewport, density, and state

- Source pixels: 1672 x 941.
- Implementation pixels: 1600 x 900.
- Yokko authored/CSS-equivalent viewport: 1600 x 900; native Direct3D 11
  capture, device-scale normalization not applicable.
- The source was downsampled to 1600 x 900 before comparison. No browser
  chrome, window frame, or surrounding canvas is present.
- State: English locale, `Afterimage [Insane]`, rank B, score 0537761,
  accuracy 82.51%, max combo 20, HD + DT 1.50x, judgments
  8 / 12 / 0 / 0 / 0 / 4.

## Comparison history

1. Pass 1 used the old kneeling production mascot and a cyan left field.
   This produced a P1 silhouette mismatch and a P2 backdrop mismatch against
   the selected standing composition.
2. Pass 3 replaced the field with the selected ivory/cyan diagonal broadcast
   split. The layout, result ribbon, metrics, MOD row, judgments, and actions
   then aligned, but the kneeling mascot remained a P1 mismatch.
3. Pass 4 introduced a dedicated standing mascot asset, generated from the
   selected pose and official Yokko identity art, then chroma-keyed and
   validated as an RGBA resource. The focused mascot comparison confirms the
   full character, shoes, IV stand, cable, and transparent edges are present.
4. The pass-4 focused result crop showed a P2 hierarchy drift: rank, score,
   metrics, judgment values, and Retry copy were visibly smaller than the
   source. The final pass increased those optical sizes while keeping every
   dynamic value inside its panel.

## Final review

- Fonts and typography: Roboto Bold preserves the selected dense broadcast
  hierarchy. RESULT, rank, score, metrics, judgments, and actions now match
  the source's optical emphasis without wrapping or truncation. The song title
  remains dynamic and uses Yokko's existing non-italic display face.
- Spacing and layout rhythm: the 1600 x 900 authored canvas uses the same
  left-character/right-result split, ribbon height, three-part metric row,
  judgment baseline, and bottom action rail. No element clips or overlaps.
- Colors and visual tokens: the implementation reuses Yokko's navy, cyan,
  pale cyan, ivory, yellow, and pink tokens. The diagonal field and score
  ribbon maintain the source foreground/background balance.
- Image quality and asset fidelity: the official logo remains a real texture.
  The standing mascot is a separate 1024 x 1536 RGBA resource with transparent
  corners, crisp opaque subject edges, and no visible chroma fringe. No
  flattened full-screen mockup is shipped.
- Copy and content: score, rank, accuracy, combo, every judgment, MOD acronyms,
  fixed rate, and localized action labels remain code-driven. English and
  Chinese localization continue to work.
- Icons and interactions: Font Awesome replay, play, music, star, and chevron
  icons remain aligned inside real clickable controls. Focused gameplay
  regression coverage confirms the result appears after completion, exposes
  all three actions, and Watch Replay starts a recorded replay.
- Responsiveness and accessibility: proportional 1600 x 900 scaling is
  covered at 1777.78 x 1000, 1024 x 576, and 1600 x 1000. Buttons retain
  high-contrast fills, 78-pixel visible height, keyboard callbacks, hover
  feedback, and localized labels.

## Findings

- P0: none.
- P1: none.
- P2: none.
- P3: the selected concept uses star separators in the ticker and an italic
  song title; production keeps Yokko's existing plus-separated ticker and
  non-italic localized display face.
- P3: the selected concept colors the first score digit cyan; production keeps
  the entire dynamic score white to avoid per-digit styling complexity.

## Verification

- Isolated `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors.
- Focused result, display-scale, completion, and replay tests:
  25 passed, 0 failed.
- Final Direct3D 11 preview: exited normally and captured at 1600 x 900.
- Shared-tree test build was not used because the user-visible Yokko process
  locks its desktop output and unrelated concurrent importer-test edits are
  incomplete; the isolated worktree contains the exact result files and asset.

final result: passed

# Gameplay result broadcast redesign (2026-07-31, QA3)

## Evidence

- Source visual truth:
  `D:\yokko\docs\design\gameplay\yokko-result-broadcast-selected.png`
- Final native implementation:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-option2-deep-final.png`
- Equal-size full-view comparison:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-option2-comparison-deep-final.png`
- Focused score-ribbon comparison:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-option2-focus-score-deep-final.png`
- Focused data-and-controls comparison:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-option2-focus-controls-deep-final.png`

## Viewport, density, and state

- Source pixels: 1672 x 941, normalized to 1600 x 900.
- Implementation pixels: 1600 x 900.
- Authored viewport: 1600 x 900; native Direct3D 11 capture, with no browser
  chrome, window frame, device frame, or density mismatch.
- State: English locale, `Afterimage [Insane]`, rank B, score 0537761,
  accuracy 82.51%, max combo 20, HD + DT 1.50x, judgments
  8 / 12 / 0 / 0 / 0 / 4.

## Comparison history

1. The user rejected QA2 because the production ribbon still read as a plain
   rectangle, the first score digit was not cyan, the RESULT/title hierarchy
   was undersized, the mascot was too tall, and the action rail sat too low.
   These were reclassified as one P1 composition mismatch plus several P2
   fidelity mismatches.
2. Deep pass 1 replaced the approximate box-built score panel with a dedicated
   transparent ribbon asset. It restored the slanted lead-in, clipped tail,
   cyan keyline, yellow accent, right-side diamonds, and dot matrix while
   keeping rank and score live.
3. Deep pass 2 aligned the 1600 x 900 geometry: RESULT and title sizing,
   title underline, rank/score baselines, mascot crop and height, summary
   metrics, judgment row, and 96-pixel action rail.
4. The final pass restored the compact secondary icon tiles, exact button
   spacing, unobstructed labels, source-style English capitalization,
   lower-right dot field, and pulse-line detail. The final full-view and both
   focused comparisons show no actionable P0/P1/P2 mismatch.

## Required fidelity review

- Fonts and typography: RESULT now matches the source's optical width and
  height. The B rank, seven-digit score, metric values, judgment values, and
  Retry label share the source hierarchy. The first live score digit is cyan
  and the remaining digits stay white. No text wraps or truncates.
- Spacing and layout rhythm: score-ribbon bounds, divider, mascot silhouette,
  metric columns, judgment baseline, and all three buttons align to the
  normalized 1600 x 900 source. Persistent controls remain fully visible.
- Colors and visual tokens: navy, cyan, pale cyan, ivory, yellow, and pink
  preserve the selected broadcast balance. The lower result surface uses a
  restrained pale-cyan wash rather than a flat white block.
- Image quality and asset fidelity: the irregular ribbon is a separate RGBA
  project resource, not a code-drawn approximation or flattened screen. It
  has transparent corners and clean chroma-removed edges. The official logo
  and standing mascot remain separate real textures.
- Copy and content: song data, rank, score, MODs, accuracy, combo, judgments,
  and localized actions remain code-driven. English labels now read
  `Watch Replay` and `Song Select`, matching the source capitalization.
- Interactions and responsiveness: Retry, Watch Replay, and Song Select remain
  live clickable controls with existing keyboard callbacks and hover states.
  The authored 1600 x 900 composition continues to scale proportionally.

## Findings

- P0: none.
- P1: none.
- P2: none.
- P3: the generated ribbon has a slightly softer navy depth gradient and a
  denser right dot field than the concept.
- P3: the production ticker keeps plus separators instead of the concept's
  occasional star separators.

## Verification

- Isolated desktop solution build: passed with 0 warnings and 0 errors.
- Focused display-scale, result-overlay, completed-play, and replay tests:
  25 passed, 0 failed.
- Final Direct3D 11 preview: exited normally and captured at 1600 x 900.
- The isolated worktree was used to avoid the running Yokko process and
  unrelated concurrent edits in the shared tree.

final result: passed

---

# Settings gameplay overflow verification (2026-07-31)

## Evidence

- Source: `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-deb18dc4-d327-4c30-911b-afd047b04fca.png`
- Fixed top state: `C:\Users\mochi\AppData\Local\Temp\yokko-settings-feedback-scroll-final-1600x998.png`
- Fixed scroll-end state: `C:\Users\mochi\AppData\Local\Temp\yokko-settings-feedback-scroll-end-1600x998.png`
- Full comparison: `C:\Users\mochi\AppData\Local\Temp\yokko-settings-feedback-comparison-1600x998.png`
- Focused comparison: `C:\Users\mochi\AppData\Local\Temp\yokko-settings-feedback-focused-comparison.png`
- Viewport and density: source 3200x1996 at 2x, normalized to the implementation's 1600x998 at 1x without cropping or aspect-ratio changes.
- State: Settings > Gameplay > Feedback, Chinese locale, large UI scale.

## Findings

- P0: none.
- P1: none.
- P2: none.
- Fonts, copy, existing imagery, palette, and component spacing are unchanged.
- Overflow stays inside the feedback panel. The final controls are reachable by mouse-wheel or touchpad scrolling, and section changes reset the scroll position.
- The first implementation exposed the framework's thick green scrollbar. It was replaced with a 4px Yokko-cyan scrollbar before the final captures.
- The current playback-rate tab is an intentional existing product change outside this overflow fix.

## Verification

- Focused `TestGameplayOverflowContentCanScroll`: passed.
- Isolated `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors.
- The shared worktree's current SongSelect compilation errors are unrelated; the final settings files were verified in an isolated worktree.

The focused comparison was required because the final two controls are too small to judge reliably in the full-screen comparison.

final result: passed

---

# Settings gameplay frame refinement (2026-07-31)

## Evidence

- Source state: `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-c27516b7-08f8-47e0-bd1b-73b77f005fa8.png`
- Boundary defect crop: `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-c6dbf932-9909-4365-be51-6256b0d388ee.png`
- Final implementation: `C:\Users\mochi\AppData\Local\Temp\yokko-settings-feedback-bounded-final.png`
- Full comparison: `C:\Users\mochi\AppData\Local\Temp\yokko-settings-feedback-final-comparison.png`
- Focused before/after comparison: `C:\Users\mochi\AppData\Local\Temp\yokko-settings-feedback-boundary-comparison.png`
- Viewport: final implementation 1600x1000 at 1x. The 2590x1492 source is a cropped 2x capture and was normalized to 1295x746 before comparison.
- State: Settings > Gameplay > Feedback, Chinese locale, large UI scale.

## Comparison history

1. The first overflow fix made the current 320px content taller than its 296px viewport. Its almost-full-height cyan scrollbar read as a broken right border.
2. The viewport and shared panel height were raised to 328px so the current feedback controls fit with 30px bottom padding; scrolling remains latent for genuinely larger future content.
3. The user then identified that the right side still looked unbounded. The two 406px cards plus 14px gutter occupied 826px; with the 20px left inset they exceeded the 840px parent by 6px and were visibly clipped.
4. The card grid was corrected to 393px + 14px + 393px = 800px, restoring matching 20px left and right insets. The final focused comparison shows both card borders and the outer panel border without clipping.
5. The settings-page mascot layer, interaction, animation, sparkles, responsive positioning, and its obsolete layout assertion were removed at the user's request. Other screens and the shared mascot resource were intentionally left untouched for future reuse.

## Required fidelity review

- Fonts and typography: unchanged; Chinese headings, notes, values, and state labels retain the existing type scale and remain fully visible.
- Spacing and layout rhythm: the feedback frame now reaches the footer cleanly, the two-column cards have symmetric 20px insets, and the final rows retain comfortable bottom padding.
- Colors and visual tokens: existing navy, cyan, pink, pale-cyan, divider, and ivory tokens are unchanged.
- Image quality and asset fidelity: no replacement assets were introduced. The settings mascot was intentionally removed rather than altered; logo and decorative assets remain sharp.
- Copy and content: unchanged. Toggle values and key bindings in the captures reflect live persisted preferences and are not visual regressions.

## Findings

- P0: none.
- P1: none.
- P2: none.
- P3: none.

## Verification

- Isolated `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors.
- Direct3D 11 settings preview: exited normally and captured at 1600x1000.
- Shared-worktree compilation remains blocked by unrelated in-progress gameplay layout editor types; the settings changes were therefore verified in an isolated worktree.

final result: passed

---

# Settings interaction stability verification (2026-07-31)

## Evidence

- Reported button defect: `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-4c241004-208a-41b3-8a2d-ffa7690c3b7b.png`
- Final implementation: `C:\Users\mochi\AppData\Local\Temp\yokko-settings-interaction-stable-final.png`
- Full-view comparison: `C:\Users\mochi\AppData\Local\Temp\yokko-settings-interaction-final-comparison.png`
- Focused button comparison: `C:\Users\mochi\AppData\Local\Temp\yokko-settings-button-position-comparison.png`
- Viewport: final implementation 1600x1000 at 1x. The 968x942 defect image is a focused 2x crop and was normalized to 484x471 before comparing the same sidebar region.
- State: Settings > Gameplay > Feedback, Chinese locale, large UI scale.

## Comparison history

1. P1 interaction defect: pressing the Back button animated the entire button to absolute `Y=2`, moving it from its 182px layout slot over the logo. The release animation then left it at absolute `Y=0`.
2. The press animation now records the button's actual resting Y, moves only 2px relative to it, and restores that exact resting position on release.
3. P1 interaction defect: the Gameplay content scroll container allowed elastic clamp extension even when its content exactly matched the viewport, so wheel input could pull the entire panel into a blank state.
4. Clamp extension is now zero. Wheel events are rejected and the container is reset to the top when the scrollable extent is at most 0.5px; genuinely overflowing future content remains scrollable.

## Required fidelity review

- Fonts and typography: unchanged; all labels retain the existing family, weights, sizes, line height, and wrapping.
- Spacing and layout rhythm: the Back button remains in its authored 182px slot through press feedback; the Gameplay panel stays pinned to its frame when content fits.
- Colors and visual tokens: unchanged.
- Image quality and asset fidelity: existing logo and decorations remain intact and sharp; no new or replacement assets were introduced.
- Copy and content: unchanged.

## Findings

- P0: none.
- P1: none after the two interaction fixes.
- P2: none.
- P3: none.

## Verification

- Focused regressions: 2 passed, 0 failed.
- Scroll regression covers non-overflow rejection, real overflow scrolling, and section-change reset.
- Button regression locks the pressed position to 184px for the authored 182px resting slot.
- Isolated `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors.
- Direct3D 11 settings preview: exited normally and captured at 1600x1000.
- The shared worktree became temporarily uncompilable because of an unrelated concurrent layout-editor edit, so final focused verification was repeated in an isolated worktree at the current committed baseline.

final result: passed

---

# Gameplay layout editor Yokko style verification (2026-07-31)

## Evidence

- Source visual truth: `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-738f6afe-51bf-4566-8e9b-ef56cc12bd5a.jpg`
- Previous editor structure reference: `D:\yokko\artifacts\gameplay-layout-editor\layout-editor-resize-implementation.png`
- Final native implementation: `D:\yokko\artifacts\gameplay-layout-editor\layout-editor-chill-round-final.png`
- Side-by-side visual-language comparison: `D:\yokko\artifacts\gameplay-layout-editor\homepage-style-reference-left-chill-round-editor-right.png`
- Source pixels: 1920x1200. Implementation pixels and CSS viewport: 1600x1000 at 1x. The source was normalized to 1600x1000 before the 3200x1000 side-by-side comparison.
- State: paused four-key gameplay with the HUD layout editor open, Chinese locale, timing bar moved and resized.

## Comparison history

1. P1 visual-language mismatch: the first editor used nearly black toolbars and panels, thin generic cyan outlines, and debugger-like microcopy. It worked but did not belong to the white/navy/cyan/yellow Yokko home system.
2. The top toolbar was rebuilt as an ivory card with navy border, cyan edge and shadow, yellow corner node, Roboto/Yokko typography, and home-style secondary/primary actions with Font Awesome icons.
3. The preview was rebuilt as an ivory/navy card while preserving the real dark gameplay thumbnail. This keeps the preview readable without falsely turning gameplay into a light theme.
4. Transform labels now use compact ivory/navy badges. Corner handles use Yokko yellow and edge handles use pale cyan, both with navy borders, so corners and single-axis edges are visually distinguishable.
5. Top and bottom cover handles now use ivory cards with navy type and cyan/pink semantic edge accents. No illustration, custom SVG, emoji, or ornamental raster asset was added.
6. Post-fix native capture at 1600x1000 shows the toolbar, all three target frames, all 24 handles, cover bars, and full-page preview without overlap or clipping.
7. P1 text-rendering defect: the first styled capture displayed `?` for `局`, `拉`, `素`, and `页` because new inline Chinese copy bypassed Yokko's subsetted localisation font source.
8. All editor copy now comes from `YokkoStrings`, the UI explicitly uses the `Yokko` family, and both Regular and Bold atlases were regenerated from Chill Round Gothic v3.75. The final native capture contains no replacement question marks.

## Required fidelity review

- Fonts and typography: editor titles, hints, labels, and actions explicitly use the rounded `Yokko` / `Yokko-Bold` family generated from Chill Round Gothic v3.75. Chinese no longer depends on Roboto fallback, and all glyphs in the English, Chinese, and Japanese localisation tables are test-covered.
- Spacing and layout rhythm: 8px card radii, 1.5px navy borders, cyan offset shadows, compact 56px toolbar, and consistent internal padding match the home control proportions without covering the playfield.
- Colors and visual tokens: all editor chrome now reuses `HomeControlColours.Navy`, `Cyan`, `PaleCyan`, `Yellow`, `Pink`, and `Ivory`. The gameplay canvas intentionally remains dark.
- Image quality and asset fidelity: no image asset was required for this functional overlay. Existing gameplay rendering remains live and sharp; standard controls use the bundled Font Awesome icon set.
- Copy and content: labels are concise Chinese product copy: HUD layout, drag/resize guidance, reset, save and return, element labels, and full-page preview.

## Findings

- P0: none.
- P1: none after the visual-language rebuild.
- P2: none.
- P3: the small editor labels intentionally remain quieter than the home menu because they sit over active gameplay content.

## Verification

- Focused tests: 41 passed, 0 failed. The set covers both localisation font atlases, the complete layout-editor interaction regression, homepage layout and sticker fitting, immediate language switching, and minimum readable typography across every settings page.
- Direct3D 11 native preview: exited normally and captured at 1600x1000.
- Primary interaction paths retained: move, four-edge resize, four-corner resize, cover adjustment, reset, save and return, and live overview synchronization.

final result: passed

---

# Gameplay Result Overlay Interaction Polish QA4

- Source visual truth: `D:\yokko\docs\design\gameplay\yokko-result-broadcast-selected.png`
- Static native implementation: `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-interactive-static-1600x900.png`
- Score-ribbon hover implementation: `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-interactive-score-hover-1600x900.png`
- Full-view source/static comparison: `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-interactive-comparison-static.png`
- Static/hover state comparison: `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-interactive-comparison-states.png`
- Source pixels: 1672x941, normalized to the authored 1600x900 result stage.
- Native render: Direct3D 11, 1600x1000 raw window at 1x, with the centered authored stage cropped from y=50 through y=949 to an exact 1600x900 comparison image.
- State: completed four-key play with the same score, chart metadata, judgements, and mods. The implementation capture uses the active Chinese locale while the selected visual uses English, so localized copy was excluded from geometry-drift findings.

## Comparison history

1. The selected result composition was already matched in the preceding static implementation pass: the broadcast header, mascot crop, paper score ribbon, asymmetric result grid, mod rail, cyan bottom band, and three action cards preserve the same hierarchy and proportions.
2. This pass retained that resting composition and introduced interaction at component level instead of changing the selected art direction.
3. The score ribbon now has a subtle four-pixel lift and 1.008 scale response. The static/hover comparison shows the response remains inside the authored stage without clipping, overlap, or hierarchy change.
4. Metric cells add a pale-cyan wash, animated underline, and restrained value lift; judgement cells add semantic-colour washes and proportional bottom bars; mod chips lift with a cyan accent; actions reveal shortcut hints and animate icon/chevron position, with a short press response.
5. Focused region comparison was not separately required because the score ribbon and the complete action/metric regions remain legible at the exact 1600x900 full-view size. The dedicated static/hover full-view comparison provides the relevant interaction-state evidence.

## Required fidelity review

- Fonts and typography: the established Yokko result type hierarchy, weights, alignment, numeric scale, and wrapping remain unchanged at rest. Hover transforms affect only the intended values or cards and do not disturb surrounding copy.
- Spacing and layout rhythm: the 1600x900 stage, left/right composition split, ribbon angle, grid alignment, mod rail, cyan footer, and action spacing remain visually aligned with the selected source. Interaction motion is bounded and causes no reflow.
- Colors and visual tokens: all new states reuse the existing navy, cyan, pale-cyan, yellow, pink, ivory, and judgement colours. There is no foreign glow, generic grey overlay, or new gradient language.
- Image quality and asset fidelity: the real transparent Yokko mascot and result-ribbon resource remain sharp and correctly cropped. No illustration, sticker, decorative mark, or non-standard icon was replaced by code-native approximation.
- Copy and content: result metadata, score, accuracy, combo, judgements, mods, and action labels remain live code-rendered content. Shortcut hints are hidden at rest and revealed only on pointer focus, preventing extra visual noise.

## Findings

- P0: none.
- P1: none.
- P2: none.
- P3: only the score-ribbon hover state has a dedicated native screenshot in this pass. Metric, judgement, mod, action-hover, and action-press behavior compiled and is exercised through the same pointer-input implementation, but each state was not captured as a separate image.

## Verification

- Isolated artifact build for the interaction snapshot: 0 warnings, 0 errors.
- Focused tests: 26 passed, 0 failed in 4 seconds. Coverage included `TestSceneGameplayResultOverlay`, completed-play result-overlay behavior, and display settings.
- Native Direct3D 11 static and hover captures completed successfully at the authored 1600x900 content size.
- A later broad rebuild of the concurrently edited shared tree was blocked by unrelated in-progress changes in `SkinSettingsPanel.cs` and a `GameplayScrollVelocityTest`/`DrawableNote` API mismatch; those files were not changed for this result-overlay pass.

final result: passed

---

# Gameplay Mods footer option 1 QA (2026-07-31)

## Evidence

- User-supplied production reference:
  `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-801629d3-256a-4ba5-8334-4447d9bd8b38.png`
- Selected source visual:
  `C:\Users\mochi\.codex\generated_images\019fb72c-4c90-73c0-8b44-35807879ec2f\call_IHh6LBcdS7126znzk0JEpQ4H.png`
- Final native implementation:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb72c-4c90-73c0-8b44-35807879ec2f\mods-footer-option1-compact-grouped-final.png`
- Same-input focused comparison:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb72c-4c90-73c0-8b44-35807879ec2f\mods-footer-option1-compact-grouped-comparison.png`

## Viewport, density, and state

- The final density check uses the user's 2048 x 202 production footer
  reference, normalized proportionally to 1600px wide without horizontal
  cropping or stretching.
- Native implementation: 1600 x 1000 at 1x. The real footer occupies the
  exact 1600 x 130 bottom region.
- The source is an intentionally tall concept study. Production preserves its
  hierarchy and component language inside Yokko's real 130px footer instead
  of increasing the footer and reducing gameplay workspace height.
- State: Chinese locale, Difficulty Increase page, active gameplay Mods, and
  1.00x rate.

## Comparison history

1. The first implementation preserved only the source palette and three-part
   arrangement. User review correctly rejected it as P1 design drift: Back was
   330 x 88, Reset only 84 x 84, and Done 410 x 94, leaving the production
   footer much flatter, emptier, and more generic than the selected concept.
2. The rejected sizes and decoration system were replaced rather than tuned:
   Back is now 430 x 104, Reset 102 x 102, and Done 500 x 106. Back and Done
   typography increased to 46 and 50, with 52px Undo and 48px Play icons.
3. Source-specific visual structure was restored: tall ESC tile, strong corner
   cuts, navy surface depth, outer white guide frames, yellow top markers,
   pink primary underline, and diagonal technical hatch marks.
4. The first faithful native capture still lacked the source's lower hatch
   rhythm. The second pass added white hatches under Back/Reset and cyan
   hatches inside Done.
5. The final combined comparison shows the same left/centre/right proportions,
   control hierarchy, labels, icon scale, accents, and frame language within
   the real 1600 x 130 production footer.
6. A subsequent typography pass overcorrected the labels to 62/66 inside
   430 x 104 and 500 x 106 cards. User review correctly identified the result
   as P1 scale imbalance: wide, shallow controls with display text that felt
   forced into the available height.
7. The final proportion pass treats each control as one system: Back is
   390 x 100 with 52 type and a 70px keycap, Reset is 96 x 96 with a 48px
   icon, and Done is 460 x 102 with 56 type and a 44px Play icon.
8. Labels are optically centred between their icon/keycap and chevron rather
   than aligned to a fixed left offset. Corner-cut overlays are clipped inside
   the button bounds and matched to the footer gradient, removing the visible
   diamond seams found during the zoomed detail review.
9. User review found the balanced pass still occupied too much footer area.
   Reducing both controls and type together produced a 320 x 88 / 410 x 92
   intermediate pass, but repeated the unwanted large-frame/small-copy ratio.
10. The final pass intentionally decouples footprint from emphasis: Back is
    260 x 78 with 52 type, Reset is 72 x 72 with a 42px icon, and Done is
    350 x 82 with 58 type. The controls now leave the footer breathing room
    while the two action labels remain the first readable elements.
11. User review still found that pass too wide and empty. The accepted compact
    pass reduces Back to 220 x 70 and Done to 280 x 72 while retaining 50/56
    type. Reset becomes a 60 x 60 icon button and moves beside Done with a
    14px gap. Both labels move down 4px for optical centring.
12. The final cleanup removes the redundant white guide frames surrounding
    Back and the Reset/Done group. Each control keeps only its own functional
    border, so the footer no longer reads as a button nested inside a second
    container.

## Required fidelity review

- Typography: Back and Done now use large bold Chinese labels. Reset contains
  no redundant text, matching the selected source and user request.
- Spacing and layout: Back anchors the left. Reset and Done form one compact
  right-side action group, with Done remaining visually dominant. All controls
  are vertically centred within the real footer.
- Colors and tokens: the implementation uses Yokko's production cyan, ivory,
  navy, pink, and yellow palette.
- Icons and assets: Play, Undo, and Chevron use the project's standard Font
  Awesome icon source. Their sizes and semantic colors match the selected
  visual hierarchy.
- Shape language: ivory/navy surfaces use clipped corner cuts, a pink primary
  underline, and restrained yellow/pink markers without an extra outer frame.
- Copy and interaction: Back and Done retain live localized labels; Reset is
  icon-only with its existing action behavior. Hover and press feedback remain
  component-native and do not reflow the footer.

## Findings

- P0: none.
- P1: none.
- P2: none after the oversized-control and small-copy intermediate passes were
  replaced.
- P3: the source concept has a very subtle cyan lighting gradient; production
  keeps the shared solid Yokko cyan token so the footer remains consistent
  with other screens.

## Verification

- Isolated `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors.
- Focused responsive footer tests: 4 passed, 0 failed.
- Native Direct3D 11 capture: 1600 x 1000, Chinese locale.
- The selected source and production footer were reviewed together in the
  comparison image above.

final result: passed

# Gameplay Result Title Clearance QA5

## Evidence

- Source visual truth:
  `D:\yokko\docs\design\gameplay\yokko-result-broadcast-selected.png`
  (1672 x 941, normalized to 1600 x 900).
- Reported implementation defect:
  `C:\Users\mochi\AppData\Local\Temp\codex-clipboard-c198cddf-58ae-4ef4-b452-53408e2f856b.png`
  (426 x 83 focused crop; the yellow underline crosses the italic title).
- Revised implementation capture:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-title-clearance-final.png`
  (1600 x 900, Direct3D 11, English result state).
- Full-view side-by-side comparison:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-fidelity-comparison-final.png`.
- Focused title comparison:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-title-comparison-final.png`.
- Viewport/state: 1600 x 900, density 1, English locale, Afterimage
  [Insane], score 0537761, B rank, HD + DT 1.50x.

## Comparison history

1. The earlier title used a hard-coded underline Y of 226. Roboto Bold Italic's
   rendered glyph box extends below that estimate, so the 6px yellow rule
   visibly cut through the title descenders.
2. The title and underline are now owned by one `ResultSongHeading`. After the
   font is loaded, the underline is placed from the measured `DrawHeight` plus
   a 6px optical clearance instead of a guessed baseline.
3. The revised 1600 x 900 Direct3D capture and focused side-by-side image show
   a clean gap below `Afterimage [Insane]`, with the title's X position, scale,
   weight, italic treatment, underline width, and surrounding composition
   preserved.

## Required fidelity review

- Fonts and typography: Roboto Bold Italic, 48px, truncation width, weight,
  slant, and hierarchy remain unchanged; only unsafe baseline placement was
  removed.
- Spacing and layout: the underline now follows measured glyph height and
  keeps 6px clearance. The RESULT header, ribbon, and summary rail do not move.
- Colors and tokens: navy title and Yokko yellow underline remain identical to
  the selected result palette.
- Image quality and assets: the full-screen atmosphere and mascot assets retain
  their existing crop, scale, sharpness, and transparency treatment.
- Copy and content: `Afterimage [Insane]`, result values, mods, judgments, and
  actions are unchanged.
- Interaction/state: score-ribbon, metric, judgment, mod-chip, and action hover
  states remain functional; the title fix does not alter hit targets.

## Findings

- P0: none.
- P1: none.
- P2: none after replacing the fixed underline coordinate.
- P3: the implementation intentionally leaves a clearer optical gap than the
  raster reference because the user explicitly rejected glyph obstruction.

## Verification

- Clean `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors.
- Focused result/display tests: 27 passed, 0 failed.
- Regression assertion: `SongTitleUnderlineClearance >= 6` passed.
- Native Direct3D 11 render opened and compared in one combined image with the
  normalized selected source; no actionable P0/P1/P2 difference remains for
  this fix.

final result: passed

# Gameplay Result Retry Typography QA6

## Evidence

- Source visual truth:
  `D:\yokko\docs\design\gameplay\yokko-result-broadcast-selected.png`
  (1672 x 941, normalized to 1600 x 900).
- Revised implementation capture:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-retry-font-final.png`
  (1600 x 900, Direct3D 11, English result state).
- Full-view side-by-side comparison:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-retry-font-comparison.png`.
- Focused Retry comparison:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-retry-font-focus.png`.
- Viewport/state: 1600 x 900, density 1, English locale, Afterimage
  [Insane], score 0537761, B rank, HD + DT 1.50x.

## Comparison history

1. The previous Retry label used Roboto Bold at display size 60 with a
   non-uniform `1.4 x 1.08` scale. The horizontal stretch distorted the bowls,
   terminals, and letter spacing, producing the user-reported unnatural type.
2. The revised label keeps Roboto Bold but uses the shared display size 50 at
   natural `1 x 1` proportions. Its Y position moves from 12 to 18 so the
   smaller natural glyph box remains optically centred inside the 95px face.
3. The focused side-by-side comparison shows the source and implementation now
   have matching word width, weight, baseline, and surrounding icon/chevron
   rhythm without altering button dimensions.

## Required fidelity review

- Fonts and typography: Roboto Bold is rendered at its natural aspect ratio;
  there is no horizontal or vertical glyph deformation, wrapping, or clipping.
- Spacing and layout: the 405 x 95 primary button, 66px icon tile, chevron,
  border, and action-row position remain unchanged. Only the label's optical Y
  placement changed.
- Colors and tokens: white label, navy surface, yellow chevron, and cyan dot
  texture retain the selected Yokko result tokens.
- Image quality and assets: mascot, atmosphere, logo, and ribbon assets remain
  unchanged and preserve their source-matched crop and sharpness.
- Copy and content: Retry and all result data/mod/action labels are unchanged.
- Interaction/state: click action, hover lift, press scale, chevron motion, and
  key-hint behavior remain active with the same hit target.

## Findings

- P0: none.
- P1: none.
- P2: none after removing non-uniform font scaling.
- P3: minor raster-antialiasing differences between the selected mock and the
  Direct3D text renderer are expected and do not alter the letterforms.

## Verification

- Clean `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors.
- Focused result overlay and completion-flow tests: 3 passed, 0 failed.
- Native Direct3D 11 1600 x 900 capture opened and reviewed together with the
  normalized source in both full-view and focused comparison images.

final result: passed

# Gameplay Result Position, Motion, and Accent QA7

## Evidence

- Base visual truth:
  `D:\yokko\docs\design\gameplay\yokko-result-broadcast-selected.png`
  (1672 x 941, normalized to 1600 x 900).
- User-directed delta: move the mascot slightly right and add restrained Yokko
  motion plus decorative accents without changing the result layout.
- Revised implementation capture:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-motion-final.png`
  (1600 x 900, Direct3D 11, English result state).
- Source/implementation full-view comparison:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-motion-comparison.png`.
- Two-phase motion evidence:
  `C:\Users\mochi\.codex\visualizations\2026\07\31\019fb613-75f6-7de3-9f31-84e1dd164324\yokko-result-motion-frames.png`.
- Viewport/state: 1600 x 900, density 1, English locale, Afterimage
  [Insane], score 0537761, B rank, HD + DT 1.50x.

## Comparison history

1. The prior mascot centre was X 272 and used only a vertical float. Decorative
   marks and the score-ribbon signal line remained static.
2. The mascot rest centre is now X 290, an 18px rightward adjustment. It keeps
   the selected scale and crop while adding low-amplitude X/Y drift and a
   `-0.35` to `0.35` degree breathing rotation.
3. Four Font Awesome star/plus accents were placed in existing negative space
   around the mascot, title, and judgment rail. They use staggered fade, float,
   and rotation loops in Yokko cyan, pink, and yellow.
4. A 72 x 3 cyan signal runner now travels along the score ribbon's existing
   top rail. It reinforces the broadcast/signal language without crossing text.
5. The two-phase capture confirms the mascot, accents, and runner move while
   labels, result data, controls, and layout anchors remain stable.

## Required fidelity review

- Fonts and typography: RESULT, song title, score, metrics, judgments, and
  action labels retain their verified families, natural proportions, size,
  hierarchy, truncation, and line spacing.
- Spacing and layout: the mascot moves right by 18px only. The logo, IV stand,
  ribbon, summary rail, judgments, and action row retain their 1600 x 900
  production positions with no clipping or control overlap.
- Colors and tokens: new accents reuse the existing result cyan, pink, yellow,
  soft cyan, and navy tokens; no new visual language was introduced.
- Image quality and assets: the formal mascot, atmosphere, logo, and ribbon
  raster assets remain unchanged. Motion transforms the mascot drawable rather
  than regenerating, stretching, or degrading the image.
- Copy and content: all result data, mods, judgments, and action labels are
  unchanged.
- Interaction/state: Retry, replay, return, score ribbon, metrics, judgments,
  and mod-chip hover/press behavior remain functional. Ambient motion does not
  capture input or change hit targets.

## Findings

- P0: none.
- P1: none.
- P2: none after the full-view and two-phase motion reviews.
- P3: Yokko does not currently expose a global reduced-motion preference; the
  added loops therefore use deliberately low amplitude and slow timing.

## Verification

- Isolated known-good `Yokko.Game.Tests` build with the exact result overlay:
  passed with 0 warnings and 0 errors.
- Focused result overlay and completion-flow tests: 3 passed, 0 failed.
- Shared-tree broad build is currently blocked by unrelated concurrent edits in
  `JudgementReadout.cs` and `GameplayScreen.cs`; these files were not changed by
  this result-screen pass.
- Native Direct3D 11 1600 x 900 final frame and a second animation phase were
  opened and compared; no actionable P0/P1/P2 visual issue remains.

final result: passed

---

# Home mascot bubble text safe-area QA (2026-07-31)

## Evidence

- Source visual truth: `C:\Users\nyafa\AppData\Local\Temp\codex-clipboard-67a38c81-05fe-41bb-aa4a-e69c74723681.png` (1063 x 572, 1x screenshot).
- Native implementation: `D:\YOKKO\artifacts\bubble-safe-area-preview\implementation-dfjk-final.png` (1920 x 1080, 1x Direct3D 11 capture).
- Full-view comparison: `D:\YOKKO\artifacts\bubble-safe-area-preview\comparison-full-final.png`.
- Focused normalized comparison: `D:\YOKKO\artifacts\bubble-safe-area-preview\comparison-dfjk-final.png`; each component crop was aspect-fitted into a 560 x 360 panel so the bubble boundaries could be judged at a common display size.
- State: Chinese locale, idle line `D F J K，出发！`.
- CSS size and browser density: not applicable to this native osu!framework screen.

## Comparison history

1. The reported capture showed the first `D` extending into the lower-left star because the 148-unit label was centred too far left.
2. The first correction moved the label right, but the native comparison showed the final punctuation touching the lightning-side border.
3. The final correction centres the label in the irregular cyan safe area and caps it at 134 units. The second native capture shows clear spacing at both ends.

## Fidelity review

- Fonts and typography: the existing sticker font, weight, baseline, letter spacing, and CJK fallback are unchanged; only fit scale and centring changed.
- Spacing and layout: the label now stays between the star cluster and right lightning border. Bubble placement, size, mascot overlap, and animation are unchanged.
- Colors and tokens: unchanged.
- Image quality and assets: the project-owned transparent sticker texture is unchanged and remains sharp in the native capture.
- Copy and content: the exact reported localized line was rendered and compared.
- Interaction and responsiveness: click/pop/idle transforms are unchanged; the safe-area limits are asserted after text reflow.

## Findings

- P0: none.
- P1: none.
- P2: none after the second safe-area pass.
- P3: the source and final full views use different viewport sizes; fidelity judgement therefore relies on the normalized focused component comparison rather than pixel-position comparison of the full page.

## Verification

- Isolated `Yokko.Game.Tests` build passed with 0 warnings and 0 errors.
- `TestBubbleStickerLabelFitsInsideSticker` passed: 1 test, 0 failures.
- Final native Direct3D 11 capture and both comparison images were opened and inspected.

final result: passed

---

# Gameplay pause performance typography and pause-count QA (2026-07-31)

## Evidence

- Source visual truth: `C:\Users\nyafa\AppData\Local\Temp\codex-clipboard-4b8b466d-0fd1-40a5-b75a-d919db463417.png` (2048 x 1152).
- Revised native implementation: `D:\YOKKO\artifacts\pause-polish-preview\implementation.png` (1920 x 1080, Direct3D 11).
- Full comparison, reported state left and revised state right: `D:\YOKKO\artifacts\pause-polish-preview\before-left-after-right.png`.
- Focused logo and metrics comparison: `D:\YOKKO\artifacts\pause-polish-preview\focused-before-left-after-right.png`.
- State: Chinese pause overlay at Comfortable scale. The implementation fixture uses a long title, 100.00% accuracy, SS rank, and pause count 03 to exercise the widest common values.

## Comparison history

1. The reported state used an opaque logo texture at a smaller effective height, making the brand lockup look soft and vertically weak.
2. Accuracy and rank both used Archivo Black poster typography. The oversized accuracy value crowded its percent sign and the rank letter looked heavier than the surrounding home-page typography.
3. The revised state uses the existing high-resolution transparent logo asset at a larger slot, and both accuracy and rank now use the shared Roboto Bold display family at natural proportions.
4. Accuracy and rank have separate aligned columns with a protected divider. SCORE, COMBO, and PAUSES form a balanced three-column rail, with the live gameplay pause count rendered as a two-digit value.
5. The final native capture shows no overlap for `100.00%`, `SS`, `41 / 41`, or `03`; the mascot, controls, judgment ledger, and song header remain unobstructed.

## Required fidelity review

- Fonts and typography: the logo remains a real brand raster; performance values now use the same Roboto Bold family as the home UI, with natural glyph proportions and a smaller optical percent sign.
- Spacing and layout rhythm: the top metrics are split by a stable vertical rule, while the lower rail uses three evenly separated cells without crossing the mascot boundary.
- Colors and tokens: existing navy, cyan, ivory, yellow, and pink tokens are unchanged.
- Image quality and assets: the opaque `home-logo-hd` texture was replaced by the existing 2149 x 731 transparent brand asset; no logo was redrawn or approximated.
- Copy and content: SCORE and COMBO are retained; PAUSES is added as a concise peer metric backed by the existing gameplay pause counter.
- Interaction and state: the pause/resume flow, keyboard selection, and audio pause/seek behavior passed the focused native test. Failed audio pauses do not commit the displayed count to gameplay state.

## Findings

- P0: none.
- P1: none after the typography and metric-column changes.
- P2: none in the 1920 x 1080 native capture and focused comparison.
- P3: the pause overlay intentionally retains its documented 1600 x 900 internal artboard and fits it uniformly inside the shared 1920 x 1080 viewport.

## Verification

- Clean isolated `Yokko.Game.Tests` build: 0 warnings, 0 errors.
- `TestPauseOverlayStopsAndResumesAudio`: 1 passed, 0 failed, including the new `DisplayedPauseCount == 1` assertion.
- Native Direct3D 11 preview captured at 1920 x 1080 and opened together with the normalized reported screenshot.
- The main checkout build is currently blocked by unrelated concurrent `YokkoGame.cs` edits missing `FrameworkConfigManager` and `WindowMode` references; the isolated validation contains only the pause-page delta.

final result: passed

---

# Gameplay pause countdown quick-settings QA (2026-07-31)

## Evidence

- Selected expanded-state visual target: `C:\Users\nyafa\.codex\generated_images\019fb86c-308e-7681-9214-4873d2cbb4f7\exec-a4184355-8ea4-4351-bb6c-1c62f1d33ec5.png`.
- Native Direct3D 11 implementation: `D:\YOKKO\artifacts\pause-countdown-preview\expanded-final.png` (1920 x 1080).
- Same-input comparison, selected target left and implementation right: `D:\YOKKO\artifacts\pause-countdown-preview\source-left-implementation-right.png`.
- State: Chinese pause overlay, Pause Settings expanded, Resume Countdown set to 1 second.
- Density normalization: the generated target was resampled to 1920 x 1080 without cropping; the implementation remained at native 1x output.

## Comparison history

1. The first native capture rendered the value outside the drawer because its centre anchor also received an absolute X offset.
2. The value now uses a direct local centre position between the decrement and increment buttons, restoring the selected target's compact selector rhythm.
3. The generated target included a second volume control. The implementation intentionally omits it after product review established that gameplay audio is already stopped while paused.

## Required fidelity review

- Fonts and typography: localized labels use the existing sharp Yokko/Roboto display chain at natural scale; the numeric value is centred and unwarped.
- Spacing and layout: the drawer opens upward into negative space, remains inside the left paper panel, and does not cover Resume, the PAUSED timer, the song header, or performance data.
- Colors and tokens: ivory, navy, cyan, pale cyan, and pink reuse the production pause palette.
- Image quality and assets: the existing logo and mascot remain unchanged; standard Font Awesome controls are used for sliders, plus, minus, and chevron.
- Copy and content: only the requested Resume Countdown setting is present. English, Chinese, and Japanese labels are supplied through Yokko localization.
- Interaction and accessibility: mouse click or Tab toggles the drawer; plus/minus buttons and Left/Right adjust Off, 1, 2, and 3 seconds; the pause key closes an open drawer before resuming.

## Findings

- P0: none.
- P1: none after correcting the missing duration value.
- P2: none in the final same-viewport comparison.
- P3: the production drawer is deliberately one row shorter than the generated target because the rejected volume option was removed.

## Verification

- `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors.
- `TestPauseOverlayStopsAndResumesAudio`: 1 passed, 0 failed; it now verifies open, live duration update, close, and restoration.
- Native Direct3D 11 expanded-state preview captured and compared at 1920 x 1080.

final result: passed

---

# Gameplay pause secondary-action spacing QA (2026-07-31)

## Evidence

- Reported state: `D:\YOKKO\artifacts\pause-countdown-preview\expanded-final.png` (1920 x 1080, Direct3D 11).
- Revised native implementation: `D:\YOKKO\artifacts\pause-action-polish\implementation.png` (1920 x 1080, Direct3D 11).
- State: Chinese pause overlay with Pause Settings expanded and all four secondary actions visible.

## Required fidelity review

- Typography and spacing: the four secondary actions now use a centred vertical icon-and-label stack, giving four-character labels their full cell width instead of sharing it with the icon.
- Hierarchy: index, icon, label, and accent occupy separate vertical bands; Resume remains the dominant primary action.
- Consistency: Retry, HUD Layout, Settings, and Exit retain equal hit targets, keyboard order, colours, hover state, and actions.
- Rendering: labels and icons are rendered at natural scale in the native 1920 x 1080 capture, with no clipping or overlap.

## Findings

- P0: none.
- P1: none.
- P2: none after replacing the cramped horizontal content layout.
- P3: none.

## Verification

- `Yokko.Game.Tests` isolated build: passed with 0 warnings and 0 errors.
- Native Direct3D 11 preview captured and inspected at 1920 x 1080.
- `git diff --check`: passed.

final result: passed

---

# Gameplay pause detail enhancement QA (2026-07-31)

## Evidence

- Current-state capture: `D:\YOKKO\artifacts\pause-detail-audit\01-current.png` (1920 x 1080, Direct3D 11).
- Revised implementation: `D:\YOKKO\artifacts\pause-detail-audit\02-polished.png` (1920 x 1080, Direct3D 11).
- Same-state comparison: `D:\YOKKO\artifacts\pause-detail-audit\before-left-after-right.png` (before left, revised right).
- State: Chinese pause overlay with Pause Settings expanded and the primary Resume action selected.

## Required fidelity review

- Settings hierarchy: the countdown drawer gains a restrained paper shadow and a cyan connector to its trigger, making the expanded relationship explicit.
- Action grouping: a small `QUICK ACTIONS` label and hairline separate the primary Resume action from the four secondary actions.
- Focus feedback: secondary keyboard selection gains a dedicated top marker in addition to its existing background and accent-width transition.
- Typography and density: the new micro-label uses the existing display family and cyan/navy tokens; it fits the existing vertical gap without moving controls.
- Interaction: drawer open/close transitions now animate the shadow and connector together with the existing card motion.

## Findings

- P0: none.
- P1: none.
- P2: none in the final 1920 x 1080 comparison.
- P3: the secondary focus marker is not visible in the primary-selected evidence frame; its state is covered by the existing selection code path and focused interaction test.

## Verification

- `Yokko.Game.Tests` isolated build: passed with 0 warnings and 0 errors.
- `TestPauseOverlayStopsAndResumesAudio`: 1 passed, 0 failed, including the settings and secondary-selection paths.
- Native Direct3D 11 preview and same-state comparison were opened and inspected.

final result: passed
