# Yokko UI font atlas

The `PlusJakartaSans` bitmap-font atlas combines:

- Printable ASCII (English, figures, and common punctuation): Plus Jakarta Sans Medium
  - Source repository: `tokotype/PlusJakartaSans`
  - Source commit: `18d1cd2f7ea10481919d2f05c1f7064b7307fc26`
- All other portable BMP glyphs: Noto Sans CJK SC Medium
  - Source repository: `notofonts/noto-cjk`
  - Source commit: `f8d157532fbfaeda587e826d4cd5b21a49186f7c`

Both source fonts are licensed under the SIL Open Font License 1.1. The
generated bitmap atlas is distributed under the same licence. The mixed atlas
keeps one stable family and baseline in osu!framework: English and numeric UI
use Plus Jakarta Sans while Chinese, Japanese, Korean, and arbitrary imported
metadata retain the complete Noto Sans CJK coverage.

Run `python scripts/generate-localisation-font.py` from the repository root to
regenerate the atlas.
