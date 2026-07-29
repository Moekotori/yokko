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
