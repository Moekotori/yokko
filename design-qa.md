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
