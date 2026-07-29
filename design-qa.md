# Yokko Gameplay Mods Studio Index design QA

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
