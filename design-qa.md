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

# Song Select standalone footer tools QA (2026-08-01)

## Evidence

- Source visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1672 x 941, normalized to 1920 x 1080 for comparison).
- Same-state baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-large-covers-v1.png` (1920 x 1080, native Direct3D 11).
- Revised implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-footer-standalone-v1.png` (1920 x 1080, native Direct3D 11).
- Full before/after comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-footer-standalone-before-after-v1.png`.
- Full source/implementation comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-footer-standalone-reference-v1.png`.
- Focused source/before/after footer comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-footer-standalone-focused-v1.png`.
- Viewport and density: implementation and baseline are native 1920 x 1080 captures of the shared 1920 x 1080 stage at 1x. The selected 16:9 source was resized once to the same pixel dimensions before the full and focused comparisons.
- State: English UI, Comfortable scale, 7K filter, one selected expanded package, three resting footer tools.

## Comparison history

1. Baseline P2: `MODS`, `RANDOM`, and `OPTIONS` were individually rounded but also sat inside a 560 x 94 pale-cyan rounded panel with its own border and shadow. This duplicated the surface hierarchy and repeated the same extra-wrapper problem already removed from the right browser.
2. Implemented: the shared visible panel and shadow were removed. The three controls retain their responsive group container only for layout and now each receives a quiet 14% navy shadow matching the standalone Back, account, and Play cards.
3. The profile status no longer relies on a bullet text glyph. It uses the existing Font Awesome circle icon plus separate `ONLINE` copy, avoiding replacement-glyph risk without adding an asset or decorative cutout.
4. Post-fix evidence: the focused three-row board shows three discrete controls with clean gaps and consistent baselines. The full source/implementation board confirms that the footer now follows the reference's sequence of independently framed Back, profile, tools, and Play surfaces.

## Required fidelity review

- Fonts and typography: existing Yokko display typography, button labels, account metrics, and Play hierarchy are unchanged. Separating the status icon from `ONLINE` preserves its size and baseline while removing glyph dependency.
- Spacing and layout rhythm: all three tool targets remain 176 x 82 at Comfortable scale and 126 x 82 at Large scale. Existing 8 px gaps and the 560 x 94 responsive layout allocation remain stable; only the visible enclosing card was removed.
- Colours and visual tokens: the redundant pale-cyan group fill and cyan border are gone. Ivory controls, navy borders, cyan/pink accents, and yellow Play remain aligned with the selected reference and Yokko's existing home palette.
- Image quality and asset fidelity: no bitmap, screenshot slice, generated decoration, resource-library cutout, or new asset was introduced. The existing small diamond and tape decorations remain untouched.
- Copy and content: `MODS`, `RANDOM`, `OPTIONS`, `BACK`, `PLAY`, account values, and action behavior are unchanged. `ONLINE` remains the same semantic copy.
- Icons and affordances: each action keeps its existing Font Awesome icon and text label. The online indicator now also uses Font Awesome rather than a text symbol.
- Interaction and responsiveness: Mods still opens the gameplay-Mod flow, Random still changes selection, Options still opens Settings, and each remains independently hoverable. The Large/Comfortable width switch is covered by the existing geometry regression.

## Findings

- P0: none.
- P1: none.
- P2: none after removing the shared visible wrapper and restoring per-button elevation.
- P3: the implementation account card is wider than the selected concept because it exposes three live account metrics and level progress. This is an intentional content-preserving deviation and does not reintroduce a nested surface.

## Verification

- Isolated `Yokko.Game.Tests` build in `artifacts/song-select-footer-standalone`: 0 warnings and 0 errors.
- Focused `TestSceneSongSelectScreen`: 11 passed, 0 failed, 0 skipped, including the real Options destination and the three-standalone-card geometry assertion.
- Native Direct3D 11 capture exited normally at 1920 x 1080.
- The selected reference and implementation were opened together in the full comparison; the source/before/after footer crop was also opened at original detail.
- `git diff --check` passed after implementation.

final result: passed

## Song Select browse tools and top navigation QA (2026-08-01)

### Evidence

- Source visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` at 1672 x 941.
- Same-state baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-row-hierarchy-v2.png` at 1920 x 1080.
- Browse-tool implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-toolbar-cards-v1.png` at 1920 x 1080.
- Revised top-navigation implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-top-nav-v1.png` at 1920 x 1080.
- Focused toolbar source comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-toolbar-cards-reference-focused-v1.png`.
- Focused toolbar before/after: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-toolbar-cards-focused-before-after-v1.png`.
- Focused top-navigation source comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-top-nav-reference-focused-v1.png`.
- Focused top-navigation before/after: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-top-nav-focused-before-after-v1.png`.

### Comparison history

1. Toolbar baseline: Sort, Group, Library, and Converts were drawn as one uninterrupted segmented strip. The Converts state became a large cyan block, separators competed with labels, and `CONVERTS`/`SHOWN` visually collided.
2. Toolbar revision: four independent 34 px cards use 7 px radii, one-pixel neutral borders, 8 px gaps, consistent icon/label/value alignment, and a restrained active wash. The range marker and rainbow rail were reduced so the filter hierarchy starts at search rather than at the colour bar.
3. Top-navigation baseline: the 2149 x 731 brand texture was placed in a 152 x 39 slot, visibly flattening the lockup and making its subtitle unreadable. Icons had no group rhythm, and the username/avatar/bell floated as unrelated elements.
4. Top-navigation revision: the brand uses a proportional 168 x 57 slot; the bar is 72 px high; primary and system icons use consistent 48 px slots with quiet separators; the selected music item receives one yellow disc and a small pink indicator; and account state is consolidated into a rounded 172 x 46 profile capsule with a separate notification button.

### Required fidelity review

- Fonts and typography: the existing Yokko logo texture is shown at its native aspect ratio rather than recreated or distorted. Dynamic toolbar values keep their existing type family and weights.
- Spacing and layout rhythm: header, search, difficulty range, browse controls, and song browser now step through 10, 10, 8, and 6 px vertical gaps. Navigation icons retain even slots and explicit group separators.
- Colours and visual tokens: navy, ivory, cyan, yellow, and pink remain the only emphasis colours. No sci-fi glow, oversized glass panel, or dark HUD treatment was introduced.
- Image quality and asset fidelity: no new cutout or large bitmap panel was added. The existing logo and avatar source remain the only image assets in the top bar.
- Copy and content: Sort, Group, Library, Converts, filter values, username, and all dynamic states remain unchanged.
- Icons and affordances: existing Font Awesome icons remain recognizable; selection is redundant through both disc and underline; the notification dot is kept small.
- States and interactions: Sort, Group, and Converts remain interactive; search, key filters, difficulty filtering, and keyboard navigation are unchanged and covered by the focused screen suite.
- Accessibility and viewport resilience: the 72 px header and 46 px account target improve legibility at the 1920 x 1080 baseline without reducing browser space or colliding with the search row.

### Findings

- P0: none.
- P1: none.
- P2: none after restoring the logo aspect ratio, grouping navigation, separating toolbar cards, and correcting Converts value spacing.
- P3: several top-navigation icons remain visual-only because their destinations are outside this Song Select implementation pass; this matches the existing behaviour and does not block the song-selection journey.

### Verification

- Isolated `Yokko.Game.Tests` build in `artifacts/song-select-top-nav`: 0 warnings and 0 errors.
- Focused Song Select screen run: 11 passed, 0 failed, 0 skipped.
- Native Direct3D 11 captures completed at 1920 x 1080. Full and focused source comparisons plus same-state before/after boards were opened and inspected at original detail.
- `git diff --check` passed.

final result: passed

# Gameplay layout editor blocker visibility and 1080p readability QA (2026-07-31)

## Evidence

- Source visual truth: `C:\Users\nyafa\AppData\Local\Temp\codex-clipboard-4bdf2997-c59b-4425-bbd4-e15be02485ae.png` (2560 x 1440).
- Revised native implementation: `D:\YOKKO\.artifacts\layout-editor-1080p-after.png` (1920 x 1080).
- Full-view comparison, source left and implementation right: `D:\YOKKO\.artifacts\layout-editor-reference-left-after-right.png`.
- Focused left controls comparison: `D:\YOKKO\.artifacts\layout-editor-left-controls-reference-after.png`.
- Focused right controls comparison: `D:\YOKKO\.artifacts\layout-editor-right-controls-reference-after.png`.
- Viewport/state: shared 1920 x 1080 reference, Comfortable UI scale, Chinese HUD layout editor, top and bottom blockers disabled.
- Density normalization: the 2560 x 1440 source was downsampled to 1920 x 1080 without cropping; the implementation remained at native 1x output. Chart metadata differs between fixtures and was excluded from layout judgment.

## Comparison history

1. The reported state showed the inactive top blocker resize strip across the full playfield, obscuring chart content and retaining a misleading drag affordance.
2. Inactive blocker handles now render at zero alpha and reject mouse, drag, and hover input. Blockers remain addable from the dedicated right-side panel.
3. The reported 1080p-normalized controls used a narrow 264-unit action card, 320-unit right panels, and an 18-unit minimum body size.
4. The revised editor widens the action card and right-side panels, expands numeric fields and overview space, and raises the minimum body typography to 20 while preserving the existing Yokko hierarchy and all 1080p panel bounds.

## Required fidelity review

- Fonts and typography: the existing Yokko regular/bold families are retained; body text is larger and remains centred or baseline-aligned without clipping in the focused control comparisons.
- Spacing and layout rhythm: left and right control cards gain horizontal breathing room; all right-side cards remain vertically separated and the full-page preview stays inside the 1080p viewport.
- Colors and visual tokens: existing navy, cyan, pink, yellow, pale-cyan, and ivory tokens are unchanged; enabled states retain their prior semantic colors.
- Image quality and assets: no imagery or decorative asset was replaced. The native Direct3D 11 capture remains sharp at 1920 x 1080.
- Copy and content: all editor actions, layer labels, live settings, feedback settings, and localized hints remain present. Only inactive in-canvas blocker affordances are intentionally absent.

## Findings

- P0: none.
- P1: none after removing the inactive blocker strip and its input interception.
- P2: none after the 1080p typography and control-width pass.
- P3: the editor remains intentionally dense so the inspector, blocker settings, live settings, and overview can coexist in a single 1080p column.

## Verification

- `Yokko.Game` isolated build: passed with 0 warnings and 0 errors.
- `Yokko.Game.Tests` isolated build: passed with 0 warnings and 0 errors.
- Focused `TestGameplayLayoutEditorPausesAndShowsFullPagePreview`: 1 passed, 0 failed, including blocker add, resize, independent removal, and editor interaction paths.
- Native Direct3D 11 preview captured at 1920 x 1080 and inspected in full-view and focused same-input comparisons.
- `git diff --check` passed for the four edited gameplay layout editor files.

final result: passed

# Song Select left information hierarchy QA (2026-08-01)

## Evidence

- Source visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` at 1672 x 941.
- Previous implementation baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-action-rail-final-v1.png` at 1920 x 1080.
- Revised Direct3D 11 implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-left-rounded-v2.png` at 1920 x 1080.
- Source and implementation comparison board: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-left-reference-comparison-v2.png`.
- Viewport/state: 1920 x 1080, Comfortable density, 7K filter, one focused package, and one selected chart.

## Comparison history

1. Baseline P2: the left column used two unrelated irregular paper textures, an oversized rotated cover, two tape motifs, and low-density micro-stat rows. These layers competed with the ranking data and made the left column look less resolved than the browser.
2. Implemented: the song summary and ranking now use the same 14 px code-drawn rounded surface, restrained 1 px borders, and 2 px low-opacity shadows. The cover is square and unrotated; chart mode and difficulty value share the top metadata axis; facts and personal performance are separated into one compact information rail.
3. Post-fix evidence: the revised capture shows aligned 850 px surfaces, stable 18/22 px internal spacing, no large decorative cutout, and a ranking table that ends immediately after the seventh row. The existing verified mascot is repositioned into the intentional lower-left breathing room as the only large character element.

## Required fidelity review

- Fonts and typography: existing Yokko display/body families remain. The title keeps balanced two-line layout; artist, mapper, chart facts, and performance now form one clear descending hierarchy.
- Spacing and layout rhythm: the selected card is reduced from 255 px to 240 px; ranking begins 20 px earlier and reduces from 510 px to 448 px. Both panels share the same width, radius, border, and shadow geometry.
- Colours and visual tokens: only existing navy, ivory, cyan, yellow, and pink tokens are used. Large paper textures were removed; surface opacity keeps the wallpaper present without reducing text contrast.
- Image quality and asset fidelity: album art keeps the existing aspect-fill crop and is rendered at 204 x 204 without rotation or stretching. No new generated asset, large resource-library cutout, screenshot slice, or approximate code art was introduced.
- Copy and content: title, artist, mapper, chart mode, rating, length, BPM, notes, score, accuracy, rate, ranking, mods, and play count remain dynamic.
- Icons: existing FontAwesome icons remain small semantic markers; none are used as decorative illustrations.
- States and interactions: selection transitions, ranking/history toggle, selected-mods entry point, filtering, focused-package expansion, keyboard navigation, and preview continuity are unchanged.
- Accessibility and viewport resilience: text maintains the existing navy-on-ivory contrast; long titles still balance to two lines and all ranking rows retain their pointer height.

## Findings

- P0: none.
- P1: none.
- P2: none after removing the large paper assets and unifying the left-side surface system.
- P3: the selected reference uses a more open canvas behind the song summary. Yokko intentionally retains a restrained rounded container here because the active wallpaper can be much brighter than the reference and the user explicitly requested refined rounded surfaces.

## Verification

- Isolated `Yokko.Game.Tests` build in `artifacts/song-select-left-layout`: 0 warnings and 0 errors.
- Focused Song Select run: 30 passed, 0 failed, 0 skipped, covering the screen layout, ranking toggle, title layout, square artwork cropping, focused package behaviour, virtualisation, and selection state.
- The 1920 x 1080 Direct3D 11 capture and the combined source/implementation board were both opened and inspected at original detail.

final result: passed

---

# Song Select difficulty-range filter QA (2026-08-01)

## Evidence

- Selected Product Design reference: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png`.
- Default native implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-difficulty-filter-first-pass.png` (1920 x 1080, Direct3D 11).
- Active 5.00+ filter state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-difficulty-filter-active.png` (1920 x 1080, Direct3D 11).
- Full reference/implementation comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-difficulty-filter-reference-comparison.png` (reference left, implementation right).
- Focused controls comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-difficulty-filter-focused-comparison.png` (reference top, implementation bottom).

## Required fidelity review

- Structure and hierarchy: the implementation now has the reference's missing search -> difficulty range -> browse controls sequence. The key-mode buttons remain alongside search and Converts remains in the browse row because both are real Yokko filters, but the information order matches the selected target.
- Typography and spacing: the new 32 px range row uses the existing 8/10 px compact browse-control typography, 7 px radius, and 8 px vertical rhythm. Search, range, toolbar, and browser do not overlap at the 1920 x 1080 shared baseline.
- Colours and surfaces: the row reuses Yokko ivory, navy, cyan, yellow, and pink tokens. The multicolour scale is a quiet four-pixel guide rather than a luminous or sci-fi treatment.
- Interaction: clicking or dragging changes a real minimum threshold. Zero shows `ALL`; an active threshold shows a `+` suffix. Entries below the threshold disappear, package chart counts update, and an excluded selection falls back through the existing selection path.
- Rating semantics: MSD uses a 0-30 range in 0.25 increments; Rebirth Stars uses a 0-10 range in 0.1 increments. Each rating mode retains its own threshold so values never cross incompatible units.
- Assets: no new bitmap, generated image, cutout, sprite crop, or resource dependency was introduced. The control uses only existing font icons and simple UI geometry.
- Responsive behaviour: the song browser begins at y=220 and still terminates above the 130 px footer. The right column remains 850 px wide and aligned with the search and toolbar.

## Findings

- P0: none.
- P1: none.
- P2: none after the first native comparison. The active-state capture confirms the threshold marker, value chip, list filtering, package count updates, and selected-row retention are visually coherent.
- Remaining scope outside this pass: the selected reference places Converts beside the range and exposes Collection rather than Yokko's current Library display. These are broader browse-model choices, not visual defects in the implemented range filter.

## Verification

- Isolated `Yokko.Game` build: passed with 0 warnings and 0 errors.
- `TestSceneSongSelectScreen`: 10 passed, 0 failed, including the new MSD/STAR mode-specific threshold regression.
- Native Direct3D 11 captures verified default and active filter states at 1920 x 1080.
- Full and focused reference/implementation comparisons were opened and inspected together.

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

---

# Song Select paper-layout polish QA (2026-07-31)

## Evidence

- Selected reference: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png`.
- Native Direct3D 11 implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\yokko-song-select-polish-final.png` (1920 x 1080).
- Same-viewport comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-polish-comparison-final.png`.
- Asset alpha-edge check: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\paper-assets-alpha-check.png`.

## Required fidelity review

- Left information hierarchy: selected artwork, difficulty, title, artist, mapper, rate, difficulty rating, length, BPM, notes, best score, and best accuracy are presented on one restrained paper surface instead of independent generic cards.
- Ranking hierarchy: the seven individual ribbons were replaced with one continuous paper table, quiet row separators, and one explicit current-player selection state.
- Browser hierarchy: package headers and ordinary difficulty rows use ivory surfaces; the selected difficulty uses a restrained yellow wash and outline instead of tinting the whole expanded group cyan.
- Assets: only `paper-song-info` and `paper-ranking` were introduced in this pass. Both were inspected against a checkerboard and show no white halo, black fringe, or dirty semitransparent edge. Other unverified library decorations were not used.
- Typography: the long-title fixture fits on two complete lines without mid-line truncation. Score, accuracy, mode, mapper, and difficulty labels remain legible at native scale.

## Findings

- P0: none.
- P1: none after replacing the disconnected left cards and restoring a clear selected difficulty state.
- P2: none after shortening the long-title line measure and inspecting the two used raster assets at their transparent edges.
- P3: the reference has additional mascot speech art and denser hand-drawn micro-decoration; these were deliberately not added because the available unverified cutouts may not meet production quality.

## Verification

- `Yokko.Game.Tests` isolated build: passed with 0 warnings and 0 errors.
- Native Direct3D 11 preview captured at 1920 x 1080 using the dedicated Song Select fixture.
- Reference and implementation inspected together in one combined comparison image.
- `git diff --check` passed for the touched Song Select and QA files.

final result: passed

---

# Song Select cover proportion and rating hierarchy QA (2026-07-31)

## Evidence

- User-reported state: `C:\Users\nyafa\AppData\Local\Temp\codex-clipboard-0afa7bc3-e844-4221-9973-ef4b8065fe16.png`.
- Native Direct3D 11 implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\yokko-song-select-cover-polish-final.png` (1920 x 1080).
- Focused before/after comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-cover-rating-before-after.png`.
- Full selected-reference comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-cover-comparison-final.png`.
- State: English UI, 7K filter, first two packages expanded, third package collapsed, and `Marathon x1.3` selected.

## Required fidelity review

- Cover proportion: expanded package headers use a masked 104 x 84 art slot with a relative-size child and `FillMode.Fill`; standalone song rows use a 76 x 68 art slot. Artwork is centre-cropped instead of stretched to a banner ratio.
- Rating hierarchy: compact difficulties use a small `MSD` label, a numeric value, and a thin accent underline. The previous high-chroma filled rating pill is removed.
- Data integrity: the displayed 4.11-5.40 values are generated by the production difficulty calculator from the preview fixture's 84 hit objects, not hard-coded display text.
- Selection hierarchy: the selected package play marker, cyan rail, and selected difficulty's restrained yellow wash remain visually stronger than the rating metadata.
- Assets: no new or unverified raster assets were introduced for this correction.

## Findings

- P0: none.
- P1: none.
- P2: none after correcting the cover containers and reducing the rating treatment to supporting metadata.
- P3: the selected visual reference uses some wider banner artwork, but the user's explicit proportion correction supersedes that detail; the implementation now preserves source-art proportions through cropping.

## Verification

- `Yokko.Game.Tests` isolated build: passed with 0 warnings and 0 errors.
- Native Direct3D 11 preview captured and inspected at 1920 x 1080.
- Focused user-report/implementation and full reference/implementation comparisons were opened and inspected.
- `git diff --check`: passed.

final result: passed

---

# Song Select package hierarchy polish QA (2026-07-31)

## Evidence

- Previous native state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\yokko-song-select-cover-polish-final.png` (1920 x 1080).
- Revised native Direct3D 11 state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\yokko-song-select-grouping-pass.png` (1920 x 1080).
- Full before/after comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-grouping-before-after.png`.
- Focused list comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-grouping-focused-before-after.png`.
- State: English UI, 7K filter, two expanded packages, one collapsed package, and `Marathon x1.3` selected.

## Required fidelity review

- Group hierarchy: packaged chart rows are inset 14 px from their package header while retaining a common right edge, making parent and child scopes readable without narrowing titles excessively.
- Continuity: a two-pixel difficulty-coloured guide rail and short branch connect each expanded child row to the package above.
- Selection feedback: the selected chart places a pink play pointer on the guide rail, separate from the low-emphasis MSD metadata and the yellow selected surface.
- Density: compact-row shadow opacity and offset are reduced, preventing expanded groups from reading as a stack of heavy floating cards.
- Pooling behavior: every bind restores the row's resting X position, and hover/selection transforms return to that position so pooled compact and standalone rows cannot inherit stale indentation.
- Assets: no new raster assets were added or extracted for this pass.

## Findings

- P0: none.
- P1: none.
- P2: none after the hierarchy pass; long titles and right-side mode pills remain fully inside the 850 px list width.
- P3: the hierarchy is intentionally subtle at full-screen scale; the focused comparison confirms the nesting and selected pointer remain visible without competing with song titles.

## Verification

- `Yokko.Game.Tests` isolated build: passed with 0 warnings and 0 errors.
- `TestSceneSongSelectVirtualisedList`: 2 passed, 0 failed, including the 10,000-row bounded-materialisation path.
- Native Direct3D 11 preview captured and inspected at 1920 x 1080 from the repository working directory so both preview artwork variants resolved correctly.
- Full and focused before/after comparisons were opened and inspected.
- `git diff --check`: passed.

final result: passed

---

# Song Select package motion QA (2026-07-31)

## Evidence

- Final native state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\yokko-song-select-motion-final.png` (1920 x 1080, Direct3D 11).
- Collapse sequence: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-motion-strip.png` (expanded, moving, collapsed).
- Expansion sequence: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-expansion-strip.png` (collapsed, entering, expanded).
- State: English UI, 7K filter, selected `Marathon x1.3`; the middle package is used for motion evidence so both following headers and newly inserted rows are visible.

## Required fidelity review

- Direction: stable rows reuse their previous logical top and move to the new top over 230 ms with `OutQuint`, capped to a 64 px travel distance so large libraries never produce long screen-spanning flights.
- Expansion: newly inserted rows in the affected package start 14 px above their destination and fade in over 170 ms while moving into place.
- Collapse: rows following the removed package children visibly move upward instead of teleporting; removed rows do not remain as detached ghosts.
- Virtualisation: the transition is applied only to the first actualised visible/preloaded range after a rebuild. Rows materialised later by scrolling do not replay it.
- Scroll containment: package anchoring clamps to the real `itemLayer.Height - viewportHeight` range, preventing the previous overscroll-and-rebound when the collapsed list fits inside the viewport.
- Pool reset: row and header alpha are explicitly restored on every bind so partially faded pooled drawables cannot leak state.
- Assets: no raster or decorative assets were added for motion.

## Findings

- P0: none.
- P1: none.
- P2: none after clamping package scroll; the three-frame collapse sequence no longer shows a viewport rebound.
- P3: evidence is sampled at three native frames rather than distributed as a video, but includes both in-progress states and both transition directions.

## Verification

- `Yokko.Game.Tests` isolated build: passed with 0 warnings and 0 errors.
- `TestSceneSongSelectVirtualisedList`: 4 passed, 0 failed, covering 10,000-row bounded materialisation, animated rebuild bounds, and short-content scroll clamping.
- Native Direct3D 11 captures were taken at 1920 x 1080 before, during, and after both collapse and expansion.
- The collapse strip, expansion strip, and final stable frame were opened and inspected.
- `git diff --check`: passed.

final result: passed

---

# Song Select selection transition QA (2026-08-01)

## Evidence

- Previous selection: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-selection-01-before.png` (1920 x 1080, Direct3D 11).
- In-progress selection: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-selection-02-mid.png` (1920 x 1080, Direct3D 11).
- Settled selection: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-selection-03-after.png` (1920 x 1080, Direct3D 11).
- Focused three-frame comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-selection-transition-strip.png`.
- State: English UI, 7K filter, transition from selected `Marathon x1.3` to `Neon Pulse Overdrive - Hard` across different artwork and package groups.

## Required fidelity review

- Direction: adjacent selection computes its direction from the current navigable list order. The incoming detail layer begins 10 px along that axis and settles over 210 ms with `OutQuint`; the outgoing layer moves 8 px in the opposite direction.
- Legibility: the incoming paper, cover, title, metadata, and ranking table remain fully opaque. Only the outgoing layer fades over 90 ms, preventing the double-title and double-score ghosting found in the first captured mid-frame.
- Background: the existing 220 ms wallpaper crossfade remains independent of the opaque detail layer, so artwork still blends without reducing text contrast.
- Lifecycle: superseded detail layers are removed after 240 ms; rapid repeated selection retires all stale layers and returns the host to one active layer.
- List continuity: selection updates the materialised row states and scroll target without calling `rebuildSongList`; an internal generation counter proves the list generation stays unchanged.
- Audio continuity: the transition adds no scheduler delay or extra preview call. The existing single `playSelectedPreview()` call remains on the changed-selection path.
- Assets: no new raster or decorative assets were introduced.

## Findings

- P0: none.
- P1: none.
- P2: none after replacing the full-layer crossfade with an opaque incoming layer and fast outgoing retirement.
- P3: the 10 px movement is intentionally restrained; the focused strip confirms the wallpaper blend and crisp content handoff without turning selection into a large page transition.

## Verification

- `Yokko.Game.Tests` isolated build: passed with 0 warnings and 0 errors.
- Focused Song Select suite: 14 passed, 0 failed.
- `TestSelectionTransitionDoesNotRebuildSongList` covers changed selection, one transition generation, unchanged list generation, rapid repeated selection, and stale-layer retirement.
- Native Direct3D 11 captures were taken at 1920 x 1080 before, during, and after selection; the three-frame strip was opened and inspected.
- `git diff --check`: passed.

final result: passed

---

# Song Select scroll affordance QA (2026-08-01)

## Evidence

- Top-of-list native state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-scroll-01-top.png` (1920 x 1080, Direct3D 11).
- Lower-list native state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-scroll-02-lower.png` (1920 x 1080, Direct3D 11).
- Focused comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-scroll-affordance-comparison.png` (previous stable state, top, lower).
- State: English UI, 7K filter, all three packages expanded; the lower capture selects `Petals at Daybreak` so the list reaches its real end.

## Required fidelity review

- Edge continuation: a 28 px ivory-to-transparent fade appears only at an edge which has more content beyond it. The first frame has no top haze; the final frame has no bottom haze.
- Position feedback: the 4 px cyan thumb is proportional to viewport/content height and moves along a low-contrast navy track. It remains inset inside the browser rather than consuming card width.
- Short collections: when content height fits the viewport, both fades and the indicator are fully hidden.
- Virtualisation: affordance updates reuse the existing scroll position and content-height values. They do not add list entries, rebuild rows, or change pool limits.
- Input: overlays are passive drawables above the masked scroll content and do not introduce click or hover targets.
- Assets: no raster or decorative assets were added for this pass.

## Findings

- P0: none.
- P1: none.
- P2: none after comparing the previous, top, and lower browser crops; the fade is visible without washing out a full row, and the indicator does not read as a heavy webpage scrollbar.
- P3: the preview corpus contains only eleven logical browser rows, so the thumb is relatively tall. Its minimum 32 px height and proportional formula are covered against the 10,000-row fixture but were not separately screenshotted with a production-scale library.

## Verification

- `Yokko.Game.Tests` isolated build: passed with 0 warnings and 0 errors.
- `TestSceneSongSelectVirtualisedList`: 5 passed, 0 failed, including long-list edge state, short-list chrome suppression, animated rebuild bounds, and 10,000-row bounded materialisation.
- Native Direct3D 11 captures were taken at 1920 x 1080 from both the top and lower scroll positions and inspected together in one focused comparison.
- `git diff --check`: passed after the final source and QA edits.

final result: passed

---

# Song Select ranking grade badge QA (2026-08-01)

## Evidence

- Previous full-height grade column: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-scroll-01-top.png` (1920 x 1080, Direct3D 11).
- Final compact grade badge state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-grade-badge-final.png` (1920 x 1080, Direct3D 11).
- Focused same-state comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-grade-badge-before-after.png` (old left, final right).
- State: English UI, 7K filter, seven global-ranking rows with S and A grades; MOCHI is the current-player row.

## Required fidelity review

- Hierarchy: the old 54 px wide, full-row-height cyan/lime column is replaced by a 44 x 32 badge. Player, score, combo and accuracy now remain the primary scan path.
- Colour: grade colour is limited to a 4 px leading edge and a restrained border. The badge letter uses the shared deep-navy text colour instead of reversing on a saturated block.
- Surface: an almost opaque ivory fill prevents the ranking paper's lower-right pink tape from muddying the bottom badge.
- Selection: current-player emphasis remains on the pink row border, leading rail, player name and pale-yellow row fill. The grade badge does not add a competing selected state.
- Spacing: the score and badge keep a visible gap and all seven badges stay vertically centred inside the existing 52 px rows.
- Assets: no raster asset was added, re-cut, or replaced; the existing paper and tape remain untouched.

## Findings

- P0: none.
- P1: none.
- P2: none after replacing the translucent badge fill found in the first implementation capture; the final bottom badge no longer picks up a dirty pink tint from the paper decoration.
- P3: X/XH and B/C/D grades are covered by the shared badge component and colour mapping but are not present in this seven-row visual fixture.

## Verification

- `Yokko.Game.Tests` isolated build: passed with 0 warnings and 0 errors.
- Focused Song Select filter: 16 passed, 0 failed.
- `TestRankingUsesCompactGradeBadges` verifies seven visible badges, 44 x 32 bounds, S/A grade diversity, and exactly one current-player variant.
- Native Direct3D 11 output was captured at 1920 x 1080 and inspected against the previous same-state screenshot in one focused comparison.
- `git diff --check`: passed after final source and QA edits.

final result: passed

---

# Song Select selected-row focus depth QA (2026-08-01)

## Evidence

- Previous same-state focus treatment: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-grade-badge-final.png` (1920 x 1080, Direct3D 11).
- Final selected-row depth state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-row-focus-final.png` (1920 x 1080, Direct3D 11).
- Focused same-state comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-row-focus-final-before-after.png` (old left, final right).
- State: English UI, 7K filter, expanded package with `Marathon x1.3` selected.

## Required fidelity review

- Density: the selected child row keeps the existing 58 px logical height and does not reduce the number of visible rows.
- Focus depth: a second restrained paper shadow fades in over 170 ms with `OutQuint`; it complements the pale-yellow fill and pink play pointer without adding glow or a saturated outline.
- Pull-forward motion: compact package children move only 3 px left when selected. Standalone songs stay at their resting X position so the browser mask cannot crop their cover or border.
- Hover: unselected hover exposes 42% of the focus shadow over 120 ms, then selection promotes the same layer instead of introducing a separate visual language.
- Hierarchy: the 3 px pull creates a small notch in the package guide rail but does not visually disconnect it; the selected node reads as a paper slip pulled out from its stack.
- Virtualisation: selection changes the materialised row state in place. The test fixture retains two items and proves the focus shadow and X offset transfer without rebuilding the list.
- Pooling: newly bound rows start with zero focus-shadow alpha, and `SetSelected` restores both the shadow and resting position after reuse.
- Assets: no raster or decorative asset was added, re-cut, or replaced.

## Findings

- P0: none.
- P1: none.
- P2: none after adding the 3 px compact-row pull; the first shadow-only capture was too subtle to establish a useful focus improvement.
- P3: the shadow and movement are intentionally restrained so the selected state still belongs to Yokko's paper-card language rather than reading as a floating sci-fi panel.

## Verification

- `Yokko.Game.Tests` isolated build: passed with 0 warnings and 0 errors.
- `TestSceneSongSelectVirtualisedList`: 6 passed, 0 failed.
- `TestSelectionTransfersFocusShadowWithoutRebuild` verifies selected/unselected shadow alpha, 11/14 px selected/resting X positions, in-place transfer, and unchanged item count.
- Native Direct3D 11 output was captured at 1920 x 1080 and inspected both full-screen and against the previous same-state screenshot.
- `git diff --check`: passed after final source and QA edits.

final result: passed

---

# Song Select compact browse controls QA (2026-08-01)

## Evidence

- Source visual truth / previous native state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-toolbar-before.png` (1920 x 1080, Direct3D 11).
- Revised native implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-toolbar-first-pass.png` (1920 x 1080, Direct3D 11).
- Focused same-state comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-toolbar-before-after.png` (previous left, revised right; equal 900 x 886 crops separated by 12 px).
- Viewport and density: both source and implementation are native 1920 x 1080 captures of the shared 1920 x 1080 stage at 1x logical density. No resizing or density normalization was needed before cropping.
- State: English UI, 7K filter, the same three packages and `Marathon x1.3` selection, Converts shown.

## Comparison history

1. The source state stacked search/key mode, an inert 670 px star-range summary plus an inert Converts button, and sort/group/collection into three equal-weight rows.
2. The star range always read `0.0` to infinity and the Converts action was empty, so the tallest filter region did not contribute to the browse task.
3. The revised state removes the inert star range, consolidates all secondary browsing controls into one 34 px row, and moves the song browser from Y=222 to Y=184.
4. Converts now defaults to the existing inclusive behavior, visibly reports `SHOWN/HIDDEN`, and filters charts by `ConversionSource` when toggled.
5. The post-fix focused comparison was opened and inspected. No remaining P0, P1, or P2 issue was found.

## Required fidelity review

- Fonts and typography: the existing Yokko display/body families, weights, and navy/cyan hierarchy are retained. Secondary labels remain optically smaller than values, and all four controls fit without clipping or truncation in the 850 px browser width.
- Spacing and layout rhythm: the browse header is reduced from three rows to two. Four 34 px controls occupy the full width with consistent 8 px gaps; the browser gains 38 px without changing package or beatmap row height.
- Colors and visual tokens: controls continue using ivory surfaces, cyan icons/borders, deep-navy copy, and a pink active rail. The active Converts surface uses the existing pale-cyan token instead of introducing a glow or new colour family.
- Image quality and asset fidelity: package artwork, background, logo, mascot, tape, and sticker assets are unchanged. No raster asset was added, cropped, stretched, or substituted.
- Copy and content: `SORT / TITLE`, `GROUP / BEATMAPS`, `LIBRARY / ALL SONGS`, and `CONVERTS / SHOWN` describe the actual current state. The non-interactive library scope omits a chevron and hover treatment so it does not falsely promise an unavailable menu.
- Interaction: search, key-mode filtering, and ranking switching remain covered by the existing interaction scene. The new focused test proves Converts hides and restores converted charts and transfers selection to a native chart when necessary.
- Accessibility risk visible from screenshots: text contrast remains strong on ivory/pale-cyan surfaces, and active state is expressed through fill, border, copy, and a pink rail rather than colour alone. Keyboard focus traversal was not manually exercised in this visual pass.

## Findings

- P0: none.
- P1: none.
- P2: none after replacing the inert three-row stack with the compact functional control row.
- P3: the four compact controls intentionally use dense 8-10 px secondary type to preserve list height; a later localisation pass should recheck German or other long labels.

## Verification

- `Yokko.Game.Tests` isolated build: passed with 0 warnings and 0 errors.
- Focused tests: 2 passed, 0 failed (`TestSongSelectInteractions` and `TestConvertedBeatmapFilterIsFunctional`).
- Native Direct3D 11 implementation capture completed at 1920 x 1080; the full screen and focused before/after comparison were both opened and inspected.
- `git diff --check`: passed after the final source and QA edits.

final result: passed

---

# Song Select footer tool dock QA (2026-08-01)

## Evidence

- Source visual truth / previous footer: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-footer-before.png` (1920 x 1080, Direct3D 11).
- Revised native implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-footer-dock-final.png` (1920 x 1080, Direct3D 11).
- Focused same-state comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-footer-before-after.png` (previous top, revised bottom; equal 1920 x 140 crops separated by 12 px).
- Mods destination state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-footer-dock-mods-open.png` (1920 x 1080, Direct3D 11).
- Viewport and density: source and implementation are native 1920 x 1080 captures of the shared 1920 x 1080 stage at 1x logical density. The footer comparison uses unscaled crops.
- State: English UI, 7K filter, the same selected song, no active Mods, footer at rest.

## Comparison history

1. The source footer presented Mods as a 126 x 96 remote-like control while Random and Options were separate 126 x 82 cards with wider gaps and no common container.
2. The differing heights and internal baselines made the three preparation actions read as unrelated floating widgets between the account card and Play action.
3. Options also carried an empty action despite looking fully interactive.
4. The revised implementation gives all three controls the same 126 x 82 geometry and places them at 8 px intervals inside a 410 x 94 pale-cyan paper dock with one shadow and outline.
5. Options now pushes the real `SettingsScreen`; the focused test covers entry and return. The Mods control still reaches the existing full Gameplay Mods screen, which was captured separately.
6. The post-fix full and focused captures were opened and inspected. No remaining P0, P1, or P2 issue was found.

## Required fidelity review

- Fonts and typography: all three actions use the same Yokko display face, 11 px label size, tracking, and baseline. No label is clipped or visually lower than its neighbours.
- Spacing and layout rhythm: Mods, Random, and Options share an 82 px height, 126 px width, 8 px internal gaps, and 8 px outer dock margins. The dock aligns vertically with Back, account, and Play without changing the 130 px footer.
- Colors and visual tokens: the dock uses the existing ivory, pale-cyan, navy, cyan, and pink tokens. Accent rails preserve each action's existing colour and the shared outline prevents the controls from looking like an unrelated web toolbar.
- Image quality and asset fidelity: the existing diamond sticker is retained at a smaller 22 px size inside Mods. Mascot, avatar, tape, logo, and all other raster assets are untouched; no new asset was generated or extracted.
- Copy and content: the visible labels remain `MODS`, `RANDOM`, and `OPTIONS`. Options now matches its affordance by opening Settings instead of doing nothing.
- Interaction and state: Mods retains count and open-state colour handling, Random retains `selectRandomEntry`, and Options pushes and returns from `SettingsScreen`. Hover and active changes continue using the shared `OutQuint` motion language.
- Accessibility risk visible from screenshots: all three hit targets remain at least 126 x 82, icons are paired with text labels, and pink/cyan semantic differences are not the only identifiers. Keyboard traversal and screen-reader naming were not manually exercised in this pass.

## Findings

- P0: none.
- P1: none.
- P2: none after aligning the controls and wiring Options to the real Settings screen.
- P3: the dock deliberately leaves a large breathing gap before Play so the yellow primary action stays isolated; this can be revisited only if additional footer actions are added.

## Verification

- `Yokko.Game.Tests` isolated build: passed with 0 warnings and 0 errors.
- Focused tests: 2 passed, 0 failed (`TestSongSelectInteractions` and `TestFooterOptionsOpensSettings`).
- Native Direct3D 11 captures covered both the resting footer and the real Mods destination screen at 1920 x 1080.
- Full-screen and focused before/after evidence were opened and inspected together.
- `git diff --check`: passed after the final source and QA edits.

final result: passed

---

# Song Select integrated grade mark QA (2026-08-01)

## Evidence

- Source visual truth / outlined-badge state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-carousel-first-pass.png` (1920 x 1080, Direct3D 11).
- Revised native implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-grade-mark-first-pass.png` (1920 x 1080, Direct3D 11).
- Full-view comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-grade-mark-full-before-after.png` (source top, revised bottom; two native 1920 x 1080 frames separated by 12 px).
- Focused ranking comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-grade-mark-before-after.png` (source left, revised right; equal unscaled 860 x 470 crops separated by 12 px).
- Viewport and density: source and implementation are native 1920 x 1080 captures of the shared 1920 x 1080 stage at 1x logical density. No scaling or density normalization was required.
- State: English UI, 7K filter, seven global-ranking rows, `Marathon x1.3` selected, and MOCHI highlighted as the current player.

## Comparison history

1. The source state used seven repeated 44 x 32 ivory rectangles with coloured borders and 4 px leading rails. Although smaller than the former full-height grade column, they still read as a detached control stack at the edge of the continuous paper table.
2. The revised state reduces each mark to a 36 x 32 footprint with a neutral 30 px paper disc, unframed grade letter, and an 18 px grade-colour underline. The current-player underline grows to 24 px without introducing another selected fill or outline.
3. Score anchors move 10 px toward the mark, reducing the dead gap while retaining a 12 px optical separation. All seven scores and marks keep a common right edge.
4. The focused comparison confirms that the repeated framed-card rhythm is removed. The bottom YUKI mark remains readable over the pink tape because the neutral paper disc prevents colour contamination.
5. The full and focused comparisons were opened and inspected together. No actionable P0, P1, or P2 difference remains.

## Required fidelity review

- Fonts and typography: score size, player hierarchy, combo, accuracy, rank numbers, and grade lettering retain the existing Yokko display/body faces and weights. The grade remains legible at 17 px without competing with the 20 px score.
- Spacing and layout rhythm: ranking row height stays 52 px and the table retains all seven rows. The grade footprint narrows from 44 to 36 px; score-to-grade spacing tightens from 16 to 12 px while preserving alignment and avoiding overlap.
- Colors and visual tokens: the grade letter stays deep navy. Grade colour is restricted to a short cyan/lime underline with 64% opacity, or 88% on the current-player mark; no new palette or glow was introduced.
- Image quality and asset fidelity: paper, tape, avatars, selected artwork, covers, mascot, and all decorative assets are unchanged. No raster asset was generated, extracted, stretched, or substituted.
- Copy and content: player names, ranks, mods, combo, accuracy, scores, and S/A labels are unchanged. The change does not alter ranking data or sorting.
- Interaction and state: GLOBAL/MY HISTORY switching and current-player row emphasis remain unchanged. Highlighting only lengthens the underline, while the row's pink outline, rail, name, and yellow wash remain the primary current-player cues.
- Accessibility risk visible from screenshots: the grade is still conveyed by its S/A text, not colour alone. Screenshot evidence does not establish keyboard navigation or screen-reader naming, which were outside this visual-only refinement.

## Findings

- P0: none.
- P1: none.
- P2: none; the grade now reads as metadata printed on the score sheet instead of a separate card column.
- P3: the neutral paper disc is intentionally almost invisible on ordinary ivory rows; it becomes perceptible on the selected yellow row and over the lower pink tape where separation is needed.

## Verification

- Isolated `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors.
- `TestRankingUsesCompactGradeBadges`: 1 passed, 0 failed; verifies seven 36 x 32 marks, zero component border, S/A diversity, and exactly one highlighted current-player state.
- Native Direct3D 11 capture completed at 1920 x 1080 using the production preview entry point.
- Full-screen and focused before/after comparisons were opened and inspected.
- `git diff --check`: passed after the source, test, and QA edits.

final result: passed

# Song Select package proximity curve QA (2026-08-01)

## Evidence

- Source visual truth / flat-row state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-carousel-before.png` (1920 x 1080, Direct3D 11).
- Revised native implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-carousel-first-pass.png` (1920 x 1080, Direct3D 11).
- Focused same-state comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-carousel-before-after.png` (flat left, revised right; equal 900 x 660 crops separated by 12 px).
- Cross-package transfer state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-carousel-transferred.png` (1920 x 1080, Direct3D 11).
- Viewport and density: all evidence is native 1920 x 1080 output of the shared 1920 x 1080 stage at 1x logical density. The focused comparison uses unscaled crops.
- State: English UI, 7K filter. The same first-package `Marathon x1.3` selection is used for the before/after comparison; the transfer capture selects the first row of the second package.

## Comparison history

1. The source state already differentiated the selected child row with yellow fill, pink pointer, and a 3 px pull, but all non-selected siblings shared the same X position and still formed a rigid table.
2. The revised state applies a distance-based curve only inside the selected package: selected X=11, adjacent X=14, then X=18, X=22, up to a bounded X=26.
3. Row width changes with X so every child keeps the same 850 px right boundary. Titles, metadata, and mode pills therefore remain aligned and unclipped.
4. A cross-package selection capture confirms the previous package returns to its resting X=14 while the new package receives the curve; unrelated package headers and standalone rows do not move.
5. The same-state focused comparison and transfer capture were opened and inspected. No remaining P0, P1, or P2 issue was found.

## Required fidelity review

- Fonts and typography: no typeface, size, weight, wrapping, or truncation rule changed. The modest X offsets preserve the existing title and metadata hierarchy.
- Spacing and layout rhythm: the curve uses 4 px steps with a 12 px maximum neighbour indent. Vertical spacing, 58 px child-row height, 84 px header height, and list density remain unchanged.
- Colors and visual tokens: existing ivory, pale-yellow, cyan, lime, navy, and pink states are untouched. The interaction is communicated through position and motion in addition to colour.
- Image quality and asset fidelity: package covers, wallpaper, stickers, mascot, tape, and avatar assets are unchanged. No asset was generated, re-cut, stretched, or used to fake the interaction.
- Copy and content: song titles, difficulty names, mapper credits, ratings, and mode pills remain identical and keep a common right edge.
- Interaction and motion: selection updates materialised rows in place over 170 ms with `OutQuint`; hover subtracts 3 px from the row's current curved target and restores that target on exit. Newly materialised pooled rows receive their final curve immediately, avoiding a replayed entrance animation while scrolling.
- Virtualisation: the curve is computed from lightweight item indices and touches only active pooled rows. The 10,000-row materialisation limit and package rebuild paths remain bounded.
- Accessibility risk visible from screenshots: the selected row still has fill, outline, pointer, shadow, and position cues. The curve is supplementary and does not become the only selection indicator. Reduced-motion behaviour was not separately exercised.

## Findings

- P0: none.
- P1: none.
- P2: none; the right boundary remains stable and the stepped guide reads as a deliberate selection curve rather than disconnected cards.
- P3: packages with only one or two charts naturally show little or no neighbour curve; selection fill and pointer remain the primary cues in those cases.

## Verification

- `Yokko.Game.Tests` isolated build: passed with 0 warnings and 0 errors.
- `TestSceneSongSelectVirtualisedList`: 7 passed, 0 failed.
- `TestSelectionBuildsBoundedPackageProximityCurve` verifies symmetric and edge-position curves, the 12 px cap, the shared 850 px right edge, transfer without list rebuild, and unchanged item count.
- Native Direct3D 11 captures covered both the four-row curve and a cross-package transfer at 1920 x 1080.
- Full-screen and focused before/after evidence were opened and inspected together.
- `git diff --check`: passed after the final source and QA edits.

final result: passed

---

# Song Select inline rating hierarchy QA (2026-08-01)

## Evidence

- Selected concept: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png`; its left rating pills are superseded by the user's explicit correction that this placement felt abrupt.
- Source visual truth / first corrective state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-grade-mark-first-pass.png` (1920 x 1080, Direct3D 11).
- Revised native implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-inline-rating-first-pass.png` (1920 x 1080, Direct3D 11).
- Full-view comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-inline-rating-full-before-after.png` (source top, revised bottom; two native frames separated by 12 px).
- Focused list comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-inline-rating-before-after.png` (source left, revised right; equal unscaled 865 x 670 crops separated by 12 px).
- Viewport and density: source and implementation are native 1920 x 1080 output of the shared 1920 x 1080 stage at 1x logical density. No scaling or density normalization was required.
- State: English UI, 7K filter, the same first two expanded packages, `Marathon x1.3` selected, and Etterna MSD display mode.

## Comparison history

1. The generated concept placed difficulty ratings in high-chroma pills at the far left of every child row. The first corrective implementation removed those pills but retained a 54 x 42 two-line `MSD / value` column and underline at the start of the title scan path.
2. The repeated left column still made the browser read like a technical table and forced every title to begin at X=82 despite the package guide already communicating difficulty colour.
3. The revised implementation starts titles at X=20 and expands their measure from 528 to 580 px. Rating moves to a transparent 64 x 20 trailing readout at X=610, aligned with the mapper metadata and before the unchanged mode pill at X=690.
4. The accent underline and rating surface are removed. Difficulty colour remains on the package guide and the small `MSD` unit; the numeric value stays navy, so rating remains readable without becoming the first object in every row.
5. The focused comparison confirms that long titles gain useful room, the selected proximity curve becomes visually cleaner, and `MSD 5.14` keeps at least a 16 px gap from the mode pill. No text or label overlaps.
6. The full and focused comparisons were opened and inspected together. No actionable P0, P1, or P2 difference remains.

## Required fidelity review

- Fonts and typography: title, metadata, rating unit/value, and mode-pill families and weights remain unchanged. Rating value is reduced from 12 to 10 px because it is now supporting metadata; the 15 px title remains the dominant row label.
- Spacing and layout rhythm: title X moves from 82 to 20 and gains 52 px of line measure. Metadata follows the same X=20 alignment. The rating occupies X=610-674 and the mode pill remains X=690-834, preserving a clear gap and the shared right edge.
- Colors and visual tokens: navy, cyan/lime difficulty accents, pink mode pills, ivory surfaces, and pale-yellow selection remain unchanged. No new token, glow, or saturation was introduced.
- Image quality and asset fidelity: package covers, selected artwork, wallpaper, paper textures, stickers, mascot, tape, and avatars are unchanged. No raster asset was generated, extracted, stretched, or substituted.
- Copy and content: song title, difficulty name, mapper, key mode, difficulty label, rating unit, and rating value are all retained. The rating display still switches between MSD and SR using the existing presentation formatter.
- Interaction and virtualisation: active rows update the unit, value, and difficulty colour in place. The new focused test switches from MSD to SR without changing item count; pooled-row materialisation and the 10,000-item bound remain intact.
- Accessibility risk visible from screenshots: rating remains explicit text and is not communicated by colour alone. The wider title measure reduces truncation risk. Keyboard navigation and screen-reader naming were not separately exercised in this visual layout pass.

## Findings

- P0: none.
- P1: none.
- P2: none; the user's abrupt left-rating complaint is addressed without hiding comparative difficulty information.
- P3: standalone non-package songs retain their existing cover-led, bottom-right rating readout because their 84 px card layout does not create the repeated left-column problem shown in the reported package rows.

## Verification

- Isolated `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors.
- `TestSceneSongSelectVirtualisedList`: 8 passed, 0 failed.
- `TestCompactRatingLivesInTrailingMetadata` verifies the transparent 64 x 20 trailing position, MSD-to-SR live update, and unchanged item count.
- Native Direct3D 11 capture completed at 1920 x 1080 using the production preview entry point.
- Full-screen and focused before/after comparisons were opened and inspected.
- `git diff --check`: passed after the source, test, and QA edits.

final result: passed

---

# Song Select package guide continuity QA (2026-08-01)

## Evidence

- Source visual truth: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-inline-rating-first-pass.png` (1920 x 1080, Direct3D 11).
- Revised native implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-guide-final.png` (1920 x 1080, Direct3D 11).
- Focused list comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-guide-before-after.png` (source left, revised right; equal unscaled 865 x 670 crops separated by 12 px).
- Full-view comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-guide-full-before-after.png` (source top, revised bottom; native frames separated by 12 px).
- Selection-transfer capture: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-guide-transition.png` (1920 x 1080, Direct3D 11; first child of the second package selected).
- Viewport and density: all evidence is native 1920 x 1080 output of the shared 1920 x 1080 stage at 1x logical density.
- State: English UI, 7K filter, first two packages expanded, third package collapsed. The same first-package `Marathon x1.3` selection is used for the before/after comparison.

## Comparison history

1. Child-row guide rails previously began only after each package header, leaving the header and its children visually adjacent but not structurally connected.
2. The revised expanded header adds a 2 x 22 px feeder at local X=5, Y=62. Its world X aligns exactly with the child-row guide at the resting row position, creating one continuous grouping axis.
3. The selected package feeder uses 0.92 alpha; other expanded packages use 0.34; collapsed packages use 0. This keeps hierarchy visible without adding another high-chroma badge or panel.
4. Header background, play indicator, bottom rail, and feeder transition over 120-170 ms with `OutQuint`, so transferring selection between packages no longer produces an abrupt binary change.
5. The transfer capture confirms the first package returns to the lower-alpha expanded state while the second package receives the selected feeder and header treatment.
6. Pooled headers reset feeder, rail, and indicator state when rebound as collapsed. The focused and full comparisons were opened and inspected together; no P0, P1, or P2 issue remains.

## Required fidelity review

- Fonts and typography: no typeface, size, weight, wrapping, truncation, or copy changed.
- Spacing and layout rhythm: header height, child-row height, package cover proportions, row indents, and shared right edge remain unchanged. The feeder occupies only the existing gap below an expanded header.
- Colors and visual tokens: the feeder reuses Yokko cyan with two opacity levels. Existing ivory, pale-yellow, lime, navy, pink, and paper tokens are unchanged.
- Image quality and asset fidelity: package covers, selected artwork, wallpaper, paper textures, stickers, mascot, tape, and avatars are unchanged. No raster asset was generated, re-cut, stretched, or added to the resource library.
- Copy and content: song titles, package counts, mapper credits, ratings, difficulty labels, and mode pills remain identical.
- Interaction and motion: selection transfer updates materialised headers in place. The header feeder, selected play indicator, bottom rail, and background animate without rebuilding the list or disturbing scroll state.
- Virtualisation: the header test exercises active pooled headers, selected-state transition, collapse rebound, and state reset. The bounded virtual-list mechanism is unchanged.
- Accessibility risk visible from screenshots: the connector is a supplementary grouping cue. Expanded/collapsed arrows, package counts, selected row fill, outline, pointer, and text remain explicit. Reduced-motion behaviour was not separately exercised.

## Findings

- P0: none.
- P1: none.
- P2: none; the connector reads as hierarchy rather than a decorative cyan slash, and the lower-alpha unselected state does not compete with song titles or cover art.
- P3: a child row can resume the guide in a difficulty colour different from the cyan package feeder. This is intentional: cyan communicates package grouping, while the child segment communicates chart difficulty.

## Verification

- Isolated `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors.
- `TestSceneSongSelectVirtualisedList`: 9 passed, 0 failed.
- `TestExpandedHeaderFeedsAndAnimatesPackageGuide` verifies expanded idle alpha, selected alpha, rail and indicator animation, collapsed rebound, and pooled-state reset.
- Native Direct3D 11 captures covered the stable first-package state and a cross-package selection transfer at 1920 x 1080.
- Full-screen and focused before/after evidence were opened and inspected together.
- `git diff --check`: passed after the final source and QA edits.

final result: passed

---

# Song Select progressive mode disclosure QA (2026-08-01)

## Evidence

- Source visual truth: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-guide-final.png` (1920 x 1080, Direct3D 11).
- First implementation capture: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-progressive-mode-pill-first-pass.png` (1920 x 1080, Direct3D 11).
- Revised implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-progressive-mode-pill-final.png` (1920 x 1080, Direct3D 11).
- Focused comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-progressive-mode-pill-before-after.png` (source left, final right; equal unscaled 865 x 500 crops separated by 12 px).
- Full-view comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-progressive-mode-pill-full-before-after.png` (source top, final bottom; native frames separated by 12 px).
- Mid-transition capture: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-progressive-mode-pill-transition-final.png` (1920 x 1080, Direct3D 11; selection transferring to the first child of the second package).
- Viewport and density: all source and implementation frames are native 1920 x 1080 output of the shared 1920 x 1080 stage at 1x logical density.
- State: English UI, 7K filter, first two packages expanded, third package collapsed. The stable comparison uses first-package `Marathon x1.3` selected.

## Comparison history

1. Every compact child row previously ended with the same 144 x 26 high-chroma pink pill containing key mode and difficulty name, even though difficulty already appeared in the metadata line. The repeated column competed with titles and made the browser read like a dense management table.
2. The first revision changed resting rows to a quiet 54 x 26 key-mode chip at X=780 while the selected row expands leftward to the original 144 x 26 footprint at X=690. Both states keep the same right edge at X=834.
3. Selection now cross-fades compact and expanded labels while position, width, and surface colour animate over 90-170 ms with `OutQuint`. No row, package, or scroll position is rebuilt.
4. The first combined comparison found a P2 copy defect introduced in the new expanded label: the intended middle dot had been written as the Chinese character `路`. The source was corrected to the actual `·`, rebuilt, and recaptured.
5. The final focused comparison confirms resting siblings are visually quiet, the selected difficulty remains explicit, the rating column keeps its alignment, and the corrected separator matches the existing UI copy.
6. The final transition capture shows the previous chip contracting while the new selected chip expands without clipping, overlapping the rating, or shifting the shared right edge.

## Required fidelity review

- Fonts and typography: existing Yokko display weights are retained. Resting key mode uses a 9 px label for legibility; expanded content preserves the prior 8 px label and truncation width. The corrected `·` separator matches existing metadata typography.
- Spacing and layout rhythm: the 26 px pill height, 7 px radius, and X=834 right edge are unchanged. Only the resting width contracts from 144 to 54, reducing repetition without disturbing title, metadata, or rating alignment.
- Colors and visual tokens: expanded state retains the existing 86% pink surface with white text. Resting state uses the same pink at 16% with pink text, so no new colour token or foreign visual language was introduced.
- Image quality and asset fidelity: package covers, selected artwork, wallpaper, paper textures, mascot, tape, stickers, and avatars are unchanged. No image was generated, re-cut, stretched, or added to `Yokko.Resources`.
- Copy and content: selected rows still expose key mode and full difficulty name; resting rows retain explicit key mode while the difficulty remains visible in the metadata line. Standalone cover-led rows keep the full static pill because they do not form the repeated package table pattern.
- Interaction and motion: disclosure follows the actual selected row and transfers in place over 90-170 ms. Hover, click, double-click, package grouping, difficulty-mode updates, preview continuity, and list scrolling remain unchanged.
- Virtualisation: the progressive pill is owned by the pooled row and is rebound with a deterministic compact state. The selected state is then applied without animation on materialisation, preventing replayed entrance motion while scrolling.
- Accessibility risk visible from screenshots: key mode and difficulty remain available as text, not colour alone. Selection also retains fill, outline, pointer, position, and shadow cues. Reduced-motion behaviour was not separately exercised.

## Findings

- P0: none.
- P1: none.
- P2: none after correcting the first-pass `路` separator defect and recapturing the final implementation.
- P3: the quiet 16% pink chips intentionally carry less contrast than selected pills; their key-mode text remains visibly pink and the active 7K filter is also present in the toolbar.

## Verification

- Isolated `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors after the separator correction.
- `TestSceneSongSelectVirtualisedList`: 10 passed, 0 failed.
- `TestCompactModePillProgressivelyDisclosesSelection` verifies 54-to-144 px disclosure, fixed X=834 right edge, text cross-fade, selection transfer, and unchanged item count.
- Native Direct3D 11 captures covered the stable state and a mid-transition cross-package transfer at 1920 x 1080.
- Final full-screen and focused source/implementation comparisons were opened and inspected together.
- `git diff --check`: passed after the final source, test, and QA edits.

final result: passed

---

# Song Select aspect-preserving artwork crop QA (2026-08-01)

## Evidence

- Source visual truth: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-progressive-mode-pill-final.png` (1920 x 1080, Direct3D 11).
- Revised native implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-aspect-cover-final.png` (1920 x 1080, Direct3D 11).
- Selected-artwork comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-aspect-cover-selected-before-after.png` (stretched source left, aspect-preserving crop right; equal unscaled 255 x 265 crops separated by 12 px).
- List-cover comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-aspect-cover-list-before-after.png` (stretched source left, aspect-preserving crop right; equal unscaled 142 x 654 strips separated by 12 px).
- Full-view comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-aspect-cover-full-before-after.png` (source top, revised bottom; native frames separated by 12 px).
- Alternate selected package: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-aspect-cover-second-package.png` (1920 x 1080, Direct3D 11; waterfall artwork selected).
- Viewport and density: all source and implementation frames are native 1920 x 1080 output of the shared 1920 x 1080 stage at 1x logical density.
- State: English UI, 7K filter, first two packages expanded, third package collapsed. The stable comparison uses first-package `Marathon x1.3` selected.

## Comparison history

1. The source implementation used `FillMode.Fill` for all three song-artwork slots. The fixture textures are 1672 x 941 and 1280 x 720, both approximately 16:9, but were stretched into 210 x 210 selected artwork, 104 x 84 package headers, and 76 x 68 standalone cards.
2. The visible result retained each frame boundary but changed the artwork geometry: circular sky rings became oval, clouds widened or narrowed, and waterfall proportions varied between the detail card and list headers.
3. The revised implementation computes a cover scale from the source texture dimensions, centres the correctly proportioned sprite, and lets the existing masked frame crop only the overflowing axis. Frame sizes, radii, borders, and the `YOKKO` artwork label remain unchanged.
4. The selected-artwork comparison shows the sky rings becoming circular and the horizon retaining its natural proportions. The list-strip comparison confirms the same treatment on both the sky and waterfall package covers.
5. The alternate selected-package capture confirms the 16:9 waterfall artwork also fills the 210 x 210 detail frame without stretching, exposed edges, or empty bars.
6. The focused and full comparisons were opened and inspected together. No P0, P1, or P2 issue was found in the first revised capture, so no second visual-fix iteration was required.

## Required fidelity review

- Fonts and typography: no typeface, weight, size, wrapping, truncation, antialiasing, or label placement changed.
- Spacing and layout rhythm: selected artwork remains 210 x 210; package covers remain 104 x 84; standalone covers remain 76 x 68. Card dimensions, list indents, right edges, row heights, and surrounding gaps are unchanged.
- Colors and visual tokens: no palette, opacity, border, shadow, selection, difficulty, or surface token changed.
- Image quality and asset fidelity: original source textures are used directly at their native aspect ratio. No image was regenerated, re-cut offline, resampled into a replacement asset, or added to `Yokko.Resources`. Cropping is centred and performed by the existing masked UI frames.
- Copy and content: song, artist, mapper, difficulty, key mode, rating, score, package count, and `YOKKO` artwork labels remain identical.
- Interaction and motion: changing selection still swaps artwork through the existing selection/background flow. Package expansion, row selection, preview continuity, scroll position, filters, and progressive mode disclosure are unaffected.
- Responsive behaviour: crop size is derived from the actual frame and texture sizes rather than a fixture-specific pixel crop. The shared 1920 x 1080 geometry remains authoritative.
- Accessibility risk visible from screenshots: artwork is identification support rather than the only song label; title, artist, mapper, and difficulty remain explicit text.

## Findings

- P0: none.
- P1: none.
- P2: none; the user-reported flattened artwork now preserves source geometry in all three cover slots.
- P3: centred cover cropping can remove an intentionally off-centre subject on unusual artwork. Yokko currently has no beatmap focal-point metadata; adding per-artwork focal coordinates should wait for a real failing cover rather than introducing speculative controls.

## Verification

- Isolated `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors.
- `TestSceneSongSelectVirtualisedList`: 11 passed, 0 failed.
- `TestArtworkCoverSizePreservesSourceAspectRatio` covers 16:9-to-square, 16:9-to-header, portrait-to-header, and invalid-dimension fallback geometry.
- Native Direct3D 11 captures covered both fixture artworks, three visible package headers, the selected 210 x 210 artwork, and the full 1920 x 1080 composition.
- Full-screen and both focused source/implementation comparisons were opened and inspected together.
- `git diff --check`: passed after the final source, test, and QA edits.

final result: passed

---

# Song Select selected-mods status QA (2026-08-01)

## Evidence

- Source visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1672 x 941, normalized to 1920 x 1080 without cropping).
- Default native implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-selected-mods-first-pass.png` (1920 x 1080, Direct3D 11).
- Active state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-selected-mods-active.png` (1920 x 1080, Direct3D 11; DT and HD active).
- Destination interaction: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-selected-mods-open-page.png` (1920 x 1080, Direct3D 11).
- Full reference/implementation comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-selected-mods-reference-comparison.png` (normalized reference left, implementation right).
- Focused left-column comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-selected-mods-focused-comparison.png` (reference top, implementation bottom).
- Viewport/state: shared 1920 x 1080 stage at 1x logical density, English locale, 7K filter, first two packages expanded, first-package Marathon x1.3 selected.

## Comparison history

1. The source mock places a Selected Mods action at the right edge of the score-navigation strip. The prior implementation left that area as an unstructured gap and exposed Mods only in the footer.
2. The implementation adds a 198 x 40 status/action aligned to the existing 40 px score tabs. Player-count text shifts left to retain an independent readable slot.
3. The first native comparison found no overlap, clipping, or visual-density issue. The active-state capture then verified real DT/HD summary, count badge, accent change, 1.50x detail refresh, BPM refresh, and difficulty refresh.
4. The destination capture and focused regression verify that clicking the new action opens the existing Gameplay Mods screen while preserving song-select preview continuity.

## Required fidelity review

- Fonts and typography: the action uses Yokko display typography at 8/9 px, matching the compact ranking tabs and footer controls. `SELECTED MODS`, `NONE`, and `DT · HD` remain legible without wrapping or truncation in the captured states.
- Spacing and layout rhythm: the control occupies the reference's right-side action slot and closes the previous empty gap without moving the 850 x 510 ranking body. The 7-plays counter remains separated from both tabs and action.
- Colors and visual tokens: idle uses ivory, navy, and cyan; active state changes summary, count badge, border, and rail to the existing Yokko pink. No glow, gradient, dark panel, or science-fiction styling was added to song select.
- Image quality and asset fidelity: no new bitmap, generated image, sprite-sheet crop, or extracted decoration was introduced. The existing Font Awesome sliders icon is reused at the same optical weight as nearby controls.
- Copy and content: the source's generic action is upgraded to live information. `NONE / 0` communicates the empty state; up to three active acronyms and the full count communicate gameplay state without replacing the dedicated Mods screen.
- Interaction and state: the action uses the same `ToggleModPanel` path as the footer button. Tests cover restored state, empty state, active state, navigation, screen return, and preview-preserving screen classification.
- Responsive/accessibility risk: the shared 1920 x 1080 stage has no overlap or clipping. Mod state is expressed by text and number as well as colour. Keyboard/screen-reader semantics were not established by this visual-focused pass.

## Findings

- P0: none.
- P1: none.
- P2: none after the first reference/implementation comparison.
- Follow-up scope: the existing Gameplay Mods destination has its own visual language and was not represented in the selected song-select mock. Its styling should be reviewed as a separate full-screen redesign rather than hidden inside this small song-select change.

## Verification

- Isolated `Yokko.Game` build: passed with 0 errors; the initial member-name warning was corrected and the final build has 0 warnings.
- `TestPlayPushesGameplay`: 1 passed, 0 failed after adjusting the regression to respect restored user Mod state.
- Full `TestSceneSongSelectScreen`: 10 passed, 0 failed.
- Native Direct3D 11 captures cover idle, DT/HD active, and destination-open states at 1920 x 1080.
- Full and focused normalized comparisons were opened and inspected together.
- `git diff --check`: passed before the QA report update.

final result: passed

---

# Gameplay Mods paper-layout QA (2026-08-01)

## Evidence

- Prior native implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-selected-mods-open-page.png` (1920 x 1080, Direct3D 11).
- Yokko visual-language reference: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (normalized to 1920 x 1080 for comparison).
- Final native implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\gameplay-mods-paper-layout-final.png` (1920 x 1080, Direct3D 11; HR focused, HT and HR active).
- Alternate state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\gameplay-mods-paper-layout-config.png` (1920 x 1080, Direct3D 11; Accuracy Challenge focused, three active Mods, 0.75x).
- Before/after comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\gameplay-mods-before-after-first-pass.png`.
- Source/implementation comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\gameplay-mods-style-reference-comparison-1920.png` (normalized reference left, implementation right).

## Comparison history

1. The prior page used orbit rings, node labels, compass ticks, telemetry copy, signal scanners, waveforms, hazard stripes, and a full cyan footer. These layers overpowered the actual tasks of choosing Mods, adjusting rate, reviewing active Mods, and confirming.
2. The first implementation replaced those layers with three paper cards: category navigation, Mod browsing/focus, and rate plus active selection. It retained the existing Mod state and interaction owners.
3. The first native comparison found the remaining empty-slot signal rails visually inconsistent. They were replaced with quiet pale-cyan cards and the page was captured again.
4. The final source/implementation comparison confirms the same ivory paper, navy typography, white card, cyan/pink state, and yellow primary-action language as the selected Song Select direction.

## Required fidelity review

- Layout and hierarchy: categories are isolated on the left, the primary Mod task owns the widest centre column, and rate/active selection occupy the right summary card. Back, reset, and Done remain fixed in a quiet footer.
- Typography and copy: hierarchy uses the existing `HomeTypography` family. Technical phrases such as `NODE`, `SYNC`, `INPUT ROUTE`, `MOD BUS`, and `LIVE 120HZ` are no longer visible.
- Colors and surfaces: the page uses the existing ivory, navy, cyan, pale cyan, yellow, and pink tokens. Gradients, dark panels, scanner effects, and the cyan footer were removed.
- Assets: no new bitmap, generated component, screenshot crop, or extracted decoration was added. Existing paper and logo textures plus Font Awesome icons are reused.
- Interaction: category selection, Mod focus/cycle/toggle, rate slider and presets, active-row focus/removal, reset, back, and Done continue through the existing callbacks. Mod state remains owned by `GameplayModsScreen`.
- Responsive/accessibility risk: both captures use the shared 1920 x 1080 reference stage. Active state is communicated by acronym, row membership, count, border, and colour rather than colour alone.

## Findings

- P0: none.
- P1: none.
- P2: none after replacing the old empty-slot signal rails.
- P3: the dedicated Mods workspace still uses its legacy authored 1600 x 900 inner coordinate system inside the shared 1920 x 1080 stage. It is visually centred and correct at the reference viewport, but a later structural pass can migrate the inner canvas without changing this design.

## Verification

- Isolated `Yokko.Game` build: passed with 0 warnings and 0 errors.
- Isolated `Yokko.Game.Tests` build after the final visual edit: passed with 0 warnings and 0 errors.
- `TestSceneGameplayModsScreen`: 11 passed, 3 failed. The failures are pre-existing expectation gaps outside this visual shell: a stale 15-item browser count, a 1920 x 1080 minimum-stage assertion against the existing Large-scale target, and connector-transition coverage although the HEAD implementation does not construct connectors.
- Native Direct3D 11 captures covered two categories, two playback rates, two and three active-Mod states, focus state, empty active slots, and the final footer.
- Before/after and normalized source/implementation comparisons were opened and inspected together.
- `git diff --check`: passed after the final source and QA edits.

final result: passed with pre-existing focused-test gaps documented

---

# Gameplay Mods card-browser QA (2026-08-01)

## Evidence

- osu!lazer information-architecture reference: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\osu-lazer-mod-select-reference-2024.jpg` (normalized to 1920 x 1080 for comparison; used for stacked Mod-card hierarchy, not surface styling).
- Yokko visual-language reference: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (normalized to 1920 x 1080; used for paper, typography, colour, and spacing language).
- First native card pass: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\gameplay-mods-card-browser-first-pass.png` (1920 x 1080, Direct3D 11).
- Final English implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\gameplay-mods-card-browser-final-en.png` (1920 x 1080, Direct3D 11; Difficulty Up, HR focused, HT and HR active).
- Final Chinese implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\gameplay-mods-card-browser-final-zh.png` (1920 x 1080, Direct3D 11; localized visible copy and active count).
- osu!lazer/full-screen comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\gameplay-mods-card-browser-osu-reference-comparison.png`.
- Yokko-style/full-screen comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\gameplay-mods-card-browser-yokko-style-comparison.png`.
- Focused browser comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\gameplay-mods-card-browser-focused-comparison.png` (osu!lazer Mod columns left, Yokko card browser right).

## Comparison history

1. The prior paper pass removed the science-fiction shell but kept circular Mod nodes around a large focus card. That arrangement consumed space and made consecutive Mod names, descriptions, and family states harder to scan than osu!lazer's stacked panels.
2. The central region now uses one ordered 390 x 60 card column. Every card presents acronym, name, one-line description, family position when relevant, and enabled state in a consistent reading path; focus and activation callbacks remain unchanged.
3. The first native capture exposed two visible issues: long descriptions could occupy a clipped second line, and the right focus panel lacked a semantic heading. The final pass constrains descriptions to one line and adds the localized Mod-details heading.
4. Final English and Chinese native captures plus the three combined comparisons confirm that the hierarchy follows osu!lazer's browsing model while the surface remains Yokko's ivory-paper visual language.

## Required fidelity review

- Fonts and typography: existing `HomeTypography` remains the only display family. Names, acronyms, descriptions, category labels, rate, active count, and CJK strings form a readable hierarchy without clipping in the final captures.
- Spacing and layout rhythm: the 390 x 60 cards share one x origin and a 66 px vertical cadence, allowing the six-item dense category to be compared without orbiting labels or pointer travel. The detail panel remains visually secondary to the browser.
- Colours and surfaces: existing ivory, white, navy, cyan, pink, pale cyan, and yellow tokens are retained. Selection uses outline and check state; enabled state remains visible through colour, text, and the active-summary row.
- Image and asset quality: no bitmap, generated UI component, screenshot crop, or extracted decoration was introduced. The pass therefore adds no new cut-out edge or scaling risk.
- Copy and localization: technical/sci-fi copy remains absent. Browser title, hint, detail heading, active count, and category label use localization keys; both English and Chinese screenshots were verified.
- Interaction and state: category navigation, card focus, family cycling, enable/disable, speed presets and slider, active-row focus/removal, reset, back, and Done still route through the existing screen state and callbacks.
- Responsive/accessibility risk: the screen minimum now uses the shared 1920 x 1080 reference stage. State is not communicated by colour alone. Keyboard and screen-reader semantics remain outside this visual-focused pass.

## Findings

- P0: none.
- P1: none.
- P2: none after correcting description clipping and adding the detail heading.
- P3: internal class names still retain the legacy `Orbit` terminology even though no orbit connectors or circular-node layout remain. This is invisible and can be handled later as a non-visual cleanup without risking the current interaction path.
- P3: the osu!lazer screenshot is a 2024 reference and is used only to validate stable information architecture; current upstream source still describes horizontally scrollable Mod columns and `ModPanel`-based items.

## Verification

- Isolated `Yokko.Game` build: passed with 0 warnings and 0 errors.
- Full `TestSceneGameplayModsScreen`: 15 passed, 0 failed.
- Added geometry coverage verifies six dense-category browser cards at 390 x 60 on one scan column.
- Updated transition coverage verifies that the card browser does not recreate orbit connectors while retaining activation feedback and Mod add/remove behaviour.
- Native Direct3D 11 captures cover English and Chinese at 1920 x 1080, focus state, two active Mods, rate controls, empty slots, and footer actions.
- Both normalized full-screen comparisons and the focused source/implementation comparison were opened and inspected together.
- `git diff --check`: passed for all touched Gameplay Mods source, localization, test, and QA files.

final result: passed

---

# Song Select square-cover and grade-hierarchy QA (2026-08-01)

## Evidence

- User-reported problem capture: `C:\Users\nyafa\AppData\Local\Temp\codex-clipboard-0afa7bc3-e844-4221-9973-ef4b8065fe16.png` (918 x 1079 crop; package artwork reads as a shallow banner and ranking grades appear as a solid colour column).
- Yokko visual-language reference: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1672 x 941, normalized to 1920 x 1080 for the full-style comparison).
- Current-code baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-cover-grade-current-baseline.png` (1920 x 1080, Direct3D 11).
- First implementation pass: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-square-covers-grade-badges-first-pass.png` (1920 x 1080, Direct3D 11).
- Final implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-square-covers-grade-badges-final.png` (1920 x 1080, Direct3D 11; English locale, 7K filter, two expanded packages, Marathon x1.3 selected, DT and HD active).
- Full before/final comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-cover-grade-final-before-after.png` (equal 1920 x 1080 states side by side).
- Yokko-style/final comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-cover-grade-final-style-comparison.png` (normalized visual reference left, implementation right).
- Focused cover comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-square-covers-final-focused-comparison.png` (baseline left, final right).
- Focused ranking comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-grade-badges-final-focused-comparison.png` (baseline left, final right).

## Comparison history

1. The user capture exposed two hierarchy problems: wide package-image slots made artwork read as flattened banners, while full-height grade fills competed with player name and score.
2. Current code had already removed actual texture stretching through aspect-preserving centre crop and had reduced the old grade column to compact marks. The fresh native baseline nevertheless showed that package frames remained 104 x 84 and that the final grade sat inside the decorative paper tape's unsafe corner.
3. The first implementation changed each package frame to an exact 84 x 84 crop, moved package metadata 20 px left into the recovered width, and converted grades to 30 x 26 low-opacity semantic badges inside the existing 36 x 32 table slot.
4. The first focused comparison confirmed the cover and badge hierarchy, but the ranking paper's baked lower-right tape still visually touched the seventh grade. The final pass moved the score and grade columns 54/60 px left, creating a consistent safe inset without changing row height or table interaction.
5. The final full, cover-focused, ranking-focused, and Yokko-style comparisons were opened and inspected together. No new P0/P1/P2 issue remained.

## Required fidelity review

- Fonts and typography: existing `HomeTypography` remains unchanged. Package titles retain their 18/15 px adaptive hierarchy; score remains the dominant 20 px value; grade letters drop to 15 px and use semantic grade colour rather than a heavy dark block.
- Spacing and layout rhythm: package art is exactly 84 x 84 inside the existing 84 px header, so every package begins with a stable square anchor. Metadata starts at x=128, star/play indicators shift with it, and the recovered 20 px expands title width instead of introducing dead space. Ranking row height stays 52 px and badge occupancy stays 36 x 32.
- Colours and tokens: grades use the existing cyan/green/yellow/pink semantic palette at 8-14% fill and 28-48% border opacity. The selected-player row remains identified by the established pink outline and ivory-yellow surface; grade colour does not replace selection state.
- Image quality and asset fidelity: all covers use their real loaded textures, preserve source aspect ratio, and centre-crop through a masked square rather than stretching. No new generated image, crop file, placeholder, or resource-library cut-out was introduced.
- Copy and content: no static or dynamic song metadata changed. Long package titles, song/chart counts, mapped-by copy, difficulty rating, key mode, score, and grade remain visible in the final capture.
- Interaction and state: package expand/collapse, selected package guide, selected chart, difficulty filter, active Mods summary, ranking tab switch, and player row state remain on their existing code paths. The visual changes do not rebuild the virtual list or change score data.
- Responsive/accessibility risk: final evidence uses the shared 1920 x 1080 reference stage. Artwork identity and grades are conveyed through image/content and letter labels, not colour alone. Alternate UI scales were not part of this narrowly scoped desktop pass.

## Findings

- P0: none.
- P1: none.
- P2: none after adding the ranking safe inset in the final pass.
- P3: centre-cropping a highly panoramic source into a square may trim edge subjects. This is preferable to distortion for the current library and can later be augmented with per-cover focal metadata if real charts demonstrate a systematic problem.
- P3: the ranking paper's lower-right tape remains a baked visual asset. Dynamic content now avoids its unsafe area, so replacing or re-editing that resource is not required for this pass.

## Verification

- Isolated `Yokko.Game.Tests` build: passed with 0 warnings and 0 errors.
- Focused Song Select filter: 25 passed, 0 failed across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList`.
- Geometry coverage verifies package headers expose an 84 x 84 artwork frame and landscape/portrait textures retain their source aspect ratio while filling it.
- Ranking coverage verifies seven 36 x 32 grade components, the -70 px safe inset, multiple grade states, and one highlighted current player.
- Native preview confirms Direct3D 11 and a 1920 x 1080 window; the final screenshot covers square package art, expanded/collapsed package state, selected chart, seven ranking rows, grades S/A, and the decorative safe area.
- Full-view and both focused equal-state comparisons were opened and inspected together; the normalized Yokko-style comparison was also reviewed for palette and typography continuity.
- `git diff --check`: passed for the touched Song Select source, focused tests, and QA report.

final result: passed

---

# Song Select difficulty-stack hierarchy QA (2026-08-01)

## Evidence

- Equal-state source baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-square-covers-grade-badges-final.png` (1920 x 1080, Direct3D 11; expanded package rows still repeat song titles and carry a broad difficulty tint).
- Final implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-difficulty-stack-v1.png` (1920 x 1080, Direct3D 11; English locale, 7K filter, two expanded packages, Marathon x1.3 selected).
- Focused before/final comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-difficulty-stack-comparison.png` (same viewport and state, vertically aligned around the complete right browser).
- Information-hierarchy reference: osu!lazer Song Select V2, used for compact difficulty comparison and trailing metadata rather than colour or surface cloning.

## Comparison history

1. The baseline made every child row repeat its package song title as the largest label. Difficulty name was demoted into a small metadata sentence, so adjacent Normal, Hard, and Marathon variants could not be compared on the primary reading line.
2. The final pass promotes difficulty name to the 15 px display line, leaves only `mapped by` as the quiet secondary line, and keeps rating plus key mode on a stable trailing axis. The expanded package header remains the single source of song identity.
3. Compact-row border opacity drops from 38% to 16%, the broad accent wash from 4.5% to 1.8%, and the resting shadow from 12% to 8%. Difficulty colour now behaves as a narrow rail and rating cue instead of tinting the entire repeated list.
4. The selected row retains the established yellow paper surface, pink pointer, outline, focus depth, and animated key-mode disclosure. Row height, virtualisation, proximity curve, expand/collapse, and selection callbacks are unchanged.

## Required fidelity review

- Fonts and typography: existing `HomeTypography` is retained. Difficulty names now form the strongest child-row hierarchy; mapper text remains readable but deliberately secondary. Long difficulty names truncate inside a 560 px safe width before the rating column.
- Spacing and layout rhythm: all children remain 58 px high. Primary labels share x=24, rating readouts share x=628, resting mode pills share x=780, and the selected 116 px pill begins at x=718, leaving a 26 px gap after rating metadata.
- Colours and surfaces: existing ivory, navy, pink, yellow, cyan, and semantic difficulty accents are unchanged. The reduced wash and border remove the pale-green table appearance while preserving package grouping.
- Image and asset quality: no bitmap, generated decoration, extraction, screenshot crop, or resource-library component was added. Existing square package covers keep their aspect-preserving centre crop.
- Copy and content: dynamic difficulty name, creator, rating unit/value, and key mode remain present. Repeated song titles are intentionally removed from package children because the parent header already identifies the song.
- Interaction and state: package expansion, selected-row curve, hover depth, rating-mode updates, key-mode disclosure, list pooling, and chart selection remain on their existing in-place code paths.
- Responsive/accessibility risk: evidence uses the shared 1920 x 1080 desktop reference stage. Selection is conveyed by surface, outline, pointer, and pill treatment rather than colour alone. Alternate UI scales remain outside this focused desktop pass.

## Findings

- P0: none.
- P1: none.
- P2: none after removing repeated primary song titles and reducing full-row difficulty tint.
- P3: the selected key-mode pill uses concise English status copy in the English preview. A future localisation pass can expose the status token if Song Select receives complete locale-specific copy coverage.

## Verification

- Focused Song Select filter: 26 passed, 0 failed across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList`.
- Added semantic coverage verifies compact package rows lead with `DifficultyName`, do not repeat `Title`, and retain mapper metadata.
- Existing in-place rating-mode and progressive key-mode tests pass with the new trailing geometry; list objects and logical item count remain stable.
- Native preview confirms Direct3D 11 and 1920 x 1080; the screenshot covers multiple difficulty names, ratings, key modes, selection, package guides, square covers, and the ranking panel in the same frame.
- The equal-state focused comparison was opened and inspected together. No text overlap, clipping, cover distortion, new asset artifact, or broken row cadence is visible.
- `git diff --check`: passed before appending this QA section.

final result: passed

---

# Song Select browser-shell continuity QA (2026-08-01)

## Evidence

- Equal-state source baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-difficulty-stack-v1.png` (1920 x 1080, Direct3D 11; four independently rounded browse cards and unframed package chevrons).
- Final implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-browser-shell-v1.png` (1920 x 1080, Direct3D 11; continuous browse rail, explicit package controls, expanded guide state, and the previously approved difficulty stack).
- Focused before/final comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-browser-shell-comparison.png` (same 900 x 770 right-browser crop from equal 1920 x 1080 states, stacked at native density).
- Visual language reference: the current approved Yokko paper screen and osu!lazer Song Select browsing hierarchy; no upstream surface colours or branded assets were copied.

## Comparison history

1. The baseline browse toolbar represented Sort, Group, Library, and Converts as four separate rounded cards with 8 px gaps. Together with the independent difficulty bar and package cards, the top of the browser read as a dashboard of unrelated widgets.
2. The final toolbar uses one 850 x 34 masked ivory surface with a single cyan border. Four existing controls remain interactive inside it at x=0, 188, 376, and 610, separated by quiet 20 px vertical rules instead of repeated borders and corners.
3. Package chevrons previously floated at the far right with no visible target. Each header now provides a 34 x 34 pale-cyan icon surface, a subtle expanded bottom rail, and hover feedback across the title surface. The square artwork, title, chart count, selected rail, and child guide retain their established positions.
4. The focused comparison confirms a continuous path from browse controls to package header to difficulty rows without changing browser height, row density, selected-chart content, or cover crop.

## Required fidelity review

- Fonts and typography: all labels continue to use `HomeTypography`; toolbar label/value weights and package-title hierarchy are unchanged. Removing card gaps gives the values more stable alignment without shrinking or truncating text.
- Spacing and layout rhythm: search, difficulty filter, toolbar, and browser remain at y=78, 136, 176, and 220. The toolbar occupies the same 850 x 34 envelope, so no vertical content is lost. Package headers remain 850 x 84 and children remain 58 px high.
- Colours and surfaces: existing ivory, navy, cyan, pale-cyan, pink, and yellow tokens are retained. Active Converts still uses pale cyan plus its pink rail; resting segments now inherit the shared ivory surface instead of repainting four copies.
- Image and asset quality: no bitmap, extracted decoration, generated image, screenshot crop, placeholder, or new resource was introduced. Package covers continue to use real textures with aspect-preserving square centre crops.
- Copy and content: Sort, Group, Library, Converts, package names, song/chart counts, difficulty names, mappers, ratings, and key modes remain intact. This pass changes grouping and affordance, not product copy.
- Icons: existing FontAwesome sort, group, archive, exchange, star, play, and chevron icons are retained. Chevron size remains 13 px but gains a visible 34 px surface; the full 850 x 84 package header remains clickable.
- Interaction and state: sort/group/converts callbacks, non-interactive Library state, difficulty filtering, package expand/collapse, selected package guide, chart selection, pooling, and virtualisation continue on the existing code paths. Hover feedback uses short existing easing and does not alter state.
- Responsive/accessibility risk: evidence uses the shared 1920 x 1080 desktop stage. Active state remains identifiable through fill and a pink rail; expanded state is shown through arrow direction, bottom rail, and visible children. Keyboard-specific traversal was not expanded in this visual pass.

## Findings

- P0: none.
- P1: none.
- P2: none after replacing the four isolated toolbar cards and giving package expansion an explicit target and connector.
- P3: the difficulty filter remains its own full-width row above the toolbar. It is intentionally retained because combining a draggable range track with four click targets would reduce precision at the current 850 px width.

## Verification

- Focused Song Select filter: 26 passed, 0 failed across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList`.
- Toolbar coverage verifies four 34 px controls share an 850 x 34 parent surface, keep three interactive actions, and no longer draw individual borders.
- Package-header coverage verifies square artwork, expanded bottom rail, guide stem, selected rail, explicit chevron surface, and pooled collapsed-state reset.
- Native preview confirms Direct3D 11 and 1920 x 1080; the final capture covers active Converts, expanded and collapsed packages, selected chart, ratings, key modes, square covers, and footer controls.
- The focused equal-state comparison was opened and inspected together. No text collision, cover deformation, icon clipping, row-density drift, or new asset artifact is visible.
- `git diff --check`: passed before appending this QA section.

final result: passed

# Song Select selected-details hierarchy QA (2026-08-01)

## Evidence

- Equal-state baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-browser-shell-v1.png` (1920 x 1080, Direct3D 11; selected chart metadata and personal performance compressed into one bottom row).
- Final implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-details-hierarchy-v1.png` (1920 x 1080, Direct3D 11; English locale, 7K, Marathon x1.3, 1.50x, DT + HD).
- Focused before/final comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-details-hierarchy-comparison.png` (same 880 x 275 selected-details crop from equal states, stacked at native density).
- Visual language reference: the approved Yokko ivory-paper Song Select screen and osu!lazer's separation of current chart facts from player performance; no osu! brand asset or surface styling was copied.

## Comparison history

1. The baseline placed artist and mapper low in the identity block, then compressed length, BPM, notes, best score, and best accuracy into one undifferentiated row. The last two values looked like chart metadata rather than player state.
2. The final layout moves artist and mapper to y=112 and y=139, then groups length, BPM, and notes in a dedicated 360 x 34 chart-facts row at y=164.
3. A quiet 575 px divider separates immutable chart facts from a 360 x 42 personal-performance row at y=207. Best score and best accuracy now use two 164 px fields with an internal divider, preserving room for real formatted values.
4. The long two-line title, square artwork, selected difficulty pill, playback-rate badge, and MSD badge retain their previous anchors. The focused comparison confirms that the new hierarchy did not introduce overlap or reduce artwork prominence.

## Required fidelity review

- Fonts and typography: all dynamic text continues to use `HomeTypography`. Title, artist, mapper, labels, and values keep their established weights and colours; the split rows improve scan order without introducing a new type style.
- Spacing and layout rhythm: the 850 x 255 paper card and 210 x 210 cover are unchanged. Title-to-artist spacing remains comfortable for the two-line stress case. Both metadata rows fit above the paper's lower contour without clipping.
- Colours and surfaces: existing ivory, navy, cyan, yellow, and pink tokens are retained. New separators use the existing navy at 13-14 percent opacity, avoiding another card or bordered badge.
- Image and asset quality: no bitmap, cutout, generated decoration, screenshot crop, placeholder, or new resource was introduced. The selected artwork remains a real source texture in the approved aspect-preserving square crop.
- Copy and content: song title, artist, mapper, length, BPM, note count, best score, best accuracy, difficulty, playback rate, and rating remain dynamic. No product copy was replaced or baked into an image.
- Icons: existing FontAwesome clock, waveform, music, trophy, and target icons are retained at 11-12 px and aligned with their labels. No text symbol or handcrafted vector substitute was added.
- Interaction and state: selection changes still rebuild only the active details layer, and playback-rate changes continue to update rate, BPM, difficulty, and duration through the existing in-place path. Search, filters, package expansion, difficulty selection, mods, and Play remain functional in the captured state.
- Responsive/accessibility risk: the validated surface is Yokko's shared 1920 x 1080 desktop stage. Long-title wrapping and empty performance values were stress-tested visually. Keyboard traversal and non-desktop breakpoints were not expanded by this layout-only pass.

## Findings

- P0: none.
- P1: none.
- P2: none after separating chart facts from personal performance and giving formatted score/accuracy values adequate width.
- P3: the right side below the MSD badge remains intentionally quiet. Filling it with another decorative card or inferred metric would weaken the title/rating hierarchy and reintroduce dashboard density.

## Verification

- Focused Song Select filter: 26 passed, 0 failed, 0 skipped across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList`.
- Geometry coverage verifies the chart-facts row at (252, 164) / 360 x 34 and performance row at (252, 207) / 360 x 42.
- Native preview confirms Direct3D 11 at 1920 x 1080 with the long two-line title, selected 7K chart, 1.50x rate, DT + HD, and empty personal-result state.
- The focused equal-state comparison was opened and inspected together. No title collision, value clipping, cover deformation, border residue, icon drift, or new asset artifact is visible.
- `git diff --check`: passed before appending this QA section.

final result: passed

# Song Select ranking-header integration QA (2026-08-01)

## Evidence

- Selected visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1792 x 1000 generated Song Select mock selected by the user; compact ranking tabs and Selected Mods share the leaderboard surface).
- Equal-state implementation baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-details-hierarchy-v1.png` (1920 x 1080; paper begins below the header while tabs and a 198 px Mods card float over the wallpaper).
- Final implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-header-final.png` (1920 x 1080 native Direct3D 11 capture; English locale, 7K, Marathon x1.3, 1.50x, DT + HD).
- Full-view visual-target comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-header-style-reference-full.png` (source normalized from 1792 x 1000 to 960 x 536; implementation normalized from 1920 x 1080 to 960 x 540; device scale 1).
- Focused visual-target comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-header-reference-comparison.png` (selected mock and final ranking-header crops normalized to the same 850 px width).
- Equal-state before/final comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-header-comparison.png` (identical 870 x 485 left-ranking crop, stacked at native density).

## Comparison history

1. Baseline P2: the ranking paper started 42 px below the panel origin, leaving GLOBAL, MY HISTORY, play count, and Selected Mods floating on the wallpaper. The 198 x 40 two-line Mods card also read as an unrelated overlay instead of the right-hand action in the ranking rail.
2. First implementation: the existing `paper-ranking` texture moved to the panel root and now spans 850 x 464, while row content remains at y=42. This places the tabs, play count, divider, and rows on one continuous real paper asset without stretching a screenshot or generating another decoration.
3. First implementation: Selected Mods became 154 x 40 at x=696, preserving its live summary and count while matching the selected mock's compact right-aligned control. The former left status bar became a 3 px bottom rail aligned with the ranking tab underlines.
4. First visual pass P2: active DT + HD still produced a hot-pink full outline that competed with the selected ranking row. The final pass keeps the button outline quiet navy at 24 percent opacity and carries active state through the pink summary, count badge, and bottom rail.
5. Post-fix evidence: the focused visual-target comparison and equal-state comparison were opened together. The header now reads as one paper-backed rail; the selected player's pink row remains the primary ranking emphasis.

## Required fidelity review

- Fonts and typography: existing `HomeTypography` display/body faces remain unchanged. The selected mock uses Details/Ranking, while the implementation intentionally keeps the functional GLOBAL/MY HISTORY views. Labels, 7 PLAYS, dynamic mod summary, and count remain legible with no truncation in the active two-mod state.
- Spacing and layout rhythm: the ranking panel remains 850 x 510 and the row viewport remains 850 x 422. Paper now begins at (0, 0) and ends at y=464; content still begins at y=42. The 154 px Mods control ends exactly at x=850 and shares the 40 px header height with both tabs.
- Colours and visual tokens: ivory paper, navy text, cyan icon, yellow active-ranking underline, and pink mod state all reuse existing Yokko tokens. The final quiet navy button outline prevents the action from competing with the selected score row.
- Image quality and asset fidelity: the existing lossless `SongSelect/Cute/paper-ranking` resource is reused at the full header-plus-row slot. No resource cutout, generated bitmap, screenshot slice, placeholder, custom SVG, text glyph icon, or approximate decorative asset was introduced.
- Copy and content: GLOBAL, MY HISTORY, play count, player names, mods, combo, accuracy, score, grades, SELECTED MODS, active summary, and count remain live product data. The generated mock's Details/Ranking wording was not copied because it would misrepresent the two implemented data views.
- Icons: existing FontAwesome users, archive, sliders, and ranking-row grade components remain. The Mods sliders icon is reduced from 13 px to 12 px to match its tighter control while retaining the same icon family and optical alignment.
- States and interactions: both ranking tabs remain clickable, the ranking body keeps its existing view toggle, and Selected Mods still opens the dedicated mods screen without stopping the preview. Active and empty mod states continue to update through `SetState`; hover feedback remains the existing pale-cyan surface change.
- Accessibility and viewport resilience: the implemented target is Yokko's shared 1920 x 1080 desktop stage. Tab and Mods targets retain a 40 px height, colour is not the only mod-state cue because summary/count also change, and no footer overlap or header clipping is visible. Non-desktop breakpoints and explicit keyboard focus traversal were not expanded in this layout pass.

## Findings

- P0: none.
- P1: none.
- P2: none after integrating the paper surface and reducing the active Mods outline.
- P3: the implementation keeps seven 52 px ranking rows, slightly roomier than the selected mock. This is accepted because the runtime includes two-line per-player metadata and the full 1920 x 1080 stage has the vertical room without hiding the footer.

## Verification

- Focused Song Select filter: 26 passed, 0 failed, 0 skipped across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList` after the final colour pass.
- Geometry coverage verifies paper at (0, 0) / 850 x 464 and Selected Mods at (696, 286) / 154 x 40 in the full screen.
- Existing interaction coverage verifies ranking view switching, Mods screen opening/return, preview continuity, active-mod reflection, rate updates, filters, package interaction, and bounded virtualisation.
- Native final preview confirms Direct3D 11 at 1920 x 1080. No paper seam, header collision, control clipping, selected-row competition, footer overlap, or new asset artifact is visible.
- Full-view, focused target, and equal-state before/final comparisons were each opened and inspected as combined images.
- `git diff --check`: passed before appending this QA section.

final result: passed

# Song Select ranking-metric grid QA (2026-08-01)

## Evidence

- Selected visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1792 x 1000 selected Song Select mock; ranking rows read player, score, accuracy, combo, and grade from left to right).
- Equal-state baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-header-final.png` (1920 x 1080; combo and accuracy share a stacked middle column while the 20 px score is pushed beside the grade).
- First implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-grid-v1.png` (1920 x 1080; separate score, accuracy, and combo columns, with an initially heavy 18 px score).
- Final implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-grid-final.png` (1920 x 1080 native Direct3D 11 capture; English locale, 7K, Marathon x1.3, 1.50x, DT + HD).
- Full-view target/final comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-grid-style-reference-full.png` (source normalized from 1792 x 1000 to 960 x 536; implementation normalized from 1920 x 1080 to 960 x 540; device scale 1).
- Focused target/final comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-grid-reference-comparison.png` (ranking-row crops normalized to 870 px width and inspected together).
- Equal-state before/final comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-grid-comparison.png` (identical 870 x 410 ranking crop, stacked at native density).

## Comparison history

1. Baseline P2: combo and accuracy occupied the same x=402 column on separate lines, while score used a large right-anchored 20 px treatment beside the grade. This made each row read player, combo, accuracy, score, grade rather than the selected mock's stable numeric sequence.
2. First implementation: score, accuracy, and combo received independent right edges at x=476, x=586, and x=696. Numeric origins are top-right, so varying digit counts align by their least-significant edge instead of shifting the whole column.
3. First visual pass P2: the 18 px score still carried more weight than the selected mock and visually pulled attention away from player identity. The final pass reduces it to 16 px and moves its baseline to y=16, while accuracy and combo remain 12 px at y=18.
4. Post-fix evidence: full-view, focused visual-target, and equal-state comparisons were opened. Seven real rows, four score grades, long formatted scores, percentages, four-digit combos, the current-player selection, and the paper trim all remain clear and collision-free.

## Required fidelity review

- Fonts and typography: player name remains the 15 px identity anchor and Mod labels remain an 8 px secondary line. Score now uses the same `HomeTypography.Display` family at 16 px; accuracy and combo use 12 px display numerals at a shared optical baseline. No fallback, wrap, truncation, or cramped-number issue is visible.
- Spacing and layout rhythm: rank, 42 px avatar, 218 px identity slot, and 52 px row height are unchanged. The three metric right edges create 110 px and 110 px intervals, followed by a 52 px breathing gap before the compact grade badge.
- Colours and visual tokens: score retains primary navy; accuracy and combo use the existing 68 percent navy secondary token. Grade colours and pink current-player emphasis are unchanged, preventing the numeric grid from introducing another semantic colour system.
- Image quality and asset fidelity: existing avatar textures and the real ranking-paper asset remain untouched. No country flag was invented because `SongSelectScore` has no country field and no verified per-player flag resource; no cutout, generated bitmap, screenshot crop, placeholder, custom SVG, or text-glyph asset was added.
- Copy and content: player name, Mod summary, score, accuracy, combo, and grade remain dynamic. The only content-order change is visual positioning; numeric formatting (`N0`, `P2`, and the multiplication sign) is preserved.
- Icons: avatar frames and compact grade badges remain unchanged. The selected mock's flags were intentionally omitted instead of faking unsupported profile data or adding unverified assets.
- States and interactions: global/history switching, current-player row emphasis, ranking-body toggle, Selected Mods, selection transitions, filters, and gameplay launch continue through existing paths. This pass introduces no static replacement for live ranking data.
- Accessibility and viewport resilience: right alignment makes scores with different digit counts easier to compare. Primary and secondary numeric contrast remains readable on ivory; the 1920 x 1080 target has no overlap with paper trim or footer. Explicit keyboard focus and non-desktop stages were not expanded in this layout-only pass.

## Findings

- P0: none.
- P1: none.
- P2: none after separating the numeric columns and reducing score emphasis.
- P3: the selected mock includes country flags between avatar and player name. Yokko's current score model contains no country value, so omitting this unsupported decoration is preferable to a fake or unreliable asset until profile data provides a real source.

## Verification

- Focused Song Select filter: 26 passed, 0 failed, 0 skipped across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList` after the final typography pass.
- Focused geometry coverage verifies three distinct metric right edges at x=476, x=586, and x=696; existing grade tests verify all badges stay inside the 52 px row and only the current player receives highlighted treatment.
- Native final preview confirms Direct3D 11 at 1920 x 1080. No score/name overlap, metric collision, grade clipping, row-height drift, paper artifact, or footer overlap is visible.
- Full-view, focused target, and equal-state before/final comparisons were opened and inspected as combined images after the final capture.
- `git diff --check`: passed before appending this QA section.

final result: passed

# Song Select ranking-view states QA (2026-08-01)

## Evidence

- Selected visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1792 x 1000 selected Song Select mock; ranking controls and content share one paper surface).
- Global implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-state-global.png` (1920 x 1080 native Direct3D 11 capture with seven live rows).
- Personal empty implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-state-personal.png` (1920 x 1080 native capture after switching to MY HISTORY).
- Settled switch capture: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-state-transition-850.png` (state captured 150 ms after the scheduled view change).
- Focused state board: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-state-board.png` (selected direction, global state, empty personal state, and settled post-switch state at matched density).

## Comparison history

1. Baseline P2: GLOBAL and MY HISTORY changed data immediately with no contained content transition, and an empty history left the entire ranking paper unexplained.
2. Implementation: each ranking view now owns one content layer. On view change, the outgoing layer fades/slides by 6 px while the incoming layer fades/slides by 8 px, then the stale layer is removed after 180 ms.
3. Implementation: the active tab label and underline transition over 130 ms, while play count remains live for the selected view. Re-selecting the current tab is a no-op and does not restart the transition.
4. Implementation: zero-result views use a compact archive/users icon, one display headline, and one explanatory body line directly on the existing paper. No nested empty-state card, border, illustration, or decorative resource was introduced.
5. Visual inspection: global rows retain the previously approved horizontal score/accuracy/combo grid. Personal history settles to one centred, low-density empty state with MY HISTORY in pink, GLOBAL inactive, and `0 PLAYS` aligned to the existing rail.

## Required fidelity review

- Fonts and typography: empty-state headline uses the existing 16 px Yokko display face; its 11 px body line uses the existing body face and muted navy token. Neither line wraps or competes with the header.
- Spacing and layout rhythm: the empty state is centred in the existing 850 x 422 row viewport, leaving intentional paper breathing room. The 40 px header and Selected Mods alignment remain unchanged.
- Colours and visual tokens: active personal state reuses Yokko pink, global state reuses yellow/cyan ranking accents, and empty copy stays navy on ivory paper. No sci-fi glow or dark overlay was added.
- Image quality and asset fidelity: the existing ranking-paper texture remains the only art surface. FontAwesome archive/users icons are standard project icons; no new cutout, generated bitmap, screenshot slice, placeholder, or approximate decorative asset was added.
- Copy and content: `NO LOCAL PLAYS YET` and `PLAY THIS CHART TO START YOUR HISTORY` describe the actionable empty condition without pretending missing history is an error. Ranking rows and play counts remain dynamic.
- States and interactions: both tabs remain clickable. The switch keeps one final content layer, increments a transition version for verification, and restores global rows without rebuilding or restarting song preview state.
- Accessibility and viewport resilience: the implemented target is Yokko's 1920 x 1080 shared stage. Empty-state meaning is carried by copy as well as icon/colour, tab targets retain their previous hit areas, and no footer or Selected Mods overlap is visible.

## Findings

- P0: none.
- P1: none.
- P2: none after adding the contained state transition and explaining the zero-result view.
- P3: a 760 ms diagnostic screenshot landed on the same scheduler frame that activated MY HISTORY, before the outgoing transform advanced. The 850 ms capture and focused layer assertions confirm the transition settles to one clean personal layer without persistent overlap.

## Verification

- Focused Song Select filter: 26 passed, 0 failed, 0 skipped across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList`.
- Interaction coverage switches global -> personal -> global, waits for one content layer after each transition, verifies the personal empty state, and verifies both transition-version increments.
- Native Direct3D 11 captures at 1920 x 1080 confirm the global and personal end states, selected Mods alignment, paper trim, footer separation, and unchanged right-side list hierarchy.
- The focused state board and all three full captures were opened and inspected. No persistent double image, clipped content, nested-card artifact, or new resource edge is visible.
- `git diff --check`: passed before appending this QA section.

final result: passed

# Song Select browser-focus hierarchy QA (2026-08-01)

## Evidence

- Selected visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1792 x 1000 selected Song Select mock; selected set header carries chart context and expanded difficulties form a compact child stack).
- Equal-state baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-state-global.png` (1920 x 1080; selected package header still shows a generic package title/count and child rows are 58 px high).
- Final first-package state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-browser-focus-v1.png` (1920 x 1080 native Direct3D 11 capture; selected Marathon x1.3 in the first package).
- Final transferred state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-browser-focus-transfer.png` (1920 x 1080 native capture after moving selection to Hard in the next package).
- Full-view target/final comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-browser-focus-full-reference.png` (source and implementation normalized to 930 px width at device scale 1).
- Focused target/final comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-browser-focus-reference-comparison.png` (matched browser-region comparison).
- Equal-state before/final comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-browser-focus-before-after.png` (identical 865 x 655 browser crop).
- Interaction-state comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-browser-focus-state-transfer.png` (first selected package beside the next selected package).

## Comparison history

1. Baseline P2: the selected package retained its generic package name and song/chart count, so the cyan parent focus did not explain which yellow child difficulty would launch. The only detailed current-song context lived in the separate left panel.
2. First implementation: a selected package header now transitions from its package summary to the actual selected song title, artist/mapper byline, key-mode/difficulty pill, and the current live rating. The cyan parent outline therefore names the selection while the yellow child row remains the actionable chart.
3. First implementation: package child rows reduce from 58 px to 52 px and vertically re-centre their primary label, mapper, rating readout, and progressive key-mode pill. This exposes more packages without shrinking the 84 px square package artwork.
4. Interaction pass: moving from Marathon x1.3 to the next package's Hard chart restores the first header's package summary and transfers the contextual header, cyan rail, play indicator, and yellow child selection to the next package without rebuilding the list.
5. Post-fix evidence: full-view, focused target/final, equal-state before/final, and selection-transfer comparisons were opened together. No title/rating collision, stale package context, selected-row ambiguity, artwork deformation, or footer overlap remains.

## Required fidelity review

- Fonts and typography: selected title uses the existing 17 px Yokko display face; artist/mapper and mode/rating metadata use the existing 7-9 px display treatment. Long fixture titles truncate within the header before the chevron and rating column rather than wrapping into the child stack.
- Spacing and layout rhythm: package headers remain 850 x 84 with 84 x 84 square artwork. Child rows are now 850 x 52 with a 5 px inter-item gap; primary text, secondary mapper text, rating, and mode pill retain clear baselines at the tighter density.
- Colours and visual tokens: cyan continues to represent the selected/expanded parent, pale yellow represents the exact child chart, pink carries the current mode pill and play marker, and live rating colour follows the existing difficulty palette. No new glow, gradient, or sci-fi surface was introduced.
- Image quality and asset fidelity: existing song/package wallpaper textures continue through `SongSelectArtworkCrop` with square cover-crop semantics. This intentionally differs from the selected mock's flat banners because the user explicitly rejected flattened covers. No new resource, cutout, generated image, screenshot slice, custom SVG, or approximate decorative art was added.
- Copy and content: selected header title, artist, mapper, key mode, difficulty name, and rating are all live values from `SongSelectEntry`; unselected headers return to package name and song/chart count. No unsupported star count or fake favourite/profile data was invented.
- Icons: the existing FontAwesome star, play marker, and chevron remain in one icon family. Their positions and sizes are unchanged; context is added through live text rather than another decorative symbol.
- States and interactions: package expand/collapse remains clickable, row click/double-click behavior is unchanged, selection transfers contextual header data in place, and difficulty-display mode updates both materialised rows and the active package header.
- Accessibility and viewport resilience: title truncation prevents collision with rating/chevron controls, copy carries selection meaning in addition to colour, and the 1920 x 1080 shared stage keeps all persistent footer controls visible. Non-desktop breakpoints and explicit keyboard focus traversal were not expanded in this desktop layout pass.

## Findings

- P0: none.
- P1: none.
- P2: none after replacing the selected package's generic summary with live chart context and tightening child-row density.
- P3: the selected mock shows five decorative stars in the active header. Yokko keeps one actual rating value instead because a verified five-star semantic is not available for every rating mode; adding five decorative stars would imply unsupported data.

## Verification

- Focused Song Select filter: 26 passed, 0 failed, 0 skipped across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList` from isolated artifacts.
- Focused coverage verifies 52 px compact row height, re-centred inline rating, contextual package-summary crossfade, live title/byline/mode/rating copy, selected guide rail, pooled-header reset, and bounded virtualisation.
- Native Direct3D 11 captures at 1920 x 1080 verify both package-selection end states. The second capture confirms the parent context and exact selected difficulty transfer together.
- Full-view, focused target/final, equal-state before/final, and state-transfer comparisons were opened and inspected as combined images.
- `git diff --check`: passed before appending this QA section.

final result: passed

# Song Select filter and empty-state QA (2026-08-01)

## Evidence

- Selected visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1792 x 1000; osu!lazer-like browser hierarchy expressed in Yokko's ivory-paper language).
- Final populated state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-filter-populated-state-v1.png` (1920 x 1080 native Direct3D 11 capture).
- Final filtered-empty state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-filter-empty-state-v1.png` (1920 x 1080 native capture with query, 7K, MSD minimum, and converted charts hidden).
- Source/final comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-filter-reference-comparison.png` (selected mock and current populated implementation normalized side by side).
- Interaction-state comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-filter-state-comparison.png` (identical browser crop for populated and filtered-empty states).

## Comparison history

1. Baseline P2: filtering to zero results left a single generic `NO SONGS FOUND` label. It did not identify the active conditions or provide an in-context recovery action.
2. Implemented: the browser now distinguishes a genuinely empty library from a filtered-empty library. Filtered-empty copy names the active query, key mode, rating threshold, and converted-chart visibility.
3. Implemented: `CLEAR FILTERS` restores query, key mode, both rating-mode thresholds, and converted charts with one list update while leaving sorting and grouping untouched.
4. Post-fix comparison: the empty state remains inside the established browser region, preserves the toolbar and footer hierarchy, and returns to the populated browser without introducing a modal or a second card surface.

## Required fidelity review

- Fonts and typography: the 18 px Yokko display headline and 11 px body summary reuse the existing Song Select typography. Search terms and filter values remain compact enough for a single line and long queries truncate at 24 characters.
- Spacing and layout rhythm: the 560 x 206 state cluster is centred within the 850 px browser, with a 46 px icon badge, 29 px headline gap, one-line summary, and a 168 x 40 recovery target. It does not shift persistent controls.
- Colours and visual tokens: navy carries primary copy, cyan carries search/range context, pink marks the recovery icon, and the ivory button matches existing Song Select controls. No glow, dark-tech panel, gradient, or sci-fi treatment was added.
- Image quality and asset fidelity: no bitmap, cutout, generated decoration, screenshot slice, custom SVG, or resource-library component was added. The state uses only existing surfaces, typography, and FontAwesome icons.
- Copy and content: the summary is built from real active state rather than fake sample labels. An unfiltered empty library instead reads `NO SONGS IN YOUR LIBRARY` / `IMPORT A BEATMAP TO START PLAYING` and does not offer an irrelevant reset.
- Icons: search and undo use the same FontAwesome family as the surrounding toolbar. No Unicode glyph or approximate decorative drawing is used.
- States and interactions: query changes are reflected in the visible search box; Escape still clears search first; the recovery action clears all browsing constraints in one operation; sort, grouping, selection memory, and preview playback are not reset.
- Accessibility and viewport resilience: the empty reason is explicit text rather than colour alone, the recovery control is 168 x 40, and the full 1920 x 1080 capture retains all footer actions. Keyboard focus traversal and screen-reader naming were not manually exercised in this pass.

## Findings

- P0: none.
- P1: none.
- P2: none after replacing the generic dead-end label with an explanatory, recoverable state.
- P3: the selected mock does not define a no-results state, so this state extends its information hierarchy using Yokko's existing visual tokens rather than inventing new artwork.

## Verification

- Isolated build completed with 0 warnings and 0 errors.
- Focused Song Select filter: 26 passed, 0 failed, 0 skipped across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList`.
- Focused coverage verifies the true empty-library copy, absence of an irrelevant reset, filtered-empty summary, combined reset behaviour, restored result count, existing Escape behaviour, and the previous browser/virtualisation regressions.
- Native Direct3D 11 captures at 1920 x 1080 verify populated and filtered-empty states. Both the source/final board and the identical-crop interaction board were opened and inspected.
- `git diff --check`: passed before appending this QA section.

final result: passed

# Song Select selected-song information hierarchy QA (2026-08-01)

## Evidence

- Selected visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1792 x 1000; selected-song summary leads with artwork/title, then chart facts and personal performance).
- Equal-state baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-filter-populated-state-v1.png` (1920 x 1080; playback rate and difficulty rating float in separate top-right pills while the lower grid stops early).
- Final long-title state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-left-card-v2.png` (1920 x 1080 native Direct3D 11 capture).
- Final short-title state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-left-card-short-title-v2.png` (1920 x 1080 native capture after selecting Neon Pulse Overdrive).
- Equal-state focused comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-left-card-before-after-v2.png` (identical 850 x 255 selected-song crop).
- Source/final full-view comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-left-card-reference-comparison.png` (source and implementation normalized to 960 px widths at device scale 1).

## Comparison history

1. Baseline P2: the difficulty rating and playback rate appeared as two detached cyan pills in the card's top-right corner. The title was constrained to 420 px while both lower information rows occupied only 360 of the available 575 px.
2. First implementation: the title gains the full 575 px content measure. Length, BPM, notes, and the live rating now form one chart-facts row; best score, best accuracy, and playback rate form one performance row.
3. First visual pass P2: a single-line title retained the two-line title's fixed byline positions, leaving an oversized blank gap between `Neon Pulse Overdrive` and its artist/mapper.
4. Final implementation: artist and mapper positions adapt to one- or two-line titles while the facts and performance baseline remains fixed, preserving row alignment between selections.
5. Post-fix evidence: long-title, short-title, equal-state before/after, and source/final comparisons were opened together. No title collision, byline gap, metric clipping, detached rating, artwork deformation, or ranking overlap remains.

## Required fidelity review

- Fonts and typography: the existing Yokko display/body families remain unchanged. Two-line titles use 21 px display text; one-line titles retain 28 px and now pull artist/mapper upward. Labels use 7-8 px display text and values use 12-13 px, matching the selected mock's headline-to-metadata hierarchy.
- Spacing and layout rhythm: the 210 px square artwork and 850 x 255 paper remain unchanged. The content column expands from 420 to 575 px; four chart facts and three performance facts use consistent dividers across the full measure.
- Colours and visual tokens: navy text, cyan chart metadata, yellow score icon, cyan accuracy icon, and pink playback icon reuse existing semantic tokens. The removed cyan pills reduce surface clutter without introducing a new colour or card style.
- Image quality and asset fidelity: the same source artwork continues through `SongSelectArtworkCrop` at 210 x 210 with no stretch. No new resource, cutout, generated bitmap, screenshot slice, custom SVG, or approximate decorative element was added.
- Copy and content: all values remain live: title, artist, mapper, length, BPM, note count, selected rating unit/value, best score, best accuracy, and effective playback rate. No decorative stars or unsupported status text was invented.
- Icons: clock, waveform, music, signal, trophy, bullseye, and tachometer come from the existing FontAwesome family and align to one 11-13 px rhythm.
- States and interactions: changing selection still uses the existing contained detail transition; rating-mode and playback-rate changes rebuild the displayed facts from current values; selection and lightweight rate changes retain their previous preview/list semantics.
- Accessibility and viewport resilience: rating and playback states remain explicit text rather than colour alone, long titles wrap to at most two lines, and short titles no longer create misleading whitespace. The 1920 x 1080 stage retains all ranking and footer controls; keyboard and screen-reader behaviour were not expanded in this visual pass.

## Findings

- P0: none.
- P1: none.
- P2: none after consolidating the status pills into the information grid and adapting byline spacing to title length.
- P3: the selected mock places the rating beside a short title. Yokko intentionally keeps the rating in the chart-facts row so long titles retain the full content width and the rating does not return to a detached badge.

## Verification

- Isolated build completed with 0 warnings and 0 errors.
- Focused Song Select filter: 26 passed, 0 failed, 0 skipped across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList`.
- Focused coverage verifies the expanded 575 px chart/performance rows, live playback-rate/BPM/difficulty updates, selection transitions without list reconstruction, rating-mode switching, and all previous browser/empty-state regressions.
- Native Direct3D 11 captures at 1920 x 1080 verify both long- and short-title states. The equal-state crop and normalized source/final board were opened and inspected.
- `git diff --check`: passed before appending this QA section.

final result: passed

# Song Select top navigation and browse-control QA (2026-08-01)

## Evidence

- Selected visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1672 x 941, device scale 1; osu!lazer-like navigation and browser-control hierarchy in the selected Yokko direction).
- Rendered implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-header-controls-v1.png` (1920 x 1080, device scale 1, native Direct3D 11 capture).
- Focused comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-header-controls-comparison-v1.png` (source top 192 px normalized to 1920 x 220 above the implementation's native 1920 x 220 crop).
- Viewport/state: both captures are 16:9 desktop song-select states with the music destination active, selected-song information visible, browser controls at rest, and no modal open.

## Comparison history

1. Baseline P2: the active music destination used the same naked white-icon silhouette as every other destination, relying on a yellow icon colour and a thin cyan underline. At full-screen scale the current page was much less explicit than the selected mock's yellow circular badge.
2. Implemented: the active destination now uses a 42 px yellow circle, a 20 px navy FontAwesome music icon, and a short white baseline. Inactive destinations retain the existing white icon treatment.
3. Post-fix evidence: the normalized header comparison was opened and inspected. The active page now has the same immediate visual anchor as the source while the search, key-mode, difficulty, sort/group/library/converts controls retain their existing aligned three-row structure and working states.

## Required fidelity review

- Fonts and typography: the existing Yokko display/body fonts and all search/filter text sizes remain unchanged. The implementation intentionally keeps smaller utility copy than the generated mock because its browser adds 4K/7K mode controls in the same 850 px column; there is no clipping or accidental wrap.
- Spacing and layout rhythm: the 64 px top navigation, 48 px search row, 32 px difficulty row, and 34 px browse toolbar remain on the established 1920 x 1080 grid. The 42 px active badge is optically centred inside its existing 48 x 64 destination slot and does not shift neighbouring icons.
- Colours and visual tokens: the active badge reuses Yokko yellow and navy; the baseline uses white. Search and filter surfaces retain ivory, cyan, pink, and navy semantic states. No glow, dark-tech panel, gradient, or sci-fi styling was introduced.
- Image quality and asset fidelity: the logo, avatar, wallpaper, artwork, tape, and paper resources are unchanged. No new resource, generated image, cutout, screenshot slice, custom SVG, code-drawn illustration, or approximate decorative asset was added.
- Copy and content: search, key-mode, difficulty, sorting, grouping, library, and converted-chart labels remain real current state. The source's clock/debug readout is intentionally not copied because it is not part of Yokko's navigation model.
- Icons: the active and inactive navigation destinations continue to use one FontAwesome family. The yellow circle is a regular interactive surface, not an asset substitute; icon weights and alignment remain consistent.
- States and interactions: only the already-selected music destination receives the badge. Search focus/Escape, key-mode selection, difficulty dragging, sort/group toggles, and converted-chart visibility are unchanged and remain covered by the Song Select focused suite.
- Accessibility and viewport resilience: selection is communicated by shape, foreground/background reversal, and baseline in addition to colour. The active target remains 48 x 64. The full 1920 x 1080 capture retains all persistent controls; keyboard focus and screen-reader naming were not manually expanded in this visual-only pass.

## Findings

- P0: none.
- P1: none.
- P2: none after strengthening the active navigation destination.
- P3: the generated source includes settings/home/debug destinations and one longer search field, while Yokko preserves its actual navigation destinations and dedicated 4K/7K filters. This is an intentional product-content difference, not a remaining hierarchy defect.

## Verification

- Isolated build completed with 0 warnings and 0 errors.
- Focused Song Select filter: 26 passed, 0 failed, 0 skipped across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList`.
- Native Direct3D 11 capture at 1920 x 1080 completed successfully. The full implementation capture and normalized source/implementation header comparison were both opened and inspected.

final result: passed

# Song Select ranking-row hierarchy QA (2026-08-01)

## Evidence

- Selected visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1672 x 941, device scale 1; ranking uses plain grade letters, a crown for first place, and a bordered current-player row).
- Equal-state baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-header-controls-v1.png` (1920 x 1080; every grade has a detached rounded surface and the current player is a saturated yellow strip).
- Rendered implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-hierarchy-v1.png` (1920 x 1080, device scale 1, native Direct3D 11 capture).
- Equal-state before/final comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-before-after-v1.png` (identical 850 x 470 ranking crop at native density).
- Source/final focused comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-reference-final-v1.png` (source normalized to 1920 x 1080 above the final native 850 x 470 crop).
- Viewport/state: 16:9 desktop Song Select, global ranking visible, seven scores, MOCHI as the current player at rank 6, no transition or modal active.

## Comparison history

1. Baseline P2: seven S/A grade letters each sat inside an independent rounded rectangle, making the score column read like a stack of unrelated buttons. The current player used an opaque yellow fill that outweighed the selected song and the global-rank hierarchy.
2. Implemented: grade surfaces were removed and the grade letters increased from 15 px to 17 px. Score, accuracy, combo, and grade columns retain their original right edges.
3. Implemented: the current player now uses a 92% ivory paper fill, 1.5 px pink outline, 4 px pink rail, small play icon, `#rank` label, and a 46 px pink-bordered avatar. First place gains the existing FontAwesome crown while ranks 2-3 retain their cyan/pink numerals.
4. Post-fix evidence: the full capture, equal-state before/final crop, and normalized source/final crop were opened together. No grade-card residue, metric collision, avatar clipping, row-height change, paper overflow, or ranking/footer overlap remains.

## Required fidelity review

- Fonts and typography: player names, mods, score, accuracy, combo, and tab typography remain on the existing Yokko display/body families. Grade letters now match the source's plain trailing readout and remain legible without a backing chip; all values stay on one line.
- Spacing and layout rhythm: rows remain 818 x 52 with zero added vertical spacing, the flow still begins at x=16/y=18, and all four metric columns are unchanged. The current avatar grows from 42 to 46 px but stays inside the 52 px row with 3 px vertical insets.
- Colours and visual tokens: first place reuses yellow, ranks 2-3 reuse cyan/pink, grades retain their semantic cyan/green/yellow/pink colours, and current-player emphasis uses ivory/pink instead of a saturated yellow block. No new palette or sci-fi treatment was introduced.
- Image quality and asset fidelity: all existing real avatars and the Yokko current-player crop are unchanged and remain circular with clean masks. No flag, cutout, generated bitmap, screenshot slice, custom SVG, or new resource was added; the target's flags were not fabricated because `SongSelectScore` carries no country metadata.
- Copy and content: ranks, player names, mods, scores, accuracy, combos, grades, and play count remain driven by real score state. The current label now renders as `#6` in this fixture, matching the selected mock's current-player convention without changing ordering.
- Icons: crown and play indicators use the same existing FontAwesome family as Song Select. They are limited to first place and the current player respectively; no glyph or emoji substitute is used.
- States and interactions: global/personal ranking switching, empty personal history, score ordering, content transitions, selected mods, and parent click behaviour remain on their existing paths. Only presentation inside populated rows changed.
- Accessibility and viewport resilience: grades remain explicit text rather than colour alone, current-player state is expressed through icon, `#rank`, border, rail, avatar scale, and name colour, and the 1920 x 1080 capture retains every persistent control. Keyboard and screen-reader behaviour were not expanded in this visual pass.

## Findings

- P0: none.
- P1: none.
- P2: none after removing detached grade surfaces and reducing the current-player fill weight.
- P3: the source shows country flags and a larger mascot overlap on the current row. Yokko intentionally omits fabricated flags because the score model has no country field, and keeps the existing current-player avatar rather than introducing a new or poorly extracted decoration.

## Verification

- Isolated build completed with 0 warnings and 0 errors.
- Focused Song Select filter: 26 passed, 0 failed, 0 skipped across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList`.
- Focused coverage now asserts that all seven grade labels contain no detached `Box` surfaces, while retaining grade variety, one highlighted current player, metric-column alignment, ranking transitions, and previous browser/preview regressions.
- Native Direct3D 11 capture at 1920 x 1080 completed successfully. The full implementation, before/final crop, and source/final focused comparison were opened and inspected.
- `git diff --check`: passed before appending this QA section.

final result: passed

# Song Select full-screen rhythm completion QA (2026-08-01)

## Evidence

- Selected visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1672 x 941, device scale 1; osu!lazer-like compact browser rhythm and a continuous footer action band).
- Equal-state baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-ranking-hierarchy-v1.png` (1920 x 1080; package children are 52 px high and the 410 px tool dock leaves a large inactive gap before PLAY).
- Rendered implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-fullscreen-audit-v1.png` (1920 x 1080, device scale 1, native Direct3D 11 capture).
- Source/final full-screen comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-reference-final-fullscreen-v1.png` (selected source normalized to 1920 x 1080 above the native implementation).
- Equal-state before/final comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-fullscreen-before-after-v1.png` (native 1920 x 1080 captures stacked without resampling).
- Viewport/state: 16:9 desktop Song Select, selected expanded package, global ranking visible, no transition or modal active.

## Comparison history

1. Baseline P2: 52 px child rows plus 5 px inter-item spacing made two short packages occupy most of the visible browser and weakened the compact osu!lazer-like scan rhythm.
2. Implemented: package child rows are now 44 px. The title, mapper, trailing difficulty readout, and progressive 4K/7K chip were vertically recentered without changing their horizontal columns, semantic states, or selection curve.
3. Baseline P2: the account card ended at x=860, the footer tool dock ended at x=1280, and PLAY began near x=1496, leaving an approximately 216 px inactive break in the primary action band.
4. Implemented: the dock now spans 560 px from x=870 to x=1430, with three equal 176 x 82 controls. The remaining approximately 66 px breathing space before PLAY reads as group separation rather than an abandoned slot.
5. Post-fix evidence: the source/final and equal-state before/final full-screen boards were opened together. The browser gains one compact scan rhythm, footer controls form one continuous centre group, and no label clipping, mode-chip collision, cover distortion, ranking overlap, or PLAY overlap remains.

## Required fidelity review

- Fonts and typography: existing Yokko display/body typography is unchanged. Child titles and mapper copy keep their established weights and one-line truncation; only y positions were tightened to fit the measured row height.
- Spacing and layout rhythm: 44 px child rows now sit closer to the selected source's approximately 42 px normalized rhythm. The 560 px footer dock fills the measurable dead zone while preserving 10 px internal gaps, account-card spacing, and clear separation from PLAY.
- Colours and visual tokens: no colour, gradient, glow, radius, shadow, or border token was added. The implementation continues to use ivory, navy, cyan, pink, yellow, and the existing green difficulty accent.
- Image quality and asset fidelity: cover artwork continues to use square masked cover crops without stretching. No resource-library image, generated asset, screenshot slice, custom SVG, new cutout, or code-drawn decorative illustration was introduced.
- Copy and content: difficulty names, mapper names, rating mode/value, key mode, selected state, account data, and footer labels remain driven by current product state. No placeholder or target-only metadata was fabricated.
- Icons: existing FontAwesome controls and current Yokko mascot/tape resources are unchanged; widening the tool controls does not enlarge or redraw their icons.
- States and interactions: compact-row selection, progressive selected chip, package expansion, search/filter/sort/group/converts controls, MODS, RANDOM, OPTIONS, and PLAY remain on their previous event paths. No list rebuild was added to selection or preview-rate changes.
- Accessibility and viewport resilience: selected rows retain shape, rail, fill, arrow, and explicit `SELECTED` text in addition to colour. All persistent controls fit the shared 1920 x 1080 baseline; narrower viewport behaviour and screen-reader naming were not expanded in this geometry-only pass.

## Findings

- P0: none.
- P1: none.
- P2: none after correcting browser density and footer continuity.
- P3: the selected source contains a more decorative overlapping mascot and slightly different package-header proportions. Yokko intentionally keeps its clean square covers and existing mascot resource rather than forcing uncertain cutouts or returning to flattened artwork.

## Verification

- Isolated build completed with 0 warnings and 0 errors.
- Focused Song Select filter: 26 passed, 0 failed, 0 skipped across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList`.
- Focused coverage asserts the 560 x 94 dock, equal 176 x 82 footer controls, the compact row height, transparent inline rating position, progressive key-mode chip, package selection curve, and previous browser/preview regressions.
- Native Direct3D 11 capture at 1920 x 1080 completed successfully. The final capture and both comparison boards were opened and inspected.
- `git diff --check`: passed before appending this QA section.

final result: passed

# Song Select responsive footer QA (2026-08-01)

## Evidence

- Selected visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1672 x 941, device scale 1; compact osu!lazer-like browser rhythm with a clearly separated primary PLAY action).
- Comfortable implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-responsive-footer-default-v1.png` (1920 x 1080, device scale 1, Comfortable UI scale at 100%).
- Large baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-large-scale-audit-v1.png` (1920 x 1080, device scale 1, Large UI scale at 110%; OPTIONS is partially hidden by PLAY).
- Large implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-responsive-footer-large-v1.png` (1920 x 1080, device scale 1, Large UI scale at 110%).
- Source/default comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-responsive-footer-reference-final-v1.png`.
- Large full-screen before/after: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-responsive-footer-large-before-after-v1.png`.
- Large footer focused before/after: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-responsive-footer-large-crop-v1.png`.
- Additional states inspected: `song-select-no-results-audit-v1.png` and `song-select-personal-empty-audit-v1.png`; neither exposed a P0, P1, or P2 layout defect.

## Comparison history

1. Baseline P2: Large mode reduces the effective logical width to approximately 1745 px while the footer retained its 560 px dock and three 176 px controls. The right-anchored 400 px PLAY surface consequently covered part of OPTIONS.
2. Implemented: footer geometry now responds to the current UI scale without rebuilding Song Select. Large uses a 410 x 94 dock, 126 px controls, and a 134 px horizontal step; Comfortable and Compact retain the selected 560 x 94 dock, 176 px controls, and 184 px step.
3. Post-fix evidence: the full-screen and focused before/after boards show MODS, RANDOM, and OPTIONS entirely visible in Large mode with a clean gap before PLAY. The source/default board confirms the fuller Comfortable geometry is unchanged.

## Required fidelity review

- Fonts and typography: labels, sizes, weights, and existing Yokko type families are unchanged. Compacting Large mode only changes control width and x position; no label wraps or clips.
- Spacing and layout rhythm: Comfortable retains the 560 px continuous centre group selected in the previous pass. Large deliberately returns to a 410 px group so all three controls fit its reduced logical canvas with consistent 8 px gaps.
- Colours and visual tokens: no colour, radius, shadow, border, glow, or material token changed. The ivory-paper Yokko language and yellow PLAY hierarchy remain intact.
- Image quality and asset fidelity: no resource-library image, generated asset, screenshot slice, cutout, custom SVG, or decorative bitmap was added. Existing covers and mascot resources remain untouched.
- Copy and content: MODS, RANDOM, OPTIONS, PLAY, account state, song data, ranking data, and empty-state copy remain driven by the existing product state.
- Icons: the existing FontAwesome icons retain their source, size, alignment, and interaction paths at both scale modes.
- States and interactions: the layout reacts in place to `UiScale.ValueChanged`; it does not rebuild the screen, reset selection, restart preview playback, or change button actions. Search no-results and personal-history empty states remain recoverable and visually stable.
- Accessibility and viewport resilience: every persistent footer action remains visible and separated at both tested scales. Controls retain text labels in addition to icons; keyboard and screen-reader behaviour were not expanded in this geometry-only pass.

## Findings

- P0: none.
- P1: none.
- P2: none after adding scale-specific footer geometry.
- P3: Large mode necessarily has narrower centre controls than Comfortable, but all labels remain readable and the reduced spacing is preferable to overlap or hiding an action.

## Verification

- Isolated build completed with 0 warnings and 0 errors.
- Focused Song Select filter: 26 passed, 0 failed, 0 skipped across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList`.
- Focused coverage asserts both the existing Comfortable geometry and the Large collision-safe helper values.
- Native Direct3D 11 captures at 1920 x 1080 completed successfully for Comfortable, Large, no-results, and personal-empty states. The selected source, final default capture, Large before/after capture, and focused footer crop were opened together and inspected.
- `git diff --check`: passed after appending this QA section.

final result: passed

# Song Select transient-screen return continuity QA (2026-08-01)

## Evidence

- Selected visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1672 x 941, device scale 1; osu!lazer-like Song Select with stable persistent browser and footer regions).
- Rendered Song Select implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-mod-return-final-v1.png` (1920 x 1080, device scale 1, Comfortable UI scale, active DT and HD state).
- Equal-density source/implementation board: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-mod-return-reference-final-v1.png` (source normalized to 1920 x 1080 above the native implementation).
- Rendered MODS destination: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-mods-entry-final-v1.png` (1920 x 1080, device scale 1, active DT and HD state).
- Interaction state: select a chart, open MODS from the persistent footer, leave the selection unchanged, return to Song Select, then open and return from OPTIONS.

## Comparison history

1. Baseline P2: suspending Song Select for MODS preserved preview audio, but returning unconditionally synchronized the unchanged library, rebuilt the song browser twice, recreated the selected-details layer, and reselected a replacement entry object. This made an unchanged back-navigation path vulnerable to visible list/detail flicker and scroll disturbance.
2. Implemented: Song Select records when its next screen is `GameplayModsScreen`. That return path now keeps the existing list generation, details layer, selected entry, filters, collapsed packages, and scroll state in place; it only closes the MODS affordance, restores parent opacity, and applies preview playback if the committed mods changed.
3. Implemented: non-MODS returns still refresh scores and imported data, but defer list materialisation until after that refresh, reducing the previous two browser rebuilds to one coherent rebuild.
4. Post-fix evidence: the combined source/final board retains the accepted square-cover, trailing-rating, paper-ranking, and continuous-footer composition. The MODS capture uses the same ivory, navy, cyan, pink, and yellow hierarchy. Focused visual crops were not needed because no visible geometry or asset changed in this lifecycle-only pass; the exact list/detail-generation invariants are covered by the interaction test.

## Required fidelity review

- Fonts and typography: no font family, weight, size, line height, wrapping, or truncation changed. Song Select and MODS remain on the existing Yokko display/body families.
- Spacing and layout rhythm: persistent Song Select geometry remains identical before and after MODS. No reconstructed list means scroll position and the established browser row rhythm remain stable on return.
- Colours and visual tokens: no palette, opacity, gradient, border, radius, shadow, or semantic state token changed. MODS visibly continues the same ivory-paper and navy/cyan/pink/yellow system.
- Image quality and asset fidelity: square cover crops, background artwork, logo, and mascot remain unchanged and sharp. No resource-library image, cutout, generated bitmap, screenshot slice, custom SVG, or decorative asset was added.
- Copy and content: song metadata, active-mod summary, filters, ranking values, and MODS labels remain driven by existing state. Returning from MODS does not regenerate or substitute content.
- Icons: existing FontAwesome icons remain unchanged across Song Select and MODS, including footer actions, category markers, reset, back, and done controls.
- States and interactions: MODS opens, retains preview audio, commits selection on exit, and returns to the same selected chart without rebuilding the browser. OPTIONS still refreshes potentially changed settings and score presentation, but performs one list rebuild instead of two.
- Accessibility and viewport resilience: all persistent controls remain visible at 1920 x 1080 and active states continue to use text plus shape/colour. Keyboard and screen-reader behaviour were not changed in this lifecycle pass.

## Findings

- P0: none.
- P1: none.
- P2: none after preserving the MODS return state and consolidating general return refreshes.
- P3: a dedicated motion capture could quantify the 180 ms parent fade, but current lifecycle counters and stable final frames cover the regression this pass changes.

## Verification

- Isolated build completed with 0 warnings and 0 errors.
- Focused return tests passed for unchanged MODS exit and OPTIONS exit. The MODS path preserves `SongListRebuildVersion`, `DetailsTransitionVersion`, and selected-entry identity; OPTIONS increments the song-list generation exactly once.
- Full focused Song Select filter: 26 passed, 0 failed, 0 skipped across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList`.
- Native Direct3D 11 captures at 1920 x 1080 completed successfully for Song Select and MODS. The source/final board and MODS destination were opened and inspected.
- `git diff --check`: passed after appending this QA section.

final result: passed

# Song Select interrupted selection transition QA (2026-08-01)

## Evidence

- Selected visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1672 x 941, device scale 1; one coherent selected-song card, one ranking paper, and continuously covered artwork).
- Baseline interrupted frame: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-rapid-jump-transition-v1.png` (1920 x 1080, captured 80 ms after seven consecutive selection changes).
- Revised interrupted frame: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-rapid-jump-transition-final-v1.png` (1920 x 1080, same state and timing).
- Revised settled frame: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-rapid-jump-settled-final-v1.png` (1920 x 1080, captured 800 ms after the same selection burst).
- Equal-state before/after board: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-rapid-jump-before-after-v1.png` (baseline above, revised interrupted frame below).
- Source/final full-screen board: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-rapid-jump-reference-final-v1.png` (source normalized to 1920 x 1080 above the revised settled frame).
- Viewport/state: 1920 x 1080, device scale 1, Comfortable UI scale, 7K filter, DT and HD active, seven synchronous selection changes beginning at 700 ms.

## Comparison history

1. Baseline P1: every selection retained its outgoing complete details container for 240 ms. Seven changes in one update therefore exposed duplicated difficulty pills, titles, ranking tabs, selected-mod summaries, rows, and paper silhouettes in the 80 ms frame.
2. Baseline P1: the two alternating background sprites inherited unfinished fades. Rapidly selecting through several charts left both near zero alpha and exposed the neutral grey stage instead of continuously displaying chart artwork.
3. Implemented: an interrupted details transition immediately retires every superseded paper and animates only the latest incoming layer. Normal single-step navigation retains the existing directional outgoing/incoming transition.
4. Implemented: each background blend clears stale transforms, keeps the outgoing artwork fully covering the stage, and then fades the latest incoming artwork over it. Repeated changes can restart the blend without producing an uncovered frame.
5. Post-fix evidence: the equal-state board shows one clean song card and ranking paper in the revised 80 ms frame, with no duplicated typography or outlines and no grey background exposure. The interrupted and settled revised captures remain visually coherent, and the source/final board retains the accepted overall composition.

## Required fidelity review

- Fonts and typography: the fix does not change font family, fallback, weight, size, line height, wrapping, or truncation. It prevents several complete typography layers from becoming simultaneously visible during key repeat or RANDOM-like jumps.
- Spacing and layout rhythm: selected-card, ranking, browser, and footer geometry are unchanged. Removing superseded papers preserves the intended single 850 px detail column rather than showing displaced copies at 8-10 px intervals.
- Colours and visual tokens: ivory, navy, cyan, pink, yellow, and semantic difficulty colours are unchanged. Continuous artwork coverage prevents the unintended neutral-grey flash from replacing the designed background palette.
- Image quality and asset fidelity: existing chart wallpapers remain sharp and keep their current fill crop. No resource-library image, cutout, generated bitmap, screenshot slice, custom SVG, or decorative asset was added.
- Copy and content: only the final selected chart's metadata, ranking values, and active-mod summary remain visible during an interrupted transition. No copy was changed or fabricated.
- Icons: navigation, ranking, footer, mode, and selected-state icons retain their existing FontAwesome sources and alignment; duplicate icon layers are removed with their superseded containers.
- States and interactions: adjacent navigation still uses the 210 ms directional movement. Rapid keyboard or RANDOM selection collapses obsolete work immediately, keeps the song-list generation stable, and leaves one details layer. The 10,000-row virtual-list path remains bounded.
- Accessibility and viewport resilience: the 1920 x 1080 transition frame retains every persistent control and readable label. Eliminating transient double text improves legibility; reduced-motion behaviour and screen-reader semantics were not expanded in this pass.

## Findings

- P0: none.
- P1: none after eliminating stacked details layers and uncovered background frames.
- P2: none in the revised interrupted or settled captures.
- P3: a frame-by-frame video capture could further quantify easing continuity, but the 80 ms interrupted frame targets the previously visible failure point directly.

## Verification

- Isolated build completed with 0 warnings and 0 errors.
- Focused regression now asserts that rapid selection immediately retains exactly one details paper, keeps at least one background sprite at full coverage, and does not rebuild the song list.
- Full focused Song Select filter: 26 passed, 0 failed, 0 skipped across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList`.
- Existing long-list coverage confirms 10,000 logical rows retain bounded materialised drawables and far jumps expose the correct scroll affordances.
- Native Direct3D 11 captures at 1920 x 1080 completed successfully for baseline interrupted, revised interrupted, and revised settled states. Both comparison boards were opened and inspected.
- `git diff --check`: passed after appending this QA section.

final result: passed

# Song Select package, filter, and scroll-state QA (2026-08-01)

## Evidence

- Selected visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1672 x 941, normalized to 1920 x 1080 at device scale 1; osu!lazer-like square package artwork, trailing difficulty metadata, persistent ranking paper, and footer dock).
- Expanded package implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-before-v1.png` (1920 x 1080, device scale 1, Comfortable UI scale, 7K filter, DT and HD active).
- Collapsed package implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-collapsed-v1.png` (1920 x 1080, same state after collapsing the Harmonic Bloom package).
- Selected-package collapse implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-selected-package-collapsed-v1.png` (1920 x 1080, selected chart retained while its package is collapsed).
- Search result implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-search-settled-v1.png` (1920 x 1080, query `petals`, one matching package and two charts).
- Equal-density source/implementation board: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-reference-final-v1.png` (source normalized to 1920 x 1080 above the native collapsed-package implementation).
- Viewport and density: source 1672 x 941, implementation 1920 x 1080, both 16:9; source was Lanczos-scaled to 1920 x 1080 before stacking, with no browser or device chrome.

## Comparison history

1. The source/final full-view board was opened as one comparison input. No actionable P0/P1/P2 mismatch was found in the requested cover and rating surfaces: package covers are square and aspect-fill without stretching, while chart difficulty is a transparent trailing readout instead of a detached leading badge.
2. Expanded, collapsed, selected-package-collapsed, and filtered captures were opened separately at the same native viewport. Package headers remain anchored, child rows move as one group, the selected details and ranking paper do not jump to another chart, and the footer remains continuously visible.
3. No visual fix was required in this pass. The existing 230 ms bounded layout transition, 80 ms package-head anchoring, virtualised range, and filtered selection update produced stable rendered states.

## Required fidelity review

- Fonts and typography: the right browser keeps one primary title/difficulty line and one quieter mapper line; difficulty values use the same compact optical weight as the target's trailing metadata. Long chart titles truncate or wrap within their own hierarchy rather than colliding with ratings or key-mode chips.
- Spacing and layout rhythm: square 84 px package covers establish a consistent left edge, compact child rows align beneath them, and collapsing a package removes its children without changing the browser controls or footer geometry. The left ranking remains a single paper surface with aligned numeric columns.
- Colours and visual tokens: ivory surfaces, navy text, cyan focus, pink selected state, yellow action treatment, and green difficulty accents remain consistent across expanded, collapsed, and filtered states. No sci-fi surface, neon frame, or dark glass treatment was introduced.
- Image quality and asset fidelity: package art is aspect-fill cropped into square masks and remains sharp in every captured state. No resource-library cutout, generated bitmap, screenshot slice, custom SVG, or new decorative asset was added.
- Copy and content: package counts, chart names, mapper lines, key mode, MSD labels, selected mods, and ranking values remain data-driven. Search narrows the browser to the matching package without fabricating placeholder content.
- Icons: package chevrons, star/favourite, selected marker, filter controls, footer actions, and ranking markers use the existing icon family and remain optically aligned after layout changes.
- States and interactions: package expand/collapse, selected-package collapse, search filtering, scroll clamping, continuation fades, indicator progress, and 10,000-row virtualisation are covered. Selection and details remain continuous without rebuilding on lightweight in-place updates.
- Accessibility and viewport resilience: persistent actions remain visible at 1920 x 1080; selected and collapsed states use icon direction, text, shape, and colour rather than colour alone. Reduced-motion and screen-reader semantics were not expanded in this visual pass.

## Findings

- P0: none.
- P1: none.
- P2: none in the requested square-cover, trailing-rating, expand/collapse, search, or scroll states.
- P3: the target uses shorter example titles, so its browser appears slightly calmer; Yokko's long stress-test titles intentionally remain to verify truncation and collision safety.

## Verification

- Native Direct3D 11 captures completed successfully at 1920 x 1080 for expanded, collapsing, collapsed, selected-package-collapsed, and filtered states.
- Full-view comparison used the equal-density source/final board; focused evidence used the native expanded/collapsed and filtered captures because the row typography and 84 px artwork are more legible there.
- Focused Song Select suite passed: 26 passed, 0 failed, 0 skipped across `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList` using an isolated artifacts directory so the currently running Yokko process was not disturbed.
- Regression coverage includes square aspect-fill artwork, trailing rating disclosure, package guide reuse, collapse scroll clamping, continuation hints, scroll-indicator progress, filtered empty/results states, selection continuity, and bounded 10,000-row materialisation.
- `git diff --check`: passed after appending this QA section.

final result: passed

# Song Select balanced selected-title layout QA (2026-08-01)

## Evidence

- Selected visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` (1672 x 941, normalized to 1920 x 1080 at device scale 1; selected title remains a complete, deliberate typographic block rather than an arbitrary clipped line).
- Baseline implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-search-settled-v1.png` (1920 x 1080, device scale 1, Comfortable UI scale, query `petals`; selected title truncated after `Dreamin...` while the second title line was unused).
- First two-line pass: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-title-wrap-final-v1.png` (1920 x 1080; complete copy, but greedy wrapping left `Petals` alone on the second line).
- Final implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-title-wrap-balanced-final-v1.png` (1920 x 1080; balanced `Harmonic Bloom: Symphony` / `of the Dreaming Petals` title block).
- Equal-state before/final board: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-title-wrap-before-after-v1.png` (native baseline above, native final below, no resampling).
- Source/final board: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-title-wrap-reference-final-v1.png` (source normalized to 1920 x 1080 above the native final implementation).
- Viewport and density: source 1672 x 941 and implementation 1920 x 1080, both 16:9; source was Lanczos-scaled to the native 1920 x 1080 implementation before comparison.

## Comparison history

1. Baseline P2: a medium-long selected title was treated as a single 28 px line and visibly truncated even though the card reserves enough vertical space for a second line. This made the title block look like an unfinished generic label rather than the primary song identity.
2. First-pass P2: lowering the wrap threshold preserved the complete title, but the existing greedy line breaker filled the first line to its maximum and orphaned one word on the second line.
3. Implemented: selected details now use a balanced two-line layout. When the complete title fits within two measured lines, the break point minimises the difference between line widths; titles that exceed two lines retain the existing bounded ellipsis fallback.
4. Post-fix evidence: the equal-state board shows complete copy, two optically balanced lines, and unchanged artwork/stat geometry. The source/final board preserves the selected mock's title-first hierarchy without adding target-only decoration.

## Required fidelity review

- Fonts and typography: Yokko's existing display family is retained. Short titles remain one 28 px line; wrapped titles use the established 21 px two-line treatment with -2 px line spacing. The final break avoids both truncation and a one-word orphan.
- Spacing and layout rhythm: artist and mapper labels use the existing two-line y positions, ending above the chart-facts row at y=164. The 210 px artwork, difficulty pill, dividers, ranking paper, browser, and footer do not move or collide.
- Colours and visual tokens: no colour, opacity, border, shadow, gradient, or semantic-state token changed. The navy title, cyan mapper, ivory paper, and current pink/yellow/cyan accents remain unchanged.
- Image quality and asset fidelity: cover crop, selected background, logo, paper, tape, avatars, and mascot are unchanged and remain sharp. No generated image, resource-library cutout, screenshot slice, custom SVG, or decorative asset was added.
- Copy and content: the complete data-driven song title is now readable. Artist, mapper, chart facts, playback rate, ranking values, filters, and selected mods remain unchanged.
- Icons: selected-card, filter, ranking, navigation, footer, and play icons are unchanged and retain the existing FontAwesome/Yokko sources.
- States and interactions: the balanced title is rebuilt through the existing selection and filter paths. Search, package selection, preview audio, list identity, and rapid-selection transition behaviour are unchanged.
- Accessibility and viewport resilience: the improvement handles longer real-world titles without clipping or overlap at the 1920 x 1080 baseline. Existing tests retain a bounded two-line fallback for exceptionally long or no-space text; screen-reader semantics were not changed.

## Findings

- P0: none.
- P1: none.
- P2: none after replacing clipped and orphaned title states with balanced wrapping.
- P3: line balance uses the existing lightweight character-width heuristic rather than runtime glyph measurement; the rendered Latin stress case is verified, while additional locale-specific visual corpora can be added if a real title exposes a poor break.

## Verification

- Focused text-layout tests: 4 passed, 0 failed, including the exact moderate-title balance and the existing one/two-line bounds.
- Full focused Song Select suite: 30 passed, 0 failed, 0 skipped across `SongSelectTextLayoutTest`, `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList` in an isolated artifacts directory.
- Native Direct3D 11 captures completed successfully at 1920 x 1080 for the baseline, first wrap, and balanced final states. Both comparison boards were opened and inspected as combined inputs.
- `git diff --check`: passed after appending this QA section.

final result: passed

# Song Select standalone square-cover completion QA (2026-08-01)

## Evidence

- Selected design target: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` at 1672 x 941, compared against Yokko's 1920 x 1080 shared baseline.
- Equal-state baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-standalone-cover-baseline-v1.png`.
- Equal-state final: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-standalone-cover-final-v1.png`.
- Before/after board: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-standalone-cover-before-after-v1.png`.
- Source/final board: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-standalone-cover-reference-final-v1.png`.
- Final state: 1920 x 1080, Comfortable density, `solo` search, DT + HD active, and the optional standalone-song preview fixture enabled.

## Comparison history

1. Baseline P2: package headers already used square artwork, but standalone song rows still used a 76 x 68 crop. This left one visible class of horizontally flattened covers and contradicted the user's request that song covers as a system should stop looking flat.
2. Implemented: the standalone artwork frame becomes 76 x 76 and sits at y=4 in the 84 px row. Existing aspect-fill crop behaviour is preserved, and the content column remains at x=100.
3. Post-fix evidence: the equal-state board shows a square cover with no distortion, clipping, or text collision. Its silhouette now agrees with the target's selected square-cover motif and Yokko's package-cover system.

## Required fidelity review

- Fonts and typography: unchanged; title, subtitle, mode, mapper, and rating typography preserve the current Yokko hierarchy.
- Spacing and layout rhythm: the cover has 4 px top and bottom breathing room, with a 16 px gap before the title column. Row height and neighbouring browser geometry do not change.
- Colours and visual tokens: unchanged; the current ivory paper, navy copy, cyan/pink/yellow accents, selection treatment, and difficulty semantics remain intact.
- Image quality and asset fidelity: artwork continues to use aspect-fill cropping into a square frame. No new resource, generated art, questionable cutout, screenshot slice, or decorative bitmap was introduced.
- Copy and content: unchanged; all song metadata remains data-driven and readable.
- Icons: unchanged; no icon substitution or approximation was introduced.
- States and interactions: selection, hover, filtering, virtualisation, preview continuity, and package behaviour are unaffected. The new frame is reset correctly when pooled rows are rebound or freed.
- Accessibility and viewport resilience: the square artwork remains contained inside the existing 84 px row at the 1920 x 1080 baseline and does not reduce the text hit area.

## Findings

- P0: none.
- P1: none.
- P2: none after completing the square-cover rule for standalone songs as well as package headers.
- P3: the 76 px cover is intentionally larger than compact package-child difficulty rows; this distinguishes a standalone song without returning to banner proportions.

## Verification

- Virtualised-list visual suite: 13 passed, 0 failed, including the explicit 76 x 76 standalone artwork-frame regression.
- Full focused Song Select suite: 31 passed, 0 failed, 0 skipped across `SongSelectTextLayoutTest`, `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList` in an isolated artifacts directory.
- Native Direct3D 11 baseline and final captures completed at 1920 x 1080. Both comparison boards were opened and visually inspected.
- The longest selected package-header state was also captured separately; its title, byline, difficulty, and rating already fit, so no unnecessary density change was made there.
- `git diff --check`: passed after appending this QA section.

final result: passed

# Song Select package-section rhythm QA (2026-08-01)

## Evidence

- Source visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` at 1672 x 941, normalized to the shared 1920 x 1080 reference without cropping.
- Equal-state implementation baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-before-v1.png` at 1920 x 1080.
- Revised implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-section-rhythm-final-v1.png` at 1920 x 1080.
- Focused browser comparison, baseline left and revised implementation right: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-section-rhythm-before-after-v1.png`.
- Full-view comparison, normalized source left and revised implementation right: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-section-rhythm-reference-final-v1.png`.
- Viewport/state: 1920 x 1080, Comfortable density, 7K filter, DT + HD, three expanded packages, and `Marathon x1.3` selected.

## Comparison history

1. Baseline P2: the same 5 px gap separated child difficulties, a package boundary, and the next package header. Covers and metadata were correct, but the uniform rhythm visually merged multiple packages into one undifferentiated stack.
2. Implemented: every visible section after the first receives 8 px of additional leading space. Existing 5 px row spacing remains inside each package, producing a measured 13 px inter-package gap without changing row heights or the bounded selection curve.
3. Post-fix evidence: the focused board shows clear package islands while all three packages and their visible difficulties still fit above the footer. The source/final board confirms the browser now follows the selected reference's grouped song-set rhythm rather than generic card spacing.

## Required fidelity review

- Fonts and typography: unchanged; package titles, child difficulty names, mapper copy, MSD readouts, and key-mode pills keep their existing family, weights, sizes, and wrapping behaviour.
- Spacing and layout rhythm: intra-package gaps remain 5 px; package-to-package gaps become 13 px. Header and child heights, selected-row proximity curve, search/filter stack, and footer geometry remain unchanged.
- Colours and visual tokens: unchanged; no fill, border, rail, selection, or difficulty colour was altered.
- Image quality and asset fidelity: square package covers and aspect-fill crop behaviour are unchanged. No new asset, generated decoration, resource-library cutout, screenshot slice, or approximate code art was introduced.
- Copy and content: unchanged; the same three package groups, chart metadata, selected difficulty, filters, and mods are visible in the comparison.
- Icons: unchanged; star, chevron, selected pointer, toolbar, navigation, and footer icons retain their existing sources.
- States and interactions: expansion/collapse layout animation, virtualisation, scrolling, selection transfer, filtering, and preview continuity are preserved. Section spacing is part of the virtual item geometry, so scroll bounds and transitions share one source of truth.
- Accessibility and viewport resilience: the added rhythm does not hide the footer or clip a package header at 1920 x 1080. The list still exposes scroll continuation when content exceeds the viewport.

## Findings

- P0: none.
- P1: none.
- P2: none after differentiating package boundaries from child-row spacing.
- P3: the bottom of the stress fixture ends close to the final child row because all three packages are intentionally expanded; the scroll affordance covers longer real libraries and no persistent control is obscured.

## Verification

- New geometry regression: 1 passed, confirming a 5 px intra-package gap and 13 px inter-package gap.
- Virtualised-list visual suite: 14 passed, 0 failed, including bounded 10,000-row materialisation, scroll affordances, selection curve, package guide, cover crop, and pooled-row reuse.
- Full focused Song Select suite: 32 passed, 0 failed, 0 skipped across `SongSelectTextLayoutTest`, `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList` from the isolated `artifacts/song-select-section-spacing` build.
- Native Direct3D 11 implementation capture completed at 1920 x 1080. Both combined comparison boards were opened and inspected at original detail.

final result: passed

# Song Select focused package expansion QA (2026-08-01)

## Evidence

- Source visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` at 1672 x 941, normalized to 1920 x 1080 without cropping.
- Equal-state implementation baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-section-rhythm-final-v1.png` at 1920 x 1080, with every package expanded.
- Focused-package implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-focused-package-final-v1.png` at 1920 x 1080.
- Keyboard focus-transfer state: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-focused-package-switch-v1.png` at 1920 x 1080.
- Focused browser comparison, baseline left and revised implementation right: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-focused-package-before-after-v1.png`.
- Full-view comparison, normalized source left and revised implementation right: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-focused-package-reference-final-v1.png`.
- Viewport/state: 1920 x 1080, Comfortable density, 7K filter, DT + HD, and `Marathon x1.3` selected for the default-state comparison.

## Comparison history

1. Baseline P2: all three package groups expanded simultaneously. The selected design keeps only the active package open, so the baseline flattened the package hierarchy and made the browser read like a dense data table.
2. Implemented: focused expansion now defaults to the selected package. Other package headers remain visible and clickable, all filtered difficulties remain in keyboard navigation, and crossing a package boundary atomically collapses the old package and expands the new one through the existing 230 ms layout transition.
3. Post-fix evidence: the default capture shows one expanded package and two collapsed neighbours. The focus-transfer capture shows keyboard selection moving into the next package with its cover, background, details, and selected row updating together. No chart content is removed from navigation.

## Required fidelity review

- Fonts and typography: unchanged; package titles, difficulty names, mapper copy, MSD readouts, and key-mode pills keep their existing family, weights, sizes, and wrapping behaviour.
- Spacing and layout rhythm: the established 5 px intra-package and 13 px inter-package rhythm remains intact. Collapsed headers form clearly separated package islands without changing toolbar or footer geometry.
- Colours and visual tokens: unchanged; no new fill, border, rail, selection, or difficulty colour was introduced.
- Image quality and asset fidelity: square package covers and aspect-fill crop behaviour remain unchanged. No new asset, generated decoration, resource-library cutout, screenshot slice, or approximate code art was introduced.
- Copy and content: unchanged; package titles, chart metadata, filters, mods, and selection data continue to come from the existing fixture and UI state.
- Icons: unchanged; star, chevron, selected pointer, toolbar, navigation, and footer icons keep their existing sources.
- States and interactions: pointer toggles, keyboard previous/next, global group expand/collapse, filtering, virtualisation, layout animation, and preview continuity remain available. Explicit global expand/collapse temporarily overrides focused mode; navigating into a hidden package restores focused expansion.
- Accessibility and viewport resilience: collapsed package headers stay visible and clickable, while all filtered entries remain keyboard navigable even when their rows are not currently materialised.

## Findings

- P0: none.
- P1: none.
- P2: none after introducing selected-package focus and package-boundary transfer.
- P3: the global group control intentionally overrides focus mode when the user explicitly requests all-collapsed or all-expanded state. Subsequent navigation into a hidden package restores focused mode for usability.

## Verification

- New focused-expansion regression: 1 passed, confirming a single expanded package, retained navigation across four hidden/visible charts, and atomic focus transfer at the package boundary.
- Full focused Song Select suite: 33 passed, 0 failed, 0 skipped across `SongSelectTextLayoutTest`, `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList` from the isolated `artifacts/song-select-focused-package` build.
- Isolated build: 0 warnings and 0 errors.
- Both native Direct3D 11 captures were opened and inspected at original detail.
- Both combined comparison boards were opened and inspected at original detail.

final result: passed

# Song Select compact collapsed package header QA (2026-08-01)

## Evidence

- Source visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` at 1672 x 941, normalized to 1920 x 1080 without cropping.
- Equal-state implementation baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-focused-package-final-v1.png` at 1920 x 1080, with 84 px expanded and collapsed package headers.
- Revised implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-collapsed-header-final-v1.png` at 1920 x 1080.
- Long-title stress implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-collapsed-header-long-title-v1.png` at 1920 x 1080.
- Focused browser comparison, baseline left and revised implementation right: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-collapsed-header-before-after-v1.png`.
- Full-view comparison, normalized source left and revised implementation right: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-collapsed-header-reference-final-v1.png`.
- Focused long-title comparison, ordinary package names left and stress state right: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-collapsed-header-long-title-proof-v1.png`.
- Viewport/state: 1920 x 1080, Comfortable density, 7K filter, DT + HD, `Marathon x1.3` selected, one expanded package, and two collapsed neighbours.

## Comparison history

1. Baseline P2: collapsed package headers retained the same 84 px height as the active expanded header. Although their square covers were correct, the equal height made inactive packages too visually heavy and reduced the focused browser's scan rhythm compared with the selected design.
2. Implemented: expanded headers remain 84 px, while collapsed headers use a dedicated 72 px layout with a matching 72 x 72 square cover. Collapsed titles use one 16 px truncating line and an 8 px summary line; selected difficulty context is reserved for the expanded state.
3. Post-fix evidence: the before/after board shows a visibly lighter inactive hierarchy without flattening the artwork. The stress board confirms an oversized real-world package name ends in an ellipsis before the chevron, keeps the summary line clear, and does not alter the expanded package above it.

## Required fidelity review

- Fonts and typography: expanded typography is unchanged. Collapsed package titles now use one 16 px display line with framework truncation; the count summary is reduced to 8 px and remains optically subordinate. The long-title stress state has no wrapping, collision, or clipped glyphs.
- Spacing and layout rhythm: collapsed height changes from 84 px to 72 px while the established 13 px inter-package separation remains. The browser gains density only in inactive package headers; the 44 px difficulty rows and 84 px expanded header are unchanged.
- Colours and visual tokens: unchanged; ivory surfaces, navy text, cyan focus borders, yellow stars, pink selection, and pale-cyan chevrons continue to use existing Yokko tokens.
- Image quality and asset fidelity: collapsed covers remain square and use the existing aspect-fill crop path at their native layout size. The selected reference uses flatter banners, but retaining square covers is an intentional user-directed deviation after the user explicitly rejected flattened artwork. No new asset, generated decoration, resource-library cutout, screenshot slice, or approximate code art was introduced.
- Copy and content: all package names and count summaries remain dynamic. The long-title fixture changes only preview data and proves production truncation behaviour.
- Icons: star, play indicator, and chevron retain their existing icon sources and remain vertically centred in both header heights.
- States and interactions: expand/collapse, focused-package transfer, hover, keyboard navigation, virtualisation, filtering, and preview continuity are unchanged. A collapsed selected package shows package identity rather than clipping the expanded difficulty-context row into 72 px.
- Accessibility and viewport resilience: the 72 px collapsed header remains above a 44 px pointer target. Long text truncates before the chevron, and all hidden difficulties remain keyboard navigable through the focused-package mechanism.

## Findings

- P0: none.
- P1: none.
- P2: none after separating expanded and collapsed header density.
- P3: the 72 px square-cover compromise is intentionally taller than the selected reference's banner rows. Reducing it further would weaken the square artwork identity the user requested, so this is accepted rather than actionable drift.

## Verification

- New compact-header regression: 1 passed, confirming 72 x 72 collapsed chrome, a single truncating title line, visible package summary, and suppression of expanded selected context in collapsed state.
- Focus-transfer regression now also verifies one 84 px expanded header and one 72 px collapsed header before and after keyboard package transfer.
- Full focused Song Select suite: 34 passed, 0 failed, 0 skipped across `SongSelectTextLayoutTest`, `TestSceneSongSelect`, `TestSceneSongSelectScreen`, and `TestSceneSongSelectVirtualisedList` from the isolated `artifacts/song-select-collapsed-header` build.
- Isolated build: 0 warnings and 0 errors.
- Both native Direct3D 11 implementation captures were opened and inspected at original detail.
- All three combined comparison boards were opened and inspected at original detail.

final result: passed

# Song Select compact chart-row hierarchy QA (2026-08-01)

## Evidence

- Source visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` at 1672 x 941.
- Same-state baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-left-rounded-v2.png` at 1920 x 1080.
- Revised native implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-row-hierarchy-v2.png` at 1920 x 1080.
- Full source/implementation comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-row-hierarchy-reference-v2.png`.
- Full same-state before/after comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-row-hierarchy-before-after-v2.png`.
- Focused source/implementation comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-row-hierarchy-reference-focused-v2.png`.
- Focused same-state before/after comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-row-hierarchy-focused-before-after-v2.png`.
- Viewport/state: 1920 x 1080, Comfortable density, 7K filter, Harmonic Bloom selected, one expanded package with Normal selected and Hard resting.

## Comparison history

1. Baseline P2: compact child rows used a fully opaque 7 px difficulty stripe, an additional bright package-guide stem, a strong coloured border, a 40% yellow selected wash, and neon green MSD labels. The combined treatment over-signalled difficulty and made the rows read as game HUD blocks rather than the reference's calm subordinate list.
2. Implemented: the semantic marker is reduced to 3 px at 48% opacity; the package guide uses a one-pixel stem; compact borders use neutral Yokko navy at 12%; the selected wash drops to 18%; the selected outline drops to 1.25 px; and the former green difficulty band maps into Yokko cyan.
3. Post-fix evidence: focused before/after shows the selected and resting rows retaining hierarchy without a fluorescent edge. The source comparison confirms that both now use a quiet ivory row, small coloured marker, thin rounded selection treatment, trailing rating, and a compact mode action.

## Required fidelity review

- Fonts and typography: difficulty name, mapper, rating unit/value, and key-mode text keep their existing type family, weight, line count, and truncation. No text reflow or clipping is visible.
- Spacing and layout rhythm: the established 44 px row height, 5 px in-package spacing, 14 px inset, progressive mode-pill width, and package-header alignment remain unchanged.
- Colours and visual tokens: custom fluorescent green was removed from the visible 5-10 MSD range. Compact rows now use ivory, navy, cyan, yellow, and pink from Yokko's shared palette; selected warmth is intentionally subordinate to the pink action chip.
- Image quality and asset fidelity: no bitmap, generated image, resource-library cutout, screenshot slice, or new asset was introduced. Package artwork and square cover crop are unchanged.
- Copy and content: dynamic difficulty name, mapper, rating, mode, and selected label remain unchanged.
- Icons and affordances: the existing pink play pointer remains the selection anchor. The row stays clickable and double-clickable, while the mode chip still expands only for the active chart.
- States and interactions: resting, selected, hover, pooled reuse, keyboard focus transfer, package expansion, rating-mode updates, and virtualisation remain implemented.
- Accessibility and viewport resilience: the 44 px pointer target remains intact; reduced chroma lowers visual noise without reducing text contrast or removing selection redundancies.

## Findings

- P0: none.
- P1: none.
- P2: none after quieting the difficulty rail, selected wash, outline, shadow, hover fill, and rating colour.
- P3: the source puts numeric difficulty in a leading pill, while Yokko intentionally retains the trailing transparent readout because the user explicitly found the earlier left-side score block abrupt.

## Verification

- Isolated `Yokko.Game.Tests` build in `artifacts/song-select-row-hierarchy`: 0 warnings and 0 errors.
- Focused Song Select screen/list run: 31 passed, 0 failed, 0 skipped.
- Post-colour virtualised-list run: 16 passed, 0 failed, 0 skipped, including the new compact hierarchy regression.
- Native Direct3D 11 implementation capture completed at 1920 x 1080. Full and focused source comparisons plus same-state before/after boards were opened and inspected at original detail.
- `git diff --check` passed.

final result: passed

# Song Select package artwork framing QA (2026-08-01)

## Evidence

- Source visual truth: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` at 1672 x 941.
- Same-state baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-top-nav-v1.png` at 1920 x 1080.
- Revised native implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-artwork-v1.png` at 1920 x 1080.
- Full source/implementation comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-artwork-reference-v1.png`.
- Full same-state before/after comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-artwork-before-after-v1.png`.
- Focused source/implementation comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-artwork-reference-focused-v1.png`.
- Focused same-state before/after comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-artwork-focused-before-after-v1.png`.
- Viewport/state: 1920 x 1080, Comfortable density, 7K filter, two collapsed packages and one expanded selected package.

## Comparison history

1. Baseline P2: square package artwork had already corrected the earlier flattened panoramic covers, but it still touched all four card edges. The image therefore read as a hard list slice rather than a deliberately framed cover, and its inner corners disappeared into the card.
2. Implemented: the package keeps its square allocation while the visible artwork is inset by 5 px on every edge, receives an 8 px radius and a quiet one-pixel light border, and rests on the same ivory card surface as the package copy.
3. Post-fix evidence: the focused before/after board shows distinct rounded cover corners and consistent breathing room in collapsed and expanded states. Title, count, favourite, chevron, selected context, and child rows retain their previous geometry.

## Required fidelity review

- Fonts and typography: package title, selected chart title, mapper line, song/chart counts, rating, and child-row typography are unchanged. Long collapsed titles remain one-line and truncating.
- Spacing and layout rhythm: the 72 px collapsed and 84 px expanded allocations remain stable. Visible cover sizes are 62 x 62 and 74 x 74, centred inside their square slots with 5 px padding.
- Colours and visual tokens: cover framing uses the existing ivory surface and a 58% white hairline; no new glow, gradient, saturated rail, or sci-fi treatment was introduced.
- Image quality and asset fidelity: original beatmap artwork is still used with the existing centre-crop helper. No screenshot slice, generated substitute, large cutout, or stretched resource was introduced.
- Copy and content: package titles, counts, chart metadata, mode, rating, and mapper content remain unchanged.
- Icons and affordances: favourite, chevron, play pointer, mode pill, and selected outline retain their established positions and state behaviour.
- States and interactions: collapsed, expanded, selected, hover, focus transfer, virtualisation, and pooled reuse remain covered by the existing list suite.
- Accessibility and viewport resilience: header hit targets and text widths are unchanged; the inset affects only image presentation and does not reduce the clickable card area.

## Findings

- P0: none.
- P1: none.
- P2: none after introducing the inset rounded artwork frame.
- P3: the source reference uses panoramic package banners, while Yokko intentionally keeps square covers because the user explicitly rejected flattened covers. The hierarchy and spacing are matched without copying that disliked aspect.

## Verification

- Isolated `Yokko.Game.Tests` build in `artifacts/song-select-package-artwork`: 0 warnings and 0 errors.
- Focused virtualised-list run: 16 passed, 0 failed, 0 skipped.
- Focused Song Select screen run: 11 passed, 0 failed, 0 skipped.
- Native Direct3D 11 capture completed at 1920 x 1080. Full and focused source comparisons plus same-state before/after boards were opened and inspected at original detail.
- `git diff --check` passed.

final result: passed

# Song Select large package covers and background parity QA (2026-08-01)

## Evidence

- Selected design reference: `C:\Users\nyafa\.codex\generated_images\019fb864-4eef-7463-8392-2664d1e1d9ac\exec-584587db-63d6-4cc7-9bbf-ff94fb6bdffe.png` at 1672 x 941.
- User-reported browser state: `C:\Users\nyafa\AppData\Local\Temp\codex-clipboard-a713ca0f-90f7-49c5-8cb8-3f0ccfb36f3b.png` at 1598 x 1248.
- Same-state baseline: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-package-artwork-v1.png` at 1920 x 1080.
- Revised native implementation: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-large-covers-v1.png` at 1920 x 1080.
- Full same-state before/after comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-large-covers-before-after-v1.png`.
- Focused same-state cover comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-large-covers-focused-before-after-v1.png`.
- Focused background-wash comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-browser-shade-before-after-v1.png`.
- Reported-state/revised-browser comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-large-covers-reported-v1.png`.
- Full selected-reference/implementation comparison: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-large-covers-reference-v1.png`.
- Viewport/state: implementation at 1920 x 1080, Comfortable density, 7K filter, two collapsed packages and one selected expanded package. The user report uses different content and aspect ratio, so it is used only to verify the reported right-side wash and cover-scale intent; exact geometry is judged from the same-state implementation pair.

## Comparison history

1. Baseline P2: the previous pass reduced visible package artwork to 62 x 62 collapsed and 74 x 74 expanded. It fixed flattened banners but over-corrected into thumbnail scale. A separate 900 px-wide `createLibraryShade()` also applied an 18% ivory wash only to the right side, producing the rectangular veil reported by the user.
2. User correction: package artwork should remain square or near-square but operate as a large cover, and the browser must share the same global background treatment as the left column rather than introducing a right-only panel.
3. Implemented: collapsed headers increase from 72 to 96 px with an 86 x 86 visible cover; expanded headers increase from 84 to 132 px with a 122 x 122 visible cover. Title, byline, count, mode pill, rating, play pointer, and chevron positions were re-authored for the larger geometry. The right-only `createLibraryShade()` layer was removed entirely.
4. Post-fix evidence: the same-state focused board shows the selected cover becoming the primary package visual while collapsed covers remain useful rather than dominant. The lower-background board shows the browser and left content now using the same global isolation and mood wash with no right-side rectangle.

## Required fidelity review

- Fonts and typography: type family and weights are unchanged. Larger headers add spatial breathing room without enlarging package text into billboard scale; title truncation remains active.
- Spacing and layout rhythm: collapsed/expanded package allocations are now 96/132 px. Visible covers are centred with 5 px padding; content starts at 120/156 px, preserving a 24 px text gutter after each cover slot.
- Colours and visual tokens: cards retain Yokko ivory, cyan, yellow, pink, and navy. Removing the library shade eliminates the only right-column-specific background colour treatment.
- Image quality and asset fidelity: original beatmap artwork remains centre-cropped into real square cover frames at 86/122 px. No stretched banner, generated replacement, screenshot slice, or new resource cutout was introduced.
- Copy and content: package title, selected chart title, artist/mapper, chart counts, difficulty mode, rating, and child rows remain intact.
- Icons and affordances: favourite and expansion actions stay on the trailing rail; the selected play pointer and mode pill remain visible after the height change.
- States and interactions: collapsed, expanded, selected, hover, keyboard focus transfer, scroll clamping, virtualisation, and pooled reuse remain covered by focused tests.
- Accessibility and viewport resilience: larger package rows increase cover legibility and card targets. Fewer packages are visible simultaneously, but scrolling remains available and the selected package plus its child charts fit within the 1080p browser viewport.

## Findings

- P0: none.
- P1: none.
- P2: none after enlarging the package geometry and removing the right-only background wash.
- P3: the denser large-cover presentation intentionally shows fewer package headers at once. This is the requested tradeoff and does not obstruct navigation or scrolling.

## Verification

- Isolated `Yokko.Game.Tests` build in `artifacts/song-select-large-covers`: 0 warnings and 0 errors.
- Focused virtualised-list run: 16 passed, 0 failed, 0 skipped.
- Focused Song Select screen run: 11 passed, 0 failed, 0 skipped.
- Native Direct3D 11 capture completed at 1920 x 1080. The selected reference, user report, full same-state comparison, focused cover comparison, and focused background comparison were opened and inspected at original detail.
- `git diff --check` passed.

final result: passed

# Song Select entry performance and visual-regression QA (2026-08-01)

## Evidence

- Pre-fix native construction timings: 432 ms, 444 ms, and 427 ms; average 434 ms.
- Revised native construction timings: 265 ms, 268 ms, and 293 ms; average 275 ms.
- Revised native capture: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-entry-performance-v1.png` at 1920 x 1080.
- Same-state visual-regression board: `C:\Users\nyafa\.codex\visualizations\2026\07\31\019fb864-4eef-7463-8392-2664d1e1d9ac\song-select-entry-performance-before-after-v1.png`.
- Renderer: native Direct3D 11, Comfortable density, 7K filter, one expanded package.

## Findings and implementation

1. The normal entry path eagerly constructed 28 legacy mod buttons plus the full retired inline mod-settings tree even though the footer now opens `GameplayModsScreen`. Normal entry now creates only the real footer control; the legacy tree is available solely through the explicit `YOKKO_LEGACY_INLINE_MOD_PANEL=1` diagnostic switch.
2. Song Select loaded a 2149 x 731 logo, a cropped region from a 2520 x 3360 avatar sheet, and all 15 frames of a 500 x 500 mascot GIF for display slots no larger than 512 px, 256 px, and 142 px respectively. Purpose-sized derivatives are now loaded while the original resources remain untouched.
3. The cumulative post-library static-UI stage fell from an average 348 ms to 181 ms. End-to-end construction fell from 434 ms to 275 ms, a 36.6% reduction.
4. A framework `TextureStore.GetAsync()` experiment introduced a reproducible 4-5 second wait when the same artwork was synchronously reused during detail construction. That experiment was fully reverted and is not part of the final implementation.

## Visual fidelity review

- The before/after board was opened at original detail. Panel geometry, text, artwork crop, colours, controls, selected state, and footer layout remain visually equivalent.
- Purpose-sized assets preserve the original artwork and proportions; no generated replacement, large screenshot slice, or new decorative cutout was introduced.
- The dedicated Mods screen, selected mod state, song rows, filters, ranking, footer tools, and gameplay handoff remain covered by the focused screen suite.

## Verification

- Isolated `Yokko.Game.Tests` build in `artifacts/song-select-entry-performance`: 0 warnings and 0 errors.
- Focused Song Select screen run: 11 passed, 0 failed, 0 skipped.
- Three revised native Direct3D 11 runs completed at 1920 x 1080.
- `git diff --check` passed.

final result: passed
